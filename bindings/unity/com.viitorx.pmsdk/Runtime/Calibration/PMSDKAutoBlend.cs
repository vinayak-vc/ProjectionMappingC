using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Computes edge-blend widths automatically from the camera-observed overlap
    /// between projectors (the VIOSO/Scalable-style feature). Reuses the Gray-code
    /// correspondence auto-align already produces: a camera pixel lit by two
    /// projectors is in their overlap; mapping that pixel back into each projector's
    /// raster tells how deep the overlap runs from each edge, which is exactly the
    /// blend ramp width.
    ///
    /// Pure function over the decoded correspondences (no Unity scene / native calls)
    /// so it is deterministic and testable. All correspondence sets must come from the
    /// SAME camera (same resolution and pose) — i.e. one camera that sees every
    /// projector, which is the real on-site setup.
    /// </summary>
    public static class PMSDKAutoBlend
    {
        public struct EdgeWidths
        {
            public float Left, Right, Top, Bottom;
        }

        /// <param name="correspondences">Per-projector decode over the same camWxcamH camera.</param>
        /// <param name="projW">Pattern/raster width used for the decode.</param>
        /// <param name="projH">Pattern/raster height used for the decode.</param>
        /// <param name="maxWidth">Clamp for any single edge (fraction of the raster).</param>
        /// <param name="coverage">Fraction of an edge's length that must be overlapped for it to count as a blend band (rejects corner blobs).</param>
        public static EdgeWidths[] Compute(
            IReadOnlyList<PMSDKGrayCodeDecode.Correspondence[]> correspondences,
            int camW, int camH, int projW, int projH, float maxWidth = 0.45f, float coverage = 0.5f)
        {
            int n = correspondences.Count;
            var result = new EdgeWidths[n];
            if (n == 0) return result;
            int px = camW * camH;

            // How many projectors light each camera pixel.
            var litCount = new byte[px];
            for (int p = 0; p < n; p++)
            {
                var cp = correspondences[p];
                for (int i = 0; i < px; i++)
                    if (cp[i].Valid) litCount[i]++;
            }

            for (int p = 0; p < n; p++)
            {
                var cp = correspondences[p];
                // Per raster column/row: how many pixels this projector lit, and how
                // many of those are overlap. A blend band on an edge only exists if
                // the overlap covers most of that edge's length — a coverage histogram
                // distinguishes a real edge band from a corner blob (which a simple
                // nearest-edge or max-penetration test misclassifies).
                var colLit = new int[projW];
                var colOverlap = new int[projW];
                var rowLit = new int[projH];
                var rowOverlap = new int[projH];

                for (int i = 0; i < px; i++)
                {
                    if (!cp[i].Valid) continue;
                    int x = Mathf.Clamp(cp[i].ProjectorX, 0, projW - 1);
                    int y = Mathf.Clamp(cp[i].ProjectorY, 0, projH - 1);
                    colLit[x]++; rowLit[y]++;
                    if (litCount[i] >= 2) { colOverlap[x]++; rowOverlap[y]++; }
                }

                // Right edge: scan inward from the rightmost column while the column's
                // overlap fraction stays above `coverage`. Left/Top/Bottom analogous.
                // Raster v (=Y/projH) increases upward, so high Y is the top edge.
                float right = ScanInward(colOverlap, colLit, projW, +1, coverage) / (float)projW;
                float left = ScanInward(colOverlap, colLit, projW, -1, coverage) / (float)projW;
                float top = ScanInward(rowOverlap, rowLit, projH, +1, coverage) / (float)projH;
                float bottom = ScanInward(rowOverlap, rowLit, projH, -1, coverage) / (float)projH;

                result[p] = new EdgeWidths
                {
                    Left = Mathf.Clamp(left, 0f, maxWidth),
                    Right = Mathf.Clamp(right, 0f, maxWidth),
                    Top = Mathf.Clamp(top, 0f, maxWidth),
                    Bottom = Mathf.Clamp(bottom, 0f, maxWidth)
                };
            }
            return result;
        }

        /// <summary>
        /// Find the blend band depth from one end (dir +1 = from the high-index end,
        /// -1 = from the low end). Walks inward tracking the deepest bin whose overlap
        /// fraction is at least `coverage`; unlit bins (lit == 0, e.g. raster columns
        /// no camera pixel happened to hit) are skipped, not treated as the band end,
        /// so sparse sampling doesn't truncate the band. Stops at the first LIT bin
        /// below coverage. Returns the band span in bins (includes skipped gaps).
        /// </summary>
        private static int ScanInward(int[] overlap, int[] lit, int count, int dir, float coverage)
        {
            int last = -1;
            if (dir > 0)
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    if (lit[i] == 0) continue;
                    if (overlap[i] / (float)lit[i] < coverage) break;
                    last = i;
                }
                return last < 0 ? 0 : (count - last);
            }
            for (int i = 0; i < count; i++)
            {
                if (lit[i] == 0) continue;
                if (overlap[i] / (float)lit[i] < coverage) break;
                last = i;
            }
            return last < 0 ? 0 : (last + 1);
        }
    }
}
