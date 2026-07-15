using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Managed Gray-code encode/decode, bit-compatible with the native SDK generator
    /// (src/Calibration/GrayCode.cpp): colBits = ceil(log2(width)) column patterns
    /// (MSB first) then rowBits row patterns; pixel value = Gray-code bit (255/0);
    /// Gray = (v >> 1) ^ v.
    ///
    /// Decoding recovers, for each observing-camera pixel, the projector-raster pixel
    /// that illuminated it — the correspondence that camera-assisted auto-align turns
    /// into a projector->camera homography. Kept in managed code so the roundtrip is
    /// deterministic and testable without hardware.
    /// </summary>
    public static class PMSDKGrayCodeDecode
    {
        public static int BitsFor(int size)
        {
            int bits = 0, p = 1;
            while (p < size) { p *= 2; bits++; }
            return bits;
        }

        public static int PatternCount(int width, int height)
        {
            return BitsFor(width) + BitsFor(height);
        }

        /// <summary>Value of one Gray-code pattern at coordinate coord (true = lit).</summary>
        public static bool PatternBit(int coord, int bitIndex)
        {
            int gray = (coord >> 1) ^ coord;
            return (gray & (1 << bitIndex)) != 0;
        }

        /// <summary>
        /// Generate pattern <paramref name="index"/> into an R8-style byte buffer,
        /// matching the native generator exactly. Useful for tests and for a managed
        /// fallback when the native generator is unavailable.
        /// </summary>
        public static void GeneratePattern(int index, int width, int height, byte[] outPixels)
        {
            int colBits = BitsFor(width);
            int rowBits = BitsFor(height);
            bool isColumn = index < colBits;
            int bitIndex = isColumn ? colBits - 1 - index : rowBits - 1 - (index - colBits);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int val = isColumn ? x : y;
                    outPixels[y * width + x] = PatternBit(val, bitIndex) ? (byte)255 : (byte)0;
                }
            }
        }

        /// <summary>Gray code -> binary.</summary>
        public static int GrayToBinary(int gray, int bits)
        {
            int b = gray;
            for (int shift = 1; shift < bits; shift <<= 1)
            {
                b ^= b >> shift;
            }
            return b;
        }

        public struct Correspondence
        {
            public bool Valid;
            public int ProjectorX;
            public int ProjectorY;
        }

        /// <summary>
        /// Decode a full capture set into per-camera-pixel projector coordinates.
        ///
        /// <paramref name="captures"/>: luminance (0..255) of each pattern frame,
        /// ordered exactly as generated (columns MSB-first, then rows), length =
        /// PatternCount, each of size camW*camH.
        /// <paramref name="white"/>/<paramref name="black"/>: reference frames (all-on /
        /// all-off) for per-pixel thresholding.
        /// <paramref name="minContrast"/>: reject pixels where white-black is too small
        /// (not actually lit by this projector) — keeps stray/ambient pixels out.
        /// </summary>
        public static Correspondence[] Decode(
            byte[][] captures, byte[] white, byte[] black,
            int camW, int camH, int projW, int projH, int minContrast = 30)
        {
            int colBits = BitsFor(projW);
            int rowBits = BitsFor(projH);
            int pixels = camW * camH;
            var result = new Correspondence[pixels];

            for (int i = 0; i < pixels; i++)
            {
                int w = white[i];
                int b = black[i];
                if (w - b < minContrast)
                {
                    result[i].Valid = false;
                    continue;
                }
                int mid = (w + b) / 2;

                int grayCol = 0;
                for (int bit = 0; bit < colBits; bit++)
                {
                    grayCol <<= 1;
                    if (captures[bit][i] > mid) grayCol |= 1;
                }
                int grayRow = 0;
                for (int bit = 0; bit < rowBits; bit++)
                {
                    grayRow <<= 1;
                    if (captures[colBits + bit][i] > mid) grayRow |= 1;
                }

                int px = GrayToBinary(grayCol, colBits);
                int py = GrayToBinary(grayRow, rowBits);

                result[i].Valid = px < projW && py < projH;
                result[i].ProjectorX = px;
                result[i].ProjectorY = py;
            }
            return result;
        }
    }
}
