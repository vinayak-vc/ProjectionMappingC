using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Pure static utilities that build the raster-space geometry for <see cref="PMSDKSkin"/>:
    /// unit quads for Rectangle/Ellipse skins, and triangulated polygon meshes for
    /// Polygon/Freeform skins.
    ///
    /// Convention: input vertex lists are in normalized raster space [0..1] with y-UP
    /// (matching <c>PMSDKCornerPin</c> and Unity world convention). Ellipse and Rectangle
    /// use the same 4-vertex bounding-quad; the ellipse mask is applied in the shader.
    /// Polygon output positions ARE the raster-space geometry — no native warp is used
    /// for polygon skins (M-Sk1, docs/multi-skin-design.md, D-036).
    ///
    /// All triangulation methods are self-contained so they can be unit-tested in EditMode
    /// without a Unity Play mode session.
    /// </summary>
    public static class PMSDKSkinMeshBuilder
    {
        // Vertex-count caps from D-036 / docs/multi-skin-design.md. Values above the cap
        // are clamped by callers (with a warning), never silently.
        public const int MaxPolygonVertices = 32;
        public const int MaxFreeformVertices = 128;

        /// <summary>
        /// Emit a unit-quad's positions/uvs/triangles centered at (0.5, 0.5, 0),
        /// vertex order BL, BR, TR, TL (matching <see cref="PMSDKCornerPin"/> corner order).
        /// The native <c>PMSDKMeshWarp</c> pipeline will overwrite these positions with the
        /// warped output; the UVs and vertex count are what actually matter here.
        /// </summary>
        public static void BuildUnitQuad(out Vector3[] positions, out int[] triangles, out Vector2[] uvs)
        {
            positions = new Vector3[]
            {
                new Vector3(0f, 0f, 0f), // BL
                new Vector3(1f, 0f, 0f), // BR
                new Vector3(1f, 1f, 0f), // TR
                new Vector3(0f, 1f, 0f), // TL
            };
            uvs = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            // Two CCW triangles when viewed from -Z (front) with y-UP.
            triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        }

        /// <summary>Convenience wrapper that returns a fully-configured Unity Mesh.</summary>
        public static Mesh BuildUnitQuadMesh()
        {
            BuildUnitQuad(out var p, out var t, out var u);
            var m = new Mesh { name = "PMSDKSkin_UnitQuad" };
            m.SetVertices(p);
            m.SetUVs(0, u);
            m.SetTriangles(t, 0);
            // Colors default to (1,1,1,1) so the edge-blend vertex-color multiply is a no-op
            // until an EdgeBlend attaches. Sizing the colors array to vertex count avoids a
            // "colors array size mismatch" warning if a later stage touches it.
            m.colors = new Color[] { Color.white, Color.white, Color.white, Color.white };
            // Normals must be present — PMSDKMeshWarp.InitializeNativeMeshes reads normals[i]
            // unconditionally when copying into the native vertex buffer, and would throw
            // IndexOutOfRangeException on a normals-less mesh. All normals point -Z (facing
            // the ortho projector camera which sits at -Z looking +Z into the raster plane).
            m.normals = new Vector3[] {
                new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, -1f),
                new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, -1f),
            };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Default segment count per side of the subdivided quad used by Rectangle/Ellipse skins.</summary>
        public const int QuadSegments = 16;

        /// <summary>
        /// Unit quad split into <paramref name="segments"/> x <paramref name="segments"/> cells.
        /// The native PerspectiveWarp moves every vertex through the homography, so interior
        /// vertices make the rendered texture follow the perspective (a 4-vertex quad is
        /// interpolated per triangle and shears along the diagonal on a keystoned quad, D-022)
        /// and give Auto Adjust's correction grid something to displace (D-044).
        /// Vertex index = row * (segments + 1) + column, row 0 at v = 0; UV equals position.
        /// </summary>
        public static Mesh BuildSubdividedQuadMesh(int segments)
        {
            int n = Mathf.Clamp(segments, 1, 64);
            int side = n + 1;
            Vector3[] positions = new Vector3[side * side];
            Vector2[] uvs = new Vector2[side * side];
            Vector3[] normals = new Vector3[side * side];
            Color[] colors = new Color[side * side];
            for (int row = 0; row < side; row++) {
                for (int column = 0; column < side; column++) {
                    int index = row * side + column;
                    float u = (float)column / n;
                    float v = (float)row / n;
                    positions[index] = new Vector3(u, v, 0f);
                    uvs[index] = new Vector2(u, v);
                    // See BuildUnitQuadMesh: normals are required by PMSDKMeshWarp; colours default white.
                    normals[index] = new Vector3(0f, 0f, -1f);
                    colors[index] = Color.white;
                }
            }
            int[] triangles = new int[n * n * 6];
            int t = 0;
            for (int row = 0; row < n; row++) {
                for (int column = 0; column < n; column++) {
                    int bl = row * side + column;
                    int br = bl + 1;
                    int tl = bl + side;
                    int tr = tl + 1;
                    // Same winding as BuildUnitQuad (0,1,2 / 0,2,3 over BL,BR,TR,TL).
                    triangles[t++] = bl;
                    triangles[t++] = br;
                    triangles[t++] = tr;
                    triangles[t++] = bl;
                    triangles[t++] = tr;
                    triangles[t++] = tl;
                }
            }
            Mesh mesh = new Mesh { name = "PMSDKSkin_SubdividedQuad" };
            mesh.SetVertices(positions);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.normals = normals;
            mesh.colors = colors;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Triangulate a simple polygon in raster space [0..1] using ear-clipping.
        /// Returns positions at z=0, UVs mapped from the polygon's axis-aligned bounding box
        /// (so content stretches to the polygon's bounds — "contain" mapping), and triangle
        /// indices in CCW winding.
        /// </summary>
        /// <returns>True if triangulation succeeded (polygon had ≥3 vertices, no degeneracies
        /// blocked all ears). On failure, positions/triangles/uvs are still populated with a
        /// best-effort result but the caller should treat the skin as invalid.</returns>
        public static bool BuildPolygon(
            IList<Vector2> vertices,
            out Vector3[] positions,
            out int[] triangles,
            out Vector2[] uvs)
        {
            int n = vertices != null ? vertices.Count : 0;
            if (n < 3)
            {
                positions = System.Array.Empty<Vector3>();
                triangles = System.Array.Empty<int>();
                uvs = System.Array.Empty<Vector2>();
                return false;
            }

            // Positions in raster space at z=0. Order preserved from input; ear-clip below
            // handles CCW/CW consistently.
            positions = new Vector3[n];
            uvs = new Vector2[n];

            // Axis-aligned bounding box for UV mapping.
            float xMin = float.MaxValue, yMin = float.MaxValue;
            float xMax = float.MinValue, yMax = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                var v = vertices[i];
                positions[i] = new Vector3(v.x, v.y, 0f);
                if (v.x < xMin) xMin = v.x;
                if (v.y < yMin) yMin = v.y;
                if (v.x > xMax) xMax = v.x;
                if (v.y > yMax) yMax = v.y;
            }
            float w = Mathf.Max(xMax - xMin, 1e-6f);
            float h = Mathf.Max(yMax - yMin, 1e-6f);
            for (int i = 0; i < n; i++)
            {
                uvs[i] = new Vector2((vertices[i].x - xMin) / w, (vertices[i].y - yMin) / h);
            }

            triangles = EarClipTriangulate(vertices);
            // Consider triangulation successful if we produced (n-2) triangles. Ear-clip can
            // stall on self-intersecting or heavily-degenerate polygons; in that case the
            // caller sees fewer than the expected number of triangles.
            return triangles.Length == (n - 2) * 3;
        }

        /// <summary>Convenience wrapper that returns a fully-configured Unity Mesh.</summary>
        public static Mesh BuildPolygonMesh(IList<Vector2> vertices)
        {
            if (!BuildPolygon(vertices, out var p, out var t, out var u))
            {
                // Return an empty (but valid) mesh so consumers don't crash on the render side.
                return new Mesh { name = "PMSDKSkin_Polygon_Invalid" };
            }
            var m = new Mesh { name = "PMSDKSkin_Polygon" };
            m.SetVertices(p);
            m.SetUVs(0, u);
            // UV1 = raster position (the positions are raster space): the Auto Adjust warp map is sampled there
            // (PMSDK/UnlitWarp, D-055), as quad skins get it from PMSDKMeshWarp.
            Vector2[] raster = new Vector2[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                raster[i] = new Vector2(p[i].x, p[i].y);
            }
            m.SetUVs(1, raster);
            m.SetTriangles(t, 0);
            var colors = new Color[p.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            m.colors = colors;
            // See BuildUnitQuadMesh: normals are required for PMSDKMeshWarp — even though
            // polygon skins bypass the native warp, the same GameObject may still have a
            // (disabled) PMSDKMeshWarp component whose OnEnable will run. All -Z.
            var normals = new Vector3[p.Length];
            for (int i = 0; i < normals.Length; i++) normals[i] = new Vector3(0f, 0f, -1f);
            m.normals = normals;
            m.RecalculateBounds();
            return m;
        }

        // -------- winding / area helpers --------

        /// <summary>Signed polygon area under the standard shoelace formula (y-UP: positive = CCW).</summary>
        public static float SignedArea(IList<Vector2> vertices)
        {
            int n = vertices.Count;
            if (n < 3) return 0f;
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = vertices[i];
                Vector2 b = vertices[(i + 1) % n];
                sum += a.x * b.y - b.x * a.y;
            }
            return 0.5f * sum;
        }

        public static bool IsCounterClockwise(IList<Vector2> vertices) => SignedArea(vertices) > 0f;

        // -------- ear-clip triangulation --------

        /// <summary>
        /// Triangulate a simple polygon in [0..1] raster space. Handles CW/CCW input
        /// (internally normalizes to CCW). Returns triangle indices into the ORIGINAL
        /// <paramref name="vertices"/> array.
        ///
        /// Complexity: O(n^2). Fine for vertex counts up to 128 (freeform cap).
        /// Assumptions: polygon is simple (non-self-intersecting). Behavior on
        /// self-intersecting input is undefined but bounded (returns partial triangulation).
        /// </summary>
        public static int[] EarClipTriangulate(IList<Vector2> vertices)
        {
            int n = vertices.Count;
            if (n < 3) return System.Array.Empty<int>();

            // Build the working index list. If the polygon is clockwise, reverse it so the
            // ear-clip predicate ("interior angle < 180 in CCW space") holds.
            var indices = new List<int>(n);
            bool ccw = IsCounterClockwise(vertices);
            if (ccw)
                for (int i = 0; i < n; i++) indices.Add(i);
            else
                for (int i = n - 1; i >= 0; i--) indices.Add(i);

            var tris = new List<int>((n - 2) * 3);

            int guard = 0; // hard cap protects against stall on degenerate input
            int guardMax = n * n + 8;
            while (indices.Count > 2 && guard++ < guardMax)
            {
                bool earFound = false;
                int m = indices.Count;
                for (int i = 0; i < m; i++)
                {
                    int i0 = indices[(i + m - 1) % m];
                    int i1 = indices[i];
                    int i2 = indices[(i + 1) % m];
                    Vector2 a = vertices[i0], b = vertices[i1], c = vertices[i2];

                    // Convex test in CCW space: cross > 0.
                    if (Cross(a, b, c) <= 0f) continue;

                    // No other polygon vertex may lie strictly inside triangle (a,b,c).
                    bool anyInside = false;
                    for (int k = 0; k < m; k++)
                    {
                        int ik = indices[k];
                        if (ik == i0 || ik == i1 || ik == i2) continue;
                        if (PointInTriangle(vertices[ik], a, b, c)) { anyInside = true; break; }
                    }
                    if (anyInside) continue;

                    // Emit the ear, then remove its tip index.
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }
                if (!earFound) break; // stall — return partial triangulation
            }

            return tris.ToArray();
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            // Same-sign half-plane test. All three signs equal (all ≥0 or all ≤0) → inside.
            float d1 = Cross(p, a, b);
            float d2 = Cross(p, b, c);
            float d3 = Cross(p, c, a);
            bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
            bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);
            return !(hasNeg && hasPos);
        }
    }
}
