using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>Auto-blend, dense-warp, pose-solver and persistence regression tests.</summary>
    public class PMSDKCalibrationLogicTests
    {
        // ---------- helpers ----------

        private static PMSDKGrayCodeDecode.Correspondence[] MakeCorrespondence(
            int camW, int camH, System.Func<int, int, (bool valid, int px, int py)> f)
        {
            var arr = new PMSDKGrayCodeDecode.Correspondence[camW * camH];
            for (int y = 0; y < camH; y++)
                for (int x = 0; x < camW; x++)
                {
                    var (valid, px, py) = f(x, y);
                    int i = y * camW + x;
                    if (valid) { arr[i].Valid = true; arr[i].ProjectorX = px; arr[i].ProjectorY = py; }
                }
            return arr;
        }

        // ---------- auto-blend ----------

        [Test]
        public void AutoBlend_TwoProjectors_FacingEdgesOnly()
        {
            int camW = 120, camH = 100, projW = 100, projH = 100;
            var a = MakeCorrespondence(camW, camH, (x, y) => (x < 60, (int)(x / 60f * projW), (int)(y / 100f * projH)));
            var b = MakeCorrespondence(camW, camH, (x, y) => (x >= 40 && x < 100, (int)((x - 40) / 60f * projW), (int)(y / 100f * projH)));

            var w = PMSDKAutoBlend.Compute(new List<PMSDKGrayCodeDecode.Correspondence[]> { a, b }, camW, camH, projW, projH);

            Assert.Greater(w[0].Right, 0.30f); Assert.Less(w[0].Right, 0.38f);
            Assert.Less(w[0].Left, 0.02f); Assert.Less(w[0].Top, 0.02f); Assert.Less(w[0].Bottom, 0.02f);
            Assert.Greater(w[1].Left, 0.30f); Assert.Less(w[1].Left, 0.38f);
            Assert.Less(w[1].Right, 0.02f);
        }

        [Test]
        public void AutoBlend_ThreeInARow_MiddleGetsBothEdges()
        {
            int camW = 120, camH = 100, projW = 100, projH = 100;
            var l = MakeCorrespondence(camW, camH, (x, y) => (x < 50, (int)(x / 50f * projW), (int)(y / 100f * projH)));
            var m = MakeCorrespondence(camW, camH, (x, y) => (x >= 40 && x < 80, (int)((x - 40) / 40f * projW), (int)(y / 100f * projH)));
            var r = MakeCorrespondence(camW, camH, (x, y) => (x >= 70, (int)((x - 70) / 50f * projW), (int)(y / 100f * projH)));

            var w = PMSDKAutoBlend.Compute(new List<PMSDKGrayCodeDecode.Correspondence[]> { l, m, r }, camW, camH, projW, projH);

            Assert.Greater(w[0].Right, 0.05f);
            Assert.Greater(w[1].Left, 0.05f);
            Assert.Greater(w[1].Right, 0.05f);
            Assert.Greater(w[2].Left, 0.05f);
            Assert.Less(w[0].Left, 0.02f);
            Assert.Less(w[2].Right, 0.02f);
        }

        [Test]
        public void AutoBlend_NoOverlap_AllZero()
        {
            int camW = 120, camH = 100, projW = 100, projH = 100;
            var a = MakeCorrespondence(camW, camH, (x, y) => (x < 40, (int)(x / 40f * projW), (int)(y / 100f * projH)));
            var b = MakeCorrespondence(camW, camH, (x, y) => (x >= 80, (int)((x - 80) / 40f * projW), (int)(y / 100f * projH)));

            var w = PMSDKAutoBlend.Compute(new List<PMSDKGrayCodeDecode.Correspondence[]> { a, b }, camW, camH, projW, projH);
            Assert.Less(w[0].Right, 0.02f);
            Assert.Less(w[1].Left, 0.02f);
        }

        // ---------- dense warp ----------

        [Test]
        public void DenseWarp_IdentityCorrespondence_YieldsRegularLattice()
        {
            int size = 100;
            var corr = MakeCorrespondence(size, size, (x, y) => (true, x, y));
            // Full-frame target, camera top-left convention, TL,TR,BR,BL.
            var target = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };

            var pts = PMSDKDenseWarp.FitGrid(corr, size, size, size, size, target, 3, 3);
            Assert.IsNotNull(pts);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    var p = pts[row * 3 + col];
                    Assert.AreEqual(col / 2f, p.x, 0.03f, $"u at {col},{row}");
                    Assert.AreEqual(row / 2f, p.y, 0.03f, $"v at {col},{row}");
                }
        }

        [Test]
        public void DenseWarp_NoValidSamples_ReturnsNull()
        {
            var corr = MakeCorrespondence(16, 16, (x, y) => (false, 0, 0));
            var target = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            Assert.IsNull(PMSDKDenseWarp.FitGrid(corr, 16, 16, 16, 16, target, 3, 3));
        }

        // ---------- pose solver ----------

        [Test]
        public void PoseSolver_RecoversPerturbedPose()
        {
            var go = new GameObject("PoseSolverTestCam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = 38f;
                cam.transform.position = new Vector3(0f, 1.9f, -6.2f);
                cam.transform.LookAt(new Vector3(0f, 1.6f, 0f));

                Vector3 truePos = cam.transform.position;
                float trueFov = cam.fieldOfView;

                // Non-coplanar world points around a virtual object.
                var world = new[]
                {
                    new Vector3(-1.5f, 0.8f, -1.5f), new Vector3(1.5f, 0.8f, -1.5f),
                    new Vector3(-1.5f, 0.8f, 1.5f), new Vector3(1.5f, 0.8f, 1.5f),
                    new Vector3(-1f, 2f, -1f), new Vector3(1f, 2f, -1f),
                    new Vector3(0f, 3.45f, 0f), new Vector3(-1.05f, 2.4f, 1.05f), new Vector3(1.05f, 2.4f, 1.05f)
                };
                var corr = new List<PMSDKPoseSolver.Correspondence>();
                foreach (var p in world)
                {
                    Vector3 vp = cam.WorldToViewportPoint(p);
                    if (vp.z > 0) corr.Add(new PMSDKPoseSolver.Correspondence { World = p, Viewport = new Vector2(vp.x, vp.y) });
                }
                Assert.GreaterOrEqual(corr.Count, 6);

                cam.transform.position = truePos + new Vector3(0.8f, -0.5f, 1.2f);
                cam.transform.eulerAngles += new Vector3(6f, -8f, 3f);
                cam.fieldOfView = trueFov + 10f;

                float rms = PMSDKPoseSolver.Solve(cam, corr, 120, solveFov: true);

                Assert.Less(rms, 1e-4f, "viewport RMS");
                Assert.Less(Vector3.Distance(cam.transform.position, truePos), 1e-2f, "position");
                Assert.Less(Mathf.Abs(cam.fieldOfView - trueFov), 0.1f, "fov");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PoseSolver_TooFewPoints_Fails()
        {
            var go = new GameObject("PoseSolverTestCam2");
            try
            {
                var cam = go.AddComponent<Camera>();
                var corr = new List<PMSDKPoseSolver.Correspondence>
                {
                    new PMSDKPoseSolver.Correspondence { World = Vector3.zero, Viewport = new Vector2(0.5f, 0.5f) }
                };
                Assert.IsTrue(float.IsPositiveInfinity(PMSDKPoseSolver.Solve(cam, corr)));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------- calibration file IO ----------

        [Test]
        public void CalibrationIO_SaveLoadRoundtrip()
        {
            string path = Path.Combine(Path.GetTempPath(), "pmsdk_test_calibration.json");
            try
            {
                var file = new PMSDKCalibrationFile
                {
                    surfaces = new[]
                    {
                        new PMSDKSurfaceCalibration
                        {
                            id = "TestScreen",
                            targetDisplay = 2,
                            tl = new Vector2(0.1f, 0.9f), tr = new Vector2(0.95f, 0.92f),
                            bl = new Vector2(0.05f, 0.1f), br = new Vector2(0.9f, 0.08f),
                            blendRight = 0.18f, gamma = 2.2f, blackLevel = 0.05f,
                            gridEnabled = true, gridColumns = 3, gridRows = 3,
                            gridPoints = new[]
                            {
                                new Vector2(0,0), new Vector2(0.5f,0), new Vector2(1,0),
                                new Vector2(0,0.5f), new Vector2(0.55f,0.5f), new Vector2(1,0.5f),
                                new Vector2(0,1), new Vector2(0.5f,1), new Vector2(1,1)
                            },
                            hasTarget = true,
                            targetCorners = new[] { new Vector2(0.2f,0.8f), new Vector2(0.8f,0.8f), new Vector2(0.8f,0.2f), new Vector2(0.2f,0.2f) }
                        }
                    }
                };

                Assert.IsTrue(PMSDKCalibrationIO.Save(path, file));
                var loaded = PMSDKCalibrationIO.Load(path);

                Assert.IsNotNull(loaded);
                Assert.AreEqual(1, loaded.surfaces.Length);
                var s = loaded.surfaces[0];
                Assert.AreEqual("TestScreen", s.id);
                Assert.AreEqual(2, s.targetDisplay);
                Assert.AreEqual(0.18f, s.blendRight, 1e-5f);
                Assert.IsTrue(s.gridEnabled);
                Assert.AreEqual(9, s.gridPoints.Length);
                Assert.AreEqual(0.55f, s.gridPoints[4].x, 1e-5f);
                Assert.IsTrue(s.hasTarget);
                Assert.AreEqual(0.8f, s.targetCorners[1].x, 1e-5f);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void CalibrationIO_MissingFile_ReturnsNull()
        {
            Assert.IsNull(PMSDKCalibrationIO.Load(Path.Combine(Path.GetTempPath(), "pmsdk_does_not_exist_12345.json")));
        }
    }
}
