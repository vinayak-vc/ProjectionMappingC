using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// M-Sk4 polish additions to <see cref="PMSDKSkinEditorController"/>: multi-select,
    /// group nudge, snap-to-grid + alignment guides, visibility toggle. Split into a
    /// partial file for the same reason as Phase B/C — keeps the older state machine
    /// files reviewable in isolation.
    /// </summary>
    public partial class PMSDKSkinEditorController
    {
        // ---- Snap config ----

        [Header("M-Sk4 — snap + multi-select")]
        [Tooltip("Snap corner drags to this raster-space grid step. 0 = off.")]
        public float SnapGridStep = 0f;
        [Tooltip("Raster-space tolerance for snapping to a grid line.")]
        public float SnapGridTolerance = 0.01f;
        [Tooltip("Also snap to other skins' vertex X/Y coords (alignment). 0 = off.")]
        public float SnapAlignTolerance = 0.008f;

        /// <summary>Latest snap-guide state — read by the overlay to render dashed lines
        /// while a corner drag is snapping. Cleared on pointer-up.</summary>
        public PMSDKSkinEditorSnapGuides SnapGuides;

        // ---- Multi-select ----

        /// <summary>Additional selected skin indices (beyond <see cref="SelectedSkinIndex"/>).
        /// A skin is "in the group" if it's either the primary or is in this set.</summary>
        public readonly HashSet<int> MultiSelectedIndices = new HashSet<int>();

        public bool IsInSelectionGroup(int index) =>
            index == SelectedSkinIndex || MultiSelectedIndices.Contains(index);

        public int SelectionGroupCount =>
            (SelectedSkinIndex >= 0 ? 1 : 0) + MultiSelectedIndices.Count;

        /// <summary>Add a skin to the multi-select group. If it's already the primary,
        /// no-op. Callers guarantee <paramref name="index"/> is valid.</summary>
        public void AddToSelection(int index)
        {
            if (index < 0 || index >= Skins.Count) return;
            if (index == SelectedSkinIndex) return;
            MultiSelectedIndices.Add(index);
        }

        public void RemoveFromSelection(int index)
        {
            MultiSelectedIndices.Remove(index);
        }

        /// <summary>Toggle <paramref name="index"/> in the multi-select set (used by
        /// Ctrl+click). Does not touch <see cref="SelectedSkinIndex"/>.</summary>
        public void ToggleInSelection(int index)
        {
            if (index < 0 || index >= Skins.Count) return;
            if (index == SelectedSkinIndex)
            {
                // Ctrl+click on the primary removes it from the group entirely — promote
                // any remaining multi-select to primary, or clear.
                if (MultiSelectedIndices.Count > 0)
                {
                    var iter = MultiSelectedIndices.GetEnumerator();
                    iter.MoveNext();
                    int newPrimary = iter.Current;
                    MultiSelectedIndices.Remove(newPrimary);
                    SelectedSkinIndex = newPrimary;
                }
                else
                {
                    SelectedSkinIndex = -1;
                }
                UpdateStatus();
                return;
            }
            if (!MultiSelectedIndices.Add(index)) MultiSelectedIndices.Remove(index);
            UpdateStatus();
        }

        public void ClearMultiSelection()
        {
            MultiSelectedIndices.Clear();
            UpdateStatus();
        }

        /// <summary>Group nudge: move the same-index corner across every selected skin
        /// (primary + multi-select) by <paramref name="delta"/>. Falls back to single-corner
        /// nudge when the group is empty.</summary>
        public void NudgeSelectedGroup(Vector2 delta)
        {
            if (MultiSelectedIndices.Count == 0)
            {
                NudgeSelectedCorner(delta);
                return;
            }

            Undo.Record(this, "nudge");
            if (SelectedSkinIndex >= 0) NudgeSkinCorner(Skins[SelectedSkinIndex], delta);
            foreach (var i in MultiSelectedIndices)
            {
                if (i >= 0 && i < Skins.Count) NudgeSkinCorner(Skins[i], delta);
            }
        }

        private void NudgeSkinCorner(PMSDKSkin s, Vector2 delta)
        {
            if (s == null || s.Vertices == null) return;
            int idx = Mathf.Clamp(SelectedCornerIndex, 0, s.Vertices.Count - 1);
            if (idx < 0 || idx >= s.Vertices.Count) return;
            s.Vertices[idx] = PMSDKSkinEditorGeometry.ClampToRaster(s.Vertices[idx] + delta);
        }

        // ---- Visibility toggle ----

        /// <summary>Toggle a skin's <see cref="MeshRenderer"/> — hide/show without touching
        /// solo/mute state. HUD calls this from the eyeball button in each list row.</summary>
        public void ToggleVisibilitySelected()
        {
            var s = Selected;
            if (s == null) return;
            var mr = s.GetComponent<MeshRenderer>();
            if (mr == null) return;
            Undo.Record(this, "visibility");
            mr.enabled = !mr.enabled;
        }

        public void SetSkinVisibility(PMSDKSkin s, bool visible)
        {
            if (s == null) return;
            var mr = s.GetComponent<MeshRenderer>();
            if (mr != null && mr.enabled != visible)
            {
                Undo.Record(this, "visibility");
                mr.enabled = visible;
            }
        }

        public bool IsVisible(PMSDKSkin s)
        {
            if (s == null) return false;
            var mr = s.GetComponent<MeshRenderer>();
            return mr != null && mr.enabled;
        }

        // ---- Snap application (called from Phase A's OnPointerDrag corner-drag branch) ----

        /// <summary>Apply grid + alignment snap to a raster-space target during a corner
        /// drag. Updates <see cref="SnapGuides"/> so the overlay can draw dashed lines.</summary>
        internal Vector2 ApplySnaps(Vector2 target, PMSDKSkin dragged)
        {
            SnapGuides.Clear();
            Vector2 v = PMSDKSkinEditorGeometry.ClampToRaster(target);

            // Grid snap first (independent of other skins).
            if (SnapGridStep > 0f)
            {
                v = PMSDKSkinEditorSnap.SnapToGrid(v, SnapGridStep, SnapGridTolerance);
            }

            // Then vertex-alignment snap (only if snap radius > 0).
            if (SnapAlignTolerance > 0f)
            {
                var candidates = PMSDKSkinEditorSnap.CollectVertices(Skins, dragged);
                var snapped = PMSDKSkinEditorSnap.SnapToVertices(
                    v, candidates, SnapAlignTolerance,
                    out bool sx, out bool sy, out float lx, out float ly);
                if (sx) { SnapGuides.XActive = true; SnapGuides.XLine = lx; }
                if (sy) { SnapGuides.YActive = true; SnapGuides.YLine = ly; }
                v = snapped;
            }

            return v;
        }

        /// <summary>Called from Phase A's OnPointerUp so the overlay stops drawing guides.</summary>
        internal void ClearSnapGuides()
        {
            SnapGuides.Clear();
        }
    }
}
