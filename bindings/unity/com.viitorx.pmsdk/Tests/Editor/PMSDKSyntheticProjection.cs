using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// Test-only synthetic projection room for Auto Adjust (no hardware). Renders what the skins
    /// currently display (TestPatternOverride, corner pin + correction grid, warp map) through a
    /// projector raster -> camera mapping (homography plus an optional smooth wave), lit regions with
    /// per-facet albedo, and the projector background colour of a given camera. Optionally provides a
    /// depth image (camera-space planes per image region) and pinhole intrinsics.
    /// </summary>
    internal sealed class PMSDKSyntheticProjection : IPMSDKCalibrationCamera, IPMSDKCameraIntrinsicsSource, IPMSDKDepthSource
    {
        public const float Focal = 280f;

        public readonly int W;
        public readonly int H;
        private readonly float[] rasterToCamera;
        private readonly float[] cameraToRaster;
        private readonly float wavePx;
        private readonly List<PMSDKSkin> skins;

        /// <summary>Camera whose background colour is the projector's flood (null = black).</summary>
        public Camera Background;
        /// <summary>Image regions (camera polygons) with their albedo; elsewhere <see cref="DefaultAlbedo"/>.</summary>
        public readonly List<KeyValuePair<Vector2[], float>> Albedo = new List<KeyValuePair<Vector2[], float>>();
        public float DefaultAlbedo = 0.9f;
        /// <summary>Depth planes (camera space n.X + d = 0, mm) per image region, first match wins.</summary>
        public readonly List<KeyValuePair<Vector2[], Vector4>> DepthPlanes = new List<KeyValuePair<Vector2[], Vector4>>();
        public bool ProvideDepth;
        public bool Blank;
        public bool FailBegin;
        public bool Began;
        public bool Ended;
        public int Captures;

        public int BeginCount;

        /// <param name="skins">Skins to render; null = every PMSDKSkin in the scene at capture time.</param>
        public PMSDKSyntheticProjection(IEnumerable<PMSDKSkin> skins, int width, int height, Vector2[] rasterCornersInCamera, float wavePx)
        {
            this.skins = skins != null ? new List<PMSDKSkin>(skins) : null;
            W = width;
            H = height;
            this.wavePx = wavePx;
            rasterToCamera = QuadToHomography(rasterCornersInCamera);
            cameraToRaster = Invert(rasterToCamera);
        }

        public int Width
        {
            get { return W; }
        }

        public int Height
        {
            get { return H; }
        }

        public bool HasDepth
        {
            get { return ProvideDepth && Began; }
        }

        public bool Begin()
        {
            BeginCount++;
            Began = !FailBegin;
            return Began;
        }

        public void End()
        {
            Ended = true;
        }

        private IEnumerable<PMSDKSkin> CurrentSkins()
        {
            return skins ?? (IEnumerable<PMSDKSkin>)Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None);
        }

        public bool TryGetIntrinsics(out PMSDKCameraIntrinsics intrinsics)
        {
            intrinsics = new PMSDKCameraIntrinsics { Fx = Focal, Fy = Focal, Cx = W * 0.5f, Cy = H * 0.5f };
            return true;
        }

        /// <summary>Projector raster position -> camera pixel (homography, then the wave).</summary>
        public Vector2 RasterToCamera(Vector2 raster)
        {
            Vector2 c = Apply(rasterToCamera, raster);
            if (wavePx != 0f) {
                float s = Mathf.Sin(2f * Mathf.PI * c.x / W) * Mathf.Sin(Mathf.PI * c.y / H);
                c += new Vector2(wavePx * s, 0.6f * wavePx * s);
            }
            return c;
        }

        /// <summary>Camera pixel -> projector raster (exact inverse, Newton when there is a wave).</summary>
        public Vector2 CameraToRaster(Vector2 camera)
        {
            Vector2 r = Apply(cameraToRaster, camera);
            if (wavePx == 0f) {
                return r;
            }
            for (int i = 0; i < 6; i++) {
                r = NewtonStep(delegate(Vector2 p) { return RasterToCamera(p); }, r, camera, 1e-4f);
            }
            return r;
        }

        public byte[] CaptureLuminance()
        {
            Captures++;
            byte[] frame = new byte[W * H];
            Color bg = Background != null && Background.clearFlags == CameraClearFlags.SolidColor ? Background.backgroundColor : Color.black;
            float backgroundLight = bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f;
            List<PMSDKSkin> visible = new List<PMSDKSkin>();
            Dictionary<PMSDKSkin, Color32[]> texels = new Dictionary<PMSDKSkin, Color32[]>();
            Dictionary<PMSDKSkin, WarpMapData> maps = new Dictionary<PMSDKSkin, WarpMapData>();
            foreach (PMSDKSkin skin in CurrentSkins()) {
                MeshRenderer renderer = skin != null ? skin.GetComponent<MeshRenderer>() : null;
                if (renderer == null || !renderer.enabled) {
                    continue;
                }
                visible.Add(skin);
                Texture2D texture = skin.TestPatternOverride as Texture2D;
                texels[skin] = texture != null ? texture.GetPixels32() : null;
                if (!skin.SuppressWarpMap && !string.IsNullOrEmpty(skin.WarpMapFile)) {
                    WarpMapData data = new WarpMapData();
                    string error;
                    if (PMSDKWarpMapFile.TryLoad(PMSDKSkinStore.ResolveMaskPath(skin.WarpMapFile), out data.Width, out data.Height,
                            out data.Rect, out data.Rgba, out error)) {
                        maps[skin] = data;
                    }
                }
            }
            visible.Sort(delegate(PMSDKSkin a, PMSDKSkin b) { return b.ZOrder.CompareTo(a.ZOrder); });

            for (int y = 0; y < H; y++) {
                for (int x = 0; x < W; x++) {
                    float sum = 0f;
                    for (int s = 0; s < 4; s++) {
                        Vector2 c = new Vector2(x - 0.25f + 0.5f * (s & 1), y - 0.25f + 0.5f * (s >> 1));
                        float light = Blank ? 0f : LightAt(c, visible, texels, maps, backgroundLight);
                        sum += 12f + 210f * light * AlbedoAt(c);
                    }
                    frame[y * W + x] = (byte)Mathf.Clamp(Mathf.RoundToInt(sum * 0.25f), 0, 255);
                }
            }
            return frame;
        }

        public ushort[] CaptureDepth()
        {
            if (!HasDepth) {
                return null;
            }
            ushort[] depth = new ushort[W * H];
            for (int y = 0; y < H; y++) {
                for (int x = 0; x < W; x++) {
                    Vector2 c = new Vector2(x, y);
                    foreach (KeyValuePair<Vector2[], Vector4> region in DepthPlanes) {
                        if (!Inside(region.Key, c)) {
                            continue;
                        }
                        Vector4 plane = region.Value;
                        float rx = (x - W * 0.5f) / Focal;
                        float ry = (y - H * 0.5f) / Focal;
                        float denom = plane.x * rx + plane.y * ry + plane.z;
                        float z = -plane.w / denom;
                        if (z > 0f && z < 60000f) {
                            depth[y * W + x] = (ushort)Mathf.RoundToInt(z);
                        }
                        break;
                    }
                }
            }
            return depth;
        }

        private float LightAt(Vector2 camera, List<PMSDKSkin> visible, Dictionary<PMSDKSkin, Color32[]> texels,
            Dictionary<PMSDKSkin, WarpMapData> maps, float backgroundLight)
        {
            Vector2 raster = CameraToRaster(camera);
            foreach (PMSDKSkin skin in visible) {
                Vector2 uv;
                WarpMapData map;
                if (!InverseWarp(skin, raster, out uv)) {
                    continue;
                }
                if (maps.TryGetValue(skin, out map)) {
                    if (!map.Sample(raster, out uv)) {
                        continue;
                    }
                }
                Color32[] data = texels[skin];
                Texture2D texture = skin.TestPatternOverride as Texture2D;
                if (data == null || texture == null) {
                    return 0.5f;
                }
                int tx = Mathf.Clamp((int)(uv.x * texture.width), 0, texture.width - 1);
                int ty = Mathf.Clamp((int)(uv.y * texture.height), 0, texture.height - 1);
                return data[ty * texture.width + tx].r / 255f;
            }
            // The projector's background only lights its own raster area.
            bool insideRaster = raster.x >= 0f && raster.x <= 1f && raster.y >= 0f && raster.y <= 1f;
            return insideRaster ? backgroundLight : 0f;
        }

        // Content UV under raster position for a skin (corner pin + correction), false when outside it.
        private static bool InverseWarp(PMSDKSkin skin, Vector2 raster, out Vector2 uv)
        {
            if (skin.Kind == PMSDKSkinShapeKind.Polygon || skin.Kind == PMSDKSkinShapeKind.Freeform) {
                // Mirrors PMSDKSkinMeshBuilder.BuildPolygon: raster-space outline, UV = its bounding box.
                Vector2[] outline = skin.Vertices.ToArray();
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                foreach (Vector2 v in outline) {
                    min = Vector2.Min(min, v);
                    max = Vector2.Max(max, v);
                }
                Vector2 size = Vector2.Max(max - min, new Vector2(1e-6f, 1e-6f));
                uv = new Vector2((raster.x - min.x) / size.x, (raster.y - min.y) / size.y);
                return Inside(outline, raster);
            }
            float[] h = QuadToHomography(skin.Vertices.ToArray());
            uv = Apply(Invert(h), raster);
            if (skin.HasCorrection) {
                for (int i = 0; i < 4; i++) {
                    uv = NewtonStep(delegate(Vector2 p) { return Apply(h, p) + skin.EvaluateWarpOffset(p); }, uv, raster, 1e-4f);
                }
            }
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        private float AlbedoAt(Vector2 c)
        {
            foreach (KeyValuePair<Vector2[], float> region in Albedo) {
                if (Inside(region.Key, c)) {
                    return region.Value;
                }
            }
            return DefaultAlbedo;
        }

        private sealed class WarpMapData
        {
            public int Width;
            public int Height;
            public Rect Rect;
            public float[] Rgba;

            public bool Sample(Vector2 raster, out Vector2 uv)
            {
                uv = Vector2.zero;
                float fx = (raster.x - Rect.x) / Rect.width * Width - 0.5f;
                float fy = (raster.y - Rect.y) / Rect.height * Height - 0.5f;
                int x0 = Mathf.FloorToInt(fx);
                int y0 = Mathf.FloorToInt(fy);
                float tx = fx - x0;
                float ty = fy - y0;
                float a = 0f;
                Vector2 sum = Vector2.zero;
                for (int j = 0; j < 2; j++) {
                    for (int i = 0; i < 2; i++) {
                        int px = Mathf.Clamp(x0 + i, 0, Width - 1);
                        int py = Mathf.Clamp(y0 + j, 0, Height - 1);
                        float w = (i == 0 ? 1f - tx : tx) * (j == 0 ? 1f - ty : ty);
                        int k = (py * Width + px) * 4;
                        float valid = Rgba[k + 3] > 0.5f ? 1f : 0f;
                        sum += new Vector2(Rgba[k], Rgba[k + 1]) * (w * valid);
                        a += w * valid;
                    }
                }
                if (a < 0.5f) {
                    return false;
                }
                uv = sum / a;
                // Mirrors UnlitWarp: content UVs outside the unit square are discarded.
                return uv.x >= -0.001f && uv.y >= -0.001f && uv.x <= 1.001f && uv.y <= 1.001f;
            }
        }

        // ---- small maths helpers ----

        internal static Vector2 NewtonStep(System.Func<Vector2, Vector2> f, Vector2 x, Vector2 target, float h)
        {
            Vector2 fx = f(x);
            Vector2 fu = f(x + new Vector2(h, 0f));
            Vector2 fv = f(x + new Vector2(0f, h));
            float a = (fu.x - fx.x) / h;
            float b = (fv.x - fx.x) / h;
            float c = (fu.y - fx.y) / h;
            float d = (fv.y - fx.y) / h;
            float det = a * d - b * c;
            if (Mathf.Abs(det) < 1e-12f) {
                return x;
            }
            float ex = target.x - fx.x;
            float ey = target.y - fx.y;
            return x + new Vector2((d * ex - b * ey) / det, (-c * ex + a * ey) / det);
        }

        internal static bool Inside(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++) {
                if ((polygon[i].y > p.y) != (polygon[j].y > p.y) &&
                    p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>Unit square (BL, BR, TR, TL) -> quad homography, 3x3 row-major with h[8] = 1.</summary>
        internal static float[] QuadToHomography(Vector2[] q)
        {
            float dx1 = q[1].x - q[2].x;
            float dx2 = q[3].x - q[2].x;
            float dx3 = q[0].x - q[1].x + q[2].x - q[3].x;
            float dy1 = q[1].y - q[2].y;
            float dy2 = q[3].y - q[2].y;
            float dy3 = q[0].y - q[1].y + q[2].y - q[3].y;
            float g = 0f;
            float hh = 0f;
            if (Mathf.Abs(dx3) > 1e-9f || Mathf.Abs(dy3) > 1e-9f) {
                float denom = dx1 * dy2 - dx2 * dy1;
                g = (dx3 * dy2 - dx2 * dy3) / denom;
                hh = (dx1 * dy3 - dx3 * dy1) / denom;
            }
            return new float[] {
                q[1].x - q[0].x + g * q[1].x, q[3].x - q[0].x + hh * q[3].x, q[0].x,
                q[1].y - q[0].y + g * q[1].y, q[3].y - q[0].y + hh * q[3].y, q[0].y,
                g, hh, 1f,
            };
        }

        internal static Vector2 Apply(float[] h, Vector2 p)
        {
            float w = h[6] * p.x + h[7] * p.y + h[8];
            return new Vector2((h[0] * p.x + h[1] * p.y + h[2]) / w, (h[3] * p.x + h[4] * p.y + h[5]) / w);
        }

        internal static float[] Invert(float[] m)
        {
            double a = m[0], b = m[1], c = m[2], d = m[3], e = m[4], f = m[5], g = m[6], h = m[7], i = m[8];
            double det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
            return new float[] {
                (float)((e * i - f * h) / det), (float)((c * h - b * i) / det), (float)((b * f - c * e) / det),
                (float)((f * g - d * i) / det), (float)((a * i - c * g) / det), (float)((c * d - a * f) / det),
                (float)((d * h - e * g) / det), (float)((b * g - a * h) / det), (float)((a * e - b * d) / det),
            };
        }

        /// <summary>Drives a coroutine to the end in EditMode, including nested IEnumerators.</summary>
        internal static void Drive(IEnumerator routine)
        {
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            stack.Push(routine);
            int guard = 0;
            while (stack.Count > 0) {
                Assert.Less(++guard, 200000, "coroutine did not finish");
                IEnumerator top = stack.Peek();
                if (!top.MoveNext()) {
                    stack.Pop();
                    continue;
                }
                IEnumerator nested = top.Current as IEnumerator;
                if (nested != null) {
                    stack.Push(nested);
                }
            }
        }
    }
}
