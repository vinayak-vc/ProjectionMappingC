using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode tests for Auto Adjust Phases 2-4 (D-045..D-047) against <see cref="PMSDKSyntheticProjection"/>:
    /// facet-edge snapping, Ellipse / several skins / mm score, warp maps for a curved surface,
    /// surface finding from depth, plus persistence and display-restore checks. No hardware.
    /// </summary>
    public class PMSDKAutoAdjustPhasesTests
    {
        private const int CamW = 320;
        private const int CamH = 240;

        // Projector raster corners (BL, BR, TR, TL) as seen by the synthetic camera.
        private static readonly Vector2[] RasterInCamera = {
            new Vector2(30f, 228f), new Vector2(292f, 232f), new Vector2(285f, 12f), new Vector2(36f, 18f),
        };

        private GameObject projGO;
        private GameObject ctrlGO;
        private PMSDKSkinEditorController controller;
        private Camera projectorCamera;
        private string tempFolder;

        [SetUp]
        public void Setup()
        {
            projGO = new GameObject("TestProjector", typeof(Camera));
            projGO.AddComponent<PMSDKProjector>();
            projectorCamera = projGO.GetComponent<Camera>();
            projectorCamera.clearFlags = CameraClearFlags.SolidColor;
            projectorCamera.backgroundColor = new Color(0.2f, 0.1f, 0.05f);
            ctrlGO = new GameObject("SkinEditor");
            controller = ctrlGO.AddComponent<PMSDKSkinEditorController>();
            controller.Projector = projGO.GetComponent<PMSDKProjector>();
            controller.SkinLayerName = "TransparentFX";
            controller.enabled = false;
            controller.enabled = true;
            tempFolder = Path.Combine(Application.temporaryCachePath, "PMSDKAutoAdjustTests");
            Directory.CreateDirectory(tempFolder);
        }

        [TearDown]
        public void Teardown()
        {
            foreach (PMSDKSkin s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None)) {
                Object.DestroyImmediate(s.gameObject);
            }
            if (ctrlGO != null) {
                Object.DestroyImmediate(ctrlGO);
            }
            if (projGO != null) {
                Object.DestroyImmediate(projGO);
            }
            if (Directory.Exists(tempFolder)) {
                Directory.Delete(tempFolder, true);
            }
        }

        // ------------------------------------------------------------------ persistence / plumbing

        [Test]
        public void FacetSizeAndWarpMapRoundTripThroughJson()
        {
            PMSDKSkin skin = Spawn(new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.2f), new Vector2(0.8f, 0.8f), new Vector2(0.2f, 0.8f));
            skin.FacetWidthMm = 1200f;
            skin.FacetHeightMm = 450f;
            skin.WarpMapFile = "abc_warp.pmwm";
            PMSDKSkinSetJson.Set set = PMSDKSkinSetJson.Deserialize(PMSDKSkinSetJson.Serialize(controller));
            Assert.AreEqual(1200f, set.skins[0].facet.widthMm);
            Assert.AreEqual("abc_warp.pmwm", set.skins[0].warpMapFile);
            skin.FacetWidthMm = 0f;
            skin.WarpMapFile = null;
            PMSDKSkinSetJson.Apply(controller, set); // updates the skin in place (same SkinId)
            Assert.AreEqual(1200f, skin.FacetWidthMm);
            Assert.AreEqual(450f, skin.FacetHeightMm);
            Assert.AreEqual("abc_warp.pmwm", skin.WarpMapFile);
            Assert.IsTrue(skin.HasFacetSize);

            set.skins[0].facet = null;
            set.skins[0].warpMapFile = null;
            PMSDKSkinSetJson.Apply(controller, set);
            Assert.IsFalse(skin.HasFacetSize);
            Assert.IsNull(skin.WarpMapFile);
        }

        [Test]
        public void WarpMapFileRoundTripsAndRejectsGarbage()
        {
            string path = Path.Combine(tempFolder, "map.pmwm");
            float[] rgba = new float[4 * 4 * 4];
            for (int i = 0; i < 16; i++) {
                rgba[i * 4] = i / 16f;
                rgba[i * 4 + 1] = 0.5f;
                rgba[i * 4 + 3] = i % 3 == 0 ? 0f : 1f;
            }
            PMSDKWarpMapFile.Save(path, 4, 4, new Rect(0.1f, 0.2f, 0.5f, 0.6f), rgba);
            int w;
            int h;
            Rect rect;
            float[] loaded;
            string error;
            Assert.IsTrue(PMSDKWarpMapFile.TryLoad(path, out w, out h, out rect, out loaded, out error), error);
            Assert.AreEqual(4, w);
            Assert.AreEqual(0.6f, rect.height, 1e-6f);
            Assert.AreEqual(rgba[20], loaded[20]);

            Texture2D texture = PMSDKWarpMapFile.CreateTexture(w, h, loaded);
            try {
                Color invalid = texture.GetPixel(0, 0);
                Assert.AreEqual(0f, invalid.r, "invalid texels are premultiplied to zero");
                Color valid = texture.GetPixel(1, 0);
                Assert.AreEqual(1f / 16f, valid.r, 1e-6f);
                Assert.AreEqual(1f, valid.a);
            } finally {
                Object.DestroyImmediate(texture);
            }

            File.WriteAllText(path, "not a map");
            Assert.IsFalse(PMSDKWarpMapFile.TryLoad(path, out w, out h, out rect, out loaded, out error));
            Assert.Throws<System.ArgumentException>(delegate { PMSDKWarpMapFile.Save(path, 4, 4, rect, new float[3]); });
        }

        [Test]
        public void SkinPushesWarpMapFeatherAndSuppression()
        {
            PMSDKSkin skin = Spawn(new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.2f), new Vector2(0.8f, 0.8f), new Vector2(0.2f, 0.8f));
            string path = Path.Combine(tempFolder, "skin.pmwm");
            PMSDKWarpMapFile.Save(path, 2, 2, new Rect(0f, 0f, 1f, 1f), new float[] {
                0f, 0f, 0f, 1f, 1f, 0f, 0f, 1f, 0f, 1f, 0f, 1f, 1f, 1f, 0f, 1f,
            });
            skin.WarpMapFile = path;
            skin.PerEdgeFeatherEnabled = true;
            skin.EdgeFeather = new Vector4(0.05f, 0.05f, 0.05f, 0.05f);
            skin.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            MeshRenderer renderer = skin.GetComponent<MeshRenderer>();
            renderer.GetPropertyBlock(block);
            Assert.AreEqual(1f, block.GetFloat("_UseWarpMap"));
            Assert.AreEqual(1f, block.GetFloat("_RectFeather"));
            Assert.IsTrue(skin.HasWarpMap);
            Assert.IsTrue(((IPMSDKWarpOffsetSource)skin).NeedsRasterUV);

            skin.SuppressWarpMap = true;
            skin.SuppressMasks = true;
            skin.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            renderer.GetPropertyBlock(block);
            Assert.AreEqual(0f, block.GetFloat("_UseWarpMap"));
            Assert.AreEqual(0f, block.GetFloat("_RectFeather"));
            Assert.IsFalse(((IPMSDKWarpOffsetSource)skin).NeedsRasterUV);
        }

        [Test]
        public void CalibrationDisplayRestoresEverything()
        {
            PMSDKSkin a = Spawn(new Vector2(0.1f, 0.1f), new Vector2(0.4f, 0.1f), new Vector2(0.4f, 0.4f), new Vector2(0.1f, 0.4f));
            PMSDKSkin b = Spawn(new Vector2(0.5f, 0.5f), new Vector2(0.9f, 0.5f), new Vector2(0.9f, 0.9f), new Vector2(0.5f, 0.9f));
            a.Opacity = 0.4f;
            Texture2D pattern = new Texture2D(4, 4);
            try {
                using (PMSDKCalibrationDisplay display = new PMSDKCalibrationDisplay(new List<PMSDKSkin> { a, b }, a.gameObject.layer)) {
                    display.HideAllExcept(a);
                    display.ShowPattern(a, pattern);
                    display.SetBackground(Color.white);
                    Assert.IsFalse(b.GetComponent<MeshRenderer>().enabled);
                    Assert.AreSame(pattern, a.TestPatternOverride);
                    Assert.AreEqual(1f, a.Opacity);
                    Assert.IsTrue(a.SuppressMasks && a.SuppressWarpMap);
                    Assert.AreEqual(Color.white, projectorCamera.backgroundColor);
                }
                Assert.IsTrue(b.GetComponent<MeshRenderer>().enabled);
                Assert.IsNull(a.TestPatternOverride);
                Assert.AreEqual(0.4f, a.Opacity);
                Assert.IsFalse(a.SuppressMasks || a.SuppressWarpMap);
                Assert.AreEqual(new Color(0.2f, 0.1f, 0.05f), projectorCamera.backgroundColor);
            } finally {
                Object.DestroyImmediate(pattern);
            }
        }

        [Test]
        public void NormalizedHomographyFitsMixedScales()
        {
            List<Vector2> camera = new List<Vector2>();
            List<Vector2> uv = new List<Vector2>();
            float[] h = PMSDKSyntheticProjection.QuadToHomography(RasterInCamera);
            for (int j = 0; j <= 10; j++) {
                for (int i = 0; i <= 10; i++) {
                    Vector2 p = new Vector2(i / 10f, j / 10f);
                    uv.Add(p);
                    camera.Add(PMSDKSyntheticProjection.Apply(h, p));
                }
            }
            PMSDKHomography fit = PMSDKHomography.FitNormalized(camera, uv);
            Assert.IsTrue(fit.Valid);
            for (int k = 0; k < camera.Count; k += 7) {
                Vector2 back = fit.Apply(camera[k]);
                Assert.AreEqual(uv[k].x, back.x, 1e-4f);
                Assert.AreEqual(uv[k].y, back.y, 1e-4f);
            }
        }

        // ------------------------------------------------------------------ Phase 2

        [Test]
        public void Phase2_SnapsCornersToTheFacetTheCheckerSitsOn()
        {
            RequireNative();
            // Twice the camera resolution: the middle facet is narrow, its checker needs pixels.
            const float s = 2f;
            PMSDKSyntheticProjection room = FoldedRoom(0f, s);
            Vector2[] raster = Scaled(s);
            // Middle facet in camera: the band 120 <= x <= 210 (x scale) inside the projector image.
            Vector2[] facetCamera = {
                OnEdge(raster[0], raster[1], 120f * s), OnEdge(raster[0], raster[1], 210f * s),
                OnEdge(raster[3], raster[2], 210f * s), OnEdge(raster[3], raster[2], 120f * s),
            };
            Vector2[] truth = new Vector2[4];
            for (int i = 0; i < 4; i++) {
                truth[i] = room.CameraToRaster(facetCamera[i]);
            }
            // The user placed the skin roughly on the middle facet.
            PMSDKSkin skin = Spawn(truth[0] + new Vector2(0.015f, 0.025f), truth[1] + new Vector2(-0.012f, 0.02f),
                truth[2] + new Vector2(-0.015f, -0.02f), truth[3] + new Vector2(0.012f, -0.025f));
            PMSDKAutoAdjust adjust = NewAutoAdjust();
            adjust.SnapToFacetEdges = true;

            PMSDKAutoAdjustResult result = RunAdjust(adjust, skin, room);
            TestContext.WriteLine(result.Message);
            Assert.IsTrue(result.Completed, result.Message);
            Assert.IsTrue(result.SnappedToFacet, result.Message);
            for (int i = 0; i < 4; i++) {
                Assert.Less(Vector2.Distance(skin.Vertices[i], truth[i]), 0.02f, "corner " + i + " " + skin.Vertices[i] + " vs " + truth[i]);
            }
            Assert.Less(result.Final.RmsPx, 1.0f, result.Message);
            Assert.AreEqual(new Color(0.2f, 0.1f, 0.05f), projectorCamera.backgroundColor, "flood colour restored");
        }

        [Test]
        public void Phase2_EllipseSkinIsAdjustedAndMasksComeBack()
        {
            RequireNative();
            PMSDKSyntheticProjection room = FoldedRoom(14f);
            room.Albedo.Clear();
            PMSDKSkin skin = Spawn(new Vector2(0.18f, 0.15f), new Vector2(0.82f, 0.19f), new Vector2(0.79f, 0.85f), new Vector2(0.2f, 0.8f));
            skin.Kind = PMSDKSkinShapeKind.Ellipse;
            skin.FacetWidthMm = 1600f;
            skin.FacetHeightMm = 1100f;
            PMSDKAutoAdjustResult result = RunAdjust(NewAutoAdjust(), skin, room);
            TestContext.WriteLine(result.Message);
            Assert.IsTrue(result.Completed, result.Message);
            Assert.Less(result.Final.RmsPx, result.Initial.RmsPx, result.Message);
            Assert.Greater(result.MmPerPx, 0f);
            StringAssert.Contains(" mm RMS", result.Message);
            Assert.IsFalse(skin.SuppressMasks, "masks are back on after the run");
        }

        [Test]
        public void Phase2_AdjustAllUsesOneCameraSession()
        {
            RequireNative();
            PMSDKSyntheticProjection room = FoldedRoom(0f);
            room.Albedo.Clear();
            PMSDKSkin left = Spawn(new Vector2(0.08f, 0.15f), new Vector2(0.46f, 0.15f), new Vector2(0.46f, 0.85f), new Vector2(0.08f, 0.85f));
            PMSDKSkin right = Spawn(new Vector2(0.54f, 0.15f), new Vector2(0.92f, 0.15f), new Vector2(0.92f, 0.85f), new Vector2(0.54f, 0.85f));
            PMSDKAutoAdjust adjust = NewAutoAdjust();
            List<PMSDKAutoAdjustResult> results = null;
            PMSDKSyntheticProjection.Drive(adjust.AdjustSkins(new List<PMSDKSkin> { left, right }, room,
                new List<PMSDKSkin> { left, right }, delegate(List<PMSDKAutoAdjustResult> r) { results = r; }));
            Assert.IsNotNull(results);
            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results[0].Completed, results[0].Message);
            Assert.IsTrue(results[1].Completed, results[1].Message);
            Assert.AreEqual(1, room.BeginCount, "camera started once for all skins");
            Assert.IsTrue(room.Ended);
            Assert.IsTrue(left.GetComponent<MeshRenderer>().enabled && right.GetComponent<MeshRenderer>().enabled);
        }

        // ------------------------------------------------------------------ skin border (D-048)

        [Test]
        public void SkinBorderProbeMatchesTheTrueBorder()
        {
            RequireNative();
            PMSDKSyntheticProjection room = FoldedRoom(20f);
            room.Albedo.Clear();
            PMSDKSkin skin = Spawn(new Vector2(0.15f, 0.14f), new Vector2(0.84f, 0.18f), new Vector2(0.8f, 0.86f), new Vector2(0.19f, 0.82f));
            PMSDKCheckerSpec checker = new PMSDKCheckerSpec { SquaresX = 8, SquaresY = 5, Margin = 0.08f };
            string error;
            Vector2[] uvs = PMSDKAutoAdjustNative.CheckerCornerUVs(checker, out error);
            Texture2D texture = PMSDKAutoAdjustNative.RenderCheckerTexture(checker, 1024, 768, out error);
            try {
                skin.TestPatternOverride = texture;
                byte[] frame = room.CaptureLuminance();
                Vector2[] raw;
                byte[] valid;
                int validCount;
                bool wholeBoard;
                Assert.IsTrue(PMSDKAutoAdjustNative.TryDetect(frame, room.W, room.H, checker, null, out raw, out valid, out validCount,
                    out wholeBoard, out error), error);
                List<Vector2> src = new List<Vector2>();
                List<Vector2> dst = new List<Vector2>();
                for (int i = 0; i < uvs.Length; i++) {
                    if (valid[i] != 0) {
                        src.Add(uvs[i]);
                        dst.Add(raw[i]);
                    }
                }
                PMSDKHomography target = PMSDKHomography.FitNormalized(src, dst);
                List<Vector2> borderUvs = new List<Vector2>();
                List<Vector2> borderCamera = new List<Vector2>();
                int found = PMSDKSkinEdgeProbe.FindBorder(frame, room.W, room.H, checker, uvs, raw, valid, null, 20f, borderUvs,
                    borderCamera);
                TestContext.WriteLine("found " + found + " of " + 2 * (8 + 5));
                Assert.GreaterOrEqual(found, 2 * (8 + 5) - 4, "nearly every border sample is found");

                float[] pin = PMSDKSyntheticProjection.QuadToHomography(skin.Vertices.ToArray());
                float sumError = 0f;
                float maxError = 0f;
                float maxAcross = 0f;
                for (int k = 0; k < borderUvs.Count; k++) {
                    Vector2 uv = borderUvs[k];
                    Vector2 along = uv.x <= 0f || uv.x >= 1f ? Vector2.up : Vector2.right;
                    Vector2 ideal = target.Apply(uv);
                    Vector2 tangent = target.Apply(uv + along * 0.01f) - target.Apply(uv - along * 0.01f);
                    Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized;
                    float measured = Vector2.Dot(borderCamera[k] - ideal, normal);
                    // Ground truth: where the border actually is along that normal (march the true border curve).
                    float truth = TrueAcross(room, pin, uv, along, ideal, normal);
                    float e = Mathf.Abs(measured - truth);
                    sumError += e;
                    maxError = Mathf.Max(maxError, e);
                    maxAcross = Mathf.Max(maxAcross, Mathf.Abs(truth));
                }
                TestContext.WriteLine("border samples " + found + ", mean error " + (sumError / found).ToString("0.000") + " px, max " +
                                      maxError.ToString("0.000") + " px, largest true offset " + maxAcross.ToString("0.00") + " px");
                Assert.Greater(maxAcross, 2f, "the scene must bend the border");
                Assert.Less(sumError / found, 0.25f);
                Assert.Less(maxError, 0.75f);
            } finally {
                skin.TestPatternOverride = null;
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void AutoAdjustStraightensTheBandOutsideTheCheckerCorners()
        {
            RequireNative();
            PMSDKSyntheticProjection room = FoldedRoom(20f);
            room.Albedo.Clear();
            PMSDKSkin skin = Spawn(new Vector2(0.15f, 0.14f), new Vector2(0.84f, 0.18f), new Vector2(0.8f, 0.86f), new Vector2(0.19f, 0.82f));
            List<Vector2> cornersBefore = new List<Vector2>(skin.Vertices);
            float before = MaxOffCornerQuad(room, skin);
            PMSDKAutoAdjust adjust = NewAutoAdjust();
            adjust.StopRmsPx = 0.2f;
            PMSDKAutoAdjustResult result = RunAdjust(adjust, skin, room);
            float after = MaxOffCornerQuad(room, skin);
            TestContext.WriteLine(result.Message + " | worst point off the straight quad through the corners: " + before.ToString("0.00") +
                                  " -> " + after.ToString("0.00") + " px");
            Assert.IsTrue(result.Completed, result.Message);
            Assert.Greater(before, 8f, "the scene must bend the skin's border");
            Assert.Less(after, before * 0.5f, "the band outside the checker corners is corrected too");
            for (int i = 0; i < 4; i++) {
                Assert.AreEqual(cornersBefore[i], skin.Vertices[i], "corners stay where the user put them");
            }

            // Without border measurement the band is not corrected (the previous behaviour).
            skin.ClearCorrection();
            adjust.MeasureSkinEdges = false;
            RunAdjust(adjust, skin, room);
            Assert.Greater(MaxOffCornerQuad(room, skin), after * 1.5f);
        }

        // Largest distance (camera px) of the skin's content from the perspective quad through its four
        // corners as the camera sees them, over a dense grid covering the whole skin.
        private static float MaxOffCornerQuad(PMSDKSyntheticProjection room, PMSDKSkin skin)
        {
            const int n = 21;
            float[] pin = PMSDKSyntheticProjection.QuadToHomography(skin.Vertices.ToArray());
            Vector2[] unit = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            Vector2[] cameraCorners = new Vector2[4];
            for (int i = 0; i < 4; i++) {
                cameraCorners[i] = room.RasterToCamera(Warped(skin, pin, unit[i]));
            }
            float[] ideal = PMSDKSyntheticProjection.QuadToHomography(cameraCorners);
            float max = 0f;
            for (int j = 0; j < n; j++) {
                for (int i = 0; i < n; i++) {
                    Vector2 uv = new Vector2(i / (n - 1f), j / (n - 1f));
                    max = Mathf.Max(max, Vector2.Distance(room.RasterToCamera(Warped(skin, pin, uv)), PMSDKSyntheticProjection.Apply(ideal, uv)));
                }
            }
            return max;
        }

        private static Vector2 Warped(PMSDKSkin skin, float[] pin, Vector2 uv)
        {
            Vector2 raster = PMSDKSyntheticProjection.Apply(pin, uv);
            return skin.HasCorrection ? raster + skin.EvaluateWarpOffset(uv) : raster;
        }

        // Signed distance along `normal` from `ideal` to the true border curve near content UV `uv`.
        private static float TrueAcross(PMSDKSyntheticProjection room, float[] pin, Vector2 uv, Vector2 along, Vector2 ideal, Vector2 normal)
        {
            Vector2 tangentDir = new Vector2(normal.y, -normal.x);
            float bestT = 0f;
            float bestDist = float.PositiveInfinity;
            for (int i = -400; i <= 400; i++) {
                float t = Mathf.Clamp01(Vector2.Dot(uv, along) + i * 0.0005f);
                Vector2 p = uv - along * Vector2.Dot(uv, along) + along * t;
                Vector2 c = room.RasterToCamera(PMSDKSyntheticProjection.Apply(pin, p));
                float off = Mathf.Abs(Vector2.Dot(c - ideal, tangentDir));
                if (off < bestDist) {
                    bestDist = off;
                    bestT = t;
                }
            }
            Vector2 q = uv - along * Vector2.Dot(uv, along) + along * bestT;
            return Vector2.Dot(room.RasterToCamera(PMSDKSyntheticProjection.Apply(pin, q)) - ideal, normal);
        }

        // ------------------------------------------------------------------ Phase 3

        [Test]
        public void Phase3_WarpMapStraightensACurvedSurface()
        {
            RequireNative();
            PMSDKSyntheticProjection room = FoldedRoom(24f);
            room.Albedo.Clear();
            PMSDKSkin skin = Spawn(new Vector2(0.15f, 0.14f), new Vector2(0.84f, 0.18f), new Vector2(0.8f, 0.86f), new Vector2(0.19f, 0.82f));
            PMSDKAutoAdjust measurer = NewAutoAdjust();
            PMSDKWarpMapBuilder builder = measurer.gameObject.AddComponent<PMSDKWarpMapBuilder>();
            builder.SettleFrames = 0;
            builder.PatternResolution = 128;
            builder.MapSize = 96;
            builder.OutputFolder = tempFolder;

            bool measured = false;
            PMSDKAutoAdjustScore before = new PMSDKAutoAdjustScore();
            PMSDKSyntheticProjection.Drive(measurer.MeasureSkin(skin, room, null, delegate(bool ok, PMSDKAutoAdjustScore s, string m) {
                measured = ok;
                before = s;
            }));
            Assert.IsTrue(measured);

            PMSDKWarpMapResult result = null;
            PMSDKSyntheticProjection.Drive(builder.BuildWarpMap(skin, room, null, delegate(PMSDKWarpMapResult r) { result = r; }));
            TestContext.WriteLine("before " + before.RmsPx.ToString("0.00") + " px; " + result.Message);
            Assert.IsTrue(result.Success, result.Message);
            Assert.Greater(result.Coverage, 0.5f);
            Assert.IsTrue(File.Exists(skin.WarpMapFile));
            Assert.IsTrue(skin.HasWarpMap);
            Assert.IsTrue(result.Verified, result.Message);
            Assert.Greater(before.RmsPx, 1.2f, "the surface must need correcting");
            Assert.Less(result.Verification.RmsPx, before.RmsPx * 0.5f, result.Message);
            Assert.IsNull(skin.TestPatternOverride);
            Assert.IsFalse(skin.SuppressWarpMap);
        }

        [Test]
        public void PhysicalSpacingBuildOnAHandDrawnPolygonSkin()
        {
            // D-055 user flow: a hand-drawn Polygon skin, no marked camera region, Build warp map with physical spacing
            // (the "Calibrate skins" buttons run exactly this per skin). Depth here comes from the simulated camera.
            RequireNative();
            PMSDKSyntheticProjection room = FoldedRoom(0f);
            room.Albedo.Clear();
            room.ProvideDepth = true;
            room.DepthPlanes.Add(new KeyValuePair<Vector2[], Vector4>(
                new[] { new Vector2(0f, 0f), new Vector2(CamW, 0f), new Vector2(CamW, CamH), new Vector2(0f, CamH) },
                Plane(0f, 0f, -1f, 0f, 0f, 2000f)));
            List<Vector2> outline = new List<Vector2> {
                new Vector2(0.12f, 0.15f), new Vector2(0.5f, 0.3f), new Vector2(0.85f, 0.12f), new Vector2(0.88f, 0.8f),
                new Vector2(0.5f, 0.62f), new Vector2(0.15f, 0.85f),
            };
            PMSDKSkin skin = controller.SpawnSkinFromSnapshot(PMSDKSkinShapeKind.Polygon, outline, "drawn-polygon", "Drawn polygon");
            controller.RefreshSkinList();
            PMSDKWarpMapBuilder builder = new GameObject("Builder").AddComponent<PMSDKWarpMapBuilder>();
            try {
                builder.SettleFrames = 0;
                builder.PatternResolution = 128;
                builder.MapSize = 96;
                builder.OutputFolder = tempFolder;
                builder.Target = PMSDKWarpMapTarget.PhysicalSpacing;
                builder.SetReportGrid(8, 8);
                List<PMSDKSkin> all = new List<PMSDKSkin>(controller.Skins);

                PMSDKWarpMapResult result = null;
                PMSDKSyntheticProjection.Drive(builder.BuildWarpMap(skin, room, all, delegate(PMSDKWarpMapResult r) { result = r; }));
                Assert.IsNotNull(result);
                TestContext.WriteLine(result.Message);
                Assert.IsTrue(result.Success, result.Message);
                Assert.IsFalse(result.CalibrationStale);
                Assert.IsTrue(skin.HasWarpMap, "the polygon skin now carries a warp map");
                Assert.IsNotNull(result.Surface);
                Assert.IsTrue(result.Surface.Valid, result.Surface.Message);
                TestContext.WriteLine(result.Surface.ToText());
                // 1 camera pixel is ~7 mm here (280 px focal at 2 m): require the grid within ~1.5 camera pixels.
                Assert.Less(result.Surface.RmsMm, 1.5f * 2000f / PMSDKSyntheticProjection.Focal);
                Assert.Less(Mathf.Abs(result.Surface.ShearDeg), 1f);

                // The measurement button works on the same skin, also without a marked region.
                PMSDKSurfaceReport report = null;
                PMSDKSyntheticProjection.Drive(builder.MeasureSurface(skin, room, all, delegate(PMSDKSurfaceReport r) { report = r; }));
                Assert.IsNotNull(report);
                Assert.IsTrue(report.Valid, report.Message);
                Assert.AreEqual(1, controller.Skins.Count, "no skin was created or left behind");
            } finally {
                Object.DestroyImmediate(builder.gameObject);
            }
        }

        [Test]
        public void TemporaryProbeSkinIsNeverSaved()
        {
            Spawn(new Vector2(0.1f, 0.1f), new Vector2(0.4f, 0.1f), new Vector2(0.4f, 0.4f), new Vector2(0.1f, 0.4f));
            controller.SpawnSkinFromSnapshot(PMSDKSkinShapeKind.Rectangle, new List<Vector2> {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            }, PMSDKSurfaceFinder.ProbeId, "Surface probe");
            controller.RefreshSkinList();
            Assert.AreEqual(2, controller.Skins.Count);
            PMSDKSkinSetJson.Set set = PMSDKSkinSetJson.Capture(controller);
            Assert.AreEqual(1, set.skins.Count, "the probe is not captured");
            Assert.AreNotEqual(PMSDKSurfaceFinder.ProbeId, set.skins[0].id);
        }

        // ------------------------------------------------------------------ Phase 4

        [Test]
        public void Phase4_FindsLitPlanesAndCreatesSkins()
        {
            RequireNative();
            PMSDKSyntheticProjection room = new PMSDKSyntheticProjection(null, CamW, CamH, RasterInCamera, 0f);
            room.Background = projectorCamera;
            room.ProvideDepth = true;
            Vector2[] leftRegion = { new Vector2(60f, 200f), new Vector2(150f, 200f), new Vector2(150f, 50f), new Vector2(60f, 50f) };
            Vector2[] rightRegion = { new Vector2(175f, 205f), new Vector2(265f, 205f), new Vector2(265f, 45f), new Vector2(175f, 45f) };
            Vector2[] darkRegion = { new Vector2(0f, 240f), new Vector2(24f, 240f), new Vector2(24f, 0f), new Vector2(0f, 0f) };
            room.DepthPlanes.Add(new KeyValuePair<Vector2[], Vector4>(leftRegion, Plane(0.3f, 0f, -1f, 0f, 0f, 2000f)));
            room.DepthPlanes.Add(new KeyValuePair<Vector2[], Vector4>(rightRegion, Plane(-0.35f, 0.1f, -1f, 0f, 0f, 2100f)));
            room.DepthPlanes.Add(new KeyValuePair<Vector2[], Vector4>(darkRegion, Plane(0f, 0f, -1f, 0f, 0f, 3000f)));

            PMSDKSurfaceFinder finder = new GameObject("Finder").AddComponent<PMSDKSurfaceFinder>();
            try {
                finder.SettleFrames = 0;
                finder.PatternResolution = 64;
                PMSDKSurfaceFinderResult result = null;
                PMSDKSyntheticProjection.Drive(finder.FindSurfaces(controller, room, delegate(PMSDKSurfaceFinderResult r) { result = r; }));
                TestContext.WriteLine(result.Message);
                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(3, result.Planes.Count, result.Message);
                Assert.AreEqual(2, result.Created.Count, result.Message);
                Assert.AreEqual(1, result.Skipped.Count, result.Message);
                StringAssert.Contains("not lit", result.Skipped[0]);
                Assert.AreEqual(2, controller.Skins.Count, "the temporary probe skin is gone");

                foreach (Vector2[] region in new Vector2[][] { leftRegion, rightRegion }) {
                    Vector2 centre = room.CameraToRaster((region[0] + region[2]) * 0.5f);
                    PMSDKSkin match = null;
                    foreach (PMSDKSkin s in result.Created) {
                        Vector2 c = (s.Vertices[0] + s.Vertices[1] + s.Vertices[2] + s.Vertices[3]) * 0.25f;
                        if (Vector2.Distance(c, centre) < 0.05f) {
                            match = s;
                        }
                    }
                    Assert.IsNotNull(match, "a skin over region centred at " + centre);
                    for (int i = 0; i < 4; i++) {
                        Vector2 expected = room.CameraToRaster(region[i]);
                        Assert.Less(Vector2.Distance(match.Vertices[i], expected), 0.03f, "corner " + i);
                    }
                    Assert.IsTrue(match.HasFacetSize);
                }
            } finally {
                Object.DestroyImmediate(finder.gameObject);
            }
        }

        [Test]
        public void Phase4_RequiresADepthCamera()
        {
            PMSDKSurfaceFinder finder = new GameObject("Finder").AddComponent<PMSDKSurfaceFinder>();
            try {
                PMSDKSurfaceFinderResult result = null;
                PMSDKSyntheticProjection.Drive(finder.FindSurfaces(controller, null, delegate(PMSDKSurfaceFinderResult r) { result = r; }));
                StringAssert.Contains("camera", result.Message);
                RequireNative();
                PMSDKSyntheticProjection noDepth = new PMSDKSyntheticProjection(null, CamW, CamH, RasterInCamera, 0f);
                PMSDKSyntheticProjection.Drive(finder.FindSurfaces(controller, noDepth, delegate(PMSDKSurfaceFinderResult r) { result = r; }));
                Assert.IsFalse(result.Success);
                StringAssert.Contains("depth", result.Message);
                Assert.AreEqual(0, controller.Skins.Count);
            } finally {
                Object.DestroyImmediate(finder.gameObject);
            }
        }

        [Test]
        public void Phase4_MapsASurfaceMarkedInTheCameraImage()
        {
            PMSDKSyntheticProjection room = new PMSDKSyntheticProjection(null, CamW, CamH, RasterInCamera, 0f);
            room.Background = projectorCamera;
            // L-shaped mark (camera pixels, row 0 = top): the notch at the top right must stay unlit.
            Vector2[] mark = {
                new Vector2(70f, 190f), new Vector2(230f, 190f), new Vector2(230f, 130f), new Vector2(140f, 130f),
                new Vector2(140f, 60f), new Vector2(70f, 60f),
            };
            byte[] mask = new byte[CamW * CamH];
            for (int y = 0; y < CamH; y++) {
                for (int x = 0; x < CamW; x++) {
                    mask[y * CamW + x] = Inside(mark, new Vector2(x, y)) ? (byte)255 : (byte)0;
                }
            }

            PMSDKSurfaceFinder finder = new GameObject("Finder").AddComponent<PMSDKSurfaceFinder>();
            string maskFile = null;
            try {
                finder.SettleFrames = 0;
                finder.PatternResolution = 128;
                PMSDKSurfaceFinderResult result = null;
                PMSDKSyntheticProjection.Drive(finder.MapMarkedSurface(controller, room, mask, CamW, CamH, "Marked",
                    delegate(PMSDKSurfaceFinderResult r) { result = r; }));
                TestContext.WriteLine(result.Message);
                Assert.IsTrue(result.Success, result.Message);
                Assert.AreEqual(1, controller.Skins.Count, "only the marked skin remains (probe removed)");
                PMSDKSkin skin = result.Created[0];
                Assert.AreEqual(PMSDKSkinShapeKind.Rectangle, skin.Kind);
                maskFile = PMSDKSkinStore.ResolveMaskPath(skin.AlphaMaskPath);
                Assert.IsTrue(File.Exists(maskFile));

                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                foreach (Vector2 m in mark) {
                    Vector2 r = room.CameraToRaster(m);
                    min = Vector2.Min(min, r);
                    max = Vector2.Max(max, r);
                }
                Assert.Less(Vector2.Distance(skin.Vertices[0], min), 0.03f, "bottom-left");
                Assert.Less(Vector2.Distance(skin.Vertices[2], max), 0.03f, "top-right");

                Texture2D texture = new Texture2D(2, 2);
                try {
                    Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(maskFile)));
                    Assert.Greater(MaskAt(texture, skin, room.CameraToRaster(new Vector2(100f, 160f))), 0.9f, "inside the L");
                    Assert.Greater(MaskAt(texture, skin, room.CameraToRaster(new Vector2(200f, 160f))), 0.9f, "inside the L foot");
                    Assert.Less(MaskAt(texture, skin, room.CameraToRaster(new Vector2(200f, 80f))), 0.1f, "the notch stays dark");
                } finally {
                    Object.DestroyImmediate(texture);
                }
            } finally {
                Object.DestroyImmediate(finder.gameObject);
                if (maskFile != null && File.Exists(maskFile)) {
                    File.Delete(maskFile);
                }
            }
        }

        [Test]
        public void Phase4_MarkedSurfaceDropsStrayGrayCodeSamples()
        {
            // Dense samples over raster [0.3, 0.6] x [0.4, 0.7] plus scattered bit-error samples (seen on hardware).
            PMSDKGrayCaptureResult map = new PMSDKGrayCaptureResult();
            int camW = 200;
            int camH = 200;
            for (int y = 0; y < camH; y++) {
                for (int x = 0; x < camW; x++) {
                    map.CameraPixels.Add(new Vector2(x, y));
                    map.ContentUvs.Add(new Vector2(0.3f + 0.3f * x / (camW - 1), 0.4f + 0.3f * y / (camH - 1)));
                }
            }
            System.Random random = new System.Random(7);
            for (int i = 0; i < 300; i++) {
                int index = random.Next(map.ContentUvs.Count);
                map.ContentUvs[index] = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
            }
            byte[] mask = new byte[camW * camH];
            for (int i = 0; i < mask.Length; i++) {
                mask[i] = 255;
            }

            PMSDKSurfaceFinder finder = new GameObject("Finder").AddComponent<PMSDKSurfaceFinder>();
            string maskFile = null;
            try {
                string message;
                PMSDKSkin skin = finder.PlaceMarkedSkin(controller, mask, camW, map, "Marked", out message);
                TestContext.WriteLine(message);
                Assert.IsNotNull(skin, message);
                maskFile = PMSDKSkinStore.ResolveMaskPath(skin.AlphaMaskPath);
                // A stray landing within a texel of the surface edge is indistinguishable from an edge sample, so the
                // box may grow by about one first-pass texel (~0.005 here); far strays must not stretch it.
                Assert.Less(Vector2.Distance(skin.Vertices[0], new Vector2(0.3f, 0.4f)), 0.02f, "bottom-left");
                Assert.Less(Vector2.Distance(skin.Vertices[2], new Vector2(0.6f, 0.7f)), 0.02f, "top-right");
                StringAssert.Contains("stray dropped", message);
            } finally {
                Object.DestroyImmediate(finder.gameObject);
                if (maskFile != null && File.Exists(maskFile)) {
                    File.Delete(maskFile);
                }
            }
        }

        [Test]
        public void CameraMarkReadsARedOutlineOnAnUpscaledPhoto()
        {
            // 2x upscaled 64 x 48 photo (Unity order, row 0 = bottom) with a 1-pixel red rectangle outline.
            int cw = 64;
            int ch = 48;
            int w = cw * 2;
            int h = ch * 2;
            Color32[] pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++) {
                pixels[i] = new Color32(90, 90, 90, 255);
            }
            // Camera rows 10..29 (top-down) and columns 20..49 -> image rows (from the top) 20..59, columns 40..99.
            for (int yTop = 20; yTop <= 59; yTop++) {
                for (int x = 40; x <= 99; x++) {
                    bool edge = yTop == 20 || yTop == 59 || x == 40 || x == 99;
                    if (edge) {
                        pixels[(h - 1 - yTop) * w + x] = new Color32(255, 0, 0, 255);
                    }
                }
            }
            byte[] mask;
            string error;
            Assert.IsTrue(PMSDKCameraMark.TryMakeMask(pixels, w, h, cw, ch, out mask, out error), error);
            Assert.AreEqual(255, mask[20 * cw + 35], "inside (camera row 20, col 35)");
            Assert.AreEqual(0, mask[5 * cw + 35], "above the outline");
            Assert.AreEqual(0, mask[20 * cw + 55], "right of the outline");
            int marked = 0;
            foreach (byte b in mask) {
                marked += b > 0 ? 1 : 0;
            }
            Assert.AreEqual(20 * 30, marked, 30, "about the outlined area");
        }

        [Test]
        public void CameraMarkReadsABlackAndWhiteMaskAndRejectsBadInput()
        {
            int cw = 32;
            int ch = 16;
            Color32[] pixels = new Color32[cw * ch];
            for (int y = 0; y < ch; y++) {
                for (int x = 0; x < cw; x++) {
                    // White left half; row 0 here is the bottom of the image.
                    byte v = x < cw / 2 ? (byte)255 : (byte)0;
                    pixels[y * cw + x] = new Color32(v, v, v, 255);
                }
            }
            byte[] mask;
            string error;
            Assert.IsTrue(PMSDKCameraMark.TryMakeMask(pixels, cw, ch, cw, ch, out mask, out error), error);
            Assert.AreEqual(255, mask[3 * cw + 2]);
            Assert.AreEqual(0, mask[3 * cw + 30]);

            Assert.IsFalse(PMSDKCameraMark.TryMakeMask(pixels, cw, ch, 20, 10, out mask, out error));
            StringAssert.Contains("whole-number upscale", error);
            Color32[] empty = new Color32[cw * ch];
            Assert.IsFalse(PMSDKCameraMark.TryMakeMask(empty, cw, ch, cw, ch, out mask, out error));
            StringAssert.Contains("Nothing is marked", error);
        }

        [Test]
        public void CameraRegionRoundTripsPerSkin()
        {
            PMSDKSkin skin = Spawn(new Vector2(0.1f, 0.1f), new Vector2(0.5f, 0.1f), new Vector2(0.5f, 0.5f), new Vector2(0.1f, 0.5f));
            string path = PMSDKSkinStore.ResolveMaskPath(PMSDKCameraMark.RegionFileFor(skin));
            try {
                byte[] region;
                int w;
                int h;
                string error;
                Assert.IsFalse(PMSDKCameraMark.TryLoadRegion(skin, out region, out w, out h, out error), "no region yet");
                Assert.AreEqual("", error);
                byte[] mask = new byte[40 * 30];
                mask[2 * 40 + 5] = 255; // row 2 from the top
                PMSDKCameraMark.SaveRegion(skin, mask, 40, 30);
                Assert.IsTrue(PMSDKCameraMark.TryLoadRegion(skin, out region, out w, out h, out error), error);
                Assert.AreEqual(40, w);
                Assert.AreEqual(30, h);
                Assert.AreEqual(255, region[2 * 40 + 5]);
                Assert.AreEqual(0, region[27 * 40 + 5]);
            } finally {
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void Phase4_MarkedSurfaceRejectsAMarkOfTheWrongSize()
        {
            PMSDKSyntheticProjection room = new PMSDKSyntheticProjection(null, CamW, CamH, RasterInCamera, 0f);
            room.Background = projectorCamera;
            PMSDKSurfaceFinder finder = new GameObject("Finder").AddComponent<PMSDKSurfaceFinder>();
            try {
                PMSDKSurfaceFinderResult result = null;
                PMSDKSyntheticProjection.Drive(finder.MapMarkedSurface(controller, room, new byte[64 * 48], 64, 48, "Marked",
                    delegate(PMSDKSurfaceFinderResult r) { result = r; }));
                Assert.IsFalse(result.Success);
                StringAssert.Contains("camera delivers", result.Message);
                Assert.AreEqual(0, controller.Skins.Count);
                Assert.IsTrue(room.Ended, "the camera is released");
            } finally {
                Object.DestroyImmediate(finder.gameObject);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static bool Inside(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++) {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) {
                    inside = !inside;
                }
            }
            return inside;
        }

        // Mask value at a raster point; the marked skin is an axis-aligned box, so UV is a plain rescale.
        private static float MaskAt(Texture2D texture, PMSDKSkin skin, Vector2 raster)
        {
            Vector2 min = skin.Vertices[0];
            Vector2 max = skin.Vertices[2];
            float u = (raster.x - min.x) / (max.x - min.x);
            float v = (raster.y - min.y) / (max.y - min.y);
            return texture.GetPixelBilinear(u, v).r;
        }

        private static void RequireNative()
        {
            if (!PMSDKAutoAdjustNative.IsAvailable()) {
                Assert.Ignore("ProjectionMappingSDK.dll without Auto Adjust exports is loaded; redeploy the native plugin.");
            }
            try {
                PMSDKAutoAdjustNative.MetricScale(new Vector2[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, 1f, 1f);
            } catch (System.EntryPointNotFoundException) {
                Assert.Ignore("ProjectionMappingSDK.dll predates Auto Adjust Phases 2-4; redeploy the native plugin.");
            }
        }

        private PMSDKSkin Spawn(Vector2 bl, Vector2 br, Vector2 tr, Vector2 tl)
        {
            PMSDKSkin skin = controller.SpawnSkinFromSnapshot(PMSDKSkinShapeKind.Rectangle, new List<Vector2> { bl, br, tr, tl },
                System.Guid.NewGuid().ToString("N").Substring(0, 8), "Test skin");
            controller.RefreshSkinList();
            return skin;
        }

        private PMSDKAutoAdjust NewAutoAdjust()
        {
            PMSDKAutoAdjust adjust = ctrlGO.AddComponent<PMSDKAutoAdjust>();
            adjust.SettleFrames = 0;
            return adjust;
        }

        private PMSDKAutoAdjustResult RunAdjust(PMSDKAutoAdjust adjust, PMSDKSkin skin, IPMSDKCalibrationCamera camera)
        {
            PMSDKAutoAdjustResult result = null;
            PMSDKSyntheticProjection.Drive(adjust.AdjustSkin(skin, camera, new List<PMSDKSkin>(controller.Skins),
                delegate(PMSDKAutoAdjustResult r) { result = r; }));
            Assert.IsNotNull(result, "onDone must always be called");
            return result;
        }

        // Three vertical facets of different brightness (the folded soffit seen from the camera).
        private PMSDKSyntheticProjection FoldedRoom(float wavePx, float scale = 1f)
        {
            PMSDKSyntheticProjection room = new PMSDKSyntheticProjection(null, Mathf.RoundToInt(CamW * scale), Mathf.RoundToInt(CamH * scale),
                Scaled(scale), wavePx * scale);
            room.Background = projectorCamera;
            room.Albedo.Add(new KeyValuePair<Vector2[], float>(Band(0f, 120f * scale, scale), 0.95f));
            room.Albedo.Add(new KeyValuePair<Vector2[], float>(Band(120f * scale, 210f * scale, scale), 0.6f));
            room.Albedo.Add(new KeyValuePair<Vector2[], float>(Band(210f * scale, 320f * scale, scale), 0.35f));
            return room;
        }

        private static Vector2[] Scaled(float scale)
        {
            Vector2[] corners = new Vector2[4];
            for (int i = 0; i < 4; i++) {
                corners[i] = RasterInCamera[i] * scale;
            }
            return corners;
        }

        private static Vector2[] Band(float x0, float x1, float scale)
        {
            float h = CamH * scale;
            return new Vector2[] { new Vector2(x0, h), new Vector2(x1, h), new Vector2(x1, 0f), new Vector2(x0, 0f) };
        }

        // Point on the segment a-b at camera x.
        private static Vector2 OnEdge(Vector2 a, Vector2 b, float x)
        {
            float t = (x - a.x) / (b.x - a.x);
            return Vector2.Lerp(a, b, t);
        }

        // Plane with normal (nx, ny, nz) through (px, py, pz), packed as (n.x, n.y, n.z, d).
        private static Vector4 Plane(float nx, float ny, float nz, float px, float py, float pz)
        {
            Vector3 n = new Vector3(nx, ny, nz).normalized;
            return new Vector4(n.x, n.y, n.z, -(n.x * px + n.y * py + n.z * pz));
        }
    }
}
