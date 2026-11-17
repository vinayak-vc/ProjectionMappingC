using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="PMSDKSkinMeshBuilder"/>. Pure static / value tests; no
    /// Unity runtime allocations touched. Cover the M-Sk1 acceptance criteria for the
    /// mesh-builder listed in <c>docs/multi-skin-design.md</c>:
    ///
    ///   - unit-quad has 4 verts / 2 tris and BL,BR,TR,TL winding
    ///   - polygon triangulation produces (n-2) triangles for a simple polygon
    ///   - CW-input polygons still produce (n-2) triangles (internally normalized to CCW)
    ///   - UV mapping stretches to the polygon's bounding box
    ///   - degenerate inputs (n<3) return empty geometry, not an exception
    ///   - concave polygons triangulate correctly (ear-clip covers the whole shape)
    /// </summary>
    public class PMSDKSkinMeshBuilderTests
    {
        // ---- unit quad ----

        [Test]
        public void UnitQuad_HasFourVerticesTwoTriangles()
        {
            PMSDKSkinMeshBuilder.BuildUnitQuad(out var p, out var t, out var u);
            Assert.AreEqual(4, p.Length);
            Assert.AreEqual(4, u.Length);
            Assert.AreEqual(6, t.Length); // 2 tris x 3 indices
        }

        [Test]
        public void UnitQuad_HasBLBRTRTLWinding()
        {
            PMSDKSkinMeshBuilder.BuildUnitQuad(out var p, out _, out _);
            Assert.AreEqual(new Vector3(0f, 0f, 0f), p[0]);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), p[1]);
            Assert.AreEqual(new Vector3(1f, 1f, 0f), p[2]);
            Assert.AreEqual(new Vector3(0f, 1f, 0f), p[3]);
        }

        [Test]
        public void UnitQuad_UVsFillZeroToOne()
        {
            PMSDKSkinMeshBuilder.BuildUnitQuad(out _, out _, out var u);
            Assert.AreEqual(new Vector2(0f, 0f), u[0]);
            Assert.AreEqual(new Vector2(1f, 1f), u[2]);
        }

        // ---- signed area / winding ----

        [Test]
        public void SignedArea_CCWSquare_IsPositive()
        {
            var v = new List<Vector2> {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f),
            };
            Assert.Greater(PMSDKSkinMeshBuilder.SignedArea(v), 0f);
            Assert.IsTrue(PMSDKSkinMeshBuilder.IsCounterClockwise(v));
        }

        [Test]
        public void SignedArea_CWSquare_IsNegative()
        {
            var v = new List<Vector2> {
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 0f),
            };
            Assert.Less(PMSDKSkinMeshBuilder.SignedArea(v), 0f);
            Assert.IsFalse(PMSDKSkinMeshBuilder.IsCounterClockwise(v));
        }

        // ---- polygon triangulation ----

        [Test]
        public void EarClip_ConvexPentagon_ProducesThreeTriangles()
        {
            var v = RegularPolygon(5, 0.5f, 0.5f, 0.25f);
            var tris = PMSDKSkinMeshBuilder.EarClipTriangulate(v);
            Assert.AreEqual((5 - 2) * 3, tris.Length);
        }

        [Test]
        public void EarClip_ConcaveArrow_TriangulatesFully()
        {
            // A concave "chevron" — CCW arrow pointing right. Ear-clip must handle it.
            var v = new List<Vector2> {
                new Vector2(0.1f, 0.2f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.1f, 0.8f),
                new Vector2(0.3f, 0.5f), // reflex vertex (the concave tip)
            };
            var tris = PMSDKSkinMeshBuilder.EarClipTriangulate(v);
            Assert.AreEqual((4 - 2) * 3, tris.Length,
                "Concave quadrilateral must produce 2 triangles.");
        }

        [Test]
        public void EarClip_CWInput_NormalizedInternally()
        {
            // Same square as CCW test but wound CW — ear-clip must reverse internally and
            // still produce 2 triangles.
            var v = new List<Vector2> {
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(1f, 1f), new Vector2(1f, 0f),
            };
            var tris = PMSDKSkinMeshBuilder.EarClipTriangulate(v);
            Assert.AreEqual(6, tris.Length);
        }

        [Test]
        public void EarClip_LessThanThree_ReturnsEmpty()
        {
            Assert.AreEqual(0, PMSDKSkinMeshBuilder.EarClipTriangulate(new List<Vector2>()).Length);
            Assert.AreEqual(0, PMSDKSkinMeshBuilder.EarClipTriangulate(
                new List<Vector2> { new Vector2(0f, 0f) }).Length);
            Assert.AreEqual(0, PMSDKSkinMeshBuilder.EarClipTriangulate(
                new List<Vector2> { Vector2.zero, Vector2.right }).Length);
        }

        // ---- BuildPolygon integration ----

        [Test]
        public void BuildPolygon_UVsStretchToBoundingBox()
        {
            // Off-center polygon: bounding box is [0.2..0.6] x [0.3..0.7]. UVs must map
            // (0.2, 0.3) → (0,0) and (0.6, 0.7) → (1,1).
            var v = new List<Vector2> {
                new Vector2(0.2f, 0.3f), new Vector2(0.6f, 0.3f),
                new Vector2(0.6f, 0.7f), new Vector2(0.2f, 0.7f),
            };
            bool ok = PMSDKSkinMeshBuilder.BuildPolygon(v, out var p, out var t, out var u);
            Assert.IsTrue(ok);
            Assert.AreEqual(4, p.Length);
            Assert.AreEqual(6, t.Length);
            Assert.That(u[0].x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(u[0].y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(u[2].x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(u[2].y, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void BuildPolygon_DegenerateInput_ReturnsFalseAndEmpty()
        {
            bool ok = PMSDKSkinMeshBuilder.BuildPolygon(
                new List<Vector2> { Vector2.zero, Vector2.right }, out var p, out var t, out var u);
            Assert.IsFalse(ok);
            Assert.AreEqual(0, p.Length);
            Assert.AreEqual(0, t.Length);
            Assert.AreEqual(0, u.Length);
        }

        [Test]
        public void BuildPolygon_HexagonAtCap_ProducesFourTriangles()
        {
            var v = RegularPolygon(6, 0.5f, 0.5f, 0.25f);
            bool ok = PMSDKSkinMeshBuilder.BuildPolygon(v, out var p, out var t, out var u);
            Assert.IsTrue(ok);
            Assert.AreEqual(6, p.Length);
            Assert.AreEqual((6 - 2) * 3, t.Length);
        }

        [Test]
        public void BuildPolygon_AtVertexCap_StillWorks()
        {
            // Regular polygon at the freeform cap — proves ear-clip performance is
            // acceptable at the documented worst case (O(n^2), n=128).
            var v = RegularPolygon(PMSDKSkinMeshBuilder.MaxFreeformVertices, 0.5f, 0.5f, 0.4f);
            bool ok = PMSDKSkinMeshBuilder.BuildPolygon(v, out _, out var t, out _);
            Assert.IsTrue(ok);
            Assert.AreEqual((PMSDKSkinMeshBuilder.MaxFreeformVertices - 2) * 3, t.Length);
        }

        // ---- helpers ----

        private static List<Vector2> RegularPolygon(int n, float cx, float cy, float r)
        {
            var list = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 2f * i / n + Mathf.PI * 0.5f;
                list.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            return list;
        }
    }
}
