using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// 2D planar homography (3x3, h33 fixed to 1) used by camera-assisted auto-align.
    ///
    /// Maps source points to destination points:
    ///   [u']   [h0 h1 h2] [x]
    ///   [v'] = [h3 h4 h5] [y]
    ///   [w ]   [h6 h7 1 ] [1]
    ///   u = u'/w, v = v'/w
    ///
    /// Fit from >= 4 correspondences by least squares (normal equations). No external
    /// math dependency — deterministic and unit-testable.
    /// </summary>
    public class PMSDKHomography
    {
        // h[0..7]; h8 == 1 implicitly.
        public readonly double[] H = new double[8];
        public bool Valid { get; private set; }

        public static PMSDKHomography Fit(IReadOnlyList<Vector2> src, IReadOnlyList<Vector2> dst)
        {
            var h = new PMSDKHomography();
            int n = Mathf.Min(src.Count, dst.Count);
            if (n < 4)
            {
                h.Valid = false;
                return h;
            }

            // Normal equations for the 8 unknowns: A^T A x = A^T b, where each
            // correspondence contributes two rows to A (8 columns) and b.
            double[,] ata = new double[8, 8];
            double[] atb = new double[8];
            double[] row = new double[8];

            for (int i = 0; i < n; i++)
            {
                double x = src[i].x, y = src[i].y;
                double u = dst[i].x, v = dst[i].y;

                // Row for u: h0 x + h1 y + h2 - u h6 x - u h7 y = u
                row[0] = x; row[1] = y; row[2] = 1; row[3] = 0; row[4] = 0; row[5] = 0;
                row[6] = -u * x; row[7] = -u * y;
                Accumulate(ata, atb, row, u);

                // Row for v: h3 x + h4 y + h5 - v h6 x - v h7 y = v
                row[0] = 0; row[1] = 0; row[2] = 0; row[3] = x; row[4] = y; row[5] = 1;
                row[6] = -v * x; row[7] = -v * y;
                Accumulate(ata, atb, row, v);
            }

            h.Valid = SolveSymmetric8(ata, atb, h.H);
            return h;
        }

        public Vector2 Apply(Vector2 p)
        {
            double w = H[6] * p.x + H[7] * p.y + 1.0;
            if (Mathf.Abs((float)w) < 1e-12f) return p;
            double u = (H[0] * p.x + H[1] * p.y + H[2]) / w;
            double v = (H[3] * p.x + H[4] * p.y + H[5]) / w;
            return new Vector2((float)u, (float)v);
        }

        private static void Accumulate(double[,] ata, double[] atb, double[] row, double rhs)
        {
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    ata[r, c] += row[r] * row[c];
                }
                atb[r] += row[r] * rhs;
            }
        }

        /// <summary>Gaussian elimination with partial pivoting for an 8x8 system.</summary>
        private static bool SolveSymmetric8(double[,] a, double[] b, double[] x)
        {
            const int n = 8;
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                double best = System.Math.Abs(a[col, col]);
                for (int r = col + 1; r < n; r++)
                {
                    double v = System.Math.Abs(a[r, col]);
                    if (v > best) { best = v; pivot = r; }
                }
                if (best < 1e-15) return false;

                if (pivot != col)
                {
                    for (int c = 0; c < n; c++) { (a[col, c], a[pivot, c]) = (a[pivot, c], a[col, c]); }
                    (b[col], b[pivot]) = (b[pivot], b[col]);
                }

                double diag = a[col, col];
                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double factor = a[r, col] / diag;
                    if (factor == 0) continue;
                    for (int c = col; c < n; c++) a[r, c] -= factor * a[col, c];
                    b[r] -= factor * b[col];
                }
            }
            for (int i = 0; i < n; i++) x[i] = b[i] / a[i, i];
            return true;
        }
    }
}
