using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Pure snap-math helpers for the Skin Editor's M-Sk4 polish pass. All coordinates
    /// are in raster space [0..1]. Snapping is applied inside the controller's corner-drag
    /// path — the overlay reads the last-frame guides to render dashed alignment lines.
    /// </summary>
    public static class PMSDKSkinEditorSnap
    {
        /// <summary>Snap a raster-space value to the nearest multiple of <paramref name="step"/>
        /// if it's within <paramref name="tolerance"/> of one. Returns the input unchanged
        /// when snap is disabled (step ≤ 0) or no candidate is close enough.</summary>
        public static float SnapToGrid(float v, float step, float tolerance)
        {
            if (step <= 0f) return v;
            float snapped = Mathf.Round(v / step) * step;
            return Mathf.Abs(snapped - v) <= tolerance ? snapped : v;
        }

        /// <summary>Snap a 2D raster point to the grid on each axis independently.</summary>
        public static Vector2 SnapToGrid(Vector2 p, float step, float tolerance) =>
            new Vector2(SnapToGrid(p.x, step, tolerance), SnapToGrid(p.y, step, tolerance));

        /// <summary>Snap a raster point to another skin's vertex X or Y coord if one is
        /// within <paramref name="tolerance"/>. Returns the (possibly modified) point + which
        /// axes snapped. Used by the overlay to draw alignment guides.</summary>
        public static Vector2 SnapToVertices(
            Vector2 p,
            IReadOnlyList<Vector2> candidates,
            float tolerance,
            out bool snappedX, out bool snappedY,
            out float snapLineX, out float snapLineY)
        {
            snappedX = snappedY = false;
            snapLineX = snapLineY = 0f;
            if (candidates == null || candidates.Count == 0 || tolerance <= 0f) return p;

            float bestDx = tolerance, bestDy = tolerance;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                float dx = Mathf.Abs(c.x - p.x);
                if (dx < bestDx) { bestDx = dx; snapLineX = c.x; snappedX = true; }
                float dy = Mathf.Abs(c.y - p.y);
                if (dy < bestDy) { bestDy = dy; snapLineY = c.y; snappedY = true; }
            }
            return new Vector2(snappedX ? snapLineX : p.x, snappedY ? snapLineY : p.y);
        }

        /// <summary>Convenience: collect all vertices from every skin EXCEPT the one being
        /// dragged (so a corner doesn't snap to itself). Used by the controller as it drives
        /// a corner drag.</summary>
        public static List<Vector2> CollectVertices(
            IReadOnlyList<PMSDKSkin> skins, PMSDKSkin exclude)
        {
            var list = new List<Vector2>();
            if (skins == null) return list;
            for (int i = 0; i < skins.Count; i++)
            {
                var s = skins[i];
                if (s == null || s == exclude || s.Vertices == null) continue;
                for (int k = 0; k < s.Vertices.Count; k++) list.Add(s.Vertices[k]);
            }
            return list;
        }
    }

    /// <summary>Live snap-guide state — the overlay reads this each frame to draw the
    /// alignment lines while a corner drag is snapping.</summary>
    public struct PMSDKSkinEditorSnapGuides
    {
        public bool XActive;
        public bool YActive;
        /// <summary>Raster-space X of the vertical guide line (when XActive).</summary>
        public float XLine;
        /// <summary>Raster-space Y of the horizontal guide line (when YActive).</summary>
        public float YLine;

        public void Clear() { XActive = YActive = false; XLine = YLine = 0f; }
    }
}
