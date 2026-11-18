using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>Pass limits for <see cref="PMSDKSurfaceMetrics"/> (physical units).</summary>
    public struct PMSDKSurfaceTolerance
    {
        /// <summary>RMS distance of the content from a uniform grid on the unfolded surface (mm).</summary>
        public float RmsMm;
        /// <summary>95th percentile of that distance (mm).</summary>
        public float P95Mm;
        /// <summary>Largest allowed difference of a facet's grid spacing from the overall spacing (percent).</summary>
        public float SpacingPercent;
        /// <summary>Largest allowed deviation of grid lines from perpendicular (degrees).</summary>
        public float ShearDeg;
        /// <summary>Largest allowed jump of the grid across a fold (mm).</summary>
        public float TransitionMm;

        public static PMSDKSurfaceTolerance Default()
        {
            return new PMSDKSurfaceTolerance { RmsMm = 3f, P95Mm = 6f, SpacingPercent = 2f, ShearDeg = 1f, TransitionMm = 5f };
        }
    }

    /// <summary>Measured grid quality on one facet.</summary>
    public sealed class PMSDKSurfaceFaceReport
    {
        public int Plane;
        public int Samples;
        /// <summary>Physical length of one content unit along u / v on this facet (mm).</summary>
        public float MmPerU;
        public float MmPerV;
        /// <summary>Deviation of the u and v grid directions from perpendicular (degrees).</summary>
        public float ShearDeg;
        /// <summary>This facet's spacing relative to the whole surface's (percent; + = stretched).</summary>
        public float HorizontalSpacingErrorPercent;
        public float VerticalSpacingErrorPercent;
        /// <summary>Grid cell width / height on this facet (mm).</summary>
        public float CellWidthMm;
        public float CellHeightMm;
        /// <summary>Largest deviation of a single cell's width or height from the overall cell size (mm), over cells
        /// with enough samples; NaN when no cell could be measured.</summary>
        public float MaxCellDimensionErrorMm = float.NaN;
        /// <summary>Distance of the grid from straight lines within the facet: RMS and 95th percentile (mm).</summary>
        public float StraightnessRmsMm;
        public float StraightnessP95Mm;
    }

    /// <summary>Grid continuity across the fold between two neighbouring facets.</summary>
    public sealed class PMSDKSurfaceTransitionReport
    {
        public int PlaneA;
        public int PlaneB;
        public int SamplesA;
        public int SamplesB;
        /// <summary>Offset of the grid on one side of the fold relative to the other (mm).</summary>
        public float JumpMm;
        /// <summary>Spacing change across the fold (percent, larger of the two directions).</summary>
        public float SpacingChangePercent;
    }

    /// <summary>Calibration report: how far the projected content is from a uniform grid on the physical surface.</summary>
    public sealed class PMSDKSurfaceReport
    {
        public bool Valid;
        public string Message = "";
        public int Samples;
        public int Outliers;
        public int CellsU;
        public int CellsV;
        /// <summary>Overall physical length of one content unit along u / v (mm) and grid shear (degrees).</summary>
        public float MmPerU;
        public float MmPerV;
        public float ShearDeg;
        public float CellWidthMm;
        public float CellHeightMm;
        public float RmsMm;
        public float P95Mm;
        public float MaxMm;
        public PMSDKSurfaceTolerance Tolerance;
        public bool Pass;
        public readonly List<string> Failures = new List<string>();
        public readonly List<PMSDKSurfaceFaceReport> Faces = new List<PMSDKSurfaceFaceReport>();
        public readonly List<PMSDKSurfaceTransitionReport> Transitions = new List<PMSDKSurfaceTransitionReport>();
        /// <summary>The uniform grid the content is compared with: surface mm = U * u + V * v + Offset.</summary>
        public Vector2 FitU;
        public Vector2 FitV;
        public Vector2 FitOffset;

        /// <summary>Where content UV <paramref name="uv"/> belongs on the unfolded surface (mm).</summary>
        public Vector2 ExpectedSurface(Vector2 uv)
        {
            return FitU * uv.x + FitV * uv.y + FitOffset;
        }

        /// <summary>One-line summary for a status label.</summary>
        public string Summary()
        {
            if (!Valid) {
                return "Surface measurement failed: " + Message;
            }
            float worstSpacing = 0f;
            foreach (PMSDKSurfaceFaceReport face in Faces) {
                worstSpacing = Mathf.Max(worstSpacing, Mathf.Max(Mathf.Abs(face.HorizontalSpacingErrorPercent), Mathf.Abs(face.VerticalSpacingErrorPercent)));
            }
            return (Pass ? "PASS" : "FAIL") + ": RMS " + F(RmsMm) + " mm, P95 " + F(P95Mm) + " mm, worst facet spacing " + F(worstSpacing) +
                   " %, cell " + F(CellWidthMm) + " x " + F(CellHeightMm) + " mm on " + Faces.Count + " facet(s).";
        }

        /// <summary>Full text report.</summary>
        public string ToText()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("Projection Mapping Calibration Report (physical surface coordinates, mm)");
            if (!Valid) {
                text.AppendLine("Measurement failed: " + Message);
                return text.ToString();
            }
            text.AppendLine("Samples: " + Samples + " (" + Outliers + " rejected as decode outliers)");
            text.AppendLine("Grid: " + CellsU + " x " + CellsV + " cells; overall cell " + F(CellWidthMm) + " x " + F(CellHeightMm) +
                            " mm; shear " + F(ShearDeg) + " deg");
            text.AppendLine();
            foreach (PMSDKSurfaceFaceReport face in Faces) {
                text.AppendLine("Surface: facet " + face.Plane + " (" + face.Samples + " samples)");
                text.AppendLine("  Horizontal Spacing Error: " + F(face.HorizontalSpacingErrorPercent) + " % (cell width " + F(face.CellWidthMm) + " mm)");
                text.AppendLine("  Vertical Spacing Error: " + F(face.VerticalSpacingErrorPercent) + " % (cell height " + F(face.CellHeightMm) + " mm)");
                text.AppendLine("  Maximum Cell Dimension Error: " + (float.IsNaN(face.MaxCellDimensionErrorMm) ? "n/a" : F(face.MaxCellDimensionErrorMm) + " mm"));
                text.AppendLine("  Grid Shear: " + F(face.ShearDeg) + " deg");
                text.AppendLine("  Grid Straightness Error: RMS " + F(face.StraightnessRmsMm) + " mm, P95 " + F(face.StraightnessP95Mm) + " mm");
                text.AppendLine();
            }
            foreach (PMSDKSurfaceTransitionReport t in Transitions) {
                text.AppendLine("Facet Transition " + t.PlaneA + " / " + t.PlaneB + ": jump " + F(t.JumpMm) + " mm, spacing change " +
                                F(t.SpacingChangePercent) + " % (" + t.SamplesA + " / " + t.SamplesB + " samples near the fold)");
            }
            if (Transitions.Count == 0) {
                text.AppendLine("Facet Transition: none (single facet or no neighbouring facets)");
            }
            text.AppendLine();
            text.AppendLine("Overall RMS Error: " + F(RmsMm) + " mm");
            text.AppendLine("95th Percentile Error: " + F(P95Mm) + " mm");
            text.AppendLine("Maximum Error (after outlier rejection): " + F(MaxMm) + " mm");
            text.AppendLine("Limits: RMS " + F(Tolerance.RmsMm) + " mm, P95 " + F(Tolerance.P95Mm) + " mm, facet spacing " + F(Tolerance.SpacingPercent) +
                            " %, shear " + F(Tolerance.ShearDeg) + " deg, transition " + F(Tolerance.TransitionMm) + " mm");
            text.AppendLine("Calibration Status: " + (Pass ? "PASS" : "FAIL"));
            foreach (string failure in Failures) {
                text.AppendLine("  - " + failure);
            }
            return text.ToString();
        }

        private static string F(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Surface-aware grid validation (D-051). Input: for many camera pixels on the surface, the content UV the
    /// projector shows there (Gray code through the skin's full warp) and the pixel's position on the unfolded
    /// physical surface (<see cref="PMSDKSurfaceGeometry"/>). A physically correct projection maps content UV to
    /// the unfolded surface by one affine map (uniform spacing in u and in v, straight lines, continuous across
    /// folds); everything else is distortion. Errors are reported in millimetres. Camera pixels never enter
    /// the error terms, so camera perspective and lens distortion are not mistaken for projection distortion.
    /// </summary>
    public static class PMSDKSurfaceMetrics
    {
        // Samples farther than this many RMS from the first fit are Gray-code decode errors, not geometry.
        private const float OutlierSigma = 4f;
        private const int MinFaceSamples = 50;
        private const int MinCellSamples = 12;
        // Samples within this camera distance of the other facet count as "near the fold".
        private const int FoldBandPx = 12;

        public static PMSDKSurfaceReport Evaluate(IList<Vector2> rawPixels, IList<Vector2> surfaceMm, IList<Vector2> uvs, IList<int> planes,
            PMSDKSurfaceGeometry geometry, int cellsU, int cellsV, PMSDKSurfaceTolerance tolerance)
        {
            PMSDKSurfaceReport report = new PMSDKSurfaceReport();
            report.Tolerance = tolerance;
            report.CellsU = Mathf.Max(1, cellsU);
            report.CellsV = Mathf.Max(1, cellsV);
            int count = uvs != null ? uvs.Count : 0;
            if (count < MinFaceSamples || surfaceMm == null || surfaceMm.Count != count || planes == null || planes.Count != count) {
                report.Message = "Too few samples on the surface (" + count + ").";
                return report;
            }

            // Global affine map UV -> unfolded mm, refitted once without decode outliers.
            List<int> all = new List<int>(count);
            for (int i = 0; i < count; i++) {
                all.Add(i);
            }
            Affine global;
            if (!Affine.TryFit(uvs, surfaceMm, all, out global)) {
                report.Message = "The content does not span the surface (degenerate UV samples).";
                return report;
            }
            float firstRms = Rms(global, uvs, surfaceMm, all);
            List<int> inliers = new List<int>(count);
            float limit = Mathf.Max(OutlierSigma * firstRms, 1f);
            foreach (int i in all) {
                if ((global.Apply(uvs[i]) - surfaceMm[i]).magnitude <= limit) {
                    inliers.Add(i);
                }
            }
            if (inliers.Count < MinFaceSamples || !Affine.TryFit(uvs, surfaceMm, inliers, out global)) {
                report.Message = "Too few consistent samples after outlier rejection.";
                return report;
            }
            report.Samples = inliers.Count;
            report.Outliers = count - inliers.Count;
            report.FitU = global.ColumnU;
            report.FitV = global.ColumnV;
            report.FitOffset = global.Offset;
            report.MmPerU = global.ColumnU.magnitude;
            report.MmPerV = global.ColumnV.magnitude;
            report.ShearDeg = global.ShearDeg;
            report.CellWidthMm = report.MmPerU / report.CellsU;
            report.CellHeightMm = report.MmPerV / report.CellsV;

            List<float> errors = new List<float>(inliers.Count);
            foreach (int i in inliers) {
                errors.Add((global.Apply(uvs[i]) - surfaceMm[i]).magnitude);
            }
            report.RmsMm = RmsOf(errors);
            report.P95Mm = Percentile(errors, 0.95f);
            report.MaxMm = Percentile(errors, 1f);

            // Per facet.
            Dictionary<int, List<int>> byPlane = new Dictionary<int, List<int>>();
            foreach (int i in inliers) {
                List<int> list;
                if (!byPlane.TryGetValue(planes[i], out list)) {
                    list = new List<int>();
                    byPlane[planes[i]] = list;
                }
                list.Add(i);
            }
            List<int> planeIds = new List<int>(byPlane.Keys);
            planeIds.Sort();
            Dictionary<int, Affine> local = new Dictionary<int, Affine>();
            foreach (int plane in planeIds) {
                List<int> members = byPlane[plane];
                Affine fit;
                if (members.Count < MinFaceSamples || !Affine.TryFit(uvs, surfaceMm, members, out fit)) {
                    continue;
                }
                local[plane] = fit;
                PMSDKSurfaceFaceReport face = new PMSDKSurfaceFaceReport();
                face.Plane = plane;
                face.Samples = members.Count;
                face.MmPerU = fit.ColumnU.magnitude;
                face.MmPerV = fit.ColumnV.magnitude;
                face.ShearDeg = fit.ShearDeg;
                face.HorizontalSpacingErrorPercent = (face.MmPerU / report.MmPerU - 1f) * 100f;
                face.VerticalSpacingErrorPercent = (face.MmPerV / report.MmPerV - 1f) * 100f;
                face.CellWidthMm = face.MmPerU / report.CellsU;
                face.CellHeightMm = face.MmPerV / report.CellsV;
                // Straightness: distance of each sample from its iso-u / iso-v line of the facet's own uniform grid.
                List<float> straightness = new List<float>(members.Count);
                Vector2 directionU = fit.ColumnU.normalized;
                Vector2 directionV = fit.ColumnV.normalized;
                Vector2 normalOfULines = new Vector2(-directionV.y, directionV.x);
                Vector2 normalOfVLines = new Vector2(-directionU.y, directionU.x);
                foreach (int i in members) {
                    Vector2 residual = surfaceMm[i] - fit.Apply(uvs[i]);
                    straightness.Add(Mathf.Max(Mathf.Abs(Vector2.Dot(residual, normalOfULines)), Mathf.Abs(Vector2.Dot(residual, normalOfVLines))));
                }
                face.StraightnessRmsMm = RmsOf(straightness);
                face.StraightnessP95Mm = Percentile(straightness, 0.95f);
                report.Faces.Add(face);
            }

            MeasureCells(report, uvs, surfaceMm, planes, inliers);
            if (geometry != null && rawPixels != null && rawPixels.Count == count) {
                MeasureTransitions(report, geometry, rawPixels, uvs, surfaceMm, planes, inliers, global, local);
            }
            Judge(report);
            report.Valid = true;
            report.Message = report.Summary();
            return report;
        }

        private static void MeasureCells(PMSDKSurfaceReport report, IList<Vector2> uvs, IList<Vector2> surfaceMm, IList<int> planes, List<int> inliers)
        {
            int cellsU = report.CellsU;
            int cellsV = report.CellsV;
            List<int>[] cells = new List<int>[cellsU * cellsV];
            foreach (int i in inliers) {
                int cu = Mathf.FloorToInt(uvs[i].x * cellsU);
                int cv = Mathf.FloorToInt(uvs[i].y * cellsV);
                if (cu < 0 || cv < 0 || cu >= cellsU || cv >= cellsV) {
                    continue;
                }
                int index = cv * cellsU + cu;
                if (cells[index] == null) {
                    cells[index] = new List<int>();
                }
                cells[index].Add(i);
            }
            foreach (List<int> members in cells) {
                if (members == null || members.Count < MinCellSamples) {
                    continue;
                }
                Affine fit;
                if (!Affine.TryFit(uvs, surfaceMm, members, out fit)) {
                    continue;
                }
                float width = fit.ColumnU.magnitude / cellsU;
                float height = fit.ColumnV.magnitude / cellsV;
                float error = Mathf.Max(Mathf.Abs(width - report.CellWidthMm), Mathf.Abs(height - report.CellHeightMm));
                int plane = MajorityPlane(planes, members);
                foreach (PMSDKSurfaceFaceReport face in report.Faces) {
                    if (face.Plane == plane) {
                        face.MaxCellDimensionErrorMm = float.IsNaN(face.MaxCellDimensionErrorMm) ? error : Mathf.Max(face.MaxCellDimensionErrorMm, error);
                    }
                }
            }
        }

        private static void MeasureTransitions(PMSDKSurfaceReport report, PMSDKSurfaceGeometry geometry, IList<Vector2> rawPixels, IList<Vector2> uvs,
            IList<Vector2> surfaceMm, IList<int> planes, List<int> inliers, Affine global, Dictionary<int, Affine> local)
        {
            for (int a = 0; a < geometry.Planes.Count; a++) {
                for (int b = a + 1; b < geometry.Planes.Count; b++) {
                    if (!geometry.AreNeighbours(a, b) || !local.ContainsKey(a) || !local.ContainsKey(b)) {
                        continue;
                    }
                    Vector2 sumA = Vector2.zero;
                    Vector2 sumB = Vector2.zero;
                    int countA = 0;
                    int countB = 0;
                    foreach (int i in inliers) {
                        int plane = planes[i];
                        if (plane == a && geometry.DistanceToPlane(b, rawPixels[i]) <= FoldBandPx) {
                            sumA += surfaceMm[i] - global.Apply(uvs[i]);
                            countA++;
                        } else if (plane == b && geometry.DistanceToPlane(a, rawPixels[i]) <= FoldBandPx) {
                            sumB += surfaceMm[i] - global.Apply(uvs[i]);
                            countB++;
                        }
                    }
                    if (countA < MinCellSamples || countB < MinCellSamples) {
                        continue;
                    }
                    PMSDKSurfaceTransitionReport transition = new PMSDKSurfaceTransitionReport();
                    transition.PlaneA = a;
                    transition.PlaneB = b;
                    transition.SamplesA = countA;
                    transition.SamplesB = countB;
                    transition.JumpMm = (sumA / countA - sumB / countB).magnitude;
                    Affine fa = local[a];
                    Affine fb = local[b];
                    float changeU = Mathf.Abs(fa.ColumnU.magnitude / fb.ColumnU.magnitude - 1f) * 100f;
                    float changeV = Mathf.Abs(fa.ColumnV.magnitude / fb.ColumnV.magnitude - 1f) * 100f;
                    transition.SpacingChangePercent = Mathf.Max(changeU, changeV);
                    report.Transitions.Add(transition);
                }
            }
        }

        private static void Judge(PMSDKSurfaceReport report)
        {
            PMSDKSurfaceTolerance t = report.Tolerance;
            if (report.RmsMm > t.RmsMm) {
                report.Failures.Add("overall RMS " + report.RmsMm.ToString("0.00", CultureInfo.InvariantCulture) + " mm > " + t.RmsMm + " mm");
            }
            if (report.P95Mm > t.P95Mm) {
                report.Failures.Add("95th percentile " + report.P95Mm.ToString("0.00", CultureInfo.InvariantCulture) + " mm > " + t.P95Mm + " mm");
            }
            if (Mathf.Abs(report.ShearDeg) > t.ShearDeg) {
                report.Failures.Add("grid shear " + report.ShearDeg.ToString("0.00", CultureInfo.InvariantCulture) + " deg > " + t.ShearDeg + " deg");
            }
            foreach (PMSDKSurfaceFaceReport face in report.Faces) {
                float spacing = Mathf.Max(Mathf.Abs(face.HorizontalSpacingErrorPercent), Mathf.Abs(face.VerticalSpacingErrorPercent));
                if (spacing > t.SpacingPercent) {
                    report.Failures.Add("facet " + face.Plane + " spacing off by " + spacing.ToString("0.00", CultureInfo.InvariantCulture) + " % > " +
                                        t.SpacingPercent + " %");
                }
            }
            foreach (PMSDKSurfaceTransitionReport transition in report.Transitions) {
                if (transition.JumpMm > t.TransitionMm) {
                    report.Failures.Add("fold " + transition.PlaneA + "/" + transition.PlaneB + " jump " +
                                        transition.JumpMm.ToString("0.00", CultureInfo.InvariantCulture) + " mm > " + t.TransitionMm + " mm");
                }
            }
            report.Pass = report.Failures.Count == 0;
        }

        private static int MajorityPlane(IList<int> planes, List<int> members)
        {
            Dictionary<int, int> votes = new Dictionary<int, int>();
            int best = -1;
            int bestVotes = 0;
            foreach (int i in members) {
                int v;
                votes.TryGetValue(planes[i], out v);
                v++;
                votes[planes[i]] = v;
                if (v > bestVotes) {
                    bestVotes = v;
                    best = planes[i];
                }
            }
            return best;
        }

        private static float Rms(Affine fit, IList<Vector2> uvs, IList<Vector2> surfaceMm, List<int> indices)
        {
            double sum = 0.0;
            foreach (int i in indices) {
                sum += (fit.Apply(uvs[i]) - surfaceMm[i]).sqrMagnitude;
            }
            return indices.Count > 0 ? (float)Math.Sqrt(sum / indices.Count) : 0f;
        }

        private static float RmsOf(List<float> values)
        {
            double sum = 0.0;
            foreach (float v in values) {
                sum += (double)v * v;
            }
            return values.Count > 0 ? (float)Math.Sqrt(sum / values.Count) : 0f;
        }

        private static float Percentile(List<float> values, float fraction)
        {
            if (values.Count == 0) {
                return 0f;
            }
            List<float> sorted = new List<float>(values);
            sorted.Sort();
            int index = Mathf.Clamp(Mathf.CeilToInt(fraction * sorted.Count) - 1, 0, sorted.Count - 1);
            return sorted[index];
        }

        /// <summary>mm = ColumnU * u + ColumnV * v + Offset, least squares.</summary>
        internal struct Affine
        {
            public Vector2 ColumnU;
            public Vector2 ColumnV;
            public Vector2 Offset;

            public Vector2 Apply(Vector2 uv)
            {
                return ColumnU * uv.x + ColumnV * uv.y + Offset;
            }

            /// <summary>Deviation of the u and v directions from perpendicular (degrees).</summary>
            public float ShearDeg
            {
                get { return Vector2.Angle(ColumnU, ColumnV) - 90f; }
            }

            public static bool TryFit(IList<Vector2> uvs, IList<Vector2> targets, List<int> indices, out Affine fit)
            {
                fit = new Affine();
                if (indices.Count < 3) {
                    return false;
                }
                // Centre the inputs for conditioning; solve the 3x3 normal equations for x and y.
                double mu = 0.0;
                double mv = 0.0;
                foreach (int i in indices) {
                    mu += uvs[i].x;
                    mv += uvs[i].y;
                }
                mu /= indices.Count;
                mv /= indices.Count;
                double suu = 0.0;
                double suv = 0.0;
                double svv = 0.0;
                double sux = 0.0;
                double svx = 0.0;
                double suy = 0.0;
                double svy = 0.0;
                double sx = 0.0;
                double sy = 0.0;
                foreach (int i in indices) {
                    double u = uvs[i].x - mu;
                    double v = uvs[i].y - mv;
                    double x = targets[i].x;
                    double y = targets[i].y;
                    suu += u * u;
                    suv += u * v;
                    svv += v * v;
                    sux += u * x;
                    svx += v * x;
                    suy += u * y;
                    svy += v * y;
                    sx += x;
                    sy += y;
                }
                double det = suu * svv - suv * suv;
                if (Math.Abs(det) < 1e-12 * Math.Max(1.0, suu * svv)) {
                    return false;
                }
                double ax = (sux * svv - svx * suv) / det;
                double bx = (svx * suu - sux * suv) / det;
                double ay = (suy * svv - svy * suv) / det;
                double by = (svy * suu - suy * suv) / det;
                double cx = sx / indices.Count - ax * mu - bx * mv;
                double cy = sy / indices.Count - ay * mu - by * mv;
                fit.ColumnU = new Vector2((float)ax, (float)ay);
                fit.ColumnV = new Vector2((float)bx, (float)by);
                fit.Offset = new Vector2((float)cx, (float)cy);
                return true;
            }
        }
    }
}
