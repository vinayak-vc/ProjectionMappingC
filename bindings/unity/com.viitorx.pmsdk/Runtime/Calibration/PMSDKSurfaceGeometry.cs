using System;
using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Piecewise-planar surface measured by a calibrated depth camera (D-051): every camera pixel of the
    /// surface is assigned to one detected plane (facet), and the facets are unfolded about their shared fold
    /// lines into one flat millimetre frame. Distances measured in that frame are distances ALONG the
    /// physical surface, so equal steps there are equal physical grid spacing on every facet, folds included.
    ///
    /// Camera space is the OAK convention used by <see cref="PMSDKAutoAdjustNative.TryDetectPlanes"/>: x right,
    /// y down, z forward, millimetres, plane normal . X + d = 0. Rays come from lens-undistorted pixels, so the
    /// camera's lens distortion never reaches the surface coordinates. The unfolded frame's x axis is the camera's
    /// x axis projected onto the root (largest) facet, y points up as seen by the camera.
    /// </summary>
    public sealed class PMSDKSurfaceGeometry
    {
        // A pixel outside every plane outline takes a plane whose outline lies at most this far away.
        private const int DefaultReachPx = 40;
        // Facets with fewer labelled pixels inside the region are ignored (noise planes, background).
        private const float MinRegionShare = 0.01f;
        // Two facets are neighbours when this many pixel pairs touch across their labels.
        private const int MinTouchingPairs = 20;
        // Normals closer than this (radians) are treated as parallel: no fold line, no rotation.
        private const float ParallelEpsilon = 1e-3f;
        // Box filter (pixels) applied to measured depth before it is used for positions (removes Gray-code steps).
        private const int PointDepthSmoothing = 9;
        // A pixel this far (mm) from its 3x3 median is a decode spike; real relief is a few mm, spikes 100+ mm.
        private const float SpikeToleranceMm = 30f;
        // Measured points farther than this from their facet's plane belong to another surface (real relief ~ +-6 mm, noise ~3.5 mm).
        private const float OffSurfaceToleranceMm = 15f;
        // At least this share of the surface region must lie on measured facets.
        private const float MinRegionCoverage = 0.6f;

        private readonly List<PMSDKDetectedPlane> planes;
        private readonly int[] labels;
        private readonly int[][] reach;
        private readonly Quaternion[] rotations;
        private readonly Vector3[] pivots;
        private readonly Vector3[] offsets;
        private readonly int[] parents;
        private readonly bool[,] adjacency;
        private readonly PMSDKCameraIntrinsics intrinsics;
        // Smoothed measured depth (mm, 0 = none) when positions come from measured points instead of ideal planes.
        private float[] pointDepth;
        private Vector3 origin;
        private Vector3 axisX;
        private Vector3 axisY;

        public int Width { get; private set; }

        public int Height { get; private set; }

        /// <summary>Facets in use (subset of the detected planes, in detection order).</summary>
        public IList<PMSDKDetectedPlane> Planes
        {
            get { return planes.AsReadOnly(); }
        }

        /// <summary>Index (into <see cref="Planes"/>) of the facet the unfolding starts from.</summary>
        public int RootPlane { get; private set; }

        /// <summary>Share of the region's pixels (all pixels without a region) assigned to a facet.</summary>
        public float RegionCoverage { get; private set; }

        private PMSDKSurfaceGeometry(List<PMSDKDetectedPlane> planes, PMSDKCameraIntrinsics intrinsics, int width, int height)
        {
            this.planes = planes;
            this.intrinsics = intrinsics;
            Width = width;
            Height = height;
            labels = new int[width * height];
            reach = new int[planes.Count][];
            rotations = new Quaternion[planes.Count];
            pivots = new Vector3[planes.Count];
            offsets = new Vector3[planes.Count];
            parents = new int[planes.Count];
            adjacency = new bool[planes.Count, planes.Count];
        }

        /// <summary>
        /// Labels every camera pixel with a facet and unfolds the facets.
        /// </summary>
        /// <param name="detected">Planes from <see cref="PMSDKAutoAdjustNative.TryDetectPlanes"/>.</param>
        /// <param name="depthMm">Depth aligned to the camera image (mm, 0 = none); decides between facets near folds. May be null.</param>
        /// <param name="region">Optional surface region (camera size, row 0 = top, &gt; 127 = surface); only facets inside it are used.</param>
        public static bool TryBuild(List<PMSDKDetectedPlane> detected, PMSDKCameraIntrinsics intrinsics, int width, int height, ushort[] depthMm,
            byte[] region, out PMSDKSurfaceGeometry geometry, out string error)
        {
            return TryBuild(detected, intrinsics, width, height, depthMm, region, false, out geometry, out error);
        }

        /// <param name="useMeasuredPoints">
        /// Place every pixel at its measured 3D point (smoothed <paramref name="depthMm"/>) instead of where its ray meets
        /// the ideal facet plane (D-052). Use it only with precise dense depth (projector triangulation): real surfaces
        /// bulge a few mm, and the ray-plane point then slides sideways by bulge x tan(view angle), differently for every
        /// camera position. Planes still define the facets and the unfolding.
        /// </param>
        public static bool TryBuild(List<PMSDKDetectedPlane> detected, PMSDKCameraIntrinsics intrinsics, int width, int height, ushort[] depthMm,
            byte[] region, bool useMeasuredPoints, out PMSDKSurfaceGeometry geometry, out string error)
        {
            geometry = null;
            if (detected == null || detected.Count == 0) {
                error = "No planes were found in the depth image.";
                return false;
            }
            if (intrinsics == null || intrinsics.Fx <= 0f || intrinsics.Fy <= 0f) {
                error = "Camera intrinsics are required to measure the surface.";
                return false;
            }
            if (width <= 0 || height <= 0 || (region != null && region.Length != width * height) ||
                (depthMm != null && depthMm.Length < width * height)) {
                error = "Region or depth image size does not match the camera.";
                return false;
            }

            List<PMSDKDetectedPlane> used = SelectPlanes(detected, width, height, region);
            if (used.Count == 0) {
                error = "None of the " + detected.Count + " detected planes overlaps the surface region.";
                return false;
            }
            PMSDKSurfaceGeometry result = new PMSDKSurfaceGeometry(used, intrinsics, width, height);
            result.Label(depthMm, region);
            result.RegionCoverage = result.Coverage(region);
            if (region != null && result.RegionCoverage < MinRegionCoverage) {
                // Most of the surface lies on no measured facet: the depth cannot resolve this surface (too far, too
                // little texture, folds below the depth resolution). Refuse instead of flattening it onto wrong planes.
                error = "Only " + (result.RegionCoverage * 100f).ToString("0") + " % of the surface lies on the " + used.Count +
                        " facet(s) found in the depth image (need " + (MinRegionCoverage * 100f).ToString("0") +
                        " %): the depth camera cannot resolve this surface's facets. Move the camera closer or use projector-camera triangulation.";
                return false;
            }
            result.FindNeighbours(region);
            if (useMeasuredPoints && depthMm != null) {
                result.pointDepth = SmoothDepth(depthMm, width, height, PointDepthSmoothing);
            }
            if (!result.Unfold(out error)) {
                return false;
            }
            geometry = result;
            error = "";
            return true;
        }

        /// <summary>Facet of a raw camera pixel (-1 = none).</summary>
        public int LabelAt(Vector2 rawPixel)
        {
            int x = Mathf.RoundToInt(rawPixel.x);
            int y = Mathf.RoundToInt(rawPixel.y);
            if (x < 0 || y < 0 || x >= Width || y >= Height) {
                return -1;
            }
            return labels[y * Width + x];
        }

        /// <summary>Camera-pixel distance from <paramref name="rawPixel"/> to facet <paramref name="plane"/>'s outline
        /// (0 inside; int.MaxValue when farther than the search reach).</summary>
        public int DistanceToPlane(int plane, Vector2 rawPixel)
        {
            int x = Mathf.RoundToInt(rawPixel.x);
            int y = Mathf.RoundToInt(rawPixel.y);
            if (plane < 0 || plane >= planes.Count || x < 0 || y < 0 || x >= Width || y >= Height) {
                return int.MaxValue;
            }
            return reach[plane][y * Width + x];
        }

        public bool AreNeighbours(int a, int b)
        {
            return a >= 0 && b >= 0 && a < planes.Count && b < planes.Count && adjacency[a, b];
        }

        /// <summary>Facet the unfolding reached <paramref name="plane"/> from (-1 for the root or a facet not reached).</summary>
        public int ParentOf(int plane)
        {
            return parents[plane];
        }

        /// <summary>3D point (camera space, mm) where the ray of an undistorted pixel meets <paramref name="plane"/>.</summary>
        public bool TryIntersect(int plane, Vector2 undistortedPixel, out Vector3 point)
        {
            Vector3 ray = new Vector3((undistortedPixel.x - intrinsics.Cx) / intrinsics.Fx, (undistortedPixel.y - intrinsics.Cy) / intrinsics.Fy, 1f);
            PMSDKDetectedPlane p = planes[plane];
            float denominator = Vector3.Dot(p.Normal, ray);
            if (Mathf.Abs(denominator) < 1e-6f) {
                point = Vector3.zero;
                return false;
            }
            float t = -p.D / denominator;
            point = ray * t;
            return t > 0f;
        }

        /// <summary>Unfolded surface position (mm) of a camera pixel, or false when it lies on no facet.</summary>
        public bool TryToSurface(Vector2 rawPixel, Vector2 undistortedPixel, out Vector2 surfaceMm, out int plane)
        {
            surfaceMm = Vector2.zero;
            plane = LabelAt(rawPixel);
            if (plane < 0 || parents[plane] == -2) {
                return false;
            }
            Vector3 point;
            if (pointDepth != null) {
                // Measured points: a pixel far off its facet's plane lies on another surface (a side face, or depth
                // smoothed across an edge) and is not part of this surface.
                PMSDKDetectedPlane facet = planes[plane];
                if (!TryMeasuredPoint(rawPixel, undistortedPixel, out point) ||
                    Mathf.Abs(Vector3.Dot(facet.Normal, point) + facet.D) > OffSurfaceToleranceMm) {
                    return false;
                }
            } else if (!TryIntersect(plane, undistortedPixel, out point)) {
                return false;
            }
            surfaceMm = Flatten(plane, point);
            return true;
        }

        /// <summary>Measured 3D point of a pixel (smoothed depth along its undistorted ray), when measured points are in use.</summary>
        private bool TryMeasuredPoint(Vector2 rawPixel, Vector2 undistortedPixel, out Vector3 point)
        {
            point = Vector3.zero;
            if (pointDepth == null) {
                return false;
            }
            int x = Mathf.RoundToInt(rawPixel.x);
            int y = Mathf.RoundToInt(rawPixel.y);
            if (x < 0 || y < 0 || x >= Width || y >= Height) {
                return false;
            }
            float z = pointDepth[y * Width + x];
            if (z <= 0f) {
                return false;
            }
            point = new Vector3((undistortedPixel.x - intrinsics.Cx) / intrinsics.Fx * z, (undistortedPixel.y - intrinsics.Cy) / intrinsics.Fy * z, z);
            return true;
        }

        /// <summary>
        /// Box-filtered depth over valid pixels only (0 stays 0), robust to decode spikes: a few percent of
        /// triangulated pixels are hundreds of mm off (wrong Gray-code bits), and a plain mean lets each one drag its
        /// whole window by tens of mm. Spikes are replaced by their 3x3 median first (replaced, not dropped, so the
        /// window stays symmetric on steeply inclined surfaces), then the box mean averages away quantisation.
        /// </summary>
        public static float[] SmoothDepth(ushort[] depth, int width, int height, int size)
        {
            ushort[] median = Median3x3(depth, width, height);
            ushort[] despiked = new ushort[width * height];
            for (int i = 0; i < despiked.Length; i++) {
                despiked[i] = Mathf.Abs(depth[i] - median[i]) > SpikeToleranceMm ? median[i] : depth[i];
            }
            return BoxMean(despiked, width, height, size);
        }

        /// <summary>Median of the valid pixels in each 3x3 window (0 where the pixel itself is invalid).</summary>
        private static ushort[] Median3x3(ushort[] depth, int width, int height)
        {
            ushort[] result = new ushort[width * height];
            ushort[] window = new ushort[9];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    if (depth[y * width + x] == 0) {
                        continue;
                    }
                    int n = 0;
                    for (int wy = Mathf.Max(0, y - 1); wy <= Mathf.Min(height - 1, y + 1); wy++) {
                        for (int wx = Mathf.Max(0, x - 1); wx <= Mathf.Min(width - 1, x + 1); wx++) {
                            ushort z = depth[wy * width + wx];
                            if (z == 0) {
                                continue;
                            }
                            int k = n++;
                            while (k > 0 && window[k - 1] > z) {
                                window[k] = window[k - 1];
                                k--;
                            }
                            window[k] = z;
                        }
                    }
                    result[y * width + x] = window[n / 2];
                }
            }
            return result;
        }

        /// <summary>Box mean of valid pixels (0 stays 0); integral images, O(pixels).</summary>
        private static float[] BoxMean(ushort[] depth, int width, int height, int size)
        {
            int half = Mathf.Max(0, size / 2);
            double[] sum = new double[(width + 1) * (height + 1)];
            int[] count = new int[(width + 1) * (height + 1)];
            for (int y = 0; y < height; y++) {
                double rowSum = 0.0;
                int rowCount = 0;
                for (int x = 0; x < width; x++) {
                    ushort z = depth[y * width + x];
                    if (z > 0) {
                        rowSum += z;
                        rowCount++;
                    }
                    int i = (y + 1) * (width + 1) + x + 1;
                    sum[i] = sum[i - (width + 1)] + rowSum;
                    count[i] = count[i - (width + 1)] + rowCount;
                }
            }
            float[] result = new float[width * height];
            for (int y = 0; y < height; y++) {
                int y0 = Mathf.Max(0, y - half);
                int y1 = Mathf.Min(height, y + half + 1);
                for (int x = 0; x < width; x++) {
                    if (depth[y * width + x] == 0) {
                        continue;
                    }
                    int x0 = Mathf.Max(0, x - half);
                    int x1 = Mathf.Min(width, x + half + 1);
                    int a = y0 * (width + 1) + x0;
                    int b = y0 * (width + 1) + x1;
                    int c = y1 * (width + 1) + x0;
                    int d = y1 * (width + 1) + x1;
                    int n = count[d] - count[b] - count[c] + count[a];
                    if (n > 0) {
                        result[y * width + x] = (float)((sum[d] - sum[b] - sum[c] + sum[a]) / n);
                    }
                }
            }
            return result;
        }

        /// <summary>Unfolded position (mm) of a 3D point lying on facet <paramref name="plane"/>.</summary>
        public Vector2 Flatten(int plane, Vector3 point)
        {
            Vector3 unfolded = ToRootPlane(plane, point);
            Vector3 relative = unfolded - origin;
            return new Vector2(Vector3.Dot(relative, axisX), Vector3.Dot(relative, axisY));
        }

        /// <summary>Applies the facet's chain of fold rotations, landing the point in the root facet's plane.</summary>
        private Vector3 ToRootPlane(int plane, Vector3 point)
        {
            Vector3 p = point;
            int current = plane;
            while (current >= 0 && parents[current] >= 0) {
                p = pivots[current] + rotations[current] * (p - pivots[current]) + offsets[current];
                current = parents[current];
            }
            return p;
        }

        private static List<PMSDKDetectedPlane> SelectPlanes(List<PMSDKDetectedPlane> detected, int width, int height, byte[] region)
        {
            if (region == null) {
                return new List<PMSDKDetectedPlane>(detected);
            }
            int regionPixels = 0;
            for (int i = 0; i < region.Length; i++) {
                if (region[i] > 127) {
                    regionPixels++;
                }
            }
            List<PMSDKDetectedPlane> used = new List<PMSDKDetectedPlane>();
            foreach (PMSDKDetectedPlane plane in detected) {
                int inside = 0;
                ForEachPixelInPolygon(plane.Polygon, width, height, delegate(int index) {
                    if (region[index] > 127) {
                        inside++;
                    }
                });
                if (regionPixels > 0 && inside >= Mathf.Max(1, regionPixels * MinRegionShare)) {
                    used.Add(plane);
                }
            }
            return used;
        }

        private void Label(ushort[] depthMm, byte[] region)
        {
            int count = Width * Height;
            for (int k = 0; k < planes.Count; k++) {
                int[] distance = new int[count];
                for (int i = 0; i < count; i++) {
                    distance[i] = int.MaxValue;
                }
                Queue<int> queue = new Queue<int>();
                ForEachPixelInPolygon(planes[k].Polygon, Width, Height, delegate(int index) {
                    distance[index] = 0;
                    queue.Enqueue(index);
                });
                while (queue.Count > 0) {
                    int index = queue.Dequeue();
                    int next = distance[index] + 1;
                    if (next > DefaultReachPx) {
                        continue;
                    }
                    int x = index % Width;
                    int y = index / Width;
                    TryVisit(distance, queue, x - 1, y, next);
                    TryVisit(distance, queue, x + 1, y, next);
                    TryVisit(distance, queue, x, y - 1, next);
                    TryVisit(distance, queue, x, y + 1, next);
                }
                reach[k] = distance;
            }

            for (int i = 0; i < count; i++) {
                labels[i] = -1;
                if (region != null && region[i] <= 127) {
                    continue;
                }
                // Candidates: the facets whose outline contains the pixel; if none, every facet within reach.
                bool insideAny = false;
                for (int k = 0; k < planes.Count && !insideAny; k++) {
                    insideAny = reach[k][i] == 0;
                }
                bool useDepth = depthMm != null && depthMm[i] != 0;
                int best = -1;
                int bestDistance = int.MaxValue;
                float bestResidual = float.MaxValue;
                for (int k = 0; k < planes.Count; k++) {
                    int d = reach[k][i];
                    if (d == int.MaxValue || (insideAny && d != 0)) {
                        continue;
                    }
                    // Several candidates (overlapping outlines, or near a fold): the measured depth decides; without
                    // depth the nearest outline.
                    float residual = useDepth ? DepthResidual(k, i, depthMm) : float.NaN;
                    bool better;
                    if (useDepth && !float.IsNaN(residual)) {
                        better = residual < bestResidual;
                    } else {
                        better = d < bestDistance;
                    }
                    if (best < 0 || better) {
                        best = k;
                        bestDistance = d;
                        bestResidual = float.IsNaN(residual) ? float.MaxValue : residual;
                    }
                }
                labels[i] = best;
            }
        }

        /// <summary>|measured depth - facet depth| along the pixel's ray (NaN without a usable depth).</summary>
        private float DepthResidual(int plane, int index, ushort[] depthMm)
        {
            if (depthMm == null || depthMm[index] == 0) {
                return float.NaN;
            }
            // Raw pixel is close enough for choosing between neighbouring facets; exact rays are used for positions.
            Vector2 pixel = new Vector2(index % Width, index / Width);
            Vector3 point;
            if (!TryIntersect(plane, pixel, out point)) {
                return float.NaN;
            }
            return Mathf.Abs(point.z - depthMm[index]);
        }

        private float Coverage(byte[] region)
        {
            int total = 0;
            int labelled = 0;
            for (int i = 0; i < labels.Length; i++) {
                if (region != null && region[i] <= 127) {
                    continue;
                }
                total++;
                if (labels[i] >= 0) {
                    labelled++;
                }
            }
            return total > 0 ? (float)labelled / total : 0f;
        }

        private void TryVisit(int[] distance, Queue<int> queue, int x, int y, int value)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) {
                return;
            }
            int index = y * Width + x;
            if (distance[index] <= value) {
                return;
            }
            distance[index] = value;
            queue.Enqueue(index);
        }

        private void FindNeighbours(byte[] region)
        {
            int[,] touching = new int[planes.Count, planes.Count];
            for (int y = 0; y < Height; y++) {
                for (int x = 0; x < Width; x++) {
                    int a = labels[y * Width + x];
                    if (a < 0) {
                        continue;
                    }
                    if (x + 1 < Width) {
                        int b = labels[y * Width + x + 1];
                        if (b >= 0 && b != a) {
                            touching[a, b]++;
                            touching[b, a]++;
                        }
                    }
                    if (y + 1 < Height) {
                        int b = labels[(y + 1) * Width + x];
                        if (b >= 0 && b != a) {
                            touching[a, b]++;
                            touching[b, a]++;
                        }
                    }
                }
            }
            for (int a = 0; a < planes.Count; a++) {
                for (int b = 0; b < planes.Count; b++) {
                    adjacency[a, b] = a != b && touching[a, b] >= MinTouchingPairs;
                }
            }
        }

        private bool Unfold(out string error)
        {
            int[] pixelCount = new int[planes.Count];
            Vector3[] sum = new Vector3[planes.Count];
            for (int i = 0; i < labels.Length; i++) {
                int k = labels[i];
                if (k < 0) {
                    continue;
                }
                Vector3 point;
                if (TryIntersect(k, new Vector2(i % Width, i / Width), out point)) {
                    pixelCount[k]++;
                    sum[k] += point;
                }
            }
            int root = 0;
            for (int k = 1; k < planes.Count; k++) {
                if (pixelCount[k] > pixelCount[root]) {
                    root = k;
                }
            }
            if (pixelCount[root] == 0) {
                error = "No camera pixel lies on a detected plane.";
                return false;
            }
            RootPlane = root;

            for (int k = 0; k < planes.Count; k++) {
                parents[k] = -2; // not reached
                rotations[k] = Quaternion.identity;
            }
            parents[root] = -1;
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(root);
            while (queue.Count > 0) {
                int parent = queue.Dequeue();
                for (int child = 0; child < planes.Count; child++) {
                    if (parents[child] != -2 || !adjacency[parent, child]) {
                        continue;
                    }
                    parents[child] = parent;
                    SetFold(child, parent);
                    queue.Enqueue(child);
                }
            }

            PMSDKDetectedPlane rootPlane = planes[root];
            Vector3 centroid = sum[root] / pixelCount[root];
            origin = centroid - rootPlane.Normal * (Vector3.Dot(rootPlane.Normal, centroid) + rootPlane.D);
            axisX = Vector3.ProjectOnPlane(Vector3.right, rootPlane.Normal);
            if (axisX.sqrMagnitude < 1e-6f) {
                axisX = Vector3.ProjectOnPlane(Vector3.up, rootPlane.Normal);
            }
            axisX.Normalize();
            axisY = Vector3.Cross(rootPlane.Normal, axisX).normalized;
            // Camera y points down; the unfolded y axis points up as the camera sees it.
            if (Vector3.Dot(axisY, Vector3.down) < 0f) {
                axisY = -axisY;
            }
            error = "";
            return true;
        }

        /// <summary>Rotation about the fold line between <paramref name="child"/> and <paramref name="parent"/> that lays the
        /// child facet into the parent's plane (points on the fold stay put, so the unfolding is continuous).</summary>
        private void SetFold(int child, int parent)
        {
            PMSDKDetectedPlane c = planes[child];
            PMSDKDetectedPlane p = planes[parent];
            Vector3 direction = Vector3.Cross(c.Normal, p.Normal);
            float sine = direction.magnitude;
            if (sine < ParallelEpsilon) {
                // Parallel facets (a step): no fold line; move the child onto the parent's plane along the parent
                // normal. On the child, n_p . X = -d_c * s (s = +-1 for equal / opposite normals), so
                // X' = X - n_p (n_p . X + d_p) = X + n_p (d_c * s - d_p).
                float s = Mathf.Sign(Vector3.Dot(c.Normal, p.Normal));
                rotations[child] = Quaternion.identity;
                pivots[child] = Vector3.zero;
                offsets[child] = p.Normal * (c.D * s - p.D);
                return;
            }
            // Point on both planes: n . X = -d for each, closest to the origin.
            float c1 = -c.D;
            float c2 = -p.D;
            Vector3 pointOnLine = (Vector3.Cross(p.Normal, direction) * c1 + Vector3.Cross(direction, c.Normal) * c2) / (sine * sine);
            rotations[child] = Quaternion.FromToRotation(c.Normal, p.Normal);
            pivots[child] = pointOnLine;
            offsets[child] = Vector3.zero;
        }

        private static void ForEachPixelInPolygon(Vector2[] polygon, int width, int height, Action<int> visit)
        {
            if (polygon == null || polygon.Length < 3) {
                return;
            }
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (Vector2 v in polygon) {
                minY = Mathf.Min(minY, v.y);
                maxY = Mathf.Max(maxY, v.y);
            }
            int y0 = Mathf.Clamp(Mathf.CeilToInt(minY), 0, height - 1);
            int y1 = Mathf.Clamp(Mathf.FloorToInt(maxY), 0, height - 1);
            List<float> crossings = new List<float>();
            for (int y = y0; y <= y1; y++) {
                crossings.Clear();
                float sy = y;
                for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++) {
                    Vector2 a = polygon[i];
                    Vector2 b = polygon[j];
                    if ((a.y > sy) != (b.y > sy)) {
                        crossings.Add(a.x + (sy - a.y) * (b.x - a.x) / (b.y - a.y));
                    }
                }
                crossings.Sort();
                for (int k = 0; k + 1 < crossings.Count; k += 2) {
                    int x0 = Mathf.Clamp(Mathf.CeilToInt(crossings[k]), 0, width - 1);
                    int x1 = Mathf.Clamp(Mathf.FloorToInt(crossings[k + 1]), 0, width - 1);
                    for (int x = x0; x <= x1; x++) {
                        visit(y * width + x);
                    }
                }
            }
        }
    }
}
