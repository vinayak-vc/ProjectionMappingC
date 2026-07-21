using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>Deterministic pixel checks for the professional test-pattern set.</summary>
    public class PMSDKTestPatternTests
    {
        private const int Size = 256;

        private static Color[] Gen(PMSDKTestPatternType type)
        {
            var px = new Color[Size * Size];
            PMSDKTestPatterns.Generate(type, px, Size, 8, Color.white, Color.black,
                new PMSDKTestPatterns.Overlays()); // overlays off for determinism
            return px;
        }

        private static Color At(Color[] p, int x, int y) => p[y * Size + x];

        [Test]
        public void Solids_AreUniform()
        {
            foreach (var (type, expected) in new[]
            {
                (PMSDKTestPatternType.White, Color.white),
                (PMSDKTestPatternType.Black, Color.black),
                (PMSDKTestPatternType.Red, Color.red),
                (PMSDKTestPatternType.Green, Color.green),
                (PMSDKTestPatternType.Blue, Color.blue)
            })
            {
                var p = Gen(type);
                Assert.AreEqual(expected, At(p, 0, 0), type.ToString());
                Assert.AreEqual(expected, At(p, Size / 2, Size / 2), type.ToString());
                Assert.AreEqual(expected, At(p, Size - 1, Size - 1), type.ToString());
            }
        }

        [Test]
        public void Checkerboard_AlternatesCells()
        {
            var p = Gen(PMSDKTestPatternType.Checkerboard);
            int cell = Size / 8;
            Assert.AreEqual(Color.white, At(p, cell / 2, cell / 2));            // cell (0,0)
            Assert.AreEqual(Color.black, At(p, cell + cell / 2, cell / 2));     // cell (1,0)
            Assert.AreEqual(Color.black, At(p, cell / 2, cell + cell / 2));     // cell (0,1)
            Assert.AreEqual(Color.white, At(p, cell + cell / 2, cell + cell / 2)); // cell (1,1)
        }

        [Test]
        public void FocusGrid_HasFineLinesOnBlackField()
        {
            var p = Gen(PMSDKTestPatternType.FocusGrid);
            int step = Mathf.Max(8, Size / 32);
            Assert.AreEqual(Color.white, At(p, step, 3));       // vertical line
            Assert.AreEqual(Color.white, At(p, 3, step));       // horizontal line
            Assert.AreEqual(Color.white, At(p, 77, 77));        // diagonal (x==y)
            Assert.AreEqual(Color.black, At(p, step + 3, step + 2)); // field between lines
        }

        [Test]
        public void Convergence_HasCentreTargetOnGrayField()
        {
            var p = Gen(PMSDKTestPatternType.Convergence);
            int c = (int)(0.5f * (Size - 1));
            Assert.AreEqual(Color.white, At(p, c, c)); // centre crosshair
            // circle ring at radius size/16 around the centre target
            int r = Size / 16;
            Assert.AreEqual(Color.yellow, At(p, c + r, c));
            // gray field away from all 9 targets
            var field = At(p, (int)(0.3f * Size), (int)(0.3f * Size));
            Assert.AreEqual(0.25f, field.r, 1e-4f);
        }

        [Test]
        public void ColorBars_TopBarsAndBottomStrip()
        {
            var p = Gen(PMSDKTestPatternType.ColorBars);
            int topY = Size - 8;    // visual top (texture rows are bottom-up)
            int botY = 4;           // reference strip
            // first bar = 75% gray, last bar = 75% blue
            Assert.AreEqual(0.75f, At(p, 4, topY).r, 1e-4f);
            var lastBar = At(p, Size - 4, topY);
            Assert.AreEqual(0f, lastBar.r, 1e-4f);
            Assert.AreEqual(0.75f, lastBar.b, 1e-4f);
            // bottom strip: white then black segments
            Assert.AreEqual(Color.white, At(p, 4, botY));
            Assert.AreEqual(Color.black, At(p, Size / 5 + 4, botY));
        }

        [Test]
        public void CanvasReference_PlusSpansFullWidthAndHeight()
        {
            int w = 512, h = 128;
            var px = new Color[w * h];
            var line = Color.white;
            var plus = new Color(1f, 0.9f, 0.1f, 1f);
            var bg = Color.black;
            PMSDKCanvasReferencePattern.Fill(px, w, h, plus: true, hLines: 1, vLines: 3,
                thickness: 3, line: line, plusColor: plus, bg: bg);

            // Centre horizontal line spans the ENTIRE width (end-to-end level reference).
            int cy = h / 2;
            for (int x = 0; x < w; x += 16)
                Assert.AreEqual(plus, px[cy * w + x], $"plus row broken at x={x}");
            // Centre vertical line spans the entire height.
            int cx = w / 2;
            for (int y = 0; y < h; y += 16)
                Assert.AreEqual(plus, px[y * w + cx], $"plus column broken at y={y}");
            // Reference h-line at 1/2 (hLines=1) coincides with centre; v-line at w/4.
            int vx = w * 1 / 4;
            Assert.AreEqual(line, px[8 * w + vx]);
            // Background where nothing is drawn.
            Assert.AreEqual(bg, px[8 * w + 8]);
        }

        [Test]
        public void GrayRamp_MonotonicTop_SteppedBottom()
        {
            var p = Gen(PMSDKTestPatternType.GrayRamp);
            int topY = Size - 8;
            float prev = -1f;
            for (int x = 0; x < Size; x += 16)
            {
                float v = At(p, x, topY).r;
                Assert.GreaterOrEqual(v, prev, $"ramp not monotonic at x={x}");
                prev = v;
            }
            // stepped wedge: constant within a step, 16 steps from 0 to 1
            int botY = 4;
            int stepW = Size / 16;
            Assert.AreEqual(At(p, 2, botY).r, At(p, stepW - 2, botY).r, 1e-4f);
            Assert.AreEqual(0f, At(p, 2, botY).r, 1e-4f);
            Assert.AreEqual(1f, At(p, Size - 2, botY).r, 1e-4f);
        }
    }
}
