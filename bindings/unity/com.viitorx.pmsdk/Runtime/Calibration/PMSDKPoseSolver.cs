using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Solves a projector camera's pose (+ field of view) so that a set of known 3D
    /// points on the virtual twin reproject to their marked positions in the projector
    /// output — i.e. so the rendered twin overlays the real physical object. This is
    /// projector resectioning / PnP, done directly against Unity's camera projection
    /// (Levenberg–Marquardt over [posXYZ, eulerXYZ, fov] with a numeric Jacobian), so
    /// there is no OpenCV↔Unity coordinate conversion and no native dependency.
    ///
    /// Needs a rough initial camera pose (operator points the projector cam near the
    /// object first); LM refines from there. 6+ non-coplanar correspondences recommended.
    /// </summary>
    public static class PMSDKPoseSolver
    {
        public struct Correspondence
        {
            public Vector3 World;      // 3D point on the virtual twin
            public Vector2 Viewport;   // where it should land in the output (0..1, bottom-left)
        }

        /// <summary>
        /// Refine cam.transform + cam.fieldOfView to minimize reprojection error.
        /// Returns final RMS error in viewport units (multiply by output pixels for px).
        /// </summary>
        public static float Solve(Camera cam, IReadOnlyList<Correspondence> corr,
                                  int maxIterations = 80, bool solveFov = true)
        {
            int n = corr.Count;
            if (cam == null || n < 4) return float.PositiveInfinity;

            int np = solveFov ? 7 : 6;
            double[] p = ReadParams(cam, np);
            double lambda = 1e-3;
            double prevErr = Residuals(cam, corr, p, null);

            var J = new double[2 * n, 7];
            var r = new double[2 * n];

            for (int iter = 0; iter < maxIterations; iter++)
            {
                Residuals(cam, corr, p, r);

                // Numeric Jacobian (central differences).
                for (int k = 0; k < np; k++)
                {
                    double h = JacobianStep(k);
                    double[] pp = (double[])p.Clone();
                    double[] pm = (double[])p.Clone();
                    pp[k] += h; pm[k] -= h;
                    var rp = new double[2 * n];
                    var rm = new double[2 * n];
                    Residuals(cam, corr, pp, rp);
                    Residuals(cam, corr, pm, rm);
                    for (int i = 0; i < 2 * n; i++) J[i, k] = (rp[i] - rm[i]) / (2 * h);
                }

                // JtJ (np x np) and Jtr (np)
                var jtj = new double[np, np];
                var jtr = new double[np];
                for (int a = 0; a < np; a++)
                {
                    for (int b = 0; b < np; b++)
                    {
                        double s = 0;
                        for (int i = 0; i < 2 * n; i++) s += J[i, a] * J[i, b];
                        jtj[a, b] = s;
                    }
                    double sr = 0;
                    for (int i = 0; i < 2 * n; i++) sr += J[i, a] * r[i];
                    jtr[a] = sr;
                }

                // LM: try damping until the step reduces error.
                bool improved = false;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    var A = (double[,])jtj.Clone();
                    for (int d = 0; d < np; d++) A[d, d] += lambda * (A[d, d] + 1e-9);
                    var delta = SolveLinear(A, jtr, np);
                    if (delta == null) { lambda *= 10; continue; }

                    double[] cand = (double[])p.Clone();
                    for (int k = 0; k < np; k++) cand[k] -= delta[k];
                    double err = Residuals(cam, corr, cand, null);

                    if (err < prevErr)
                    {
                        p = cand; prevErr = err; lambda = System.Math.Max(lambda * 0.5, 1e-9);
                        improved = true;
                        break;
                    }
                    lambda *= 10;
                }
                if (!improved || prevErr < 1e-10) break;
            }

            ApplyParams(cam, p, np);
            return Mathf.Sqrt((float)(prevErr / n));
        }

        private static double JacobianStep(int k)
        {
            // Position/rotation vs fov scale differently.
            if (k < 3) return 1e-3;   // metres
            if (k < 6) return 1e-2;   // degrees
            return 1e-2;              // fov degrees
        }

        private static double[] ReadParams(Camera cam, int np)
        {
            var t = cam.transform;
            Vector3 pos = t.position; Vector3 e = t.eulerAngles;
            var p = new double[7];
            p[0] = pos.x; p[1] = pos.y; p[2] = pos.z;
            p[3] = e.x; p[4] = e.y; p[5] = e.z;
            p[6] = cam.fieldOfView;
            return p;
        }

        private static void ApplyParams(Camera cam, double[] p, int np)
        {
            cam.transform.position = new Vector3((float)p[0], (float)p[1], (float)p[2]);
            cam.transform.eulerAngles = new Vector3((float)p[3], (float)p[4], (float)p[5]);
            if (np >= 7) cam.fieldOfView = Mathf.Clamp((float)p[6], 5f, 120f);
        }

        // Sets the camera to params p, computes per-correspondence residuals into
        // outR (may be null), returns sum of squared error. Points behind the camera
        // are penalised so the optimiser stays in front.
        private static double Residuals(Camera cam, IReadOnlyList<Correspondence> corr,
                                        double[] p, double[] outR)
        {
            ApplyParams(cam, p, p.Length >= 7 ? 7 : 6);
            double sum = 0;
            for (int i = 0; i < corr.Count; i++)
            {
                Vector3 vp = cam.WorldToViewportPoint(corr[i].World);
                double dx, dy;
                if (vp.z <= 0.001f)
                {
                    // Behind camera: large penalty, pushed proportional to how far behind.
                    dx = 10.0 + (-vp.z);
                    dy = 10.0 + (-vp.z);
                }
                else
                {
                    dx = vp.x - corr[i].Viewport.x;
                    dy = vp.y - corr[i].Viewport.y;
                }
                if (outR != null) { outR[2 * i] = dx; outR[2 * i + 1] = dy; }
                sum += dx * dx + dy * dy;
            }
            return sum;
        }

        // Gaussian elimination with partial pivoting; returns null if singular.
        private static double[] SolveLinear(double[,] a, double[] b, int n)
        {
            var m = (double[,])a.Clone();
            var x = (double[])b.Clone();
            for (int col = 0; col < n; col++)
            {
                int piv = col; double best = System.Math.Abs(m[col, col]);
                for (int rr = col + 1; rr < n; rr++)
                {
                    double v = System.Math.Abs(m[rr, col]);
                    if (v > best) { best = v; piv = rr; }
                }
                if (best < 1e-15) return null;
                if (piv != col)
                {
                    for (int c = 0; c < n; c++) { (m[col, c], m[piv, c]) = (m[piv, c], m[col, c]); }
                    (x[col], x[piv]) = (x[piv], x[col]);
                }
                double diag = m[col, col];
                for (int rr = 0; rr < n; rr++)
                {
                    if (rr == col) continue;
                    double f = m[rr, col] / diag;
                    if (f == 0) continue;
                    for (int c = col; c < n; c++) m[rr, c] -= f * m[col, c];
                    x[rr] -= f * x[col];
                }
            }
            for (int i = 0; i < n; i++) x[i] /= m[i, i];
            return x;
        }
    }
}
