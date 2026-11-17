using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// Explicit (run by hand) walkthrough of Auto Adjust Phases 1-4 in the synthetic room. Writes what
    /// the camera sees (a grid shown on the skins, before and after) plus the frames captured during each
    /// run as PNGs, and a summary.txt, to PMSDK_SIM_DEMO_OUT (default: temporaryCachePath/PMSDKSimDemo).
    /// Red outline = true facet, green outline = the skin's corners. Nothing is asserted beyond the runs finishing.
    /// </summary>
    [Explicit("Writes demo images; run by hand.")]
    [Category("SimDemo")]
    public class PMSDKAutoAdjustSimDemo
    {
        private const int CamW = 320;
        private const int CamH = 240;

        private static readonly Vector2[] RasterInCamera = {
            new Vector2(30f, 228f), new Vector2(292f, 232f), new Vector2(285f, 12f), new Vector2(36f, 18f),
        };

        private GameObject projGO;
        private GameObject ctrlGO;
        private PMSDKSkinEditorController controller;
        private Camera projectorCamera;
        private Texture2D grid;
        private string outFolder;
        private StringBuilder summary;

        [SetUp]
        public void Setup()
        {
            projGO = new GameObject("DemoProjector", typeof(Camera));
            projGO.AddComponent<PMSDKProjector>();
            projectorCamera = projGO.GetComponent<Camera>();
            projectorCamera.clearFlags = CameraClearFlags.SolidColor;
            projectorCamera.backgroundColor = Color.black;
            ctrlGO = new GameObject("SkinEditor");
            controller = ctrlGO.AddComponent<PMSDKSkinEditorController>();
            controller.Projector = projGO.GetComponent<PMSDKProjector>();
            controller.SkinLayerName = "TransparentFX";
            controller.enabled = false;
            controller.enabled = true;
            grid = MakeGrid(512, 32);
            string env = System.Environment.GetEnvironmentVariable("PMSDK_SIM_DEMO_OUT");
            outFolder = string.IsNullOrEmpty(env) ? Path.Combine(Application.temporaryCachePath, "PMSDKSimDemo") : env;
            Directory.CreateDirectory(outFolder);
            summary = new StringBuilder();
        }

        [TearDown]
        public void Teardown()
        {
            File.AppendAllText(Path.Combine(outFolder, "summary.txt"), summary.ToString());
            Debug.Log("[PMSDK SimDemo] " + TestContext.CurrentContext.Test.Name + " -> " + outFolder + "\n" + summary);
            foreach (PMSDKSkin s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None)) {
                Object.DestroyImmediate(s.gameObject);
            }
            Object.DestroyImmediate(ctrlGO);
            Object.DestroyImmediate(projGO);
            Object.DestroyImmediate(grid);
        }

        [Test]
        public void Phase1_StraightenOneSkin()
        {
            RequireNative();
            PMSDKSyntheticProjection room = Room(20f, 1f, false);
            PMSDKSkin skin = Spawn(new Vector2(0.15f, 0.14f), new Vector2(0.84f, 0.18f), new Vector2(0.8f, 0.86f), new Vector2(0.19f, 0.82f));
            SaveView(room, "phase1_before", null);
            PMSDKAutoAdjust adjust = NewAutoAdjust();
            string straightBefore = StraightnessLine("grid check before", room, skin, adjust.CheckerFor(skin));
            adjust.StopRmsPx = 0.2f;
            PMSDKAutoAdjustResult result = RunAdjust(adjust, skin, new RecordingCamera(room, outFolder, "phase1_frame"));
            SaveView(room, "phase1_after", null);
            Report("Phase 1 (straighten one skin)", result);
            summary.AppendLine(straightBefore);
            summary.AppendLine(StraightnessLine("grid check after", room, skin, adjust.CheckerFor(skin)));
            Assert.IsTrue(result.Completed, result.Message);
        }

        [Test]
        public void Phase1b_StraightenOneSkinDenserChecker()
        {
            RequireNative();
            PMSDKSyntheticProjection room = Room(20f, 1f, false);
            PMSDKSkin skin = Spawn(new Vector2(0.15f, 0.14f), new Vector2(0.84f, 0.18f), new Vector2(0.8f, 0.86f), new Vector2(0.19f, 0.82f));
            PMSDKAutoAdjust adjust = NewAutoAdjust();
            adjust.AutoChecker = false;
            adjust.StopRmsPx = 0.2f;
            string straightBefore = StraightnessLine("grid check before", room, skin, adjust.CheckerFor(skin));
            PMSDKAutoAdjustResult result = RunAdjust(adjust, skin, new RecordingCamera(room, outFolder, "phase1b_frame"));
            SaveView(room, "phase1b_after", null);
            Report("Phase 1b (same scene, fixed 10x7 checker)", result);
            summary.AppendLine(straightBefore);
            summary.AppendLine(StraightnessLine("grid check after", room, skin, adjust.CheckerFor(skin)));
            Assert.IsTrue(result.Completed, result.Message);
        }

        [Test]
        public void Phase2_SnapToFacet()
        {
            RequireNative();
            const float s = 2f;
            PMSDKSyntheticProjection room = Room(0f, s, true);
            Vector2[] raster = Scaled(s);
            Vector2[] facetCamera = {
                OnEdge(raster[0], raster[1], 120f * s), OnEdge(raster[0], raster[1], 210f * s),
                OnEdge(raster[3], raster[2], 210f * s), OnEdge(raster[3], raster[2], 120f * s),
            };
            Vector2[] truth = new Vector2[4];
            for (int i = 0; i < 4; i++) {
                truth[i] = room.CameraToRaster(facetCamera[i]);
            }
            PMSDKSkin skin = Spawn(truth[0] + new Vector2(0.015f, 0.025f), truth[1] + new Vector2(-0.012f, 0.02f),
                truth[2] + new Vector2(-0.015f, -0.02f), truth[3] + new Vector2(0.012f, -0.025f));
            skin.FacetWidthMm = 600f;
            skin.FacetHeightMm = 1500f;
            SaveView(room, "phase2_before", facetCamera);
            PMSDKAutoAdjust adjust = NewAutoAdjust();
            adjust.SnapToFacetEdges = true;
            PMSDKAutoAdjustResult result = RunAdjust(adjust, skin, new RecordingCamera(room, outFolder, "phase2_frame"));
            SaveView(room, "phase2_after", facetCamera);
            Report("Phase 2 (snap corners to facet edges)", result);
            float worst = 0f;
            for (int i = 0; i < 4; i++) {
                worst = Mathf.Max(worst, Vector2.Distance(skin.Vertices[i], truth[i]));
            }
            summary.AppendLine(StraightnessLine("grid check after", room, skin, adjust.CheckerFor(skin)));
            summary.AppendLine("  worst corner distance from the true facet corner: " + worst.ToString("0.0000") + " (raster units)");
            Assert.IsTrue(result.Completed, result.Message);
        }

        [Test]
        public void Phase3_WarpMapOnCurvedSurface()
        {
            RequireNative();
            PMSDKSyntheticProjection room = Room(24f, 1f, false);
            PMSDKSkin skin = Spawn(new Vector2(0.15f, 0.14f), new Vector2(0.84f, 0.18f), new Vector2(0.8f, 0.86f), new Vector2(0.19f, 0.82f));
            SaveView(room, "phase3_before", null);
            PMSDKAutoAdjust measurer = NewAutoAdjust();
            PMSDKWarpMapBuilder builder = measurer.gameObject.AddComponent<PMSDKWarpMapBuilder>();
            builder.SettleFrames = 0;
            builder.PatternResolution = 128;
            builder.MapSize = 96;
            builder.OutputFolder = outFolder;
            PMSDKWarpMapResult result = null;
            PMSDKSyntheticProjection.Drive(builder.BuildWarpMap(skin, new RecordingCamera(room, outFolder, "phase3_frame"), null,
                delegate(PMSDKWarpMapResult r) { result = r; }));
            SaveView(room, "phase3_after", null);
            summary.AppendLine("Phase 3 (warp map on a curved surface)");
            summary.AppendLine("  " + result.Message);
            Assert.IsTrue(result.Success, result.Message);
        }

        [Test]
        public void Phase4_FindSurfacesFromDepth()
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
            room.Began = true;
            SaveDepth(room.CaptureDepth(), "phase4_depth");
            room.Began = false;

            PMSDKSurfaceFinder finder = new GameObject("Finder").AddComponent<PMSDKSurfaceFinder>();
            try {
                finder.SettleFrames = 0;
                finder.PatternResolution = 64;
                PMSDKSurfaceFinderResult result = null;
                PMSDKSyntheticProjection.Drive(finder.FindSurfaces(controller, new RecordingCamera(room, outFolder, "phase4_frame"),
                    delegate(PMSDKSurfaceFinderResult r) { result = r; }));
                SaveView(room, "phase4_after", null);
                summary.AppendLine("Phase 4 (find surfaces from depth)");
                summary.AppendLine("  " + result.Message);
                foreach (PMSDKSkin created in result.Created) {
                    summary.AppendLine("  created '" + created.DisplayName + "' size " + created.FacetWidthMm.ToString("0") + " x " +
                                       created.FacetHeightMm.ToString("0") + " mm");
                }
                foreach (string skipped in result.Skipped) {
                    summary.AppendLine("  skipped: " + skipped);
                }
                Assert.IsTrue(result.Success, result.Message);
            } finally {
                Object.DestroyImmediate(finder.gameObject);
            }
        }

        // ------------------------------------------------------------------ helpers

        // Whole-skin check, independent of the checker: how far the content's camera image is from the
        // best-fit perspective (homography) of the unit square, over a dense grid, in camera px.
        private string StraightnessLine(string label, PMSDKSyntheticProjection room, PMSDKSkin skin, PMSDKCheckerSpec checker)
        {
            float cellX = (1f - 2f * checker.Margin) / checker.SquaresX;
            float cellY = (1f - 2f * checker.Margin) / checker.SquaresY;
            Rect corners = Rect.MinMaxRect(checker.Margin + cellX, checker.Margin + cellY, 1f - checker.Margin - cellX, 1f - checker.Margin - cellY);
            return "  " + label + " (" + checker.SquaresX + "x" + checker.SquaresY + " checker): between measured corners " +
                   Straightness(room, skin, corners) + "; whole skin " + Straightness(room, skin, Rect.MinMaxRect(0f, 0f, 1f, 1f)) +
                   "; whole skin vs the straight quad through its corners " + AgainstCornerQuad(room, skin);
        }

        // Same as Straightness but against the homography through the skin's four corners as the camera sees
        // them ("keep corners": the edges should be straight lines between the corners).
        private static string AgainstCornerQuad(PMSDKSyntheticProjection room, PMSDKSkin skin)
        {
            const int n = 21;
            float[] h = PMSDKSyntheticProjection.QuadToHomography(skin.Vertices.ToArray());
            Vector2[] cameraCorners = new Vector2[4];
            for (int i = 0; i < 4; i++) {
                cameraCorners[i] = room.RasterToCamera(skin.Vertices[i] + (skin.HasCorrection ? skin.EvaluateWarpOffset(Unit(i)) : Vector2.zero));
            }
            float[] ideal = PMSDKSyntheticProjection.QuadToHomography(cameraCorners);
            float sum = 0f;
            float max = 0f;
            for (int j = 0; j < n; j++) {
                for (int i = 0; i < n; i++) {
                    Vector2 uv = new Vector2(i / (n - 1f), j / (n - 1f));
                    Vector2 raster = PMSDKSyntheticProjection.Apply(h, uv);
                    if (skin.HasCorrection) {
                        raster += skin.EvaluateWarpOffset(uv);
                    }
                    float e = Vector2.Distance(room.RasterToCamera(raster), PMSDKSyntheticProjection.Apply(ideal, uv));
                    sum += e * e;
                    max = Mathf.Max(max, e);
                }
            }
            return "RMS " + Mathf.Sqrt(sum / (n * n)).ToString("0.00") + " px, max " + max.ToString("0.00") + " px";
        }

        private static Vector2 Unit(int corner)
        {
            return corner == 0 ? new Vector2(0f, 0f) : corner == 1 ? new Vector2(1f, 0f) : corner == 2 ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        }

        private static string Straightness(PMSDKSyntheticProjection room, PMSDKSkin skin, Rect area)
        {
            const int n = 21;
            float[] h = PMSDKSyntheticProjection.QuadToHomography(skin.Vertices.ToArray());
            List<Vector2> uvs = new List<Vector2>();
            List<Vector2> cams = new List<Vector2>();
            for (int j = 0; j < n; j++) {
                for (int i = 0; i < n; i++) {
                    Vector2 uv = new Vector2(Mathf.Lerp(area.xMin, area.xMax, i / (n - 1f)), Mathf.Lerp(area.yMin, area.yMax, j / (n - 1f)));
                    Vector2 raster = PMSDKSyntheticProjection.Apply(h, uv);
                    if (skin.HasCorrection) {
                        raster += skin.EvaluateWarpOffset(uv);
                    }
                    uvs.Add(uv);
                    cams.Add(room.RasterToCamera(raster));
                }
            }
            PMSDKHomography fit = PMSDKHomography.FitNormalized(uvs, cams);
            float sum = 0f;
            float max = 0f;
            for (int k = 0; k < uvs.Count; k++) {
                float e = Vector2.Distance(fit.Apply(uvs[k]), cams[k]);
                sum += e * e;
                max = Mathf.Max(max, e);
            }
            return "RMS " + Mathf.Sqrt(sum / uvs.Count).ToString("0.00") + " px, max " + max.ToString("0.00") + " px";
        }

        private void Report(string title, PMSDKAutoAdjustResult result)
        {
            summary.AppendLine(title);
            summary.AppendLine("  " + result.Message);
            summary.AppendLine("  RMS per measurement (px): " +
                               string.Join(" -> ", result.RmsHistory.ConvertAll(delegate(float v) { return v.ToString("0.00"); })));
        }

        // Shows the grid on every skin, captures the camera, draws outlines and saves a PNG.
        private void SaveView(PMSDKSyntheticProjection room, string name, Vector2[] facetCamera)
        {
            List<PMSDKSkin> skins = new List<PMSDKSkin>(controller.Skins);
            foreach (PMSDKSkin skin in skins) {
                skin.TestPatternOverride = grid;
            }
            byte[] frame = room.CaptureLuminance();
            foreach (PMSDKSkin skin in skins) {
                skin.TestPatternOverride = null;
            }
            Color32[] pixels = new Color32[room.W * room.H];
            for (int y = 0; y < room.H; y++) {
                for (int x = 0; x < room.W; x++) {
                    byte v = frame[y * room.W + x];
                    pixels[(room.H - 1 - y) * room.W + x] = new Color32(v, v, v, 255);
                }
            }
            if (facetCamera != null) {
                DrawPolygon(pixels, room.W, room.H, facetCamera, new Color32(230, 40, 40, 255));
            }
            foreach (PMSDKSkin skin in skins) {
                Vector2[] outline = new Vector2[4];
                for (int i = 0; i < 4; i++) {
                    outline[i] = room.RasterToCamera(skin.Vertices[i]);
                }
                DrawPolygon(pixels, room.W, room.H, outline, new Color32(40, 220, 80, 255));
            }
            SavePng(pixels, room.W, room.H, name);
        }

        private void SaveDepth(ushort[] depth, string name)
        {
            Color32[] pixels = new Color32[CamW * CamH];
            for (int y = 0; y < CamH; y++) {
                for (int x = 0; x < CamW; x++) {
                    ushort d = depth[y * CamW + x];
                    byte v = d == 0 ? (byte)0 : (byte)Mathf.Clamp(255 - (d - 1500) / 8, 40, 255);
                    pixels[(CamH - 1 - y) * CamW + x] = new Color32(v, v, v, 255);
                }
            }
            SavePng(pixels, CamW, CamH, name);
        }

        private void SavePng(Color32[] pixels, int w, int h, string name)
        {
            Texture2D texture = new Texture2D(w, h, TextureFormat.RGB24, false);
            try {
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(outFolder, name + ".png"), texture.EncodeToPNG());
            } finally {
                Object.DestroyImmediate(texture);
            }
        }

        // Camera coordinates have a top-left origin; pixels are stored bottom row first.
        private static void DrawPolygon(Color32[] pixels, int w, int h, Vector2[] polygon, Color32 color)
        {
            for (int i = 0; i < polygon.Length; i++) {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f) + 1;
                for (int k = 0; k <= steps; k++) {
                    Vector2 p = Vector2.Lerp(a, b, k / (float)steps);
                    int x = Mathf.RoundToInt(p.x);
                    int y = Mathf.RoundToInt(p.y);
                    if (x >= 0 && x < w && y >= 0 && y < h) {
                        pixels[(h - 1 - y) * w + x] = color;
                    }
                }
            }
        }

        private static Texture2D MakeGrid(int size, int cell)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    bool line = x % cell < 4 || y % cell < 4 || x >= size - 4 || y >= size - 4;
                    byte v = line ? (byte)255 : (byte)40;
                    pixels[y * size + x] = new Color32(v, v, v, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static void RequireNative()
        {
            if (!PMSDKAutoAdjustNative.IsAvailable()) {
                Assert.Ignore("ProjectionMappingSDK.dll without Auto Adjust exports is loaded; redeploy the native plugin.");
            }
        }

        private PMSDKSkin Spawn(Vector2 bl, Vector2 br, Vector2 tr, Vector2 tl)
        {
            PMSDKSkin skin = controller.SpawnSkinFromSnapshot(PMSDKSkinShapeKind.Rectangle, new List<Vector2> { bl, br, tr, tl },
                System.Guid.NewGuid().ToString("N").Substring(0, 8), "Demo skin");
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

        // Projector raster on a wall; with facets, three vertical bands of different brightness (a folded soffit).
        private PMSDKSyntheticProjection Room(float wavePx, float scale, bool facets)
        {
            PMSDKSyntheticProjection room = new PMSDKSyntheticProjection(null, Mathf.RoundToInt(CamW * scale), Mathf.RoundToInt(CamH * scale),
                Scaled(scale), wavePx * scale);
            room.Background = projectorCamera;
            if (facets) {
                room.Albedo.Add(new KeyValuePair<Vector2[], float>(Band(0f, 120f * scale, scale), 0.95f));
                room.Albedo.Add(new KeyValuePair<Vector2[], float>(Band(120f * scale, 210f * scale, scale), 0.6f));
                room.Albedo.Add(new KeyValuePair<Vector2[], float>(Band(210f * scale, 320f * scale, scale), 0.35f));
            }
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

        private static Vector2 OnEdge(Vector2 a, Vector2 b, float x)
        {
            float t = (x - a.x) / (b.x - a.x);
            return Vector2.Lerp(a, b, t);
        }

        private static Vector4 Plane(float nx, float ny, float nz, float px, float py, float pz)
        {
            Vector3 n = new Vector3(nx, ny, nz).normalized;
            return new Vector4(n.x, n.y, n.z, -(n.x * px + n.y * py + n.z * pz));
        }

        /// <summary>Forwards to the synthetic room and saves every captured frame as a PNG.</summary>
        private sealed class RecordingCamera : IPMSDKCalibrationCamera, IPMSDKCameraIntrinsicsSource, IPMSDKDepthSource
        {
            private readonly PMSDKSyntheticProjection inner;
            private readonly string folder;
            private readonly string prefix;
            private int index;

            public RecordingCamera(PMSDKSyntheticProjection inner, string folder, string prefix)
            {
                this.inner = inner;
                this.folder = folder;
                this.prefix = prefix;
            }

            public int Width
            {
                get { return inner.Width; }
            }

            public int Height
            {
                get { return inner.Height; }
            }

            public bool HasDepth
            {
                get { return inner.HasDepth; }
            }

            public bool Begin()
            {
                return inner.Begin();
            }

            public void End()
            {
                inner.End();
            }

            public bool TryGetIntrinsics(out PMSDKCameraIntrinsics intrinsics)
            {
                return inner.TryGetIntrinsics(out intrinsics);
            }

            public ushort[] CaptureDepth()
            {
                return inner.CaptureDepth();
            }

            public byte[] CaptureLuminance()
            {
                byte[] frame = inner.CaptureLuminance();
                Texture2D texture = new Texture2D(inner.W, inner.H, TextureFormat.RGB24, false);
                try {
                    Color32[] pixels = new Color32[frame.Length];
                    for (int y = 0; y < inner.H; y++) {
                        for (int x = 0; x < inner.W; x++) {
                            byte v = frame[y * inner.W + x];
                            pixels[(inner.H - 1 - y) * inner.W + x] = new Color32(v, v, v, 255);
                        }
                    }
                    texture.SetPixels32(pixels);
                    texture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, prefix + "_" + index.ToString("000") + ".png"), texture.EncodeToPNG());
                } finally {
                    Object.DestroyImmediate(texture);
                }
                index++;
                return frame;
            }
        }
    }
}
