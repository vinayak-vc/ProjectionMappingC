using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Projector-camera geometry from Gray-code correspondences (D-052). The camera's intrinsics are known (OAK
    /// factory calibration); the projector is modelled as a pinhole with square pixels, principal point x at the
    /// raster centre, unknown focal length and principal point y (projectors have a large vertical lens offset).
    ///
    /// 1. Robust fundamental matrix between undistorted camera pixels and projector pixels (normalised 8-point,
    ///    RANSAC on the Sampson distance).
    /// 2. Self-calibration: the essential matrix Kp^T F Kc must have two equal singular values; the projector focal
    ///    length and principal point y that make it so are found by a coarse grid and a local refinement.
    /// 3. Relative pose from the essential matrix (the candidate that puts the points in front of both).
    /// 4. Every correspondence is triangulated (midpoint of the two rays); the scale comes from the depth camera's
    ///    median distance, so positions are in millimetres in the camera frame (x right, y down, z forward).
    /// The result is a dense depth image of everything the projector lights, far more precise than the OAK's own
    /// stereo when camera and projector are ~1 m apart. Valid only while neither moves.
    /// The scene must not be a single plane (a plane makes the fundamental matrix undefined).
    /// </summary>
    [Serializable]
    public sealed class PMSDKProjectorCalibration
    {
        /// <summary>Projector raster size in pixels used for the intrinsics (16:9 output; only the aspect matters).</summary>
        public const float RasterWidth = 1920f;
        public const float RasterHeight = 1080f;

        private const int RansacIterations = 2000;
        private const float SampsonThresholdPx = 2f;
        private const int MaxFitSamples = 20000;
        private const float MinInlierFraction = 0.2f;
        private const float MaxMedianReprojectionPx = 2.5f;

        public float FocalPx;
        public float PrincipalX;
        public float PrincipalY;
        /// <summary>Rotation (row-major 3 x 3) and translation (mm) taking camera-frame points into the projector frame.</summary>
        public float[] Rotation = new float[9];
        public Vector3 Translation;
        public float BaselineMm;
        /// <summary>False when no depth camera fixed the scale (positions then have an arbitrary unit).</summary>
        public bool Metric;
        public int Samples;
        public float InlierFraction;
        public float MedianReprojectionPx;
        public int CameraWidth;
        public int CameraHeight;
        public string CreatedUtc = "";

        /// <summary>Triangulated distance (z, mm) per camera pixel, 0 = none. Not serialised to JSON (saved next to it).</summary>
        [NonSerialized] public ushort[] Depth;

        public string Describe()
        {
            return "projector focal " + FocalPx.ToString("0") + " px (throw ratio " + (FocalPx / RasterWidth).ToString("0.00") + "), lens centre y " +
                   (PrincipalY / RasterHeight).ToString("0.00") + " H, baseline " + BaselineMm.ToString("0") + (Metric ? " mm" : " units") +
                   ", reprojection " + MedianReprojectionPx.ToString("0.00") + " px, " + (InlierFraction * 100f).ToString("0") + " % inliers";
        }

        // ------------------------------------------------------------------ solve

        /// <summary>
        /// Calibrates from camera pixels (raw, row 0 = top) and projector raster positions (0..1, y up).
        /// </summary>
        /// <param name="cameraDepthMm">Optional depth camera image (mm, 0 = none) used only to set the metric scale.</param>
        public static bool TrySolve(IList<Vector2> cameraPixels, IList<Vector2> raster, PMSDKCameraIntrinsics intrinsics, int cameraWidth, int cameraHeight,
            ushort[] cameraDepthMm, out PMSDKProjectorCalibration calibration, out string error)
        {
            calibration = null;
            if (cameraPixels == null || raster == null || cameraPixels.Count != raster.Count || cameraPixels.Count < 100) {
                error = "Too few camera-projector correspondences.";
                return false;
            }
            if (intrinsics == null || intrinsics.Fx <= 0f) {
                error = "Projector calibration needs the camera intrinsics.";
                return false;
            }
            Vector2[] undistorted;
            if (!PMSDKAutoAdjustNative.TryUndistort(ToArray(cameraPixels), intrinsics, out undistorted, out error)) {
                return false;
            }
            int count = cameraPixels.Count;
            Vector2[] projector = new Vector2[count];
            for (int i = 0; i < count; i++) {
                projector[i] = new Vector2(raster[i].x * RasterWidth, (1f - raster[i].y) * RasterHeight);
            }

            // 1. Fundamental matrix on a subsample.
            System.Random random = new System.Random(12345);
            int[] subset = Subsample(count, MaxFitSamples, random);
            double[] f;
            List<int> inliers;
            if (!TryFundamental(undistorted, projector, subset, random, out f, out inliers)) {
                error = "Could not estimate the camera-projector geometry (fundamental matrix).";
                return false;
            }
            float inlierFraction = (float)inliers.Count / subset.Length;
            if (inlierFraction < MinInlierFraction) {
                error = "The camera-projector correspondences are inconsistent (" + (inlierFraction * 100f).ToString("0") +
                        " % inliers); is the scene a single flat wall? Calibrate with the projector also lighting a second surface.";
                return false;
            }

            // A single plane is explained by a homography as well as by F: then F (and self-calibration) is undefined.
            if (IsNearlyPlanar(undistorted, projector, subset, inliers.Count)) {
                error = "The projector lights a single flat surface; calibration needs depth variety. Let the full-screen " +
                        "pattern also hit a second surface (ceiling, side wall) and calibrate again.";
                return false;
            }

            // 2. Self-calibration of the projector.
            double[] kc = { intrinsics.Fx, 0, intrinsics.Cx, 0, intrinsics.Fy, intrinsics.Cy, 0, 0, 1 };
            double cx = RasterWidth * 0.5;
            double focal;
            double principalY;
            SelfCalibrate(f, kc, cx, out focal, out principalY);

            // 3. Pose.
            double[] kp = { focal, 0, cx, 0, focal, principalY, 0, 0, 1 };
            double[] e = Multiply(Multiply(Transpose(kp), f), kc);
            Vector3[] cameraRays = new Vector3[count];
            Vector3[] projectorRays = new Vector3[count];
            for (int i = 0; i < count; i++) {
                cameraRays[i] = new Vector3((undistorted[i].x - intrinsics.Cx) / intrinsics.Fx, (undistorted[i].y - intrinsics.Cy) / intrinsics.Fy, 1f);
                projectorRays[i] = new Vector3((float)((projector[i].x - cx) / focal), (float)((projector[i].y - principalY) / focal), 1f);
            }
            double[] rotation;
            double[] translation;
            if (!TryPose(e, cameraRays, projectorRays, inliers, out rotation, out translation)) {
                error = "Could not recover the projector pose.";
                return false;
            }

            // 4. Triangulate everything (unit baseline), then fix the scale with the depth camera.
            Vector3[] points = new Vector3[count];
            bool[] valid = new bool[count];
            List<float> reprojection = new List<float>();
            for (int i = 0; i < count; i++) {
                valid[i] = TryTriangulate(cameraRays[i], projectorRays[i], rotation, translation, out points[i]);
            }
            foreach (int i in inliers) {
                if (valid[i]) {
                    Vector3 q = Apply(rotation, translation, points[i]);
                    Vector2 back = new Vector2((float)(q.x / q.z * focal + cx), (float)(q.y / q.z * focal + principalY));
                    reprojection.Add((back - projector[i]).magnitude);
                }
            }
            reprojection.Sort();
            float medianReprojection = reprojection.Count > 0 ? reprojection[reprojection.Count / 2] : float.MaxValue;
            if (medianReprojection > MaxMedianReprojectionPx) {
                error = "Projector model does not fit (median reprojection " + medianReprojection.ToString("0.0") + " px).";
                return false;
            }
            float scale = 1000f;
            bool metric = false;
            if (cameraDepthMm != null && cameraDepthMm.Length >= cameraWidth * cameraHeight) {
                List<float> ratios = new List<float>();
                for (int i = 0; i < count; i += 3) {
                    int x = Mathf.RoundToInt(cameraPixels[i].x);
                    int y = Mathf.RoundToInt(cameraPixels[i].y);
                    if (!valid[i] || x < 0 || y < 0 || x >= cameraWidth || y >= cameraHeight) {
                        continue;
                    }
                    ushort z = cameraDepthMm[y * cameraWidth + x];
                    if (z > 0 && points[i].z > 0f) {
                        ratios.Add(z / points[i].z);
                    }
                }
                if (ratios.Count > 100) {
                    ratios.Sort();
                    scale = ratios[ratios.Count / 2];
                    metric = true;
                }
            }

            PMSDKProjectorCalibration result = new PMSDKProjectorCalibration();
            result.FocalPx = (float)focal;
            result.PrincipalX = (float)cx;
            result.PrincipalY = (float)principalY;
            for (int k = 0; k < 9; k++) {
                result.Rotation[k] = (float)rotation[k];
            }
            result.Translation = new Vector3((float)translation[0], (float)translation[1], (float)translation[2]) * scale;
            result.BaselineMm = result.Translation.magnitude;
            result.Metric = metric;
            result.Samples = count;
            result.InlierFraction = inlierFraction;
            result.MedianReprojectionPx = medianReprojection;
            result.CameraWidth = cameraWidth;
            result.CameraHeight = cameraHeight;
            result.CreatedUtc = DateTime.UtcNow.ToString("o");
            result.Depth = new ushort[cameraWidth * cameraHeight];
            for (int i = 0; i < count; i++) {
                int x = Mathf.RoundToInt(cameraPixels[i].x);
                int y = Mathf.RoundToInt(cameraPixels[i].y);
                float z = points[i].z * scale;
                if (valid[i] && z > 0f && z < 65535f && x >= 0 && y >= 0 && x < cameraWidth && y < cameraHeight) {
                    result.Depth[y * cameraWidth + x] = (ushort)Mathf.RoundToInt(z);
                }
            }
            calibration = result;
            error = "";
            return true;
        }

        /// <summary>
        /// Median epipolar (Sampson) distance, in projector pixels, of new camera-pixel / projector-raster correspondences
        /// against this calibration. Small (~1 px) while camera and projector are where they were calibrated; a few px
        /// or more means one of them moved.
        /// </summary>
        public float MedianEpipolarErrorPx(IList<Vector2> cameraPixels, IList<Vector2> raster, PMSDKCameraIntrinsics intrinsics)
        {
            if (cameraPixels == null || raster == null || cameraPixels.Count == 0 || cameraPixels.Count != raster.Count || intrinsics == null) {
                return float.MaxValue;
            }
            int step = Mathf.Max(1, cameraPixels.Count / 5000);
            List<Vector2> sampleCamera = new List<Vector2>();
            List<Vector2> sampleProjector = new List<Vector2>();
            for (int i = 0; i < cameraPixels.Count; i += step) {
                sampleCamera.Add(cameraPixels[i]);
                sampleProjector.Add(new Vector2(raster[i].x * RasterWidth, (1f - raster[i].y) * RasterHeight));
            }
            Vector2[] undistorted;
            string error;
            if (!PMSDKAutoAdjustNative.TryUndistort(sampleCamera.ToArray(), intrinsics, out undistorted, out error)) {
                return float.MaxValue;
            }
            // E = [t]x R, F = Kp^-T E Kc^-1 (b^T F a = 0 for undistorted camera pixel a, projector pixel b).
            double[] r = new double[9];
            for (int k = 0; k < 9; k++) {
                r[k] = Rotation[k];
            }
            double[] tx = { 0, -Translation.z, Translation.y, Translation.z, 0, -Translation.x, -Translation.y, Translation.x, 0 };
            double[] e = Multiply(tx, r);
            double[] kcInverse = { 1.0 / intrinsics.Fx, 0, -intrinsics.Cx / intrinsics.Fx, 0, 1.0 / intrinsics.Fy, -intrinsics.Cy / intrinsics.Fy, 0, 0, 1 };
            double[] kpInverse = { 1.0 / FocalPx, 0, -PrincipalX / FocalPx, 0, 1.0 / FocalPx, -PrincipalY / FocalPx, 0, 0, 1 };
            double[] f = Multiply(Multiply(Transpose(kpInverse), e), kcInverse);
            List<float> distances = new List<float>(undistorted.Length);
            for (int i = 0; i < undistorted.Length; i++) {
                distances.Add((float)Math.Sqrt(Sampson(f, undistorted[i], sampleProjector[i])));
            }
            distances.Sort();
            return distances[distances.Count / 2];
        }

        // ------------------------------------------------------------------ persistence

        public static string Folder
        {
            get { return Path.Combine(Application.persistentDataPath, "ProjectorCalibration"); }
        }

        public void Save(string folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "projector.json"), JsonUtility.ToJson(this, true));
            if (Depth != null) {
                byte[] bytes = new byte[Depth.Length * 2];
                Buffer.BlockCopy(Depth, 0, bytes, 0, bytes.Length);
                File.WriteAllBytes(Path.Combine(folder, "depth_u16.bin"), bytes);
            }
        }

        /// <summary>False with an empty error when no calibration was saved.</summary>
        public static bool TryLoad(string folder, out PMSDKProjectorCalibration calibration, out string error)
        {
            calibration = null;
            error = "";
            string json = Path.Combine(folder, "projector.json");
            string depth = Path.Combine(folder, "depth_u16.bin");
            if (!File.Exists(json)) {
                return false;
            }
            try {
                PMSDKProjectorCalibration loaded = JsonUtility.FromJson<PMSDKProjectorCalibration>(File.ReadAllText(json));
                if (File.Exists(depth)) {
                    byte[] bytes = File.ReadAllBytes(depth);
                    if (bytes.Length == loaded.CameraWidth * loaded.CameraHeight * 2) {
                        loaded.Depth = new ushort[bytes.Length / 2];
                        Buffer.BlockCopy(bytes, 0, loaded.Depth, 0, bytes.Length);
                    }
                }
                if (loaded.Depth == null) {
                    error = "The saved projector calibration has no depth image.";
                    return false;
                }
                calibration = loaded;
                return true;
            } catch (Exception ex) {
                error = "Could not read the projector calibration: " + ex.Message;
                return false;
            }
        }

        // ------------------------------------------------------------------ geometry

        /// <summary>Midpoint of the closest points of the camera ray (from the origin) and the projector ray.</summary>
        public static bool TryTriangulate(Vector3 cameraRay, Vector3 projectorRay, double[] rotation, double[] translation, out Vector3 point)
        {
            // Projector centre and ray in the camera frame: C = -R^T t, d = R^T p.
            Vector3 centre = -TransposeApply(rotation, translation);
            Vector3 direction = TransposeApply(rotation, new double[] { projectorRay.x, projectorRay.y, projectorRay.z });
            Vector3 a = cameraRay;
            Vector3 b = direction;
            float aa = Vector3.Dot(a, a);
            float bb = Vector3.Dot(b, b);
            float ab = Vector3.Dot(a, b);
            Vector3 w = -centre;
            float aw = Vector3.Dot(a, w);
            float bw = Vector3.Dot(b, w);
            float denominator = aa * bb - ab * ab;
            if (Mathf.Abs(denominator) < 1e-9f) {
                point = Vector3.zero;
                return false;
            }
            float s = (ab * bw - bb * aw) / denominator;
            float t = (aa * bw - ab * aw) / denominator;
            Vector3 onCamera = a * s;
            Vector3 onProjector = centre + b * t;
            point = (onCamera + onProjector) * 0.5f;
            return s > 0f && t > 0f;
        }

        private static Vector3 Apply(double[] r, double[] t, Vector3 p)
        {
            return new Vector3(
                (float)(r[0] * p.x + r[1] * p.y + r[2] * p.z + t[0]),
                (float)(r[3] * p.x + r[4] * p.y + r[5] * p.z + t[1]),
                (float)(r[6] * p.x + r[7] * p.y + r[8] * p.z + t[2]));
        }

        private static Vector3 TransposeApply(double[] r, double[] v)
        {
            return new Vector3(
                (float)(r[0] * v[0] + r[3] * v[1] + r[6] * v[2]),
                (float)(r[1] * v[0] + r[4] * v[1] + r[7] * v[2]),
                (float)(r[2] * v[0] + r[5] * v[1] + r[8] * v[2]));
        }

        // ------------------------------------------------------------------ fundamental matrix

        private static bool TryFundamental(Vector2[] a, Vector2[] b, int[] subset, System.Random random, out double[] best, out List<int> inliers)
        {
            best = null;
            inliers = new List<int>();
            double[] ta = NormalisingTransform(a, subset);
            double[] tb = NormalisingTransform(b, subset);
            int[] sample = new int[8];
            for (int iteration = 0; iteration < RansacIterations; iteration++) {
                for (int k = 0; k < 8; k++) {
                    sample[k] = subset[random.Next(subset.Length)];
                }
                double[] candidate;
                if (!TryEightPoint(a, b, sample, ta, tb, out candidate)) {
                    continue;
                }
                List<int> current = Inliers(candidate, a, b, subset);
                if (current.Count > inliers.Count) {
                    inliers = current;
                    best = candidate;
                }
            }
            if (best == null) {
                return false;
            }
            // Refit on all inliers, then recount.
            double[] refined;
            if (TryEightPoint(a, b, inliers.ToArray(), ta, tb, out refined)) {
                List<int> refinedInliers = Inliers(refined, a, b, subset);
                if (refinedInliers.Count >= inliers.Count) {
                    best = refined;
                    inliers = refinedInliers;
                }
            }
            return true;
        }

        // A homography with this share of the fundamental matrix's inliers means the scene is (nearly) one plane.
        private const float PlanarInlierRatio = 0.9f;

        private static bool IsNearlyPlanar(Vector2[] a, Vector2[] b, int[] subset, int fundamentalInliers)
        {
            List<Vector2> source = new List<Vector2>(subset.Length);
            List<Vector2> target = new List<Vector2>(subset.Length);
            foreach (int i in subset) {
                source.Add(a[i]);
                target.Add(b[i]);
            }
            // Least squares, then one refit on the points it explains (a robust enough check here).
            PMSDKHomography h = PMSDKHomography.FitNormalized(source, target);
            for (int pass = 0; pass < 2 && h.Valid; pass++) {
                List<Vector2> keptSource = new List<Vector2>();
                List<Vector2> keptTarget = new List<Vector2>();
                for (int k = 0; k < source.Count; k++) {
                    if ((h.Apply(source[k]) - target[k]).magnitude < SampsonThresholdPx * 4f) {
                        keptSource.Add(source[k]);
                        keptTarget.Add(target[k]);
                    }
                }
                if (keptSource.Count < 8) {
                    break;
                }
                h = PMSDKHomography.FitNormalized(keptSource, keptTarget);
            }
            if (!h.Valid) {
                return false;
            }
            int explained = 0;
            for (int k = 0; k < source.Count; k++) {
                // Transfer error is one-sided, so it gets twice the Sampson threshold.
                if ((h.Apply(source[k]) - target[k]).magnitude < SampsonThresholdPx * 2f) {
                    explained++;
                }
            }
            return explained >= PlanarInlierRatio * fundamentalInliers;
        }

        private static List<int> Inliers(double[] f, Vector2[] a, Vector2[] b, int[] subset)
        {
            List<int> result = new List<int>();
            double limit = SampsonThresholdPx * SampsonThresholdPx;
            foreach (int i in subset) {
                if (Sampson(f, a[i], b[i]) < limit) {
                    result.Add(i);
                }
            }
            return result;
        }

        /// <summary>Squared Sampson distance of b^T F a = 0.</summary>
        private static double Sampson(double[] f, Vector2 a, Vector2 b)
        {
            double fa0 = f[0] * a.x + f[1] * a.y + f[2];
            double fa1 = f[3] * a.x + f[4] * a.y + f[5];
            double fa2 = f[6] * a.x + f[7] * a.y + f[8];
            double ftb0 = f[0] * b.x + f[3] * b.y + f[6];
            double ftb1 = f[1] * b.x + f[4] * b.y + f[7];
            double value = b.x * fa0 + b.y * fa1 + fa2;
            double denominator = fa0 * fa0 + fa1 * fa1 + ftb0 * ftb0 + ftb1 * ftb1;
            return denominator > 1e-18 ? value * value / denominator : double.MaxValue;
        }

        private static bool TryEightPoint(Vector2[] a, Vector2[] b, int[] indices, double[] ta, double[] tb, out double[] f)
        {
            f = null;
            double[,] ata = new double[9, 9];
            double[] row = new double[9];
            foreach (int i in indices) {
                double ax = ta[0] * a[i].x + ta[2];
                double ay = ta[0] * a[i].y + ta[5];
                double bx = tb[0] * b[i].x + tb[2];
                double by = tb[0] * b[i].y + tb[5];
                row[0] = bx * ax; row[1] = bx * ay; row[2] = bx;
                row[3] = by * ax; row[4] = by * ay; row[5] = by;
                row[6] = ax; row[7] = ay; row[8] = 1.0;
                for (int r = 0; r < 9; r++) {
                    for (int c = 0; c < 9; c++) {
                        ata[r, c] += row[r] * row[c];
                    }
                }
            }
            double[] values;
            double[,] vectors;
            SymmetricEigen(ata, out values, out vectors);
            int smallest = 0;
            for (int k = 1; k < 9; k++) {
                if (values[k] < values[smallest]) {
                    smallest = k;
                }
            }
            double[] fn = new double[9];
            for (int k = 0; k < 9; k++) {
                fn[k] = vectors[k, smallest];
            }
            // Enforce rank 2.
            double[] u;
            double[] s;
            double[] v;
            Svd3(fn, out u, out s, out v);
            double[] rank2 = Multiply(Multiply(u, Diagonal(s[0], s[1], 0.0)), Transpose(v));
            // Denormalise: F = Tb^T F' Ta.
            f = Multiply(Multiply(Transpose(tb), rank2), ta);
            double norm = 0.0;
            for (int k = 0; k < 9; k++) {
                norm += f[k] * f[k];
            }
            norm = Math.Sqrt(norm);
            if (norm < 1e-18) {
                return false;
            }
            for (int k = 0; k < 9; k++) {
                f[k] /= norm;
            }
            return true;
        }

        private static double[] NormalisingTransform(Vector2[] points, int[] subset)
        {
            double mx = 0.0;
            double my = 0.0;
            foreach (int i in subset) {
                mx += points[i].x;
                my += points[i].y;
            }
            mx /= subset.Length;
            my /= subset.Length;
            double distance = 0.0;
            foreach (int i in subset) {
                distance += Math.Sqrt((points[i].x - mx) * (points[i].x - mx) + (points[i].y - my) * (points[i].y - my));
            }
            distance /= subset.Length;
            double s = distance > 1e-12 ? Math.Sqrt(2.0) / distance : 1.0;
            return new double[] { s, 0, -s * mx, 0, s, -s * my, 0, 0, 1 };
        }

        // ------------------------------------------------------------------ self-calibration and pose

        private static double EssentialCost(double[] f, double[] kc, double cx, double focal, double principalY)
        {
            double[] kp = { focal, 0, cx, 0, focal, principalY, 0, 0, 1 };
            double[] e = Multiply(Multiply(Transpose(kp), f), kc);
            double[] u;
            double[] s;
            double[] v;
            Svd3(e, out u, out s, out v);
            return s[0] > 1e-18 ? (s[0] - s[1]) / s[0] : 1.0;
        }

        private static void SelfCalibrate(double[] f, double[] kc, double cx, out double focal, out double principalY)
        {
            double bestCost = double.MaxValue;
            focal = RasterWidth;
            principalY = RasterHeight * 0.5;
            for (double fc = 0.2 * RasterWidth; fc <= 4.0 * RasterWidth; fc *= 1.02) {
                for (double cy = -0.5 * RasterHeight; cy <= 1.5 * RasterHeight; cy += RasterHeight / 50.0) {
                    double cost = EssentialCost(f, kc, cx, fc, cy);
                    if (cost < bestCost) {
                        bestCost = cost;
                        focal = fc;
                        principalY = cy;
                    }
                }
            }
            double stepF = focal * 0.02;
            double stepC = RasterHeight / 50.0;
            for (int iteration = 0; iteration < 60 && (stepF > 0.01 || stepC > 0.01); iteration++) {
                bool moved = false;
                for (int a = -1; a <= 1; a++) {
                    for (int b = -1; b <= 1; b++) {
                        double cost = EssentialCost(f, kc, cx, focal + a * stepF, principalY + b * stepC);
                        if (cost < bestCost - 1e-15) {
                            bestCost = cost;
                            focal += a * stepF;
                            principalY += b * stepC;
                            moved = true;
                        }
                    }
                }
                if (!moved) {
                    stepF *= 0.5;
                    stepC *= 0.5;
                }
            }
        }

        private static bool TryPose(double[] e, Vector3[] cameraRays, Vector3[] projectorRays, List<int> inliers, out double[] rotation, out double[] translation)
        {
            rotation = null;
            translation = null;
            double[] u;
            double[] s;
            double[] v;
            Svd3(e, out u, out s, out v);
            if (Determinant(u) < 0) {
                for (int k = 0; k < 9; k++) {
                    u[k] = -u[k];
                }
            }
            if (Determinant(v) < 0) {
                for (int k = 0; k < 9; k++) {
                    v[k] = -v[k];
                }
            }
            double[] w = { 0, -1, 0, 1, 0, 0, 0, 0, 1 };
            double[][] rotations = {
                Multiply(Multiply(u, w), Transpose(v)),
                Multiply(Multiply(u, Transpose(w)), Transpose(v)),
            };
            double[] t = { u[2], u[5], u[8] };
            int bestFront = -1;
            int step = Math.Max(1, inliers.Count / 500);
            foreach (double[] r in rotations) {
                for (int sign = -1; sign <= 1; sign += 2) {
                    double[] candidate = { t[0] * sign, t[1] * sign, t[2] * sign };
                    int front = 0;
                    for (int k = 0; k < inliers.Count; k += step) {
                        int i = inliers[k];
                        Vector3 point;
                        if (TryTriangulate(cameraRays[i], projectorRays[i], r, candidate, out point) && Apply(r, candidate, point).z > 0f) {
                            front++;
                        }
                    }
                    if (front > bestFront) {
                        bestFront = front;
                        rotation = r;
                        translation = candidate;
                    }
                }
            }
            return rotation != null && bestFront > 0;
        }

        // ------------------------------------------------------------------ small linear algebra (row-major 3 x 3)

        private static double[] Multiply(double[] a, double[] b)
        {
            double[] c = new double[9];
            for (int r = 0; r < 3; r++) {
                for (int col = 0; col < 3; col++) {
                    c[r * 3 + col] = a[r * 3] * b[col] + a[r * 3 + 1] * b[3 + col] + a[r * 3 + 2] * b[6 + col];
                }
            }
            return c;
        }

        private static double[] Transpose(double[] a)
        {
            return new double[] { a[0], a[3], a[6], a[1], a[4], a[7], a[2], a[5], a[8] };
        }

        private static double[] Diagonal(double a, double b, double c)
        {
            return new double[] { a, 0, 0, 0, b, 0, 0, 0, c };
        }

        private static double Determinant(double[] m)
        {
            return m[0] * (m[4] * m[8] - m[5] * m[7]) - m[1] * (m[3] * m[8] - m[5] * m[6]) + m[2] * (m[3] * m[7] - m[4] * m[6]);
        }

        /// <summary>A = U diag(s) V^T with s descending (via the eigen decomposition of A^T A).</summary>
        private static void Svd3(double[] a, out double[] u, out double[] s, out double[] v)
        {
            double[] ata = Multiply(Transpose(a), a);
            double[,] m = new double[3, 3];
            for (int r = 0; r < 3; r++) {
                for (int c = 0; c < 3; c++) {
                    m[r, c] = ata[r * 3 + c];
                }
            }
            double[] values;
            double[,] vectors;
            SymmetricEigen(m, out values, out vectors);
            int[] order = { 0, 1, 2 };
            Array.Sort(order, delegate(int x, int y) { return values[y].CompareTo(values[x]); });
            v = new double[9];
            s = new double[3];
            for (int k = 0; k < 3; k++) {
                s[k] = Math.Sqrt(Math.Max(0.0, values[order[k]]));
                for (int r = 0; r < 3; r++) {
                    v[r * 3 + k] = vectors[r, order[k]];
                }
            }
            u = new double[9];
            for (int k = 0; k < 2; k++) {
                double[] column = new double[3];
                for (int r = 0; r < 3; r++) {
                    column[r] = a[r * 3] * v[k] + a[r * 3 + 1] * v[3 + k] + a[r * 3 + 2] * v[6 + k];
                }
                double length = Math.Sqrt(column[0] * column[0] + column[1] * column[1] + column[2] * column[2]);
                for (int r = 0; r < 3; r++) {
                    u[r * 3 + k] = length > 1e-18 ? column[r] / length : (r == k ? 1.0 : 0.0);
                }
            }
            // Third column: completes a right-handed orthonormal basis (works for rank-2 matrices).
            u[2] = u[3] * u[7] - u[6] * u[4];
            u[5] = u[6] * u[1] - u[0] * u[7];
            u[8] = u[0] * u[4] - u[3] * u[1];
        }

        /// <summary>Cyclic Jacobi eigen decomposition of a symmetric matrix (columns of vectors are eigenvectors).</summary>
        private static void SymmetricEigen(double[,] matrix, out double[] values, out double[,] vectors)
        {
            int n = matrix.GetLength(0);
            double[,] a = (double[,])matrix.Clone();
            vectors = new double[n, n];
            for (int i = 0; i < n; i++) {
                vectors[i, i] = 1.0;
            }
            for (int sweep = 0; sweep < 60; sweep++) {
                double off = 0.0;
                for (int p = 0; p < n; p++) {
                    for (int q = p + 1; q < n; q++) {
                        off += a[p, q] * a[p, q];
                    }
                }
                if (off < 1e-30) {
                    break;
                }
                for (int p = 0; p < n; p++) {
                    for (int q = p + 1; q < n; q++) {
                        if (Math.Abs(a[p, q]) < 1e-300) {
                            continue;
                        }
                        double theta = (a[q, q] - a[p, p]) / (2.0 * a[p, q]);
                        double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
                        if (theta == 0.0) {
                            t = 1.0;
                        }
                        double c = 1.0 / Math.Sqrt(t * t + 1.0);
                        double sn = t * c;
                        for (int k = 0; k < n; k++) {
                            double akp = a[k, p];
                            double akq = a[k, q];
                            a[k, p] = c * akp - sn * akq;
                            a[k, q] = sn * akp + c * akq;
                        }
                        for (int k = 0; k < n; k++) {
                            double apk = a[p, k];
                            double aqk = a[q, k];
                            a[p, k] = c * apk - sn * aqk;
                            a[q, k] = sn * apk + c * aqk;
                        }
                        for (int k = 0; k < n; k++) {
                            double vkp = vectors[k, p];
                            double vkq = vectors[k, q];
                            vectors[k, p] = c * vkp - sn * vkq;
                            vectors[k, q] = sn * vkp + c * vkq;
                        }
                    }
                }
            }
            values = new double[n];
            for (int i = 0; i < n; i++) {
                values[i] = a[i, i];
            }
        }

        private static int[] Subsample(int count, int max, System.Random random)
        {
            if (count <= max) {
                int[] all = new int[count];
                for (int i = 0; i < count; i++) {
                    all[i] = i;
                }
                return all;
            }
            int[] result = new int[max];
            for (int k = 0; k < max; k++) {
                result[k] = random.Next(count);
            }
            return result;
        }

        private static Vector2[] ToArray(IList<Vector2> list)
        {
            Vector2[] array = new Vector2[list.Count];
            list.CopyTo(array, 0);
            return array;
        }
    }
}
