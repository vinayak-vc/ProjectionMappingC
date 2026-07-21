using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Camera-measured luminance compensation. Projectors are brightest at centre and
    /// dimmer at the edges (vignetting); a soft-edge blend overlap is built from both
    /// projectors' DIMMEST edges, so even a perfect alpha ramp leaves a visible band.
    /// Gamma tuning cannot fix a spatial brightness difference — it must be measured.
    ///
    /// Reuses the all-white capture auto-align already takes for the shadow mask: that
    /// frame IS a picture of each projector's wall luminance. Mapping the camera pixels
    /// back into projector raster (via the same Gray-code correspondence) gives a
    /// per-projector raster luminance map. Normalizing every projector down to one shared
    /// target (compensation can only DIM, never add light) yields a per-projector gain
    /// map = target / measured, which the UnlitWarp shader samples as `_GainTex`.
    ///
    /// Pure function over the captures + correspondences (no Unity scene / native calls)
    /// so it is deterministic and testable — the same contract as <see cref="PMSDKAutoBlend"/>.
    /// All captures/correspondences must come from the SAME camera (same resolution and
    /// pose, same locked exposure) so luminance is comparable across projectors.
    ///
    /// Scope: flattens bright-content banding/unevenness. Does NOT fix the doubled
    /// projector-black glow on dark content — that is a separate per-region black-level item.
    /// </summary>
    public static class PMSDKLuminanceCompensation
    {
        /// <summary>Per-projector gain map in raster space (row-major, y increasing upward — the corner-pin/grid convention).</summary>
        public struct GainMap
        {
            public int Width;
            public int Height;
            public float[] Gain;   // length Width*Height, each in [gainMin, 1]
            public bool Valid;     // false if this projector had no usable measurement
        }

        /// <param name="whiteCaptures">Per-projector all-white camera capture (row-major luminance 0..255, top-left origin, camW*camH). Same order as <paramref name="correspondences"/>.</param>
        /// <param name="correspondences">Per-projector camera->projector decode over the same camWxcamH camera.</param>
        /// <param name="projW">Pattern/raster width used for the decode.</param>
        /// <param name="projH">Pattern/raster height used for the decode.</param>
        /// <param name="mapW">Output gain-map width (may be coarser than the raster — the map is smooth).</param>
        /// <param name="mapH">Output gain-map height.</param>
        /// <param name="gainMin">Lower clamp on gain: how far a bright region may be dimmed (guards against over-dimming from a noisy-dark target).</param>
        /// <param name="smoothRadius">Box-blur radius (map cells) to suppress webcam noise. 0 = no smoothing.</param>
        /// <param name="targetPercentile">Compensation target = this percentile of pooled valid luminance across ALL projectors (0 = true min; a small value resists a few noisy-dark outliers).</param>
        public static GainMap[] Compute(
            IReadOnlyList<byte[]> whiteCaptures,
            IReadOnlyList<PMSDKGrayCodeDecode.Correspondence[]> correspondences,
            int camW, int camH, int projW, int projH,
            int mapW, int mapH,
            float gainMin = 0.5f, int smoothRadius = 2, float targetPercentile = 0.02f)
        {
            int n = correspondences != null ? correspondences.Count : 0;
            var maps = new GainMap[n];
            if (n == 0) return maps;

            mapW = Mathf.Max(1, mapW);
            mapH = Mathf.Max(1, mapH);
            gainMin = Mathf.Clamp01(gainMin);
            int camPx = camW * camH;
            int cells = mapW * mapH;

            // --- per-projector measured luminance in map (raster) space ---
            var lum = new float[n][];
            var lumValid = new bool[n][];
            for (int p = 0; p < n; p++)
            {
                var cp = correspondences[p];
                byte[] white = (whiteCaptures != null && p < whiteCaptures.Count) ? whiteCaptures[p] : null;

                var sum = new float[cells];
                var cnt = new int[cells];
                if (cp != null && white != null)
                {
                    int lim = Mathf.Min(camPx, Mathf.Min(cp.Length, white.Length));
                    for (int i = 0; i < lim; i++)
                    {
                        if (!cp[i].Valid) continue;
                        // Scatter each lit camera pixel's luminance into its raster bin.
                        int mx = (int)((cp[i].ProjectorX + 0.5f) / projW * mapW);
                        int my = (int)((cp[i].ProjectorY + 0.5f) / projH * mapH);
                        if (mx < 0 || mx >= mapW || my < 0 || my >= mapH) continue;
                        int mi = my * mapW + mx;
                        sum[mi] += white[i];
                        cnt[mi]++;
                    }
                }

                var l = new float[cells];
                var v = new bool[cells];
                for (int i = 0; i < cells; i++)
                {
                    if (cnt[i] > 0) { l[i] = sum[i] / cnt[i]; v[i] = true; }
                }
                FillHoles(l, v, mapW, mapH);
                if (smoothRadius > 0) BoxBlurValid(l, v, mapW, mapH, smoothRadius);
                lum[p] = l;
                lumValid[p] = v;
            }

            // --- shared compensation target (robust min across every projector) ---
            float target = Percentile(lum, lumValid, Mathf.Clamp01(targetPercentile));

            for (int p = 0; p < n; p++)
            {
                var l = lum[p];
                var v = lumValid[p];
                var g = new float[cells];
                bool any = false;
                for (int i = 0; i < cells; i++)
                {
                    if (v[i] && l[i] > 1e-3f)
                    {
                        // target <= measured for the darkest cells, so gain <= 1 (dim only).
                        g[i] = Mathf.Clamp(target / l[i], gainMin, 1f);
                        any = true;
                    }
                    else
                    {
                        g[i] = 1f; // no measurement here -> pass through untouched
                    }
                }
                maps[p] = new GainMap { Width = mapW, Height = mapH, Gain = g, Valid = any };
            }
            return maps;
        }

        /// <summary>
        /// Fill unmeasured cells (raster bins no camera pixel happened to hit) by
        /// iterative dilation from measured 4-neighbours, so the smoothing and gain
        /// steps see a fully-populated map. Cells filled here are marked valid.
        /// </summary>
        private static void FillHoles(float[] l, bool[] v, int w, int h)
        {
            int cells = w * h;
            int holes = 0;
            for (int i = 0; i < cells; i++) if (!v[i]) holes++;
            if (holes == 0 || holes == cells) return;

            var next = new float[cells];
            var filled = new bool[cells];
            // Bounded iterations: each pass grows the filled region by one ring.
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

        /// <summary>Separable box blur over valid cells (invalid cells excluded from the average).</summary>
        private static void BoxBlurValid(float[] l, bool[] v, int w, int h, int radius)
        {
            var tmp = new float[l.Length];
            // horizontal
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
            // vertical
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

        /// <summary>The p-th percentile (0..1) of every valid luminance value pooled across all projectors.</summary>
        private static float Percentile(float[][] lum, bool[][] valid, float p)
        {
            var vals = new List<float>();
            for (int a = 0; a < lum.Length; a++)
            {
                var l = lum[a];
                var v = valid[a];
                if (l == null) continue;
                for (int i = 0; i < l.Length; i++)
                    if (v[i]) vals.Add(l[i]);
            }
            if (vals.Count == 0) return 0f;
            vals.Sort();
            int idx = Mathf.Clamp(Mathf.RoundToInt(p * (vals.Count - 1)), 0, vals.Count - 1);
            return vals[idx];
        }
    }
}
