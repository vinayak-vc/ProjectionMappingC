using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Diagnostic calibration grid (D-051): equal cells over the whole content, white lines (every 4th line and the
    /// border thicker), a dot at every intersection, alternating cell shades and a "column.row" label in each cell
    /// (0-based, column from the left, row from the bottom = content v). Shown on a skin it makes any stretch,
    /// bend or jump visible and nameable ("cell 7.3 is wider than 2.3"). Pure rasterizer, deterministic.
    /// </summary>
    public static class PMSDKCalibrationGrid
    {
        // 3 x 5 bitmap digits (rows top to bottom, 3 bits each, MSB = left) and '.' at index 10.
        private static readonly int[][] Glyphs = {
            new[] { 7, 5, 5, 5, 7 }, new[] { 2, 6, 2, 2, 7 }, new[] { 7, 1, 7, 4, 7 }, new[] { 7, 1, 7, 1, 7 }, new[] { 5, 5, 7, 1, 1 },
            new[] { 7, 4, 7, 1, 7 }, new[] { 7, 4, 7, 5, 7 }, new[] { 7, 1, 1, 1, 1 }, new[] { 7, 5, 7, 5, 7 }, new[] { 7, 5, 7, 1, 7 },
            new[] { 0, 0, 0, 0, 2 },
        };

        private static readonly Color32 Line = new Color32(255, 255, 255, 255);
        private static readonly Color32 CellA = new Color32(40, 40, 40, 255);
        private static readonly Color32 CellB = new Color32(70, 70, 70, 255);
        private static readonly Color32 LabelColour = new Color32(255, 214, 64, 255);

        /// <summary>Pixels (Unity order, row 0 = bottom = v 0) of a <paramref name="cellsU"/> x <paramref name="cellsV"/> grid.</summary>
        public static Color32[] Render(int cellsU, int cellsV, int pixelsPerCell, out int width, out int height)
        {
            cellsU = Mathf.Clamp(cellsU, 1, 256);
            cellsV = Mathf.Clamp(cellsV, 1, 256);
            pixelsPerCell = Mathf.Clamp(pixelsPerCell, 16, 256);
            width = cellsU * pixelsPerCell;
            height = cellsV * pixelsPerCell;
            Color32[] pixels = new Color32[width * height];
            int thin = Mathf.Max(1, pixelsPerCell / 24);
            int thick = thin * 2 + 1;
            for (int y = 0; y < height; y++) {
                int row = y / pixelsPerCell;
                for (int x = 0; x < width; x++) {
                    int column = x / pixelsPerCell;
                    pixels[y * width + x] = ((row + column) & 1) == 0 ? CellA : CellB;
                }
            }
            for (int c = 0; c <= cellsU; c++) {
                int centre = Mathf.Min(c * pixelsPerCell, width - 1);
                int half = (c % 4 == 0 || c == cellsU ? thick : thin) / 2;
                FillRect(pixels, width, height, centre - half, 0, centre + half, height - 1, Line);
            }
            for (int r = 0; r <= cellsV; r++) {
                int centre = Mathf.Min(r * pixelsPerCell, height - 1);
                int half = (r % 4 == 0 || r == cellsV ? thick : thin) / 2;
                FillRect(pixels, width, height, 0, centre - half, width - 1, centre + half, Line);
            }
            int dot = Mathf.Max(2, pixelsPerCell / 12);
            for (int r = 0; r <= cellsV; r++) {
                for (int c = 0; c <= cellsU; c++) {
                    int cx = Mathf.Min(c * pixelsPerCell, width - 1);
                    int cy = Mathf.Min(r * pixelsPerCell, height - 1);
                    FillRect(pixels, width, height, cx - dot, cy - dot, cx + dot, cy + dot, Line);
                }
            }
            int scale = Mathf.Max(1, pixelsPerCell / 32);
            for (int r = 0; r < cellsV; r++) {
                for (int c = 0; c < cellsU; c++) {
                    string label = c + "." + r;
                    int left = c * pixelsPerCell + thick + 2 * scale;
                    int top = (r + 1) * pixelsPerCell - thick - 2 * scale;
                    DrawText(pixels, width, height, label, left, top, scale);
                }
            }
            return pixels;
        }

        /// <summary>Grid texture (bilinear, clamped). The caller owns and destroys it.</summary>
        public static Texture2D Create(int cellsU, int cellsV, int pixelsPerCell)
        {
            int width;
            int height;
            Color32[] pixels = Render(cellsU, cellsV, pixelsPerCell, out width, out height);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true);
            texture.name = "PMSDK_CalibrationGrid";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }

        private static void DrawText(Color32[] pixels, int width, int height, string text, int left, int top, int scale)
        {
            int x = left;
            foreach (char ch in text) {
                int glyph = ch == '.' ? 10 : ch - '0';
                if (glyph < 0 || glyph > 10) {
                    continue;
                }
                int[] rows = Glyphs[glyph];
                for (int gy = 0; gy < 5; gy++) {
                    for (int gx = 0; gx < 3; gx++) {
                        if ((rows[gy] & (4 >> gx)) == 0) {
                            continue;
                        }
                        // Glyph rows run top-down; image rows bottom-up.
                        int px = x + gx * scale;
                        int py = top - (gy + 1) * scale + 1;
                        FillRect(pixels, width, height, px, py, px + scale - 1, py + scale - 1, LabelColour);
                    }
                }
                x += 4 * scale;
            }
        }

        private static void FillRect(Color32[] pixels, int width, int height, int x0, int y0, int x1, int y1, Color32 colour)
        {
            x0 = Mathf.Max(0, x0);
            y0 = Mathf.Max(0, y0);
            x1 = Mathf.Min(width - 1, x1);
            y1 = Mathf.Min(height - 1, y1);
            for (int y = y0; y <= y1; y++) {
                for (int x = x0; x <= x1; x++) {
                    pixels[y * width + x] = colour;
                }
            }
        }
    }
}
