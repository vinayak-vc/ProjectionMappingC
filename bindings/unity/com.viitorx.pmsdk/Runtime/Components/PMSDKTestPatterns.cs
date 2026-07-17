using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>Professional calibration pattern set (cycled with Y in calibration mode).</summary>
    public enum PMSDKTestPatternType
    {
        Checkerboard,   // classic alignment checker (+ border/crosshair/circle overlays)
        FocusGrid,      // fine 1-px line grid — focusing the projector lens
        Convergence,    // crosshairs + circles at centre/corners/edge midpoints — multi-projector convergence
        ColorBars,      // SMPTE-style 75% bars + reference strip — colour/channel checks
        GrayRamp,       // continuous ramp + 16-step wedge — gamma / banding / black-level
        White, Black, Red, Green, Blue // solids — uniformity, black-level, channel isolation
    }

    /// <summary>
    /// Pure test-pattern rasterizers. Static and deterministic so they are
    /// unit-testable; PMSDKTestPattern feeds the result into its runtime texture.
    /// All patterns write the full pixels buffer (size*size, row-major, bottom-up
    /// as Unity's SetPixels expects).
    /// </summary>
    public static class PMSDKTestPatterns
    {
        public struct Overlays
        {
            public bool Border; public Color BorderColor;
            public bool Crosshair; public Color CrosshairColor;
            public bool Circle; public Color CircleColor;
        }

        public static void Generate(PMSDKTestPatternType type, Color[] pixels, int size,
                                    int gridSize, Color color1, Color color2, Overlays overlays)
        {
            switch (type)
            {
                case PMSDKTestPatternType.Checkerboard: Checkerboard(pixels, size, gridSize, color1, color2); break;
                case PMSDKTestPatternType.FocusGrid: FocusGrid(pixels, size); break;
                case PMSDKTestPatternType.Convergence: Convergence(pixels, size); break;
                case PMSDKTestPatternType.ColorBars: ColorBars(pixels, size); break;
                case PMSDKTestPatternType.GrayRamp: GrayRamp(pixels, size); break;
                case PMSDKTestPatternType.White: Solid(pixels, Color.white); break;
                case PMSDKTestPatternType.Black: Solid(pixels, Color.black); break;
                case PMSDKTestPatternType.Red: Solid(pixels, Color.red); break;
                case PMSDKTestPatternType.Green: Solid(pixels, Color.green); break;
                case PMSDKTestPatternType.Blue: Solid(pixels, Color.blue); break;
            }

            // Alignment overlays only make sense on the geometric patterns.
            if (type == PMSDKTestPatternType.Checkerboard || type == PMSDKTestPatternType.FocusGrid)
            {
                ApplyOverlays(pixels, size, overlays);
            }
        }

        // ---------------- patterns ----------------

        private static void Solid(Color[] p, Color c)
        {
            for (int i = 0; i < p.Length; i++) p[i] = c;
        }

        private static void Checkerboard(Color[] p, int size, int gridSize, Color c1, Color c2)
        {
            int cell = Mathf.Max(1, size / Mathf.Max(1, gridSize));
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    p[y * size + x] = ((x / cell + y / cell) % 2 == 0) ? c1 : c2;
        }

        private static void FocusGrid(Color[] p, int size)
        {
            // Black field with fine 1-px white lines every size/32 — sharp only when
            // the lens is focused; also a centre diagonal cross for astigmatism.
            Solid(p, Color.black);
            int step = Mathf.Max(8, size / 32);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (x % step == 0 || y % step == 0)
                        p[y * size + x] = Color.white;
            for (int i = 0; i < size; i++)
            {
                p[i * size + i] = Color.white;
                p[i * size + (size - 1 - i)] = Color.white;
            }
        }

        private static void Convergence(Color[] p, int size)
        {
            // Mid-gray field with 9 crosshair+circle targets (centre, corners, edge
            // midpoints) — projectors converge when the targets coincide on the wall.
            Solid(p, new Color(0.25f, 0.25f, 0.25f, 1f));
            float[] pos = { 0.1f, 0.5f, 0.9f };
            int r = size / 16;
            foreach (float fy in pos)
                foreach (float fx in pos)
                {
                    int cx = (int)(fx * (size - 1));
                    int cy = (int)(fy * (size - 1));
                    DrawCross(p, size, cx, cy, r, Color.white);
                    DrawCircle(p, size, cx, cy, r, Color.yellow);
                }
        }

        private static readonly Color[] Bars75 =
        {
            new Color(0.75f, 0.75f, 0.75f), // gray
            new Color(0.75f, 0.75f, 0f),    // yellow
            new Color(0f, 0.75f, 0.75f),    // cyan
            new Color(0f, 0.75f, 0f),       // green
            new Color(0.75f, 0f, 0.75f),    // magenta
            new Color(0.75f, 0f, 0f),       // red
            new Color(0f, 0f, 0.75f)        // blue
        };

        private static void ColorBars(Color[] p, int size)
        {
            // Top 3/4: SMPTE-style 75% bars. Bottom 1/4: 100% white / black / 100%
            // primaries strip for channel clipping checks.
            int split = size / 4;
            for (int y = 0; y < size; y++)
            {
                bool top = y >= split; // texture is bottom-up: rows above `split` are the visual top
                for (int x = 0; x < size; x++)
                {
                    if (top)
                    {
                        int bar = Mathf.Min(x * 7 / size, 6);
                        p[y * size + x] = Bars75[bar];
                    }
                    else
                    {
                        int seg = Mathf.Min(x * 5 / size, 4);
                        p[y * size + x] = seg switch
                        {
                            0 => Color.white,
                            1 => Color.black,
                            2 => Color.red,
                            3 => Color.green,
                            _ => Color.blue
                        };
                    }
                }
            }
        }

        private static void GrayRamp(Color[] p, int size)
        {
            // Top half: continuous horizontal ramp (banding check). Bottom half:
            // 16-step wedge (gamma / black-level check).
            int half = size / 2;
            for (int y = 0; y < size; y++)
            {
                bool top = y >= half;
                for (int x = 0; x < size; x++)
                {
                    float v = top
                        ? x / (float)(size - 1)
                        : Mathf.Floor(x * 16f / size) / 15f;
                    p[y * size + x] = new Color(v, v, v, 1f);
                }
            }
        }

        // ---------------- helpers ----------------

        private static void DrawCross(Color[] p, int size, int cx, int cy, int r, Color c)
        {
            for (int d = -r; d <= r; d++)
            {
                int x = cx + d;
                if (x >= 0 && x < size) { Set(p, size, x, cy, c); Set(p, size, x, cy + 1, c); }
                int y = cy + d;
                if (y >= 0 && y < size) { Set(p, size, cx, y, c); Set(p, size, cx + 1, y, c); }
            }
        }

        private static void DrawCircle(Color[] p, int size, int cx, int cy, int r, Color c)
        {
            int rInSq = (r - 2) * (r - 2), rSq = r * r;
            for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(size - 1, cy + r); y++)
                for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(size - 1, cx + r); x++)
                {
                    int dx = x - cx, dy = y - cy, d = dx * dx + dy * dy;
                    if (d <= rSq && d >= rInSq) Set(p, size, x, y, c);
                }
        }

        private static void ApplyOverlays(Color[] p, int size, Overlays o)
        {
            float centre = size / 2f;
            float radius = centre * 0.9f;
            float radiusSq = radius * radius, innerSq = (radius - 4) * (radius - 4);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (o.Circle)
                    {
                        float dx = x - centre, dy = y - centre;
                        float d = dx * dx + dy * dy;
                        if (d <= radiusSq && d >= innerSq) Set(p, size, x, y, o.CircleColor);
                    }
                    if (o.Crosshair && (x == size / 2 || x == size / 2 - 1 || y == size / 2 || y == size / 2 - 1))
                        Set(p, size, x, y, o.CrosshairColor);
                    if (o.Border && (x < 4 || x >= size - 4 || y < 4 || y >= size - 4))
                        Set(p, size, x, y, o.BorderColor);
                }
        }

        private static void Set(Color[] p, int size, int x, int y, Color c)
        {
            if (x >= 0 && x < size && y >= 0 && y < size) p[y * size + x] = c;
        }
    }
}
