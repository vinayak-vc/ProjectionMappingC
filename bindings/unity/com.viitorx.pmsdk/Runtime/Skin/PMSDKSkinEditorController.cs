using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// State machine + public API for the Skin Editor scene. Owns the current tool,
    /// selection, and pointer state. <see cref="PMSDKSkinEditorInput"/> feeds this via
    /// its public methods; <see cref="PMSDKSkinEditorOverlay"/> reads its state to
    /// render handles and outlines. This split keeps the pure state model out of the
    /// input polling loop so unit tests can drive it directly (M-Sk1 tests only cover
    /// the geometry helpers; controller state tests land in M-Sk2 phase B).
    ///
    /// Phase A (this file) covers:
    ///   - Tool switching (Select / Rectangle / Ellipse / Polygon / Freeform).
    ///   - Pointer-driven draw-rectangle (Rectangle/Ellipse tools) via drag on empty canvas.
    ///   - Corner and edge-midpoint dragging on any existing skin.
    ///   - Skin/corner cycling, arrow-key nudge with Shift/Ctrl scaling, delete, add-default.
    /// </summary>
    [DisallowMultipleComponent]
    public class PMSDKSkinEditorController : MonoBehaviour
    {
        public enum Tool { Select, Rectangle, Ellipse, Polygon, Freeform }
        public enum DragState { Idle, DrawingRect, DraggingCorner, DraggingEdge }

        [Header("Scene refs")]
        public PMSDKProjector Projector;
        [Tooltip("Optional parent under which new skins will be created. Defaults to the projector's parent.")]
        public Transform SkinsParent;
        [Tooltip("Layer for newly created skins. Falls back to TransparentFX if unset.")]
        public string SkinLayerName = "TransparentFX";

        [Header("Interaction")]
        [Tooltip("Corner-picking radius in raster units (1.0 == full projector width).")]
        public float HandlePickThreshold = 0.02f;
        [Tooltip("Edge-midpoint picking radius in raster units.")]
        public float EdgePickThreshold = 0.015f;
        [Tooltip("Minimum drag distance (raster units) before a click becomes a drag-to-draw.")]
        public float MinDrawDistance = 0.01f;

        [Header("Nudge steps (raster units)")]
        public float NudgeFine   = 0.001f;
        public float NudgeNormal = 0.005f;
        public float NudgeCoarse = 0.02f;

        // ---- runtime state ----
        public Tool CurrentTool { get; private set; } = Tool.Select;
        public DragState State { get; private set; } = DragState.Idle;
        public List<PMSDKSkin> Skins { get; private set; } = new List<PMSDKSkin>();
        public int SelectedSkinIndex = -1;
        public int SelectedCornerIndex = 0;
        public string StatusLine { get; private set; } = "";

        // Drag bookkeeping.
        private Vector2 dragStartRaster;
        private Vector2 lastPointerRaster;
        private int activeVertexIndex = -1;
        private int activeEdgeIndex = -1;
        private PMSDKSkin activeSkin;
        // For DrawingRect: the transient skin being sized. Committed on pointer-up.
        private PMSDKSkin transientDrawSkin;

        // ---- lifecycle ----

        private void OnEnable()
        {
            RefreshSkinList();
            UpdateStatus();
        }

        /// <summary>Rebuild the skin list from the scene. Cheap; safe to call any time.</summary>
        public void RefreshSkinList()
        {
            Skins.Clear();
            // Prefer skins that share the projector reference; if the projector is null,
            // just enumerate everything in the scene so the editor still works in the
            // no-projector-yet setup case.
            var all = Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None);
            foreach (var s in all)
            {
                if (Projector == null || s.Projector == Projector) Skins.Add(s);
            }
            // Sort by z-order so cycling matches visual stacking (higher zOrder first ==
            // topmost on screen).
            Skins.Sort((a, b) => b.ZOrder.CompareTo(a.ZOrder));
            if (SelectedSkinIndex >= Skins.Count) SelectedSkinIndex = Skins.Count - 1;
            if (SelectedSkinIndex < 0 && Skins.Count > 0) SelectedSkinIndex = 0;
        }

        public PMSDKSkin Selected =>
            (SelectedSkinIndex >= 0 && SelectedSkinIndex < Skins.Count) ? Skins[SelectedSkinIndex] : null;

        // ---- tool + selection commands ----

        public void PickTool(Tool t)
        {
            CurrentTool = t;
            UpdateStatus();
        }

        public void CycleSkin(int direction)
        {
            if (Skins.Count == 0) { SelectedSkinIndex = -1; UpdateStatus(); return; }
            SelectedSkinIndex = (SelectedSkinIndex + direction + Skins.Count) % Skins.Count;
            SelectedCornerIndex = 0;
            UpdateStatus();
        }

        public void CycleCorner(int direction)
        {
            var s = Selected;
            if (s == null || s.Vertices == null || s.Vertices.Count == 0) return;
            int n = s.Vertices.Count;
            SelectedCornerIndex = (SelectedCornerIndex + direction + n) % n;
            UpdateStatus();
        }

        public void NudgeSelectedCorner(Vector2 delta)
        {
            var s = Selected;
            if (s == null || s.Vertices == null || SelectedCornerIndex < 0 || SelectedCornerIndex >= s.Vertices.Count) return;
            s.Vertices[SelectedCornerIndex] = PMSDKSkinEditorGeometry.ClampToRaster(
                s.Vertices[SelectedCornerIndex] + delta);
        }

        public void DeleteSelected()
        {
            var s = Selected;
            if (s == null) return;
            if (Application.isEditor && !Application.isPlaying) DestroyImmediate(s.gameObject);
            else Destroy(s.gameObject);
            RefreshSkinList();
            UpdateStatus();
        }

        /// <summary>Add a default-sized shape of the current tool at canvas center. Bound to `N`.</summary>
        public void AddDefaultShape()
        {
            PMSDKSkinShapeKind kind = CurrentTool switch
            {
                Tool.Ellipse => PMSDKSkinShapeKind.Ellipse,
                Tool.Polygon => PMSDKSkinShapeKind.Polygon,
                Tool.Freeform => PMSDKSkinShapeKind.Freeform,
                _ => PMSDKSkinShapeKind.Rectangle,
            };
            var skin = SpawnSkin(kind, DefaultVerticesFor(kind, new Vector2(0.5f, 0.5f), 0.15f));
            SelectSkin(skin);
        }

        // ---- pointer flow ----

        public void OnPointerDown(Vector2 raster)
        {
            lastPointerRaster = raster;
            dragStartRaster = raster;

            // Hit-test the selected skin first (so re-grabbing its corners is sticky),
            // then fall back to any other skin's corners/edges.
            if (TryPickHandleOnSkin(Selected, raster, out int cornerA, out int edgeA))
            {
                BeginHandleDrag(Selected, cornerA, edgeA);
                return;
            }
            for (int i = 0; i < Skins.Count; i++)
            {
                if (i == SelectedSkinIndex) continue;
                if (TryPickHandleOnSkin(Skins[i], raster, out int cornerB, out int edgeB))
                {
                    SelectedSkinIndex = i;
                    BeginHandleDrag(Skins[i], cornerB, edgeB);
                    UpdateStatus();
                    return;
                }
            }

            // No handle hit — depending on the tool: select a skin under cursor, or begin
            // drawing a new rectangle/ellipse.
            if (CurrentTool == Tool.Rectangle || CurrentTool == Tool.Ellipse)
            {
                // Defer creation until we've moved past MinDrawDistance; until then behave
                // as a click-select on any skin under the cursor.
                State = DragState.DrawingRect;
                activeSkin = null;
                transientDrawSkin = null;
                return;
            }

            // Select mode: click-select whichever skin's polygon contains the point.
            int hit = HitTestSkinContaining(raster);
            if (hit >= 0)
            {
                SelectedSkinIndex = hit;
                SelectedCornerIndex = 0;
                UpdateStatus();
            }
        }

        public void OnPointerDrag(Vector2 raster)
        {
            lastPointerRaster = raster;

            switch (State)
            {
                case DragState.DraggingCorner:
                    if (activeSkin != null && activeVertexIndex >= 0)
                    {
                        activeSkin.Vertices[activeVertexIndex] =
                            PMSDKSkinEditorGeometry.ClampToRaster(raster);
                    }
                    break;

                case DragState.DraggingEdge:
                    if (activeSkin != null && activeEdgeIndex >= 0)
                    {
                        int n = activeSkin.Vertices.Count;
                        int i = activeEdgeIndex;
                        int j = (i + 1) % n;
                        Vector2 delta = raster - lastPointerRaster;
                        // Compute in a temporary so we don't apply half the move then
                        // clamp the other half against clamped output.
                        delta = raster - dragStartRaster;
                        activeSkin.Vertices[i] = PMSDKSkinEditorGeometry.ClampToRaster(
                            activeSkin.Vertices[i] + delta);
                        activeSkin.Vertices[j] = PMSDKSkinEditorGeometry.ClampToRaster(
                            activeSkin.Vertices[j] + delta);
                        // Reset dragStart so subsequent drags produce incremental deltas.
                        dragStartRaster = raster;
                    }
                    break;

                case DragState.DrawingRect:
                    if (Vector2.Distance(dragStartRaster, raster) >= MinDrawDistance)
                    {
                        if (transientDrawSkin == null)
                        {
                            var kind = CurrentTool == Tool.Ellipse
                                ? PMSDKSkinShapeKind.Ellipse
                                : PMSDKSkinShapeKind.Rectangle;
                            transientDrawSkin = SpawnSkin(kind,
                                PMSDKSkinEditorGeometry.BuildRectFromDrag(dragStartRaster, raster));
                            SelectSkin(transientDrawSkin);
                        }
                        else
                        {
                            transientDrawSkin.Vertices =
                                PMSDKSkinEditorGeometry.BuildRectFromDrag(dragStartRaster, raster);
                        }
                    }
                    break;
            }
        }

        public void OnPointerUp(Vector2 raster)
        {
            // If DrawingRect never committed (user clicked without moving past
            // MinDrawDistance), treat as a plain click-select.
            if (State == DragState.DrawingRect && transientDrawSkin == null)
            {
                int hit = HitTestSkinContaining(raster);
                if (hit >= 0) { SelectedSkinIndex = hit; SelectedCornerIndex = 0; }
            }

            State = DragState.Idle;
            activeSkin = null;
            activeVertexIndex = -1;
            activeEdgeIndex = -1;
            transientDrawSkin = null;
            UpdateStatus();
        }

        // ---- private ----

        private bool TryPickHandleOnSkin(PMSDKSkin skin, Vector2 raster,
            out int cornerIndex, out int edgeIndex)
        {
            cornerIndex = -1; edgeIndex = -1;
            if (skin == null || skin.Vertices == null) return false;
            cornerIndex = PMSDKSkinEditorGeometry.PickCorner(raster, skin.Vertices, HandlePickThreshold);
            if (cornerIndex >= 0) return true;
            // Only rectangles and polygons support edge-midpoint drag (both adjacent corners
            // move together). Ellipse uses its 4 bounding-corner handles only.
            if (skin.Kind == PMSDKSkinShapeKind.Rectangle
                || skin.Kind == PMSDKSkinShapeKind.Polygon
                || skin.Kind == PMSDKSkinShapeKind.Freeform)
            {
                edgeIndex = PMSDKSkinEditorGeometry.PickEdgeMidpoint(raster, skin.Vertices, EdgePickThreshold);
                if (edgeIndex >= 0) return true;
            }
            return false;
        }

        private void BeginHandleDrag(PMSDKSkin skin, int cornerIndex, int edgeIndex)
        {
            activeSkin = skin;
            activeVertexIndex = cornerIndex;
            activeEdgeIndex = edgeIndex;
            if (cornerIndex >= 0)
            {
                State = DragState.DraggingCorner;
                SelectedCornerIndex = cornerIndex;
            }
            else if (edgeIndex >= 0)
            {
                State = DragState.DraggingEdge;
            }
            UpdateStatus();
        }

        private int HitTestSkinContaining(Vector2 raster)
        {
            // Iterate in DRAWING order (top of stack first, which is Skins[0] after our sort).
            for (int i = 0; i < Skins.Count; i++)
            {
                var s = Skins[i];
                if (s == null || s.Vertices == null) continue;
                bool inside = s.Kind == PMSDKSkinShapeKind.Ellipse
                    ? PMSDKSkinEditorGeometry.ContainsPointEllipse(s.Vertices, raster)
                    : PMSDKSkinEditorGeometry.ContainsPoint(s.Vertices, raster);
                if (inside) return i;
            }
            return -1;
        }

        private void SelectSkin(PMSDKSkin s)
        {
            if (s == null) { SelectedSkinIndex = -1; return; }
            RefreshSkinList();
            SelectedSkinIndex = Skins.IndexOf(s);
            SelectedCornerIndex = 0;
            UpdateStatus();
        }

        private PMSDKSkin SpawnSkin(PMSDKSkinShapeKind kind, List<Vector2> vertices)
        {
            int layer = LayerMask.NameToLayer(SkinLayerName);
            if (layer < 0) layer = 0;

            Transform parent = SkinsParent;
            if (parent == null && Projector != null) parent = Projector.transform.parent;

            var go = new GameObject($"Skin_{kind}", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = layer;
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(16f / 9f, 1f, 1f);

            var mr = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("PMSDK/UnlitWarp");
            if (shader != null)
                mr.sharedMaterial = new Material(shader) { name = $"{go.name}_Material" };

            var skin = go.AddComponent<PMSDKSkin>();
            skin.Kind = kind;
            skin.SkinId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            skin.DisplayName = go.name;
            skin.Vertices = vertices;
            skin.Projector = Projector;

            bool isQuad = kind == PMSDKSkinShapeKind.Rectangle || kind == PMSDKSkinShapeKind.Ellipse;
            if (isQuad)
            {
                var mw = go.AddComponent<PMSDKMeshWarp>();
                mw.Projector = Projector;
                go.AddComponent<PMSDKCornerPin>();
            }
            go.AddComponent<PMSDKSkinContent>();

            RefreshSkinList();
            return skin;
        }

        private static List<Vector2> DefaultVerticesFor(PMSDKSkinShapeKind kind, Vector2 center, float radius)
        {
            var list = new List<Vector2>();
            switch (kind)
            {
                case PMSDKSkinShapeKind.Rectangle:
                case PMSDKSkinShapeKind.Ellipse:
                    list.Add(new Vector2(center.x - radius, center.y - radius));
                    list.Add(new Vector2(center.x + radius, center.y - radius));
                    list.Add(new Vector2(center.x + radius, center.y + radius));
                    list.Add(new Vector2(center.x - radius, center.y + radius));
                    break;
                case PMSDKSkinShapeKind.Polygon:
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Mathf.PI * 2f * i / 6f + Mathf.PI * 0.5f;
                        list.Add(new Vector2(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius));
                    }
                    break;
                case PMSDKSkinShapeKind.Freeform:
                    list.Add(new Vector2(center.x - radius, center.y - radius));
                    list.Add(new Vector2(center.x + radius, center.y - radius));
                    list.Add(new Vector2(center.x, center.y + radius));
                    break;
            }
            return list;
        }

        private void UpdateStatus()
        {
            var s = Selected;
            string tool = CurrentTool.ToString();
            string sel = s != null
                ? $"{SelectedSkinIndex + 1}/{Skins.Count}: {s.DisplayName} ({s.Kind}) corner {SelectedCornerIndex + 1}/{s.Vertices?.Count ?? 0}"
                : Skins.Count > 0 ? $"— of {Skins.Count} (none selected)" : "no skins";
            StatusLine = $"Tool: {tool}   |   {sel}   |   State: {State}";
        }
    }
}
