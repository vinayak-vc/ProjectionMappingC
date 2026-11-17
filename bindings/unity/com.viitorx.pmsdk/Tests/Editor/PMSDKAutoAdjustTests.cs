using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode tests for Auto Adjust Phase 1 (D-044): subdivided quad mesh, the skin correction
    /// grid and its persistence, the native wrapper, and the full verify-and-refine loop driven by a
    /// synthetic camera that renders the skin's displayed checker through a non-planar "room".
    /// Native-dependent tests are ignored when ProjectionMappingSDK.dll lacks the Auto Adjust exports.
    /// </summary>
    public class PMSDKAutoAdjustTests
    {
        private GameObject projGO;
        private GameObject ctrlGO;
        private PMSDKSkinEditorController controller;
        private readonly List<GameObject> created = new List<GameObject>();

        [SetUp]
        public void Setup()
        {
            projGO = new GameObject("TestProjector", typeof(Camera));
            projGO.AddComponent<PMSDKProjector>();
            ctrlGO = new GameObject("SkinEditor");
            controller = ctrlGO.AddComponent<PMSDKSkinEditorController>();
            controller.Projector = projGO.GetComponent<PMSDKProjector>();
            controller.SkinLayerName = "TransparentFX";
            controller.enabled = false;
            controller.enabled = true;
        }

        [TearDown]
        public void Teardown()
        {
            foreach (PMSDKSkin s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None)) {
                Object.DestroyImmediate(s.gameObject);
            }
            foreach (GameObject go in created) {
                if (go != null) {
                    Object.DestroyImmediate(go);
                }
            }
            created.Clear();
            if (ctrlGO != null) {
                Object.DestroyImmediate(ctrlGO);
            }
            if (projGO != null) {
                Object.DestroyImmediate(projGO);
            }
        }

        // ------------------------------------------------------------------ mesh

        [Test]
        public void SubdividedQuad_HasGridVerticesUvsAndNormals()
        {
            Mesh mesh = PMSDKSkinMeshBuilder.BuildSubdividedQuadMesh(4);
            try {
                Assert.AreEqual(25, mesh.vertexCount);
                Assert.AreEqual(4 * 4 * 6, mesh.triangles.Length);
                Assert.AreEqual(25, mesh.normals.Length);
                Assert.AreEqual(25, mesh.colors.Length);
                Vector2[] uvs = mesh.uv;
                Vector3[] positions = mesh.vertices;
                Assert.AreEqual(new Vector2(0f, 0f), uvs[0]);
                Assert.AreEqual(new Vector2(1f, 0f), uvs[4]);
                Assert.AreEqual(new Vector2(0f, 1f), uvs[20]);
                Assert.AreEqual(new Vector2(0.5f, 0.25f), uvs[1 * 5 + 2]);
                for (int i = 0; i < positions.Length; i++) {
                    Assert.AreEqual(uvs[i].x, positions[i].x, 1e-6f);
                    Assert.AreEqual(uvs[i].y, positions[i].y, 1e-6f);
                }
                // Same winding as the unit quad: first triangle is BL, BR, TR of cell (0,0).
                int[] triangles = mesh.triangles;
                Assert.AreEqual(0, triangles[0]);
                Assert.AreEqual(1, triangles[1]);
                Assert.AreEqual(6, triangles[2]);
            } finally {
                Object.DestroyImmediate(mesh);
            }
        }

        // ------------------------------------------------------------------ correction grid on the skin

        [Test]
        public void SkinCorrection_IsBilinearAndValidated()
        {
            PMSDKSkin skin = NewRectangleSkin();
            Assert.IsFalse(skin.HasCorrection);
            Assert.AreEqual(Vector2.zero, skin.EvaluateWarpOffset(new Vector2(0.5f, 0.5f)));

            skin.SetCorrection(2, 2, new List<Vector2> {
                new Vector2(0f, 0f), new Vector2(0.1f, 0f), new Vector2(0f, 0.2f), new Vector2(0.1f, 0.2f),
            });
            Assert.IsTrue(skin.HasCorrection);
            Assert.AreEqual(2, skin.CorrectionColumns);
            Vector2 mid = skin.EvaluateWarpOffset(new Vector2(0.5f, 0.5f));
            Assert.AreEqual(0.05f, mid.x, 1e-6f);
            Assert.AreEqual(0.10f, mid.y, 1e-6f);
            Assert.IsTrue(((IPMSDKWarpOffsetSource)skin).HasWarpOffsets);

            Assert.Throws<System.ArgumentException>(delegate { skin.SetCorrection(3, 3, new List<Vector2> { Vector2.zero }); });
            Assert.Throws<System.ArgumentOutOfRangeException>(delegate { skin.SetCorrection(1, 3, new List<Vector2> { Vector2.zero, Vector2.zero, Vector2.zero }); });
            Assert.IsTrue(skin.HasCorrection, "a rejected update keeps the previous grid");

            skin.Kind = PMSDKSkinShapeKind.Polygon;
            Assert.IsFalse(((IPMSDKWarpOffsetSource)skin).HasWarpOffsets, "polygon skins have no native warp to offset");
            skin.Kind = PMSDKSkinShapeKind.Rectangle;

            skin.ClearCorrection();
            Assert.IsFalse(skin.HasCorrection);
            Assert.AreEqual(0, skin.CorrectionColumns);
        }

        [Test]
        public void SkinCorrection_RoundTripsThroughJsonAndUndo()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            PMSDKSkin skin = controller.Selected;
            List<Vector2> offsets = new List<Vector2>();
            for (int i = 0; i < 9; i++) {
                offsets.Add(new Vector2(0.001f * i, -0.002f * i));
            }
            skin.SetCorrection(3, 3, offsets);

            string json = PMSDKSkinSetJson.Serialize(controller);
            StringAssert.Contains("\"correction\"", json);
            PMSDKSkinSetJson.Set set = PMSDKSkinSetJson.Deserialize(json);
            skin.ClearCorrection();
            PMSDKSkinSetJson.Apply(controller, set);
            PMSDKSkin restored = controller.Selected;
            Assert.IsTrue(restored.HasCorrection);
            Assert.AreEqual(3, restored.CorrectionRows);
            Assert.AreEqual(offsets[8].y, restored.GetCorrectionOffsets()[8].y, 1e-7f);

            // Undo restores the pre-change correction (the editor snapshots the whole set).
            controller.Undo.Record(controller, "auto-adjust");
            restored.ClearCorrection();
            controller.UndoLast();
            Assert.IsTrue(controller.Selected.HasCorrection);
        }

        [Test]
        public void SkinCorrection_LegacyJsonHasNoCorrectionAndBadGridIsIgnored()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            PMSDKSkin skin = controller.Selected;
            skin.SetCorrection(2, 2, new List<Vector2> { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero });

            PMSDKSkinSetJson.Set set = PMSDKSkinSetJson.Deserialize(PMSDKSkinSetJson.Serialize(controller));
            set.skins[0].correction = null; // a file written before D-044
            PMSDKSkinSetJson.Apply(controller, set);
            Assert.IsFalse(controller.Selected.HasCorrection);

            set.skins[0].correction = new PMSDKSkinSetJson.CorrectionCfg { columns = 3, rows = 3, offsets = new List<Vector2> { Vector2.zero } };
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("invalid correction grid"));
            PMSDKSkinSetJson.Apply(controller, set);
            Assert.IsFalse(controller.Selected.HasCorrection);
        }

        // ------------------------------------------------------------------ native wrapper

        [Test]
        public void Native_CheckerCornersTextureAndWarp()
        {
            RequireNative();
            string error;
            Vector2[] uvs = PMSDKAutoAdjustNative.CheckerCornerUVs(PMSDKCheckerSpec.Default(), out error);
            Assert.IsNotNull(uvs, error);
            Assert.AreEqual(54, uvs.Length);

            PMSDKCheckerSpec bad = PMSDKCheckerSpec.Default();
            bad.SquaresY = 6;
            Assert.IsNull(PMSDKAutoAdjustNative.CheckerCornerUVs(bad, out error));
            Assert.IsNotEmpty(error);

            Texture2D texture = PMSDKAutoAdjustNative.RenderCheckerTexture(PMSDKCheckerSpec.Default(), 200, 140, out error);
            Assert.IsNotNull(texture, error);
            try {
                Assert.AreEqual(200, texture.width);
                // Top-left square of the board is black; the margin is white (texture v = 1 is the top).
                Assert.Less(texture.GetPixel(22, 140 - 1 - 20).r, 0.1f);
                Assert.Greater(texture.GetPixel(2, 2).r, 0.9f);
            } finally {
                Object.DestroyImmediate(texture);
            }

            List<Vector2> corners = new List<Vector2> {
                new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.15f), new Vector2(0.85f, 0.9f), new Vector2(0.15f, 0.8f),
            };
            Vector2 native = PMSDKAutoAdjustNative.EvaluateWarp(corners, 0, 0, null, 0.3f, 0.6f);
            Vector2 managed = SyntheticRoom.CornerPin(corners, 0.3f, 0.6f);
            Assert.AreEqual(managed.x, native.x, 1e-5f);
            Assert.AreEqual(managed.y, native.y, 1e-5f);
        }

        // ------------------------------------------------------------------ full loop

        [Test]
        public void AutoAdjust_ConvergesOnSyntheticRoomAndRestoresDisplay()
        {
            RequireNative();
            PMSDKSkin skin = NewRectangleSkin();
            skin.AlphaMaskPath = null;
            skin.Opacity = 0.7f;
            PMSDKSkin other = NewRectangleSkin();
            MeshRenderer otherRenderer = other.GetComponent<MeshRenderer>();
            List<Vector2> cornersBefore = new List<Vector2>(skin.Vertices);

            SyntheticRoom room = new SyntheticRoom(skin, 20f);
            PMSDKAutoAdjust adjust = NewAutoAdjust();
            adjust.StopRmsPx = 0.2f; // exercise several corrections, not just the default 0.5 px stop
            bool otherHiddenWhileMeasuring = false;
            room.OnCapture = delegate { otherHiddenWhileMeasuring |= !otherRenderer.enabled; };

            PMSDKAutoAdjustResult result = Run(adjust, skin, room, new List<PMSDKSkin> { skin, other });
            TestContext.WriteLine("Auto Adjust synthetic room: " + result.Message + " history=[" +
                                  string.Join(", ", result.RmsHistory.ConvertAll(delegate(float v) { return v.ToString("0.000"); })) + "]");

            Assert.IsTrue(result.Completed, result.Message);
            // With the shape-adapted checker (D-045) this scene starts near 1 px.
            Assert.Greater(result.Initial.RmsPx, 0.8f, "the synthetic surface must need correcting");
            Assert.Less(result.Final.RmsPx, 0.6f, result.Message);
            Assert.Less(result.Final.RmsPx, result.Initial.RmsPx * 0.4f, result.Message);
            Assert.IsTrue(result.Success, result.Message);
            Assert.LessOrEqual(result.Iterations, 4);
            Assert.IsTrue(skin.HasCorrection);
            Assert.IsTrue(room.Began && room.Ended, "camera Begin/End must pair up");
            Assert.IsTrue(otherHiddenWhileMeasuring, "other skins are hidden while measuring");

            // Temporary display state is restored; corners never move.
            Assert.IsNull(skin.TestPatternOverride);
            Assert.AreEqual(0.7f, skin.Opacity, 1e-6f);
            Assert.IsTrue(otherRenderer.enabled);
            for (int i = 0; i < 4; i++) {
                Assert.AreEqual(cornersBefore[i], skin.Vertices[i]);
            }
            Assert.IsFalse(adjust.IsRunning);
        }

        [Test]
        public void AutoAdjust_BlankCameraAbortsAndKeepsPreviousCorrection()
        {
            RequireNative();
            PMSDKSkin skin = NewRectangleSkin();
            List<Vector2> previous = new List<Vector2> { new Vector2(0.01f, 0f), Vector2.zero, Vector2.zero, Vector2.zero };
            skin.SetCorrection(2, 2, previous);
            SyntheticRoom room = new SyntheticRoom(skin, 0f) { Blank = true };

            PMSDKAutoAdjustResult result = Run(NewAutoAdjust(), skin, room, null);

            Assert.IsFalse(result.Completed);
            Assert.IsFalse(result.Success);
            StringAssert.Contains("not found", result.Message);
            Assert.AreEqual(2, skin.CorrectionColumns);
            Assert.AreEqual(0.01f, skin.GetCorrectionOffsets()[0].x, 1e-7f);
            Assert.IsNull(skin.TestPatternOverride);
            Assert.IsTrue(room.Ended);
        }

        [Test]
        public void AutoAdjust_RejectsNonRectangleMissingCameraAndFailedStart()
        {
            PMSDKAutoAdjust adjust = NewAutoAdjust();
            PMSDKSkin skin = NewRectangleSkin();

            PMSDKAutoAdjustResult noCamera = Run(adjust, skin, null, null);
            StringAssert.Contains("camera", noCamera.Message);

            // Ellipse skins are supported since Phase 2 (D-045); polygon skins have no corner pin.
            skin.Kind = PMSDKSkinShapeKind.Polygon;
            PMSDKAutoAdjustResult polygon = Run(adjust, skin, new SyntheticRoom(skin, 0f), null);
            StringAssert.Contains("Rectangle and Ellipse", polygon.Message);
            skin.Kind = PMSDKSkinShapeKind.Rectangle;

            RequireNative();
            SyntheticRoom broken = new SyntheticRoom(skin, 0f) { FailBegin = true };
            PMSDKAutoAdjustResult failed = Run(adjust, skin, broken, null);
            Assert.IsFalse(failed.Completed);
            StringAssert.Contains("did not start", failed.Message);
            Assert.IsFalse(broken.Ended, "End is only called after a successful Begin");
            Assert.IsFalse(skin.HasCorrection);
        }

        // ------------------------------------------------------------------ helpers

        private static void RequireNative()
        {
            if (!PMSDKAutoAdjustNative.IsAvailable()) {
                Assert.Ignore("ProjectionMappingSDK.dll without Auto Adjust exports is loaded; redeploy the native plugin.");
            }
        }

        private PMSDKSkin NewRectangleSkin()
        {
            GameObject go = new GameObject("AutoAdjustSkin", typeof(MeshFilter), typeof(MeshRenderer));
            created.Add(go);
            PMSDKSkin skin = go.AddComponent<PMSDKSkin>();
            skin.Kind = PMSDKSkinShapeKind.Rectangle;
            skin.Vertices = new List<Vector2> {
                new Vector2(0.14f, 0.12f), new Vector2(0.84f, 0.17f), new Vector2(0.80f, 0.88f), new Vector2(0.19f, 0.83f),
            };
            return skin;
        }

        private PMSDKAutoAdjust NewAutoAdjust()
        {
            GameObject go = new GameObject("AutoAdjust");
            created.Add(go);
            PMSDKAutoAdjust adjust = go.AddComponent<PMSDKAutoAdjust>();
            adjust.SettleFrames = 1;
            return adjust;
        }

        private static PMSDKAutoAdjustResult Run(PMSDKAutoAdjust adjust, PMSDKSkin skin, IPMSDKCalibrationCamera camera, IList<PMSDKSkin> others)
        {
            PMSDKAutoAdjustResult result = null;
            IEnumerator routine = adjust.AdjustSkin(skin, camera, others, delegate(PMSDKAutoAdjustResult r) { result = r; });
            int guard = 0;
            while (routine.MoveNext()) {
                guard++;
                Assert.Less(guard, 1000, "coroutine did not finish");
            }
            Assert.IsNotNull(result, "onDone must always be called");
            return result;
        }

        /// <summary>
        /// Synthetic calibration camera: projector raster -> camera pixels through a homography plus a
        /// smooth wave no corner pin can remove; renders whatever texture the skin currently shows
        /// through the skin's corner pin + correction grid.
        /// </summary>
        private sealed class SyntheticRoom : IPMSDKCalibrationCamera
        {
            private const int W = 320;
            private const int H = 240;
            private readonly PMSDKSkin skin;
            private readonly float wavePx;
            private readonly float[] rasterToCamera;

            public bool Blank;
            public bool FailBegin;
            public bool Began;
            public bool Ended;
            public System.Action OnCapture;

            public int Width
            {
                get { return W; }
            }

            public int Height
            {
                get { return H; }
            }

            public SyntheticRoom(PMSDKSkin skin, float wavePx)
            {
                this.skin = skin;
                this.wavePx = wavePx;
                // Raster corners BL, BR, TR, TL -> camera pixels (y down).
                rasterToCamera = QuadToHomography(new List<Vector2> {
                    new Vector2(22f, 228f), new Vector2(300f, 234f), new Vector2(292f, 10f), new Vector2(30f, 16f),
                });
            }

            public bool Begin()
            {
                Began = !FailBegin;
                return Began;
            }

            public void End()
            {
                Ended = true;
            }

            public byte[] CaptureLuminance()
            {
                OnCapture?.Invoke();
                byte[] frame = new byte[W * H];
                for (int i = 0; i < frame.Length; i++) {
                    frame[i] = 12;
                }
                Texture2D shown = skin.TestPatternOverride as Texture2D;
                if (Blank || shown == null) {
                    return frame;
                }
                Color32[] texels = shown.GetPixels32();
                int tw = shown.width;
                int th = shown.height;
                List<Vector2> corners = skin.Vertices;

                // Content UV for camera pixels on a coarse grid (Newton on uv -> camera), then bilinear.
                const int step = 4;
                int gw = W / step + 2;
                int gh = H / step + 2;
                Vector2[] grid = new Vector2[gw * gh];
                Vector2 guess = new Vector2(0.5f, 0.5f);
                for (int gy = 0; gy < gh; gy++) {
                    for (int gx = 0; gx < gw; gx++) {
                        Vector2 target = new Vector2(gx * step, gy * step);
                        Vector2 uv = guess;
                        for (int iter = 0; iter < 8; iter++) {
                            Vector2 f = Observe(corners, uv);
                            const float h = 1e-3f;
                            Vector2 fu = Observe(corners, uv + new Vector2(h, 0f));
                            Vector2 fv = Observe(corners, uv + new Vector2(0f, h));
                            float a = (fu.x - f.x) / h;
                            float b = (fv.x - f.x) / h;
                            float c = (fu.y - f.y) / h;
                            float d = (fv.y - f.y) / h;
                            float det = a * d - b * c;
                            if (Mathf.Abs(det) < 1e-6f) {
                                break;
                            }
                            float ex = target.x - f.x;
                            float ey = target.y - f.y;
                            uv += new Vector2((d * ex - b * ey) / det, (-c * ex + a * ey) / det);
                        }
                        bool usable = !float.IsNaN(uv.x) && !float.IsNaN(uv.y) &&
                                      uv.x > -0.5f && uv.x < 1.5f && uv.y > -0.5f && uv.y < 1.5f;
                        grid[gy * gw + gx] = usable ? uv : new Vector2(-1f, -1f);
                        guess = usable ? uv : new Vector2(0.5f, 0.5f);
                    }
                }
                for (int y = 0; y < H; y++) {
                    for (int x = 0; x < W; x++) {
                        float sum = 0f;
                        for (int s = 0; s < 4; s++) {
                            float px = Mathf.Max(0f, x - 0.25f + 0.5f * (s & 1));
                            float py = Mathf.Max(0f, y - 0.25f + 0.5f * (s >> 1));
                            Vector2 uv = SampleGrid(grid, gw, px / step, py / step);
                            sum += Shade(texels, tw, th, uv);
                        }
                        frame[y * W + x] = (byte)Mathf.Clamp(Mathf.RoundToInt(sum / 4f), 0, 255);
                    }
                }
                return frame;
            }

            private Vector2 Observe(List<Vector2> corners, Vector2 uv)
            {
                Vector2 raster = CornerPin(corners, uv.x, uv.y) + skin.EvaluateWarpOffset(uv);
                Vector2 p = Apply(rasterToCamera, raster);
                if (wavePx != 0f) {
                    float s = Mathf.Sin(2f * Mathf.PI * p.x / W) * Mathf.Sin(Mathf.PI * p.y / H);
                    p += new Vector2(wavePx * s, 0.6f * wavePx * s);
                }
                return p;
            }

            private static float Shade(Color32[] texels, int tw, int th, Vector2 uv)
            {
                if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f) {
                    return 12f;
                }
                int tx = Mathf.Clamp((int)(uv.x * tw), 0, tw - 1);
                int ty = Mathf.Clamp((int)(uv.y * th), 0, th - 1);
                // Projected black is not black and white is not saturated on a real wall.
                return 30f + texels[ty * tw + tx].r * (200f / 255f);
            }

            private static Vector2 SampleGrid(Vector2[] grid, int gw, float fx, float fy)
            {
                int x0 = (int)fx;
                int y0 = (int)fy;
                float tx = fx - x0;
                float ty = fy - y0;
                Vector2 a = grid[y0 * gw + x0];
                Vector2 b = grid[y0 * gw + x0 + 1];
                Vector2 c = grid[(y0 + 1) * gw + x0];
                Vector2 d = grid[(y0 + 1) * gw + x0 + 1];
                return a * ((1 - tx) * (1 - ty)) + b * (tx * (1 - ty)) + c * ((1 - tx) * ty) + d * (tx * ty);
            }

            /// <summary>Unit-square -> quad homography (same as the native PerspectiveWarp).</summary>
            public static Vector2 CornerPin(IList<Vector2> q, float u, float v)
            {
                float[] h = QuadToHomography(q);
                return ApplyUnit(h, u, v);
            }

            private static float[] QuadToHomography(IList<Vector2> q)
            {
                float dx1 = q[1].x - q[2].x;
                float dx2 = q[3].x - q[2].x;
                float dx3 = q[0].x - q[1].x + q[2].x - q[3].x;
                float dy1 = q[1].y - q[2].y;
                float dy2 = q[3].y - q[2].y;
                float dy3 = q[0].y - q[1].y + q[2].y - q[3].y;
                float denom = dx1 * dy2 - dx2 * dy1;
                float g = (dx3 * dy2 - dx2 * dy3) / denom;
                float hh = (dx1 * dy3 - dx3 * dy1) / denom;
                return new float[] {
                    q[1].x - q[0].x + g * q[1].x, q[3].x - q[0].x + hh * q[3].x, q[0].x,
                    q[1].y - q[0].y + g * q[1].y, q[3].y - q[0].y + hh * q[3].y, q[0].y,
                    g, hh,
                };
            }

            private static Vector2 ApplyUnit(float[] h, float u, float v)
            {
                float w = h[6] * u + h[7] * v + 1f;
                return new Vector2((h[0] * u + h[1] * v + h[2]) / w, (h[3] * u + h[4] * v + h[5]) / w);
            }

            private static Vector2 Apply(float[] h, Vector2 p)
            {
                return ApplyUnit(h, p.x, p.y);
            }
        }
    }
}
