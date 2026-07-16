using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Fits an N×M grid-warp directly from the camera correspondence, capturing warp
    /// that a 4-corner homography cannot (curved or irregular surfaces). Where the
    /// corner pin solves 4 points, this solves the whole lattice: for each grid node
    /// it finds the projector-raster coordinate that lands on the intended target
    /// point, read from the dense Gray-code correspondence.
    ///
    /// Output matches PMSDKGridWarp's control-point layout (row-major, normalized
    /// raster, y up), so it can be dropped straight into a PMSDKGridWarp. On a flat,
    /// already-aligned surface it reproduces the regular lattice (no-op); deviations
    /// appear only where the real surface bends.
    /// </summary>
    public static class PMSDKDenseWarp
    {
        /// <param name="corr">camera→projector correspondence (top-left origin).</param>
        /// <param name="target">Target rectangle in camera space, normalized, order TL,TR,BR,BL (top-left origin).</param>
        /// <param name="sampleRadius">Half-window (camera px) averaged around each node's target point.</param>
        /// <returns>cols*rows control points (row-major, y up), or null if too few samples.</returns>
        public static Vector2[] FitGrid(
            PMSDKGrayCodeDecode.Correspondence[] corr, int camW, int camH, int projW, int projH,
            Vector2[] target, int cols, int rows, int sampleRadius = 3)
        {
            if (corr == null || target == null || target.Length != 4 || cols < 2 || rows < 2) return null;

            var points = new Vector2[cols * rows];
            int found = 0;
            for (int row = 0; row < rows; row++)
            {
                float v = (float)row / (rows - 1); // grid v, up
                for (int col = 0; col < cols; col++)
                {
                    float u = (float)col / (cols - 1);
                    // Node's target point in camera space (top-left origin): s left→right,
                    // t top→bottom, so t = 1 - v.
                    Vector2 cam = Bilerp(target[0], target[1], target[2], target[3], u, 1f - v);
                    int cx = Mathf.Clamp(Mathf.RoundToInt(cam.x * camW), 0, camW - 1);
                    int cy = Mathf.Clamp(Mathf.RoundToInt(cam.y * camH), 0, camH - 1);

                    // Average the valid correspondence in a small window (robust to the
                    // occasional undecoded pixel).
                    double sx = 0, sy = 0; int cnt = 0;
                    for (int dy = -sampleRadius; dy <= sampleRadius; dy++)
                    {
                        int y = cy + dy; if (y < 0 || y >= camH) continue;
                        for (int dx = -sampleRadius; dx <= sampleRadius; dx++)
                        {
                            int x = cx + dx; if (x < 0 || x >= camW) continue;
                            var c = corr[y * camW + x];
                            if (!c.Valid) continue;
                            sx += c.ProjectorX; sy += c.ProjectorY; cnt++;
                        }
                    }

                    if (cnt > 0)
                    {
                        float ru = (float)(sx / cnt + 0.5) / projW;
                        // Raster v is y-up; correspondence ProjectorY is top-left origin,
                        // so flip to match the grid/warp convention.
                        float rv = 1f - (float)(sy / cnt + 0.5) / projH;
                        points[row * cols + col] = new Vector2(Mathf.Clamp01(ru), Mathf.Clamp01(rv));
                        found++;
                    }
                    else
                    {
                        // No decode near this node — fall back to the regular lattice.
                        points[row * cols + col] = new Vector2(u, v);
                    }
                }
            }

            return found >= 4 ? points : null;
        }

        private static Vector2 Bilerp(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl, float s, float t)
        {
            Vector2 top = Vector2.Lerp(tl, tr, s);
            Vector2 bottom = Vector2.Lerp(bl, br, s);
            return Vector2.Lerp(top, bottom, t);
        }
    }
}
