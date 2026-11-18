using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// Projector self-calibration and triangulation (D-052) on a synthetic rig shaped like the office test: camera
    /// ~1 m to the right of an ultra-short-throw projector (bottom lens offset), lighting a wall, the ceiling and a
    /// side wall. Projector positions are quantised like a 1024-cell Gray code. Simulation only.
    /// </summary>
    public class PMSDKProjectorCalibrationTests
    {
        private const int CamW = 1280;
        private const int CamH = 720;
        private const float CameraFocal = 570f;
        private const float ProjectorFocal = 900f;
        private const float ProjectorCy = 1120f;
        private static readonly Vector3 ProjectorCentre = new Vector3(-900f, 60f, 120f);
        private const float ProjectorYawDeg = -25f;

        [Test]
        public void SelfCalibrationRecoversTheProjectorAndMetricDepth()
        {
            RequireNative();
            List<Vector2> camera;
            List<Vector2> raster;
            ushort[] depth;
            Build(false, out camera, out raster, out depth);

            PMSDKProjectorCalibration calibration;
            string error;
            Assert.IsTrue(PMSDKProjectorCalibration.TrySolve(camera, raster, Intrinsics(), CamW, CamH, depth, out calibration, out error), error);
            TestContext.WriteLine(calibration.Describe());
            Assert.AreEqual(ProjectorFocal, calibration.FocalPx, ProjectorFocal * 0.03f, "focal length");
            Assert.AreEqual(ProjectorCy, calibration.PrincipalY, 1080f * 0.04f, "vertical lens offset");
            Assert.AreEqual(ProjectorCentre.magnitude, calibration.BaselineMm, ProjectorCentre.magnitude * 0.03f, "baseline");
            Assert.IsTrue(calibration.Metric);
            Assert.Less(calibration.MedianReprojectionPx, 1.5f);

            // Triangulated depth against the truth.
            List<float> errors = new List<float>();
            for (int y = 0; y < CamH; y += 7) {
                for (int x = 0; x < CamW; x += 7) {
                    ushort measured = calibration.Depth[y * CamW + x];
                    ushort truth = depth[y * CamW + x];
                    if (measured > 0 && truth > 0) {
                        errors.Add(Mathf.Abs(measured - truth));
                    }
                }
            }
            errors.Sort();
            Assert.Greater(errors.Count, 1000);
            TestContext.WriteLine("depth error p50 " + errors[errors.Count / 2] + " mm, p90 " + errors[errors.Count * 9 / 10] + " mm");
            Assert.Less(errors[errors.Count / 2], 6f, "median depth error (mm)");
        }

        [Test]
        public void EpipolarErrorShowsWhenTheCameraMoved()
        {
            RequireNative();
            List<Vector2> camera;
            List<Vector2> raster;
            ushort[] depth;
            Build(false, out camera, out raster, out depth);
            PMSDKProjectorCalibration calibration;
            string error;
            Assert.IsTrue(PMSDKProjectorCalibration.TrySolve(camera, raster, Intrinsics(), CamW, CamH, depth, out calibration, out error), error);
            float still = calibration.MedianEpipolarErrorPx(camera, raster, Intrinsics());
            // A bumped camera: the same projector pixels now land 12 px further right and slightly rotated.
            List<Vector2> moved = new List<Vector2>();
            foreach (Vector2 c in camera) {
                Vector2 d = c - new Vector2(CamW * 0.5f, CamH * 0.5f);
                moved.Add(new Vector2(CamW * 0.5f + d.x * 0.999f - d.y * 0.035f + 12f, CamH * 0.5f + d.x * 0.035f + d.y * 0.999f));
            }
            float bumped = calibration.MedianEpipolarErrorPx(moved, raster, Intrinsics());
            TestContext.WriteLine("epipolar error: still " + still + " px, bumped " + bumped + " px");
            Assert.Less(still, 1f);
            Assert.Greater(bumped, 3f);
        }

        [Test]
        public void PreviousLensIsReusedAndAWrongOneFallsBackToSelfCalibration()
        {
            RequireNative();
            List<Vector2> camera;
            List<Vector2> raster;
            ushort[] depth;
            Build(false, out camera, out raster, out depth);
            string error;

            // Camera moved, projector unchanged: the earlier lens is reused and the depth stays right.
            PMSDKProjectorCalibration previous = new PMSDKProjectorCalibration { FocalPx = ProjectorFocal, PrincipalX = 960f, PrincipalY = ProjectorCy };
            PMSDKProjectorCalibration reused;
            Assert.IsTrue(PMSDKProjectorCalibration.TrySolve(camera, raster, Intrinsics(), CamW, CamH, depth, previous, out reused, out error), error);
            TestContext.WriteLine("reused: " + reused.Describe());
            Assert.IsTrue(reused.LensReused);
            Assert.AreEqual(ProjectorFocal, reused.FocalPx, 0.01f);
            Assert.AreEqual(ProjectorCentre.magnitude, reused.BaselineMm, ProjectorCentre.magnitude * 0.03f, "baseline");
            Assert.Less(MedianDepthError(reused, depth), 6f, "median depth error (mm)");

            // Zoom changed: the old lens no longer fits, self-calibration takes over.
            PMSDKProjectorCalibration zoomed = new PMSDKProjectorCalibration { FocalPx = ProjectorFocal * 1.4f, PrincipalX = 960f, PrincipalY = ProjectorCy * 0.7f };
            PMSDKProjectorCalibration solved;
            Assert.IsTrue(PMSDKProjectorCalibration.TrySolve(camera, raster, Intrinsics(), CamW, CamH, depth, zoomed, out solved, out error), error);
            TestContext.WriteLine("fallback: " + solved.Describe());
            Assert.IsFalse(solved.LensReused);
            Assert.AreEqual(ProjectorFocal, solved.FocalPx, ProjectorFocal * 0.03f);
        }

        [Test]
        public void FixedLensIsUsedOrFailsButNeverFallsBack()
        {
            RequireNative();
            List<Vector2> camera;
            List<Vector2> raster;
            ushort[] depth;
            Build(false, out camera, out raster, out depth);
            string error;
            PMSDKProjectorCalibration fixedLens = new PMSDKProjectorCalibration { FocalPx = ProjectorFocal, PrincipalX = 960f, PrincipalY = ProjectorCy };
            PMSDKProjectorCalibration solved;
            Assert.IsTrue(PMSDKProjectorCalibration.TrySolve(camera, raster, Intrinsics(), CamW, CamH, depth, fixedLens, true, out solved, out error), error);
            Assert.IsTrue(solved.LensReused);
            Assert.Less(MedianDepthError(solved, depth), 6f);

            PMSDKProjectorCalibration wrongLens = new PMSDKProjectorCalibration { FocalPx = ProjectorFocal * 1.4f, PrincipalX = 960f, PrincipalY = ProjectorCy * 0.7f };
            Assert.IsFalse(PMSDKProjectorCalibration.TrySolve(camera, raster, Intrinsics(), CamW, CamH, depth, wrongLens, true, out solved, out error));
            StringAssert.Contains("does not fit", error);
        }

        [Test]
        public void LensFileRoundTrips()
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pmsdk-lens-" + System.Guid.NewGuid().ToString("N"));
            try {
                float focal;
                float centre;
                Assert.IsFalse(PMSDKProjectorCalibration.TryLoadLens(folder, out focal, out centre));
                PMSDKProjectorCalibration.SaveLens(folder, 923f, 1103f);
                Assert.IsTrue(PMSDKProjectorCalibration.TryLoadLens(folder, out focal, out centre));
                Assert.AreEqual(923f, focal, 1e-3f);
                Assert.AreEqual(1103f, centre, 1e-3f);
                PMSDKProjectorCalibration.ClearLens(folder);
                Assert.IsFalse(PMSDKProjectorCalibration.TryLoadLens(folder, out focal, out centre));
            } finally {
                if (System.IO.Directory.Exists(folder)) {
                    System.IO.Directory.Delete(folder, true);
                }
            }
        }

        [Test]
        public void ImpossibleProjectorLensIsRejected()
        {
            // Hardware 2026-11-18: self-calibration from a new camera position returned focal 249 px, lens centre 0.40 H.
            Assert.IsFalse(PMSDKProjectorCalibration.IsPlausibleLens(248.6, 436.0));
            Assert.IsTrue(PMSDKProjectorCalibration.IsPlausibleLens(923.1, 1102.8));
        }

        private static float MedianDepthError(PMSDKProjectorCalibration calibration, ushort[] truthDepth)
        {
            List<float> errors = new List<float>();
            for (int i = 0; i < truthDepth.Length; i += 7) {
                if (calibration.Depth[i] > 0 && truthDepth[i] > 0) {
                    errors.Add(Mathf.Abs(calibration.Depth[i] - truthDepth[i]));
                }
            }
            errors.Sort();
            return errors.Count > 0 ? errors[errors.Count / 2] : float.MaxValue;
        }

        [Test]
        public void SingleFlatWallIsRefused()
        {
            RequireNative();
            List<Vector2> camera;
            List<Vector2> raster;
            ushort[] depth;
            Build(true, out camera, out raster, out depth);
            PMSDKProjectorCalibration calibration;
            string error;
            Assert.IsFalse(PMSDKProjectorCalibration.TrySolve(camera, raster, Intrinsics(), CamW, CamH, depth, out calibration, out error));
            StringAssert.Contains("single flat surface", error);
        }

        [Test]
        public void TriangulatedDepthFeedsTheSurfaceGeometry()
        {
            RequireNative();
            List<Vector2> camera;
            List<Vector2> raster;
            ushort[] depth;
            Build(false, out camera, out raster, out depth);
            PMSDKProjectorCalibration calibration;
            string error;
            Assert.IsTrue(PMSDKProjectorCalibration.TrySolve(camera, raster, Intrinsics(), CamW, CamH, depth, out calibration, out error), error);
            List<PMSDKDetectedPlane> planes;
            Assert.IsTrue(PMSDKAutoAdjustNative.TryDetectPlanes(calibration.Depth, CamW, CamH, Intrinsics(), out planes, out error), error);
            TestContext.WriteLine(planes.Count + " planes");
            Assert.GreaterOrEqual(planes.Count, 3, "wall, ceiling and side wall");
            // The front wall (normal ~ -z) must come out with its true orientation.
            bool frontWall = false;
            foreach (PMSDKDetectedPlane p in planes) {
                frontWall |= Vector3.Angle(p.Normal, new Vector3(0f, 0f, -1f)) < 2f;
            }
            Assert.IsTrue(frontWall);
        }

        private static void RequireNative()
        {
            if (!PMSDKAutoAdjustNative.IsAvailable()) {
                Assert.Ignore("ProjectionMappingSDK.dll without Auto Adjust exports is loaded.");
            }
        }

        private static PMSDKCameraIntrinsics Intrinsics()
        {
            return new PMSDKCameraIntrinsics { Fx = CameraFocal, Fy = CameraFocal, Cx = CamW * 0.5f, Cy = CamH * 0.5f };
        }

        /// <summary>Casts projector rays (every 4th projector pixel) into the room and records what the camera sees.</summary>
        private static void Build(bool wallOnly, out List<Vector2> camera, out List<Vector2> raster, out ushort[] depth)
        {
            camera = new List<Vector2>();
            raster = new List<Vector2>();
            depth = new ushort[CamW * CamH];
            Quaternion toProjector = Quaternion.Euler(0f, ProjectorYawDeg, 0f);
            Quaternion toCamera = Quaternion.Inverse(toProjector);
            HashSet<int> used = new HashSet<int>();
            for (float py = 0f; py < 1080f; py += 4f) {
                for (float px = 0f; px < 1920f; px += 4f) {
                    // Gray-code cell centre (1024 cells per side), as the decoder reports it.
                    float u = (Mathf.Floor(px / 1920f * 1024f) + 0.5f) / 1024f;
                    float v = (Mathf.Floor((1f - py / 1080f) * 1024f) + 0.5f) / 1024f;
                    Vector3 rayProjector = new Vector3((px - 960f) / ProjectorFocal, (py - ProjectorCy) / ProjectorFocal, 1f);
                    Vector3 ray = toCamera * rayProjector;
                    Vector3 hit;
                    if (!Hit(ProjectorCentre, ray, wallOnly, out hit)) {
                        continue;
                    }
                    float cx = hit.x / hit.z * CameraFocal + CamW * 0.5f;
                    float cy = hit.y / hit.z * CameraFocal + CamH * 0.5f;
                    int ix = Mathf.RoundToInt(cx);
                    int iy = Mathf.RoundToInt(cy);
                    if (ix < 0 || iy < 0 || ix >= CamW || iy >= CamH || !used.Add(iy * CamW + ix)) {
                        continue;
                    }
                    camera.Add(new Vector2(ix, iy));
                    raster.Add(new Vector2(u, v));
                    depth[iy * CamW + ix] = (ushort)Mathf.RoundToInt(hit.z);
                }
            }
        }

        /// <summary>Nearest hit of the ray with the front wall (z = 2200), ceiling (y = -900) and right wall (x = 1400).</summary>
        private static bool Hit(Vector3 origin, Vector3 direction, bool wallOnly, out Vector3 hit)
        {
            hit = Vector3.zero;
            float best = float.MaxValue;
            TryPlane(origin, direction, new Vector3(0f, 0f, 1f), 2200f, ref best, ref hit);
            if (!wallOnly) {
                TryPlane(origin, direction, new Vector3(0f, 1f, 0f), -900f, ref best, ref hit);
                TryPlane(origin, direction, new Vector3(1f, 0f, 0f), 1400f, ref best, ref hit);
            }
            return best < float.MaxValue && hit.z > 100f;
        }

        private static void TryPlane(Vector3 origin, Vector3 direction, Vector3 axis, float value, ref float best, ref Vector3 hit)
        {
            float denominator = Vector3.Dot(axis, direction);
            if (Mathf.Abs(denominator) < 1e-6f) {
                return;
            }
            float t = (value - Vector3.Dot(axis, origin)) / denominator;
            if (t > 0f && t < best) {
                best = t;
                hit = origin + direction * t;
            }
        }
    }
}
