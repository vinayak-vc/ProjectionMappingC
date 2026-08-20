using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Reads keyboard + mouse each frame and drives <see cref="PMSDKSkinEditorController"/>.
    /// Isolated from the controller so the controller's state model can be unit-tested
    /// without a running Update loop.
    ///
    /// Multi-display pointer routing follows the same pattern as
    /// <c>PMSDKCalibrationManager.TryGetPointerViewport</c>: <c>Display.RelativeMouseAt</c>
    /// in standalone multi-display, raw <c>Input.mousePosition</c> against the projector
    /// camera in the Editor.
    ///
    /// Keys (Phase A + B + C):
    ///   R / E / P / F / Esc      pick tool
    ///   drag on empty canvas     draw new rect/ellipse (R/E), trace freeform (F)
    ///   click                    with P: append polygon vertex; with Select/R/E: pick handle or select
    ///   Enter                    close polygon-in-progress
    ///   drag handle / edge       reshape
    ///   N                        add default shape of current tool
    ///   Del / Backspace          delete selected
    ///   Ctrl+D                   duplicate selected
    ///   [ / ]                    z-order back / forward
    ///   X / Shift+X              solo / unmute all
    ///   Y / Shift+Y              test-pattern cycle forward / back
    ///   + / -                    insert / remove polygon vertex (on selected edge / at selected corner)
    ///   PgUp / PgDn              cycle skins
    ///   Tab / Shift+Tab          cycle corners
    ///   Arrows                   nudge (Shift = fine, Ctrl = coarse)
    ///   Ctrl+Z / Ctrl+Y          undo / redo
    ///   Ctrl+S                   save to default path
    ///   Ctrl+L                   load from default path
    ///   F1                       help (handled by HUD)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PMSDKSkinEditorController))]
    public class PMSDKSkinEditorInput : MonoBehaviour
    {
        [Tooltip("When true, all input handling is disabled. Useful for automated tests / demos.")]
        public bool InputEnabled = true;

        private PMSDKSkinEditorController controller;
        private bool pointerHeld;
        private bool freeformStrokeActive;

        private void Awake() { controller = GetComponent<PMSDKSkinEditorController>(); }

        private void Update()
        {
            if (!InputEnabled || controller == null) return;
            HandleKeyboard();
            HandleMouse();
        }

        // ---- keyboard ----

        private void HandleKeyboard()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrl  = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            // Ctrl-modified shortcuts first so plain letters don't fire under Ctrl.
            if (ctrl)
            {
                if (Input.GetKeyDown(KeyCode.Z)) { controller.UndoLast(); return; }
                if (Input.GetKeyDown(KeyCode.Y)) { controller.RedoLast(); return; }
                if (Input.GetKeyDown(KeyCode.S)) { controller.SaveDefault(); return; }
                if (Input.GetKeyDown(KeyCode.L)) { controller.LoadDefault(); return; }
                if (Input.GetKeyDown(KeyCode.D)) { controller.DuplicateSelected(); return; }
            }

            // Tool switching.
            if (Input.GetKeyDown(KeyCode.R)) controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            else if (Input.GetKeyDown(KeyCode.E)) controller.PickTool(PMSDKSkinEditorController.Tool.Ellipse);
            else if (Input.GetKeyDown(KeyCode.P)) controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            else if (Input.GetKeyDown(KeyCode.F)) controller.PickTool(PMSDKSkinEditorController.Tool.Freeform);
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Esc cancels any polygon draw in progress, otherwise switches tools.
                if (controller.IsDrawingPolygon) controller.CancelPolygonDraw();
                else controller.PickTool(PMSDKSkinEditorController.Tool.Select);
            }

            // Selection navigation.
            if (Input.GetKeyDown(KeyCode.PageUp)) controller.CycleSkin(-1);
            else if (Input.GetKeyDown(KeyCode.PageDown)) controller.CycleSkin(+1);
            if (Input.GetKeyDown(KeyCode.Tab)) controller.CycleCorner(shift ? -1 : +1);

            // Structural.
            if (Input.GetKeyDown(KeyCode.N)) controller.AddDefaultShape();
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
                controller.DeleteSelected();
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (controller.IsDrawingPolygon) controller.ClosePolygon();
            }

            // Polygon vertex insert/remove.
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                controller.InsertVertexOnSelectedEdge();
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                controller.RemoveSelectedVertex();

            // Z-order shortcuts. `[` sends back, `]` brings forward.
            if (Input.GetKeyDown(KeyCode.LeftBracket)) controller.MoveSelectedZOrder(-1);
            if (Input.GetKeyDown(KeyCode.RightBracket)) controller.MoveSelectedZOrder(+1);

            // Solo / unmute.
            if (Input.GetKeyDown(KeyCode.X))
            {
                if (shift) controller.UnmuteAll();
                else controller.ToggleSoloSelected();
            }

            // Test-pattern cycle (Y — coexists with Ctrl+Y redo above via the ctrl guard).
            if (Input.GetKeyDown(KeyCode.Y) && !ctrl)
                controller.CycleTestPatternSelected(shift ? -1 : +1);

            // Arrow-key nudge — held keys stream while pressed. Coalescing at the Undo
            // layer merges the burst into one undo slot.
            Vector2 axis = new Vector2(
                (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
            if (axis.sqrMagnitude > 0f)
            {
                float step = shift ? controller.NudgeFine
                           : (ctrl ? controller.NudgeCoarse : controller.NudgeNormal);
                controller.NudgeSelectedCorner(axis * step * Time.deltaTime * 60f);
            }
        }

        // ---- mouse ----

        private void HandleMouse()
        {
            if (!TryGetPointerRaster(out var raster)) return;
            var tool = controller.CurrentTool;

            if (Input.GetMouseButtonDown(0))
            {
                // Polygon tool: click appends a vertex (or closes if near start). Skip
                // the standard pointer flow entirely.
                if (tool == PMSDKSkinEditorController.Tool.Polygon)
                {
                    controller.AppendPolygonVertex(raster);
                    return;
                }
                // Freeform tool: mouse-down begins a stroke; drag continues; up ends.
                if (tool == PMSDKSkinEditorController.Tool.Freeform)
                {
                    controller.BeginFreeformStroke(raster);
                    freeformStrokeActive = true;
                    pointerHeld = true;
                    return;
                }

                pointerHeld = true;
                controller.OnPointerDown(raster);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (freeformStrokeActive)
                {
                    controller.EndFreeformStroke();
                    freeformStrokeActive = false;
                    pointerHeld = false;
                    return;
                }
                if (pointerHeld) controller.OnPointerUp(raster);
                pointerHeld = false;
            }
            else if (pointerHeld && Input.GetMouseButton(0))
            {
                if (freeformStrokeActive)
                {
                    controller.ContinueFreeformStroke(raster);
                    return;
                }
                controller.OnPointerDrag(raster);
            }
        }

        /// <summary>
        /// Convert the current mouse position to normalized raster coords [0..1] on the
        /// projector's viewport, y-UP. Returns false if the pointer isn't over the
        /// projector's display (in standalone) or outside the camera viewport (in Editor).
        /// Matches <c>PMSDKCalibrationManager.TryGetPointerViewport</c>.
        /// </summary>
        private bool TryGetPointerRaster(out Vector2 raster)
        {
            raster = default;
            if (controller.Projector == null) return false;
            var cam = controller.Projector.GetComponent<Camera>();
            if (cam == null) return false;

            Vector3 mouse = Input.mousePosition;
            Vector3 rel = Display.RelativeMouseAt(mouse);
            if (rel != Vector3.zero)
            {
                if ((int)rel.z != cam.targetDisplay) return false;
                mouse = new Vector3(rel.x, rel.y, 0f);
            }
            Vector3 vp = cam.ScreenToViewportPoint(mouse);
            raster = new Vector2(vp.x, vp.y);
            return raster.x >= -0.1f && raster.x <= 1.1f
                && raster.y >= -0.1f && raster.y <= 1.1f;
        }
    }
}
