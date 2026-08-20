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
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PMSDKSkinEditorController))]
    public class PMSDKSkinEditorInput : MonoBehaviour
    {
        [Tooltip("When true, all input handling is disabled. Useful for automated tests / demos.")]
        public bool InputEnabled = true;

        private PMSDKSkinEditorController controller;
        private bool pointerDownFrame;
        private bool pointerHeld;

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

            // Tool switching.
            if (Input.GetKeyDown(KeyCode.R)) controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            else if (Input.GetKeyDown(KeyCode.E)) controller.PickTool(PMSDKSkinEditorController.Tool.Ellipse);
            else if (Input.GetKeyDown(KeyCode.P)) controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            else if (Input.GetKeyDown(KeyCode.F)) controller.PickTool(PMSDKSkinEditorController.Tool.Freeform);
            else if (Input.GetKeyDown(KeyCode.Escape)) controller.PickTool(PMSDKSkinEditorController.Tool.Select);

            // Selection navigation.
            if (Input.GetKeyDown(KeyCode.PageUp)) controller.CycleSkin(-1);
            else if (Input.GetKeyDown(KeyCode.PageDown)) controller.CycleSkin(+1);
            if (Input.GetKeyDown(KeyCode.Tab)) controller.CycleCorner(shift ? -1 : +1);

            // Structural.
            if (Input.GetKeyDown(KeyCode.N)) controller.AddDefaultShape();
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
                controller.DeleteSelected();

            // Arrow-key nudge — held keys, not one-shot, so long presses stream.
            Vector2 axis = new Vector2(
                (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
            if (axis.sqrMagnitude > 0f)
            {
                float step = shift ? controller.NudgeFine
                           : (ctrl ? controller.NudgeCoarse : controller.NudgeNormal);
                // Step per frame for smooth-hold; for one-shot precise nudges we'd use
                // GetKeyDown, but this matches the calibration manager's hold-to-nudge feel.
                controller.NudgeSelectedCorner(axis * step * Time.deltaTime * 60f);
            }
        }

        // ---- mouse ----

        private void HandleMouse()
        {
            if (!TryGetPointerRaster(out var raster)) return;

            if (Input.GetMouseButtonDown(0))
            {
                pointerHeld = true;
                controller.OnPointerDown(raster);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (pointerHeld) controller.OnPointerUp(raster);
                pointerHeld = false;
            }
            else if (pointerHeld && Input.GetMouseButton(0))
            {
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
            // Slack of 10% outside the viewport so a drag started inside can extend
            // slightly beyond an edge without losing tracking.
            return raster.x >= -0.1f && raster.x <= 1.1f
                && raster.y >= -0.1f && raster.y <= 1.1f;
        }
    }
}
