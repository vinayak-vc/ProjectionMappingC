using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="PMSDKSkinEditorGeometry"/>. Pure math; no scene,
    /// no Play mode. Covers the M-Sk2 phase A picking + hit-testing helpers.
    /// </summary>
    public class PMSDKSkinEditorGeometryTests
    {
        // ---- PickCorner ----

        [Test]
        public void PickCorner_ReturnsIndexWithinThreshold()
        {
            var v = new List<Vector2> {
                new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.1f),
                new Vector2(0.9f, 0.9f), new Vector2(0.1f, 0.9f),
            };
            int i = PMSDKSkinEditorGeometry.PickCorner(new Vector2(0.11f, 0.09f), v, 0.05f);
            Assert.AreEqual(0, i);
        }

        [Test]
        public void PickCorner_ReturnsNegativeOneOutsideThreshold()
        {
            var v = new List<Vector2> { new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.1f) };
            int i = PMSDKSkinEditorGeometry.PickCorner(new Vector2(0.5f, 0.5f), v, 0.05f);
            Assert.AreEqual(-1, i);
        }

        [Test]
        public void PickCorner_PicksNearestOfMultiple()
        {
            var v = new List<Vector2> {
                new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f),
                new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f),
            };
            // Nearer to (1,1) than to any other corner.
            int i = PMSDKSkinEditorGeometry.PickCorner(new Vector2(0.95f, 0.98f), v, 0.2f);
            Assert.AreEqual(2, i);
        }

        // ---- PickEdgeMidpoint ----

        [Test]
        public void PickEdgeMidpoint_ReturnsEdgeIndex()
        {
            var v = new List<Vector2> {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f),
            };
            // Midpoint of edge 0 (BL→BR) is (0.5, 0.0). Pick right next to it.
            int i = PMSDKSkinEditorGeometry.PickEdgeMidpoint(new Vector2(0.51f, 0.005f), v, 0.05f);
            Assert.AreEqual(0, i);
        }

        [Test]
        public void PickEdgeMidpoint_ReturnsNegativeOneAtCorner()
        {
            var v = new List<Vector2> {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f),
            };
            // (0.05, 0.05) is closer to corner 0 than to any edge midpoint.
            int i = PMSDKSkinEditorGeometry.PickEdgeMidpoint(new Vector2(0.05f, 0.05f), v, 0.05f);
            Assert.AreEqual(-1, i);
        }

        // ---- ContainsPoint / ContainsPointEllipse ----

        [Test]
        public void ContainsPoint_InsideConvexSquare()
        {
            var v = new List<Vector2> {
                new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.2f),
                new Vector2(0.8f, 0.8f), new Vector2(0.2f, 0.8f),
            };
            Assert.IsTrue(PMSDKSkinEditorGeometry.ContainsPoint(v, new Vector2(0.5f, 0.5f)));
            Assert.IsFalse(PMSDKSkinEditorGeometry.ContainsPoint(v, new Vector2(0.1f, 0.5f)));
            Assert.IsFalse(PMSDKSkinEditorGeometry.ContainsPoint(v, new Vector2(0.9f, 0.5f)));
        }

        [Test]
        public void ContainsPoint_ConcavePolygonMatchesRayCast()
        {
            // Concave chevron — dip on the right side.
            var v = new List<Vector2> {
                new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.2f),
                new Vector2(0.5f, 0.5f), new Vector2(0.9f, 0.8f),
                new Vector2(0.1f, 0.8f),
            };
            // (0.7, 0.5) is in the concave notch — OUTSIDE.
            Assert.IsFalse(PMSDKSkinEditorGeometry.ContainsPoint(v, new Vector2(0.7f, 0.5f)));
            // (0.3, 0.5) is inside the left lobe.
            Assert.IsTrue(PMSDKSkinEditorGeometry.ContainsPoint(v, new Vector2(0.3f, 0.5f)));
        }

        [Test]
        public void ContainsPointEllipse_InsideAndOutside()
        {
            var v = new List<Vector2> {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f),
            };
            // Ellipse inscribed in [0,1]^2 → unit circle centered at (0.5, 0.5) with r=0.5.
            Assert.IsTrue(PMSDKSkinEditorGeometry.ContainsPointEllipse(v, new Vector2(0.5f, 0.5f)));
            Assert.IsTrue(PMSDKSkinEditorGeometry.ContainsPointEllipse(v, new Vector2(0.7f, 0.7f)));
            // Corner (0,0) is well outside the inscribed circle.
            Assert.IsFalse(PMSDKSkinEditorGeometry.ContainsPointEllipse(v, new Vector2(0f, 0f)));
        }

        // ---- BuildRectFromDrag ----

        [Test]
        public void BuildRectFromDrag_HandlesAnyCornerOrder()
        {
            // Drag from top-right to bottom-left — output must still be BL, BR, TR, TL.
            var rect = PMSDKSkinEditorGeometry.BuildRectFromDrag(
                new Vector2(0.8f, 0.7f), new Vector2(0.2f, 0.3f));
            Assert.AreEqual(4, rect.Count);
            Assert.AreEqual(new Vector2(0.2f, 0.3f), rect[0]);
            Assert.AreEqual(new Vector2(0.8f, 0.3f), rect[1]);
            Assert.AreEqual(new Vector2(0.8f, 0.7f), rect[2]);
            Assert.AreEqual(new Vector2(0.2f, 0.7f), rect[3]);
        }

        // ---- ClampToRaster ----

        [Test]
        public void ClampToRaster_ClipsBothAxes()
        {
            Assert.AreEqual(new Vector2(0f, 1f),
                PMSDKSkinEditorGeometry.ClampToRaster(new Vector2(-0.5f, 1.5f)));
            Assert.AreEqual(new Vector2(0.3f, 0.7f),
                PMSDKSkinEditorGeometry.ClampToRaster(new Vector2(0.3f, 0.7f)));
        }

        // ---- Bounds ----

        [Test]
        public void Bounds_ReturnsMinMaxRect()
        {
            var v = new List<Vector2> {
                new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.1f),
                new Vector2(0.6f, 0.9f), new Vector2(0.4f, 0.5f),
            };
            var r = PMSDKSkinEditorGeometry.Bounds(v);
            Assert.That(r.xMin, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(r.yMin, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(r.xMax, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(r.yMax, Is.EqualTo(0.9f).Within(1e-5f));
        }
    }
}
