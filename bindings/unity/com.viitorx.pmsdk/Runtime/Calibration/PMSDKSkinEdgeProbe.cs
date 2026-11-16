using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Measures where a Rectangle skin's border lands in the camera image while the Auto Adjust checker is
    /// shown (white margin inside the skin, dark outside). The checker's inner corners stop one margin plus
    /// one square short of the border, so without these samples the outer band of the skin is never
    /// measured and the correction there is only extrapolated (D-048).
    /// For each sample on the border (content UV on an edge of the unit square) the brightness profile is
    /// read along the edge normal in the raw image through the predicted position, the outermost
    /// bright-to-dark step is located to sub-pixel precision and undistorted. Only the distance across the
    /// edge is observable, not the position along it (<see cref="AcrossOnly"/>).
    /// </summary>
    public static class PMSDKSkinEdgeProbe
    {
        private const float StepPx = 0.5f;

        /// <summary>
        /// Finds border points. <paramref name="borderUvs"/> receives the content UV of each sample found and
        /// <paramref name="borderPoints"/> the undistorted camera pixel where the border crosses that sample's
        /// normal line. Returns the count found.
        /// </summary>
        /// <param name="frame">8-bit grey camera frame, rows top first.</param>
        /// <param name="uvs">Content UV of every checker corner.</param>
        /// <param name="raw">Raw (distorted) camera pixel of every checker corner.</param>
        /// <param name="valid">Non-zero for detected corners.</param>
        /// <param name="intrinsics">Lens model for undistortion; null when the image has no distortion.</param>
        public static int FindBorder(byte[] frame, int width, int height, PMSDKCheckerSpec checker, Vector2[] uvs, Vector2[] raw,
            byte[] valid, PMSDKCameraIntrinsics intrinsics, float minContrast, List<Vector2> borderUvs, List<Vector2> borderPoints)
        {
            if (frame == null || frame.Length < width * height || uvs == null || raw == null || valid == null || checker.Margin <= 1e-4f) {
                return 0;
            }
            List<Vector2> src = new List<Vector2>();
            List<Vector2> dst = new List<Vector2>();
            for (int i = 0; i < uvs.Length && i < raw.Length && i < valid.Length; i++) {
                if (valid[i] != 0) {
                    src.Add(uvs[i]);
                    dst.Add(raw[i]);
                }
            }
            PMSDKHomography rawMap = PMSDKHomography.FitNormalized(src, dst);
            if (!rawMap.Valid) {
                return 0;
            }

            List<Vector2> sampleUvs = new List<Vector2>();
            List<Vector2> sampleAlong = new List<Vector2>();
            List<Vector2> sampleInward = new List<Vector2>();
            int alongX = Mathf.Max(3, checker.SquaresX);
            int alongY = Mathf.Max(3, checker.SquaresY);
            AddEdge(sampleUvs, sampleAlong, sampleInward, alongX, new Vector2(0f, 0f), Vector2.right, Vector2.up);
            AddEdge(sampleUvs, sampleAlong, sampleInward, alongX, new Vector2(0f, 1f), Vector2.right, Vector2.down);
            AddEdge(sampleUvs, sampleAlong, sampleInward, alongY, new Vector2(0f, 0f), Vector2.up, Vector2.right);
            AddEdge(sampleUvs, sampleAlong, sampleInward, alongY, new Vector2(1f, 0f), Vector2.up, Vector2.left);

            List<int> foundSample = new List<int>();
            List<Vector2> foundRaw = new List<Vector2>();
            List<float> foundOffset = new List<float>();
            List<float> foundMargin = new List<float>();
            for (int k = 0; k < sampleUvs.Count; k++) {
                Vector2 uv = sampleUvs[k];
                Vector2 onEdge = rawMap.Apply(uv);
                Vector2 tangent = rawMap.Apply(uv + sampleAlong[k] * 0.01f) - rawMap.Apply(uv - sampleAlong[k] * 0.01f);
                if (tangent.sqrMagnitude < 1e-12f) {
                    continue;
                }
                Vector2 outward = new Vector2(-tangent.y, tangent.x).normalized;
                float marginPx = Vector2.Dot(onEdge - rawMap.Apply(uv + sampleInward[k] * checker.Margin), outward);
                if (marginPx < 0f) {
                    outward = -outward;
                    marginPx = -marginPx;
                }
                if (marginPx < 2f) {
                    continue;
                }
                Vector2 crossing;
                if (TryFindFallingEdge(frame, width, height, onEdge, outward, marginPx, minContrast, out crossing)) {
                    foundSample.Add(k);
                    foundRaw.Add(crossing);
                    foundOffset.Add(Vector2.Dot(crossing - onEdge, outward));
                    foundMargin.Add(marginPx);
                }
            }
            RejectInconsistent(sampleUvs, sampleAlong, foundSample, foundRaw, foundOffset, foundMargin);
            List<Vector2> foundUvs = new List<Vector2>();
            for (int i = 0; i < foundSample.Count; i++) {
                foundUvs.Add(sampleUvs[foundSample[i]]);
            }
            if (foundRaw.Count == 0) {
                return 0;
            }
            Vector2[] undistorted;
            string error;
            if (!PMSDKAutoAdjustNative.TryUndistort(foundRaw.ToArray(), intrinsics, out undistorted, out error)) {
                Debug.LogWarning("[PMSDK] Auto Adjust: skin border samples skipped, undistortion failed: " + error);
                return 0;
            }
            borderUvs.AddRange(foundUvs);
            borderPoints.AddRange(undistorted);
            return foundUvs.Count;
        }

        // The border moves smoothly along an edge, so a sample whose offset disagrees with every line through
        // two of its nearest neighbours on the same edge latched onto something else (e.g. the edge was
        // further inside than the search reaches) and is dropped. Using several neighbour pairs keeps one bad
        // sample from condemning its neighbours.
        private static void RejectInconsistent(List<Vector2> sampleUvs, List<Vector2> sampleAlong, List<int> found, List<Vector2> raw,
            List<float> offset, List<float> margin)
        {
            const int Neighbours = 4;
            int n = found.Count;
            bool[] reject = new bool[n];
            List<int> near = new List<int>();
            for (int i = 0; i < n; i++) {
                Vector2 uv = sampleUvs[found[i]];
                Vector2 along = sampleAlong[found[i]];
                Vector2 across = new Vector2(along.y, along.x);
                float t = Vector2.Dot(uv, along);
                near.Clear();
                for (int j = 0; j < n; j++) {
                    Vector2 other = sampleUvs[found[j]];
                    if (j != i && sampleAlong[found[j]] == along && Mathf.Abs(Vector2.Dot(other - uv, across)) < 1e-6f) {
                        near.Add(j);
                    }
                }
                near.Sort(delegate(int x, int y) {
                    return Mathf.Abs(Vector2.Dot(sampleUvs[found[x]], along) - t).CompareTo(Mathf.Abs(Vector2.Dot(sampleUvs[found[y]], along) - t));
                });
                int count = Mathf.Min(Neighbours, near.Count);
                if (count < 2) {
                    continue;
                }
                float threshold = Mathf.Max(3f, 0.3f * margin[i]);
                bool agrees = false;
                for (int p = 0; p < count && !agrees; p++) {
                    for (int q = p + 1; q < count && !agrees; q++) {
                        int a = near[p];
                        int b = near[q];
                        float ta = Vector2.Dot(sampleUvs[found[a]], along);
                        float tb = Vector2.Dot(sampleUvs[found[b]], along);
                        if (Mathf.Abs(tb - ta) < 1e-6f) {
                            continue;
                        }
                        float predicted = offset[a] + (offset[b] - offset[a]) * (t - ta) / (tb - ta);
                        agrees = Mathf.Abs(offset[i] - predicted) <= threshold;
                    }
                }
                reject[i] = !agrees;
            }
            for (int i = n - 1; i >= 0; i--) {
                if (reject[i]) {
                    found.RemoveAt(i);
                    raw.RemoveAt(i);
                    offset.RemoveAt(i);
                    margin.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// The observation to score a border sample with: the ideal point under <paramref name="target"/> moved
        /// only across the edge (by the border point's distance along the edge normal), because the position
        /// along the border is not observable.
        /// </summary>
        public static Vector2 AcrossOnly(PMSDKHomography target, Vector2 uv, Vector2 borderPoint)
        {
            Vector2 along = uv.x <= 0f || uv.x >= 1f ? Vector2.up : Vector2.right;
            Vector2 ideal = target.Apply(uv);
            Vector2 tangent = target.Apply(uv + along * 0.01f) - target.Apply(uv - along * 0.01f);
            if (tangent.sqrMagnitude < 1e-12f) {
                return borderPoint;
            }
            Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized;
            return ideal + normal * Vector2.Dot(borderPoint - ideal, normal);
        }

        /// <summary>
        /// Camera positions of the skin's four corners (content BL, BR, TR, TL), each where the lines through
        /// the two border points nearest to it on its two edges meet. False when a corner lacks samples.
        /// </summary>
        public static bool TryCornerQuad(List<Vector2> borderUvs, List<Vector2> borderPoints, out Vector2[] quad)
        {
            quad = new Vector2[4];
            Vector2[] corners = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            for (int c = 0; c < 4; c++) {
                Vector2 a0;
                Vector2 a1;
                Vector2 b0;
                Vector2 b1;
                // Horizontal edge (v fixed) and vertical edge (u fixed) through this corner.
                if (!TwoNearest(borderUvs, borderPoints, corners[c], true, out a0, out a1) ||
                    !TwoNearest(borderUvs, borderPoints, corners[c], false, out b0, out b1)) {
                    return false;
                }
                Vector2 p;
                if (!Intersect(a0, a1, b0, b1, out p)) {
                    return false;
                }
                quad[c] = p;
            }
            return true;
        }

        private static bool TwoNearest(List<Vector2> uvs, List<Vector2> points, Vector2 corner, bool horizontalEdge, out Vector2 nearest,
            out Vector2 second)
        {
            nearest = Vector2.zero;
            second = Vector2.zero;
            float best = float.PositiveInfinity;
            float next = float.PositiveInfinity;
            for (int i = 0; i < uvs.Count; i++) {
                Vector2 uv = uvs[i];
                bool onEdge = horizontalEdge ? Mathf.Abs(uv.y - corner.y) < 1e-4f && uv.x > 0f && uv.x < 1f
                                             : Mathf.Abs(uv.x - corner.x) < 1e-4f && uv.y > 0f && uv.y < 1f;
                if (!onEdge) {
                    continue;
                }
                float d = horizontalEdge ? Mathf.Abs(uv.x - corner.x) : Mathf.Abs(uv.y - corner.y);
                if (d < best) {
                    next = best;
                    second = nearest;
                    best = d;
                    nearest = points[i];
                } else if (d < next) {
                    next = d;
                    second = points[i];
                }
            }
            return !float.IsPositiveInfinity(next) && (nearest - second).sqrMagnitude > 1e-6f;
        }

        private static bool Intersect(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, out Vector2 p)
        {
            p = Vector2.zero;
            Vector2 da = a1 - a0;
            Vector2 db = b1 - b0;
            float denom = da.x * db.y - da.y * db.x;
            if (Mathf.Abs(denom) < 1e-6f * da.magnitude * db.magnitude) {
                return false;
            }
            Vector2 w = b0 - a0;
            float t = (w.x * db.y - w.y * db.x) / denom;
            p = a0 + da * t;
            return true;
        }

        // Samples at the middle of each of `count` equal parts of the edge from `start` along `along`.
        private static void AddEdge(List<Vector2> uvs, List<Vector2> alongs, List<Vector2> inwards, int count, Vector2 start,
            Vector2 along, Vector2 inward)
        {
            for (int i = 0; i < count; i++) {
                uvs.Add(start + along * ((i + 0.5f) / count));
                alongs.Add(along);
                inwards.Add(inward);
            }
        }

        // Outermost strong bright (skin margin) to dark (outside) step along `outward` through `origin`, sub-pixel.
        private static bool TryFindFallingEdge(byte[] frame, int width, int height, Vector2 origin, Vector2 outward, float marginPx,
            float minContrast, out Vector2 crossing)
        {
            crossing = origin;
            // Not deeper inside than most of the white margin (the checker squares start there), but well outside.
            float sMin = -0.7f * marginPx;
            float sMax = Mathf.Max(2f * marginPx, 12f);
            int count = Mathf.FloorToInt((sMax - sMin) / StepPx) + 1;
            if (count < 8) {
                return false;
            }
            float[] profile = new float[count];
            for (int i = 0; i < count; i++) {
                float value;
                if (!TrySample(frame, width, height, origin + outward * (sMin + i * StepPx), out value)) {
                    return false;
                }
                profile[i] = value;
            }
            float[] drop = new float[count];
            float strongest = 0f;
            for (int i = 1; i + 1 < count; i++) {
                drop[i] = profile[i - 1] - profile[i + 1];
                strongest = Mathf.Max(strongest, drop[i]);
            }
            if (strongest <= 0f) {
                return false;
            }
            // Outside the skin is dark, so the outermost strong falling step is the border.
            int best = -1;
            for (int i = count - 2; i >= 1; i--) {
                if (drop[i] >= 0.6f * strongest && drop[i] >= drop[i - 1] && drop[i] >= drop[i + 1]) {
                    best = i;
                    break;
                }
            }
            if (best < 2 || best > count - 3) {
                return false;
            }
            int window = Mathf.Max(2, Mathf.RoundToInt(0.4f * marginPx / StepPx));
            float inside = Mean(profile, best - 2 - window, best - 2);
            float outside = Mean(profile, best + 2, best + 2 + window);
            if (float.IsNaN(inside) || float.IsNaN(outside) || inside - outside < minContrast) {
                return false;
            }
            float left = drop[best - 1];
            float right = drop[best + 1];
            float denom = left - 2f * drop[best] + right;
            float offset = Mathf.Abs(denom) > 1e-6f ? Mathf.Clamp(0.5f * (left - right) / denom, -0.5f, 0.5f) : 0f;
            crossing = origin + outward * (sMin + (best + offset) * StepPx);
            return true;
        }

        private static float Mean(float[] values, int from, int to)
        {
            from = Mathf.Max(0, from);
            to = Mathf.Min(values.Length - 1, to);
            if (to - from < 1) {
                return float.NaN;
            }
            float sum = 0f;
            for (int i = from; i <= to; i++) {
                sum += values[i];
            }
            return sum / (to - from + 1);
        }

        // Bilinear grey value with pixel centres at integer coordinates (OpenCV convention).
        private static bool TrySample(byte[] frame, int width, int height, Vector2 p, out float value)
        {
            value = 0f;
            if (p.x < 0f || p.y < 0f || p.x > width - 1 || p.y > height - 1) {
                return false;
            }
            int x0 = Mathf.Min((int)p.x, width - 2);
            int y0 = Mathf.Min((int)p.y, height - 2);
            float tx = p.x - x0;
            float ty = p.y - y0;
            int i = y0 * width + x0;
            float top = frame[i] + (frame[i + 1] - frame[i]) * tx;
            float bottom = frame[i + width] + (frame[i + width + 1] - frame[i + width]) * tx;
            value = top + (bottom - top) * ty;
            return true;
        }
    }
}
