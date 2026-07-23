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

        // ---------- presets ----------

        [Test]
        public void Presets_SanitizeAndPath()
        {
            Assert.AreEqual("day_show", PMSDKCalibrationIO.SanitizePresetName("day/show"));
            Assert.AreEqual("default", PMSDKCalibrationIO.SanitizePresetName("   "));
            string basePath = Path.Combine(Path.GetTempPath(), "pmsdk_calibration.json");
            StringAssert.EndsWith("pmsdk_preset_night.json", PMSDKCalibrationIO.PresetPath(basePath, "night"));
        }

        [Test]
        public void Presets_SaveListLoadDeleteRoundtrip()
        {
            string dir = Path.Combine(Path.GetTempPath(), "pmsdk_preset_tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string basePath = Path.Combine(dir, "pmsdk_calibration.json");
            try
            {
                var file = new PMSDKCalibrationFile
                {
                    surfaces = new[] { new PMSDKSurfaceCalibration { id = "S", blendRight = 0.33f } }
                };
                Assert.IsTrue(PMSDKCalibrationIO.Save(PMSDKCalibrationIO.PresetPath(basePath, "day"), file));
                file.surfaces[0].blendRight = 0.11f;
                Assert.IsTrue(PMSDKCalibrationIO.Save(PMSDKCalibrationIO.PresetPath(basePath, "night"), file));

                var names = PMSDKCalibrationIO.ListPresets(basePath);
                CollectionAssert.AreEquivalent(new[] { "day", "night" }, names);

                var day = PMSDKCalibrationIO.Load(PMSDKCalibrationIO.PresetPath(basePath, "day"));
                Assert.AreEqual(0.33f, day.surfaces[0].blendRight, 1e-5f);

                Assert.IsTrue(PMSDKCalibrationIO.DeletePreset(basePath, "day"));
                CollectionAssert.AreEquivalent(new[] { "night" }, PMSDKCalibrationIO.ListPresets(basePath));
                Assert.IsFalse(PMSDKCalibrationIO.DeletePreset(basePath, "day"));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // ---------- luminance compensation ----------

        // A 1:1 camera==raster identity correspondence, so a white capture indexed by
        // camera pixel lands one-to-one in the raster gain map.
        private static PMSDKGrayCodeDecode.Correspondence[] IdentityCorr(int size, System.Func<int, int, bool> valid = null)
        {
            return MakeCorrespondence(size, size, (x, y) => (valid == null || valid(x, y), x, y));
        }

        private static byte[] WhiteCapture(int size, System.Func<int, int, int> lumAt)
        {
            var w = new byte[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    w[y * size + x] = (byte)Mathf.Clamp(lumAt(x, y), 0, 255);
            return w;
        }

        [Test]
        public void Luminance_VignetteFlattenedToTarget()
        {
            int size = 64;
            // Centre-bright gradient (min 130 at (0,0)); every cell distinct.
            var white = WhiteCapture(size, (x, y) => 130 + (x + y) / 2);
            var maps = PMSDKLuminanceCompensation.Compute(
                new List<byte[]> { white }, new List<PMSDKGrayCodeDecode.Correspondence[]> { IdentityCorr(size) },
                size, size, size, size, size, size,
                gainMin: 0.3f, smoothRadius: 0, targetPercentile: 0f);

            Assert.AreEqual(1, maps.Length);
            Assert.IsTrue(maps[0].Valid);
            var g = maps[0].Gain;
            const float target = 130f; // the darkest measured cell

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float gain = g[y * size + x];
                    Assert.LessOrEqual(gain, 1f + 1e-4f);
                    Assert.GreaterOrEqual(gain, 0.3f - 1e-4f);
                    // measured * gain collapses to the shared target (dim-only flatten).
                    float measured = white[y * size + x];
                    Assert.AreEqual(target, measured * gain, 0.6f);
                }

            // Brighter centre is dimmed more than the darker corner.
            Assert.Less(g[(size - 1) * size + (size - 1)], g[0]);
            Assert.AreEqual(1f, g[0], 1e-3f); // darkest cell is untouched
        }

        [Test]
        public void Luminance_EqualizesAcrossProjectors()
        {
            int size = 32;
            var bright = WhiteCapture(size, (x, y) => 200);
            var dim = WhiteCapture(size, (x, y) => 140);
            var maps = PMSDKLuminanceCompensation.Compute(
                new List<byte[]> { bright, dim },
                new List<PMSDKGrayCodeDecode.Correspondence[]> { IdentityCorr(size), IdentityCorr(size) },
                size, size, size, size, size, size,
                gainMin: 0.3f, smoothRadius: 0, targetPercentile: 0f);

            // Shared target = dimmest projector (140): bright dims to 0.7, dim stays 1.
            Assert.AreEqual(0.7f, maps[0].Gain[0], 1e-3f);
            Assert.AreEqual(1.0f, maps[1].Gain[0], 1e-3f);
        }

        [Test]
        public void Luminance_GainClampedAtFloor()
        {
            int size = 16;
            var bright = WhiteCapture(size, (x, y) => 250);
            var dim = WhiteCapture(size, (x, y) => 50); // target -> 50, ratio 0.2 < floor
            var maps = PMSDKLuminanceCompensation.Compute(
                new List<byte[]> { bright, dim },
                new List<PMSDKGrayCodeDecode.Correspondence[]> { IdentityCorr(size), IdentityCorr(size) },
                size, size, size, size, size, size,
                gainMin: 0.5f, smoothRadius: 0, targetPercentile: 0f);

            Assert.AreEqual(0.5f, maps[0].Gain[0], 1e-3f); // clamped, not 0.2
        }

        [Test]
        public void Luminance_FillsHolesAndStaysFinite()
        {
            int size = 32;
            var white = WhiteCapture(size, (x, y) => 150 + x);
            // Only every other cell is lit — the rest are holes to be filled.
            var corr = IdentityCorr(size, (x, y) => ((x + y) & 1) == 0);
            var maps = PMSDKLuminanceCompensation.Compute(
                new List<byte[]> { white }, new List<PMSDKGrayCodeDecode.Correspondence[]> { corr },
                size, size, size, size, size, size,
                gainMin: 0.4f, smoothRadius: 1, targetPercentile: 0f);

            Assert.IsTrue(maps[0].Valid);
            foreach (var gain in maps[0].Gain)
            {
                Assert.IsFalse(float.IsNaN(gain));
                Assert.GreaterOrEqual(gain, 0.4f - 1e-4f);
                Assert.LessOrEqual(gain, 1f + 1e-4f);
            }
        }

        [Test]
        public void Luminance_EmptyAndMissingInputsAreSafe()
        {
            var none = PMSDKLuminanceCompensation.Compute(
                new List<byte[]>(), new List<PMSDKGrayCodeDecode.Correspondence[]>(),
                0, 0, 64, 64, 64, 64);
            Assert.AreEqual(0, none.Length);

            // Correspondence present but no white capture -> pass-through (gain 1), invalid.
            int size = 8;
            var maps = PMSDKLuminanceCompensation.Compute(
                new List<byte[]> { null }, new List<PMSDKGrayCodeDecode.Correspondence[]> { IdentityCorr(size) },
                size, size, size, size, size, size);
            Assert.IsFalse(maps[0].Valid);
            foreach (var gain in maps[0].Gain) Assert.AreEqual(1f, gain, 1e-6f);
        }

        [Test]
        public void GainCodec_Roundtrips()
        {
            var gain = new float[] { 0f, 0.5f, 0.6f, 0.75f, 1f };
            string encoded = PMSDKGainCodec.Encode(gain);
            var decoded = PMSDKGainCodec.Decode(encoded, gain.Length);
            Assert.IsNotNull(decoded);
            for (int i = 0; i < gain.Length; i++)
                Assert.AreEqual(gain[i], decoded[i], 1f / 255f); // 8-bit quantization

            Assert.IsNull(PMSDKGainCodec.Decode(encoded, gain.Length + 1)); // count mismatch
            Assert.IsNull(PMSDKGainCodec.Decode(null, 4));
            Assert.IsNull(PMSDKGainCodec.Encode(null));
        }

        // ---------- per-region black-level compensation ----------

        [Test]
        public void BlackLevel_LiftsSingleProjectorRegionToOverlap()
        {
            int size = 64;
            // Projector A covers the whole camera; B covers only the right half (x >= 32),
            // so A-only is the left half and the overlap is the right half.
            var corrA = MakeCorrespondence(size, size, (x, y) => (true, x, y));
            var corrB = MakeCorrespondence(size, size, (x, y) =>
                (x >= size / 2, Mathf.Clamp((x - size / 2) * 2, 0, size - 1), y));
            // Uniform floors (20) and whites (200): span 180, overlap black 40.
            var whiteA = WhiteCapture(size, (x, y) => 200);
            var whiteB = WhiteCapture(size, (x, y) => 200);
            var blackA = WhiteCapture(size, (x, y) => 20);
            var blackB = WhiteCapture(size, (x, y) => 20);

            var maps = PMSDKBlackLevelCompensation.Compute(
                new List<byte[]> { whiteA, whiteB },
                new List<byte[]> { blackA, blackB },
                new List<PMSDKGrayCodeDecode.Correspondence[]> { corrA, corrB },
                size, size, size, size, size, size,
                maxLift: 0.5f, smoothRadius: 0, targetPercentile: 1f);

            Assert.IsTrue(maps[0].Valid);
            var a = maps[0].Lift;
            // deficit in A-only region = overlap(40) - A(20) = 20; lift = 20/180 = 0.111.
            Assert.AreEqual(20f / 180f, a[32 * size + 10], 0.01f); // left: A alone -> lifted
            Assert.AreEqual(0f, a[32 * size + 50], 0.01f);         // right: overlap -> no lift
            // B only ever appears in the overlap, so it needs no lift.
            Assert.IsFalse(maps[1].Valid);
        }

        [Test]
        public void BlackLevel_UniformFullOverlapNeedsNoLift()
        {
            int size = 32;
            // Both projectors cover the whole camera identically -> every point is overlap,
            // already at the target, so no uplift anywhere.
            var corr = MakeCorrespondence(size, size, (x, y) => (true, x, y));
            var white = WhiteCapture(size, (x, y) => 180);
            var black = WhiteCapture(size, (x, y) => 15);
            var maps = PMSDKBlackLevelCompensation.Compute(
                new List<byte[]> { white, white },
                new List<byte[]> { black, black },
                new List<PMSDKGrayCodeDecode.Correspondence[]> { corr, corr },
                size, size, size, size, size, size,
                maxLift: 0.5f, smoothRadius: 0, targetPercentile: 1f);

            Assert.IsFalse(maps[0].Valid);
            Assert.IsFalse(maps[1].Valid);
            foreach (var v in maps[0].Lift) Assert.AreEqual(0f, v, 1e-4f);
        }

        [Test]
        public void BlackLevel_LiftClampedAtMax()
        {
            int size = 32;
            var corrA = MakeCorrespondence(size, size, (x, y) => (true, x, y));
            var corrB = MakeCorrespondence(size, size, (x, y) =>
                (x >= size / 2, Mathf.Clamp((x - size / 2) * 2, 0, size - 1), y));
            // Small span (50) but big overlap-vs-single deficit (40) -> lift 0.8, clamps.
            var whiteA = WhiteCapture(size, (x, y) => 60);
            var whiteB = WhiteCapture(size, (x, y) => 60);
            var blackA = WhiteCapture(size, (x, y) => 10);
            var blackB = WhiteCapture(size, (x, y) => 40);
            var maps = PMSDKBlackLevelCompensation.Compute(
                new List<byte[]> { whiteA, whiteB },
                new List<byte[]> { blackA, blackB },
                new List<PMSDKGrayCodeDecode.Correspondence[]> { corrA, corrB },
                size, size, size, size, size, size,
                maxLift: 0.15f, smoothRadius: 0, targetPercentile: 1f);

            Assert.AreEqual(0.15f, maps[0].Lift[(size / 2) * size + 5], 1e-3f); // clamped
        }

        [Test]
        public void BlackLevel_EmptyAndMissingInputsAreSafe()
        {
            var none = PMSDKBlackLevelCompensation.Compute(
                new List<byte[]>(), new List<byte[]>(), new List<PMSDKGrayCodeDecode.Correspondence[]>(),
                0, 0, 64, 64, 64, 64);
            Assert.AreEqual(0, none.Length);

            int size = 8;
            var corr = MakeCorrespondence(size, size, (x, y) => (true, x, y));
            var maps = PMSDKBlackLevelCompensation.Compute(
                new List<byte[]> { null }, new List<byte[]> { null },
                new List<PMSDKGrayCodeDecode.Correspondence[]> { corr },
                size, size, size, size, size, size);
            Assert.IsFalse(maps[0].Valid);
            foreach (var v in maps[0].Lift) Assert.AreEqual(0f, v, 1e-6f);
        }
    }
}
