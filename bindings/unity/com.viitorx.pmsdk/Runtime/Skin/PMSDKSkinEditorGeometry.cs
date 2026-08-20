using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Pure geometry helpers for the Skin Editor's hit-testing and drawing math. All
    /// coordinates are in raster space [0..1] with y-UP (see <see cref="PMSDKSkin"/>).
    /// No Unity runtime allocations, no scene queries — everything unit-testable in
    /// EditMode without Play mode.
    /// </summary>
    public static class PMSDKSkinEditorGeometry
    {
        /// <summary>
        /// Nearest-corner picking. Returns the index of the vertex closest to
        /// <paramref name="raster"/>, or -1 if no vertex is within
        /// <paramref name="threshold"/> raster units.
        /// </summary>
        public static int PickCorner(Vector2 raster, IList<Vector2> vertices, float threshold)
        {
            if (vertices == null || vertices.Count == 0) return -1;
            int best = -1;
            float bestSq = threshold * threshold;
            for (int i = 0; i < vertices.Count; i++)
            {
                float dx = vertices[i].x - raster.x;
                float dy = vertices[i].y - raster.y;
                float d2 = dx * dx + dy * dy;
                if (d2 < bestSq) { bestSq = d2; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Nearest-edge-midpoint picking. Returns the index of the edge whose midpoint
        /// is closest to <paramref name="raster"/>, or -1 if no midpoint is within
        /// <paramref name="threshold"/> raster units. Edge <c>i</c> connects vertices
        /// <c>i</c> and <c>(i+1) % n</c>.
        /// </summary>
        public static int PickEdgeMidpoint(Vector2 raster, IList<Vector2> vertices, float threshold)
        {
            if (vertices == null || vertices.Count < 2) return -1;
            int n = vertices.Count;
            int best = -1;
            float bestSq = threshold * threshold;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = vertices[i];
                Vector2 b = vertices[(i + 1) % n];
                float mx = 0.5f * (a.x + b.x) - raster.x;
                float my = 0.5f * (a.y + b.y) - raster.y;
                float d2 = mx * mx + my * my;
                if (d2 < bestSq) { bestSq = d2; best = i; }
            }
            return best;
        }

        /// <summary>Signed distance from a point to a line segment (for edge picking / hover).</summary>
        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.x * ab.x + ab.y * ab.y;
            if (lenSq < 1e-12f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(((p.x - a.x) * ab.x + (p.y - a.y) * ab.y) / lenSq);
            Vector2 proj = new Vector2(a.x + t * ab.x, a.y + t * ab.y);
            return Vector2.Distance(p, proj);
        }

        /// <summary>Point-in-simple-polygon (ray casting). Vertex order (CCW/CW) doesn't matter.</summary>
        public static bool ContainsPoint(IList<Vector2> vertices, Vector2 p)
        {
            if (vertices == null || vertices.Count < 3) return false;
            int n = vertices.Count;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 vi = vertices[i], vj = vertices[j];
                if (((vi.y > p.y) != (vj.y > p.y)) &&
                    (p.x < (vj.x - vi.x) * (p.y - vi.y) / (vj.y - vi.y + 1e-20f) + vi.x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>Point-in-ellipse inscribed in the axis-aligned bounding box of 4 vertices.</summary>
        public static bool ContainsPointEllipse(IList<Vector2> bounds4, Vector2 p)
        {
            if (bounds4 == null || bounds4.Count < 4) return false;
            Rect r = Bounds(bounds4);
            if (r.width <= 0f || r.height <= 0f) return false;
            float nx = (p.x - r.center.x) / (0.5f * r.width);
            float ny = (p.y - r.center.y) / (0.5f * r.height);
            return nx * nx + ny * ny <= 1f;
        }

        /// <summary>Axis-aligned bounding box of a vertex list in raster space.</summary>
        public static Rect Bounds(IList<Vector2> vertices)
        {
            if (vertices == null || vertices.Count == 0) return Rect.zero;
            float xMin = vertices[0].x, xMax = vertices[0].x;
            float yMin = vertices[0].y, yMax = vertices[0].y;
            for (int i = 1; i < vertices.Count; i++)
            {
                var v = vertices[i];
                if (v.x < xMin) xMin = v.x;
                if (v.x > xMax) xMax = v.x;
                if (v.y < yMin) yMin = v.y;
                if (v.y > yMax) yMax = v.y;
            }
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        /// <summary>Build a rectangle-skin's 4 corners (BL, BR, TR, TL) from two opposite drag points.</summary>
        public static List<Vector2> BuildRectFromDrag(Vector2 a, Vector2 b)
        {
            float xMin = Mathf.Min(a.x, b.x);
            float xMax = Mathf.Max(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y);
            float yMax = Mathf.Max(a.y, b.y);
            return new List<Vector2>
            {
                new Vector2(xMin, yMin), // BL
                new Vector2(xMax, yMin), // BR
                new Vector2(xMax, yMax), // TR
                new Vector2(xMin, yMax), // TL
            };
        }

        /// <summary>Clamp a raster-space vertex to the [0..1] canvas.</summary>
        public static Vector2 ClampToRaster(Vector2 v)
        {
            return new Vector2(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y));
        }
    }
}
