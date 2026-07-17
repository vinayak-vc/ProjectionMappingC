using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>Homography + Gray-code decode regression tests (pure math, EditMode).</summary>
    public class PMSDKMathTests
    {
        private static Vector2 ApplyGroundTruth(Vector2 p, double[] h)
        {
            double w = h[6] * p.x + h[7] * p.y + 1.0;
            return new Vector2(
                (float)((h[0] * p.x + h[1] * p.y + h[2]) / w),
                (float)((h[3] * p.x + h[4] * p.y + h[5]) / w));
        }

        [Test]
        public void Homography_RecoversKnownPerspectiveMap()
        {
            double[] gt = { 1.2, 0.1, 5.0, -0.05, 0.9, 3.0, 0.0007, -0.0003 };
            var src = new List<Vector2>();
            var dst = new List<Vector2>();
            foreach (var p in new[] { new Vector2(0, 0), new Vector2(100, 0), new Vector2(100, 80), new Vector2(0, 80), new Vector2(50, 40), new Vector2(20, 70) })
            {
                src.Add(p);
                dst.Add(ApplyGroundTruth(p, gt));
            }

            var h = PMSDKHomography.Fit(src, dst);
            Assert.IsTrue(h.Valid);

            foreach (var p in new[] { new Vector2(33, 17), new Vector2(90, 5), new Vector2(10, 75) })
            {
                float err = Vector2.Distance(h.Apply(p), ApplyGroundTruth(p, gt));
                Assert.Less(err, 1e-3f, $"reprojection error at {p}");
            }
        }

        [Test]
        public void Homography_RejectsTooFewPoints()
        {
            var pts = new List<Vector2> { Vector2.zero, Vector2.one, Vector2.up };
            Assert.IsFalse(PMSDKHomography.Fit(pts, pts).Valid);
        }

        [Test]
        public void GrayCode_PatternMatchesNativeConvention()
        {
            // 2 + 2*(colBits+rowBits) is the robust count; managed generator covers the
            // plain sequence: colBits+rowBits patterns, MSB first, G=(B>>1)^B.
            Assert.AreEqual(14, PMSDKGrayCodeDecode.PatternCount(128, 128));
            Assert.AreEqual(7, PMSDKGrayCodeDecode.BitsFor(128));
            Assert.AreEqual(7, PMSDKGrayCodeDecode.BitsFor(100)); // ceil(log2)
        }

        [Test]
        public void GrayCode_DecodeRoundtripIsExact()
        {
            const int size = 64;
            int patterns = PMSDKGrayCodeDecode.PatternCount(size, size);
            int px = size * size;
            var caps = new byte[patterns][];
            for (int k = 0; k < patterns; k++)
            {
                caps[k] = new byte[px];
                PMSDKGrayCodeDecode.GeneratePattern(k, size, size, caps[k]);
            }
            var white = new byte[px];
            var black = new byte[px];
            for (int i = 0; i < px; i++) white[i] = 255;

            var corr = PMSDKGrayCodeDecode.Decode(caps, white, black, size, size, size, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var c = corr[y * size + x];
                    Assert.IsTrue(c.Valid);
                    Assert.AreEqual(x, c.ProjectorX);
                    Assert.AreEqual(y, c.ProjectorY);
                }
            }
        }

        [Test]
        public void GrayCode_InverseModeSurvivesAlbedoAndAmbient()
        {
            const int size = 32;
            int patterns = PMSDKGrayCodeDecode.PatternCount(size, size);
            int px = size * size;

            // Simulated surface: per-pixel albedo gradient + ambient — breaks a global
            // threshold, must not break pattern-vs-inverse comparison.
            System.Func<byte, int, int, byte> surface = (v, x, y) =>
            {
                float albedo = 0.25f + 0.75f * (x / (float)(size - 1));
                float ambient = 20f + 10f * (y / (float)(size - 1));
                return (byte)(ambient + albedo * (v / 255f) * (200f - ambient));
            };

            var caps = new byte[patterns][];
            var invs = new byte[patterns][];
            var raw = new byte[px];
            for (int k = 0; k < patterns; k++)
            {
                PMSDKGrayCodeDecode.GeneratePattern(k, size, size, raw);
                caps[k] = new byte[px];
                invs[k] = new byte[px];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int i = y * size + x;
                        caps[k][i] = surface(raw[i], x, y);
                        invs[k][i] = surface((byte)(255 - raw[i]), x, y);
                    }
                }
            }
            var white = new byte[px];
            var black = new byte[px];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    white[y * size + x] = surface(255, x, y);
                    black[y * size + x] = surface(0, x, y);
                }

            var corr = PMSDKGrayCodeDecode.Decode(caps, invs, white, black, size, size, size, size, 20);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var c = corr[y * size + x];
                    Assert.IsTrue(c.Valid, $"pixel {x},{y} rejected");
                    Assert.AreEqual(x, c.ProjectorX, $"X at {x},{y}");
                    Assert.AreEqual(y, c.ProjectorY, $"Y at {x},{y}");
                }
        }

        [Test]
        public void GrayCode_ShadowMaskRejectsUnlitPixels()
        {
            const int size = 32;
            int patterns = PMSDKGrayCodeDecode.PatternCount(size, size);
            int px = size * size;
            var caps = new byte[patterns][];
            var raw = new byte[px];
            for (int k = 0; k < patterns; k++)
            {
                PMSDKGrayCodeDecode.GeneratePattern(k, size, size, raw);
                caps[k] = new byte[px];
                for (int i = 0; i < px; i++)
                {
                    int x = i % size;
                    caps[k][i] = x < size / 2 ? (byte)15 : raw[i]; // left half unlit
                }
            }
            var white = new byte[px];
            var black = new byte[px];
            for (int i = 0; i < px; i++)
            {
                int x = i % size;
                white[i] = x < size / 2 ? (byte)15 : (byte)255;
                black[i] = x < size / 2 ? (byte)15 : (byte)0;
            }

            var corr = PMSDKGrayCodeDecode.Decode(caps, white, black, size, size, size, size, 30);
            int valid = 0;
            for (int i = 0; i < px; i++)
            {
                if (!corr[i].Valid) continue;
                valid++;
                Assert.GreaterOrEqual(i % size, size / 2, "unlit pixel decoded");
            }
            Assert.AreEqual(px / 2, valid);
        }
    }
}
