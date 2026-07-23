using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Camera-measured per-region black-level compensation. Projectors cannot emit true
    /// black — each leaks a residual floor at signal 0. Where two projectors overlap
    /// (the blend zone) BOTH floors add, so the overlap's black is ~2x a single
    /// projector's black: a brighter central band that is obvious on dark content and
    /// invisible on bright content. Luminance compensation (a multiply) cannot touch it,
    /// because gain * 0 = 0 — an additive floor needs an additive fix.
    ///
    /// The fix is "black-level uplift": raise the single-projector regions' black UP to
    /// the overlap's doubled black so the whole canvas sits at one uniform (slightly grey)
    /// floor. You cannot remove the overlap's extra light, so you match everything else to
    /// it. The sweep already captures a BLACK frame per projector (for the shadow-mask
    /// gate); that frame IS each projector's per-pixel floor. Combined with the WHITE frame
    /// (to convert an emitted-light deficit into a signal-space lift) this yields a
    /// per-projector additive lift map the UnlitWarp shader applies as `_BlackLiftTex`.
    ///
    /// Pure function over the captures + correspondences (no Unity scene / native calls),
    /// the additive twin of <see cref="PMSDKLuminanceCompensation"/> and the same testable
    /// contract as <see cref="PMSDKAutoBlend"/>. All captures/correspondences must come from
    /// the SAME camera (same resolution, pose, locked exposure).
    ///
    /// The captures are single-channel luminance, so this flattens the BRIGHTNESS of the
    /// overlap band (the dominant artifact). A residual colour tint of the lifted floor is
    /// left to a per-projector colour offset (`PMSDKColorCorrection`); measured per-channel
    /// tint would need an RGB capture (future).
    /// </summary>
    public static class PMSDKBlackLevelCompensation
    {
        /// <summary>Per-projector additive black-lift map in raster space (row-major, y-up), each cell a signal lift in [0, maxLift].</summary>
        public struct LiftMap
        {
            public int Width;
            public int Height;
            public float[] Lift;   // length Width*Height, signal units (matches the shader black-level lerp)
            public bool Valid;     // false if this projector needs (or could measure) no lift
        }

        /// <param name="whiteCaptures">Per-projector all-white camera capture (luminance 0..255, row-major, camW*camH).</param>
        /// <param name="blackCaptures">Per-projector all-black camera capture (same layout) — the residual floor.</param>
        /// <param name="correspondences">Per-projector camera->projector decode over the same camWxcamH camera.</param>
        /// <param name="mapW">Output lift-map width (may be coarse — the map is smooth).</param>
        /// <param name="mapH">Output lift-map height.</param>
        /// <param name="maxLift">Upper clamp on the signal lift (guards against a dark/odd measurement over-lifting).</param>
        /// <param name="smoothRadius">Box-blur radius (map cells) to suppress webcam noise. 0 = none.</param>
        /// <param name="targetPercentile">Uplift target = this high percentile of the pooled per-canvas summed floor (1 = the brightest overlap black; slightly below resists a few noisy-bright pixels).</param>
        public static LiftMap[] Compute(
            IReadOnlyList<byte[]> whiteCaptures,
            IReadOnlyList<byte[]> blackCaptures,
            IReadOnlyList<PMSDKGrayCodeDecode.Correspondence[]> correspondences,
            int camW, int camH, int projW, int projH,
            int mapW, int mapH,
            float maxLift = 0.15f, int smoothRadius = 2, float targetPercentile = 0.98f)
        {
            int n = correspondences != null ? correspondences.Count : 0;
            var maps = new LiftMap[n];
            if (n == 0) return maps;

            mapW = Mathf.Max(1, mapW);
            mapH = Mathf.Max(1, mapH);
            maxLift = Mathf.Clamp01(maxLift);
            int camPx = camW * camH;
            int cells = mapW * mapH;

            // How many projectors light each camera pixel, and the summed black floor there.
            var litCount = new byte[camPx];
            var totalBlack = new float[camPx];
            for (int p = 0; p < n; p++)
            {
                var cp = correspondences[p];
                byte[] black = (blackCaptures != null && p < blackCaptures.Count) ? blackCaptures[p] : null;
                if (cp == null) continue;
                int lim = Mathf.Min(camPx, cp.Length);
                for (int i = 0; i < lim; i++)
                {
                    if (!cp[i].Valid) continue;
                    litCount[i]++;
                    if (black != null && i < black.Length) totalBlack[i] += black[i];
                }
            }

            // Uplift target = the brightest per-canvas summed floor (the overlap). A high
            // percentile over lit pixels resists a few noisy-bright outliers.
            float target = HighPercentile(totalBlack, litCount, Mathf.Clamp01(targetPercentile));

            for (int p = 0; p < n; p++)
            {
                var cp = correspondences[p];
                byte[] white = (whiteCaptures != null && p < whiteCaptures.Count) ? whiteCaptures[p] : null;
                byte[] black = (blackCaptures != null && p < blackCaptures.Count) ? blackCaptures[p] : null;

                var sum = new float[cells];
                var cnt = new int[cells];
                if (cp != null && black != null)
                {
                    int lim = Mathf.Min(camPx, Mathf.Min(cp.Length, black.Length));
                    for (int i = 0; i < lim; i++)
                    {
                        if (!cp[i].Valid) continue;
                        int lc = litCount[i];
                        if (lc <= 0) continue;

                        // Deficit to reach the uniform target at this canvas point, split
                        // evenly across the projectors that cover it. In the overlap the
                        // summed floor already ~= target, so the deficit (and lift) is ~0;
                        // in single-projector regions p carries the whole lift up to target.
                        float deficitEmitted = Mathf.Max(0f, target - totalBlack[i]) / lc;
                        // Convert the emitted-light deficit into a signal-space lift using
                        // this projector's own black->white span at this pixel.
                        float span = (white != null && i < white.Length) ? (white[i] - black[i]) : (255f - black[i]);
                        float lift = deficitEmitted / Mathf.Max(span, 1f);

                        int mx = (int)((cp[i].ProjectorX + 0.5f) / projW * mapW);
                        int my = (int)((cp[i].ProjectorY + 0.5f) / projH * mapH);
                        if (mx < 0 || mx >= mapW || my < 0 || my >= mapH) continue;
                        int mi = my * mapW + mx;
                        sum[mi] += Mathf.Clamp(lift, 0f, maxLift);
                        cnt[mi]++;
                    }
                }

                var l = new float[cells];
                var v = new bool[cells];
                bool any = false;
                for (int i = 0; i < cells; i++)
                {
                    if (cnt[i] > 0)
                    {
                        l[i] = sum[i] / cnt[i];
                        v[i] = true;
                        if (l[i] > 1e-4f) any = true;
                    }
                }
                FillHoles(l, v, mapW, mapH);
                if (smoothRadius > 0) BoxBlurValid(l, v, mapW, mapH, smoothRadius);
                // Cells with no measurement stay 0 (no lift) — a black-level over-lift is
                // far more visible than a missed one.
                maps[p] = new LiftMap { Width = mapW, Height = mapH, Lift = l, Valid = any };
            }
            return maps;
        }

        /// <summary>The p-th percentile (0..1) of the summed floor over camera pixels lit by at least one projector.</summary>
        private static float HighPercentile(float[] totalBlack, byte[] litCount, float p)
        {
            var vals = new List<float>();
            for (int i = 0; i < totalBlack.Length; i++)
                if (litCount[i] > 0) vals.Add(totalBlack[i]);
            if (vals.Count == 0) return 0f;
            vals.Sort();
            int idx = Mathf.Clamp(Mathf.RoundToInt(p * (vals.Count - 1)), 0, vals.Count - 1);
            return vals[idx];
        }

        // --- shared with the luminance twin: dilation hole-fill + masked box blur ---

        private static void FillHoles(float[] l, bool[] v, int w, int h)
        {
            int cells = w * h;
            int holes = 0;
            for (int i = 0; i < cells; i++) if (!v[i]) holes++;
            if (holes == 0 || holes == cells) return;

            var next = new float[cells];
            var filled = new bool[cells];
            for (int iter = 0; iter < w + h && holes > 0; iter++)
            {
                System.Array.Copy(l, next, cells);
                System.Array.Copy(v, filled, cells);
                bool progressed = false;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (v[i]) continue;
                        float acc = 0f; int k = 0;
                        if (x > 0 && v[i - 1]) { acc += l[i - 1]; k++; }
                        if (x < w - 1 && v[i + 1]) { acc += l[i + 1]; k++; }
                        if (y > 0 && v[i - w]) { acc += l[i - w]; k++; }
                        if (y < h - 1 && v[i + w]) { acc += l[i + w]; k++; }
                        if (k > 0)
                        {
                            next[i] = acc / k;
                            filled[i] = true;
                            holes--;
                            progressed = true;
                        }
                    }
                }
                System.Array.Copy(next, l, cells);
                System.Array.Copy(filled, v, cells);
                if (!progressed) break;
            }
        }

        private static void BoxBlurValid(float[] l, bool[] v, int w, int h, int radius)
        {
            var tmp = new float[l.Length];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float acc = 0f; int k = 0;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int xx = x + dx;
                        if (xx < 0 || xx >= w) continue;
                        int j = y * w + xx;
                        if (!v[j]) continue;
                        acc += l[j]; k++;
                    }
                    tmp[y * w + x] = k > 0 ? acc / k : l[y * w + x];
                }
            }
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float acc = 0f; int k = 0;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= h) continue;
                        int j = yy * w + x;
                        if (!v[j]) continue;
                        acc += tmp[j]; k++;
                    }
                    l[y * w + x] = k > 0 ? acc / k : tmp[y * w + x];
                }
            }
        }
    }
}
