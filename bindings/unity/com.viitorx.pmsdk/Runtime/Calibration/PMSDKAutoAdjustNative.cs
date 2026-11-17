using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>Residual score of a checker detection, in undistorted camera pixels.</summary>
    public struct PMSDKAutoAdjustScore
    {
        public int ValidCount;
        public float RmsPx;
        public float MaxPx;
    }

    /// <summary>A flat facet found in a white/black capture (camera pixels, Phase 2).</summary>
    public sealed class PMSDKFacet
    {
        /// <summary>Content bottom-left, bottom-right, top-right, top-left as seen by an upright camera.</summary>
        public Vector2[] Quad = new Vector2[4];
        public Vector2[] Polygon = new Vector2[0];
        public bool QuadFromOutline;
        public float AreaPx;
        public float MeanContrast;
    }

    /// <summary>A plane found in a depth image (Phase 4). Normal in camera space (x right, y down, z forward).</summary>
    public sealed class PMSDKDetectedPlane
    {
        public Vector3 Normal;
        public float D;
        public int InlierCount;
        public float RmsMm;
        public float WidthMm;
        public float HeightMm;
        public float ViewAngleDeg;
        /// <summary>Image quad (BL, BR, TR, TL as seen by an upright camera), colour-camera pixels.</summary>
        public Vector2[] Quad = new Vector2[4];
        public Vector2[] Polygon = new Vector2[0];
    }

    /// <summary>Checker layout projected during Auto Adjust (see pmsdk::AutoAdjust::CheckerSpec).</summary>
    public struct PMSDKCheckerSpec
    {
        public int SquaresX;
        public int SquaresY;
        public float Margin;

        public static PMSDKCheckerSpec Default()
        {
            return new PMSDKCheckerSpec { SquaresX = 10, SquaresY = 7, Margin = 0.08f };
        }
    }

    /// <summary>Refinement tunables (see pmsdk::AutoAdjust::RefineOptions).</summary>
    public struct PMSDKRefineSettings
    {
        public float Damping;
        public int GridColumns;
        public int GridRows;
        public float Smoothness;
        public bool PinCorners;
        public float MaxStep;
        /// <summary>When true, <see cref="TargetQuad"/> (camera px, content BL, BR, TR, TL) defines "correct".</summary>
        public bool UseTargetQuad;
        public Vector2[] TargetQuad;

        public static PMSDKRefineSettings Default()
        {
            return new PMSDKRefineSettings {
                Damping = 0.8f,
                GridColumns = 9,
                GridRows = 9,
                Smoothness = 0.05f,
                PinCorners = true,
                MaxStep = 0.05f,
            };
        }
    }

    /// <summary>
    /// Managed wrapper over the native Auto Adjust C API (D-044). All image processing and maths
    /// run in ProjectionMappingSDK.dll; this converts between Unity types and the C structs.
    /// Coordinates: content UV and raster are y-up [0,1]; camera points are pixels, top-left origin.
    /// </summary>
    public static class PMSDKAutoAdjustNative
    {
        /// <summary>True when the loaded ProjectionMappingSDK.dll has the Auto Adjust exports.</summary>
        public static bool IsAvailable()
        {
            try {
                pmsdk_autoadjust_checker_t checker = new pmsdk_autoadjust_checker_t();
                NativeAutoAdjust.pmsdk_autoadjust_checker_init(ref checker);
                return checker.squares_x > 0;
            } catch (DllNotFoundException) {
                return false;
            } catch (EntryPointNotFoundException) {
                return false;
            }
        }

        /// <summary>Content UV of the checker's inner corners (row-major from the top-left), or null when the spec is invalid.</summary>
        public static Vector2[] CheckerCornerUVs(PMSDKCheckerSpec spec, out string error)
        {
            pmsdk_autoadjust_checker_t checker = ToNative(spec);
            int count = (int)NativeAutoAdjust.pmsdk_autoadjust_checker_corners(ref checker, null, UIntPtr.Zero);
            if (count <= 0) {
                error = NativeAutoAdjust.LastError();
                return null;
            }
            pmsdk_vec2_t[] native = new pmsdk_vec2_t[count];
            NativeAutoAdjust.pmsdk_autoadjust_checker_corners(ref checker, native, (UIntPtr)count);
            error = "";
            return ToVectors(native);
        }

        /// <summary>Renders the checker into a new RGB24 texture (caller destroys it), or null on error.</summary>
        public static Texture2D RenderCheckerTexture(PMSDKCheckerSpec spec, int width, int height, out string error)
        {
            pmsdk_autoadjust_checker_t checker = ToNative(spec);
            byte[] gray = new byte[width * height];
            pmsdk_status_t status = NativeAutoAdjust.pmsdk_autoadjust_render_checker(ref checker, width, height, 1, gray, (UIntPtr)gray.Length);
            if (status != pmsdk_status_t.PMSDK_SUCCESS) {
                error = NativeAutoAdjust.LastError();
                return null;
            }
            byte[] rgb = new byte[width * height * 3];
            for (int i = 0; i < gray.Length; i++) {
                rgb[i * 3] = gray[i];
                rgb[i * 3 + 1] = gray[i];
                rgb[i * 3 + 2] = gray[i];
            }
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.name = "PMSDK_AutoAdjustChecker";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.LoadRawTextureData(rgb);
            texture.Apply(false);
            error = "";
            return texture;
        }

        /// <summary>Detects the checker in a top-left-origin luminance frame. Returns false and a message when not found.</summary>
        public static bool TryDetect(byte[] gray, int width, int height, PMSDKCheckerSpec spec, Vector2[] predicted,
            out Vector2[] corners, out byte[] valid, out int validCount, out bool wholeBoard, out string error)
        {
            pmsdk_autoadjust_checker_t checker = ToNative(spec);
            int count = (int)NativeAutoAdjust.pmsdk_autoadjust_checker_corners(ref checker, null, UIntPtr.Zero);
            corners = null;
            valid = null;
            validCount = 0;
            wholeBoard = false;
            if (count <= 0) {
                error = NativeAutoAdjust.LastError();
                return false;
            }
            if (gray == null || gray.Length < width * height) {
                error = "The camera frame is missing or smaller than its reported size.";
                return false;
            }
            pmsdk_vec2_t[] nativePredicted = null;
            if (predicted != null) {
                if (predicted.Length != count) {
                    error = "Predicted corner count does not match the checker.";
                    return false;
                }
                nativePredicted = ToNative(predicted);
            }
            pmsdk_vec2_t[] nativeCorners = new pmsdk_vec2_t[count];
            byte[] nativeValid = new byte[count];
            int outValid;
            int outWhole;
            pmsdk_status_t status = NativeAutoAdjust.pmsdk_autoadjust_detect(gray, width, height, width, ref checker, nativePredicted, 0f,
                nativeCorners, nativeValid, (UIntPtr)count, out outValid, out outWhole);
            if (status != pmsdk_status_t.PMSDK_SUCCESS) {
                error = NativeAutoAdjust.LastError();
                return false;
            }
            corners = ToVectors(nativeCorners);
            valid = nativeValid;
            validCount = outValid;
            wholeBoard = outWhole != 0;
            error = "";
            return true;
        }

        /// <summary>Removes lens distortion; returns the input unchanged when <paramref name="intrinsics"/> is null.</summary>
        public static bool TryUndistort(Vector2[] points, PMSDKCameraIntrinsics intrinsics, out Vector2[] undistorted, out string error)
        {
            undistorted = points;
            error = "";
            if (intrinsics == null || points == null || points.Length == 0) {
                return true;
            }
            pmsdk_autoadjust_intrinsics_t native = ToNative(intrinsics);
            pmsdk_vec2_t[] output = new pmsdk_vec2_t[points.Length];
            pmsdk_status_t status = NativeAutoAdjust.pmsdk_autoadjust_undistort(ToNative(points), output, (UIntPtr)points.Length, ref native);
            if (status != pmsdk_status_t.PMSDK_SUCCESS) {
                error = NativeAutoAdjust.LastError();
                return false;
            }
            undistorted = ToVectors(output);
            return true;
        }

        /// <summary>Scores undistorted corners against the ideal perspective grid.</summary>
        public static bool TryMeasure(Vector2[] uvs, Vector2[] camera, byte[] valid, PMSDKRefineSettings settings,
            out PMSDKAutoAdjustScore score, out string error)
        {
            score = new PMSDKAutoAdjustScore();
            pmsdk_autoadjust_options_t options = ToNative(settings);
            pmsdk_autoadjust_score_t native;
            pmsdk_status_t status = NativeAutoAdjust.pmsdk_autoadjust_measure(ToNative(uvs), ToNative(camera), valid,
                (UIntPtr)uvs.Length, ref options, out native);
            if (status != pmsdk_status_t.PMSDK_SUCCESS) {
                error = NativeAutoAdjust.LastError();
                return false;
            }
            score = ToScore(native);
            error = "";
            return true;
        }

        /// <summary>
        /// One refinement step for a quad skin. <paramref name="corners"/> are BL, BR, TR, TL raster
        /// corners; the current correction may be empty (0 x 0). Returns the score before the step
        /// and the new correction grid (settings.GridColumns x settings.GridRows).
        /// </summary>
        public static bool TryRefine(IList<Vector2> corners, int correctionColumns, int correctionRows, IList<Vector2> correctionOffsets,
            Vector2[] uvs, Vector2[] camera, byte[] valid, PMSDKRefineSettings settings,
            out PMSDKAutoAdjustScore before, out Vector2[] newOffsets, out string error)
        {
            before = new PMSDKAutoAdjustScore();
            newOffsets = null;
            pmsdk_autoadjust_options_t options = ToNative(settings);
            pmsdk_vec2_t[] output = new pmsdk_vec2_t[Mathf.Max(0, settings.GridColumns * settings.GridRows)];
            pmsdk_autoadjust_score_t native;
            pmsdk_status_t status;
            using (PinnedWarp warp = new PinnedWarp(corners, correctionColumns, correctionRows, correctionOffsets)) {
                pmsdk_autoadjust_warp_t nativeWarp = warp.Value;
                status = NativeAutoAdjust.pmsdk_autoadjust_refine(ref nativeWarp, ToNative(uvs), ToNative(camera), valid, (UIntPtr)uvs.Length,
                    ref options, out native, output, (UIntPtr)output.Length);
            }
            if (status != pmsdk_status_t.PMSDK_SUCCESS) {
                error = NativeAutoAdjust.LastError();
                return false;
            }
            before = ToScore(native);
            newOffsets = ToVectors(output);
            error = "";
            return true;
        }

        /// <summary>Raster position of content UV under a corner pin plus correction grid.</summary>
        public static Vector2 EvaluateWarp(IList<Vector2> corners, int correctionColumns, int correctionRows, IList<Vector2> correctionOffsets,
            float u, float v)
        {
            pmsdk_vec2_t point;
            using (PinnedWarp warp = new PinnedWarp(corners, correctionColumns, correctionRows, correctionOffsets)) {
                pmsdk_autoadjust_warp_t nativeWarp = warp.Value;
                NativeAutoAdjust.pmsdk_autoadjust_evaluate_warp(ref nativeWarp, u, v, out point);
            }
            return new Vector2(point.x, point.y);
        }

        /// <summary>Raster positions of many content UVs under a corner pin plus correction grid.</summary>
        public static Vector2[] EvaluateWarpMany(IList<Vector2> corners, int correctionColumns, int correctionRows,
            IList<Vector2> correctionOffsets, Vector2[] uvs)
        {
            pmsdk_vec2_t[] output = new pmsdk_vec2_t[uvs.Length];
            pmsdk_status_t status;
            using (PinnedWarp warp = new PinnedWarp(corners, correctionColumns, correctionRows, correctionOffsets)) {
                pmsdk_autoadjust_warp_t nativeWarp = warp.Value;
                status = NativeAutoAdjust.pmsdk_autoadjust_evaluate_warp_many(ref nativeWarp, ToNative(uvs), (UIntPtr)uvs.Length, output);
            }
            if (status != pmsdk_status_t.PMSDK_SUCCESS) {
                throw new InvalidOperationException("evaluate_warp_many failed: " + NativeAutoAdjust.LastError());
            }
            return ToVectors(output);
        }

        // ------------------------------------------------------------------ Phase 2: facets

        /// <summary>
        /// Splits the lit area of a white capture minus a black capture (top-left origin luminance)
        /// into facets. When <paramref name="points"/> is given, <paramref name="containing"/> is the
        /// index of the facet holding most of the valid points (-1 if none).
        /// </summary>
        public static bool TrySegmentFacets(byte[] white, byte[] black, int width, int height, Vector2[] points, byte[] valid,
            out List<PMSDKFacet> facets, out int containing, out string error)
        {
            facets = new List<PMSDKFacet>();
            containing = -1;
            if (white == null || black == null || white.Length < width * height || black.Length < width * height) {
                error = "White and black captures are missing or too small.";
                return false;
            }
            pmsdk_autoadjust_facet_options_t options = new pmsdk_autoadjust_facet_options_t();
            NativeAutoAdjust.pmsdk_autoadjust_facet_options_init(ref options);
            IntPtr handle = NativeAutoAdjust.pmsdk_autoadjust_segment_facets(white, black, width, height, width, ref options);
            if (handle == IntPtr.Zero) {
                error = NativeAutoAdjust.LastError();
                return false;
            }
            try {
                int count = (int)NativeAutoAdjust.pmsdk_autoadjust_facets_count(handle);
                for (int i = 0; i < count; i++) {
                    pmsdk_vec2_t[] quad = new pmsdk_vec2_t[4];
                    int fromOutline;
                    float area;
                    float contrast;
                    NativeAutoAdjust.pmsdk_autoadjust_facets_get(handle, (UIntPtr)i, quad, out fromOutline, out area, out contrast);
                    int polygonCount = (int)NativeAutoAdjust.pmsdk_autoadjust_facets_copy_polygon(handle, (UIntPtr)i, null, UIntPtr.Zero);
                    pmsdk_vec2_t[] polygon = new pmsdk_vec2_t[polygonCount];
                    NativeAutoAdjust.pmsdk_autoadjust_facets_copy_polygon(handle, (UIntPtr)i, polygon, (UIntPtr)polygonCount);
                    facets.Add(new PMSDKFacet {
                        Quad = ToVectors(quad),
                        Polygon = ToVectors(polygon),
                        QuadFromOutline = fromOutline != 0,
                        AreaPx = area,
                        MeanContrast = contrast,
                    });
                }
                if (points != null && points.Length > 0) {
                    containing = NativeAutoAdjust.pmsdk_autoadjust_facets_find_containing(handle, ToNative(points), valid, (UIntPtr)points.Length);
                }
            } finally {
                NativeAutoAdjust.pmsdk_autoadjust_facets_destroy(handle);
            }
            error = "";
            return true;
        }

        /// <summary>New corner-pin corners (raster BL, BR, TR, TL) so the content corners land on <paramref name="targetQuad"/>.</summary>
        public static bool TrySolveCorners(IList<Vector2> corners, int correctionColumns, int correctionRows, IList<Vector2> correctionOffsets,
            Vector2[] uvs, Vector2[] camera, byte[] valid, Vector2[] targetQuad, out Vector2[] newCorners, out string error)
        {
            newCorners = null;
            if (targetQuad == null || targetQuad.Length != 4) {
                error = "The target quad needs 4 points.";
                return false;
            }
            pmsdk_vec2_t[] output = new pmsdk_vec2_t[4];
            pmsdk_status_t status;
            using (PinnedWarp warp = new PinnedWarp(corners, correctionColumns, correctionRows, correctionOffsets)) {
                pmsdk_autoadjust_warp_t nativeWarp = warp.Value;
                status = NativeAutoAdjust.pmsdk_autoadjust_solve_corners(ref nativeWarp, ToNative(uvs), ToNative(camera), valid,
                    (UIntPtr)uvs.Length, ToNative(targetQuad), output);
            }
            if (status != pmsdk_status_t.PMSDK_SUCCESS) {
                error = NativeAutoAdjust.LastError();
                return false;
            }
            newCorners = ToVectors(output);
            error = "";
            return true;
        }

        /// <summary>Millimetres per camera pixel on a facet of known size (0 when unknown).</summary>
        public static float MetricScale(Vector2[] quad, float widthMm, float heightMm)
        {
            if (quad == null || quad.Length != 4) {
                return 0f;
            }
            return NativeAutoAdjust.pmsdk_autoadjust_metric_scale(ToNative(quad), widthMm, heightMm);
        }

        // ------------------------------------------------------------------ Phase 3: warp map

        /// <summary>Raster -> content UV map (RGBA floats, row 0 at the rect's bottom) from scattered samples.</summary>
        public static bool TryBuildWarpMap(Vector2[] raster, Vector2[] uv, Rect rect, int width, int height, float maxFillDistancePx,
            out float[] rgba, out float coverage, out string error)
        {
            rgba = null;
            coverage = 0f;
            if (raster == null || uv == null || raster.Length != uv.Length) {
                error = "Raster and UV samples must have the same length.";
                return false;
            }
            pmsdk_autoadjust_warpmap_options_t options = new pmsdk_autoadjust_warpmap_options_t();
            NativeAutoAdjust.pmsdk_autoadjust_warpmap_options_init(ref options);
            options.width = width;
            options.height = height;
            options.max_fill_distance_px = maxFillDistancePx;
            float[] output = new float[Mathf.Max(0, width * height * 4)];
            pmsdk_status_t status = NativeAutoAdjust.pmsdk_autoadjust_build_warp_map(ToNative(raster), ToNative(uv), (UIntPtr)raster.Length,
                rect.x, rect.y, rect.width, rect.height, ref options, output, (UIntPtr)output.Length, out coverage);
            if (status != pmsdk_status_t.PMSDK_SUCCESS) {
                error = NativeAutoAdjust.LastError();
                return false;
            }
            rgba = output;
            error = "";
            return true;
        }

        // ------------------------------------------------------------------ Phase 4: planes

        /// <summary>Planes in a depth image (mm, 0 = none) taken with <paramref name="intrinsics"/> (lens distortion ignored).</summary>
        public static bool TryDetectPlanes(ushort[] depthMm, int width, int height, PMSDKCameraIntrinsics intrinsics,
            out List<PMSDKDetectedPlane> planes, out string error)
        {
            planes = new List<PMSDKDetectedPlane>();
            if (depthMm == null || depthMm.Length < width * height || intrinsics == null) {
                error = "Depth image or intrinsics missing.";
                return false;
            }
            pmsdk_autoadjust_plane_options_t options = new pmsdk_autoadjust_plane_options_t();
            NativeAutoAdjust.pmsdk_autoadjust_plane_options_init(ref options);
            pmsdk_autoadjust_intrinsics_t lens = ToNative(intrinsics);
            IntPtr handle = NativeAutoAdjust.pmsdk_autoadjust_detect_planes(depthMm, width, height, width, ref lens, ref options);
            if (handle == IntPtr.Zero) {
                error = NativeAutoAdjust.LastError();
                return false;
            }
            try {
                int count = (int)NativeAutoAdjust.pmsdk_autoadjust_planes_count(handle);
                for (int i = 0; i < count; i++) {
                    pmsdk_autoadjust_plane_info_t info;
                    NativeAutoAdjust.pmsdk_autoadjust_planes_get(handle, (UIntPtr)i, out info);
                    int polygonCount = (int)NativeAutoAdjust.pmsdk_autoadjust_planes_copy_polygon(handle, (UIntPtr)i, null, UIntPtr.Zero);
                    pmsdk_vec2_t[] polygon = new pmsdk_vec2_t[polygonCount];
                    NativeAutoAdjust.pmsdk_autoadjust_planes_copy_polygon(handle, (UIntPtr)i, polygon, (UIntPtr)polygonCount);
                    planes.Add(new PMSDKDetectedPlane {
                        Normal = new Vector3(info.normal.x, info.normal.y, info.normal.z),
                        D = info.d,
                        InlierCount = info.inlier_count,
                        RmsMm = info.rms_mm,
                        WidthMm = info.width_mm,
                        HeightMm = info.height_mm,
                        ViewAngleDeg = info.view_angle_deg,
                        Quad = new Vector2[] {
                            new Vector2(info.quad0.x, info.quad0.y), new Vector2(info.quad1.x, info.quad1.y),
                            new Vector2(info.quad2.x, info.quad2.y), new Vector2(info.quad3.x, info.quad3.y),
                        },
                        Polygon = ToVectors(polygon),
                    });
                }
            } finally {
                NativeAutoAdjust.pmsdk_autoadjust_planes_destroy(handle);
            }
            error = "";
            return true;
        }

        private sealed class PinnedWarp : IDisposable
        {
            private GCHandle handle;
            public pmsdk_autoadjust_warp_t Value;

            public PinnedWarp(IList<Vector2> corners, int columns, int rows, IList<Vector2> offsets)
            {
                if (corners == null || corners.Count < 4) {
                    throw new ArgumentException("A quad skin needs 4 corners.", nameof(corners));
                }
                Value = new pmsdk_autoadjust_warp_t {
                    corner0 = ToNative(corners[0]),
                    corner1 = ToNative(corners[1]),
                    corner2 = ToNative(corners[2]),
                    corner3 = ToNative(corners[3]),
                    grid_columns = 0,
                    grid_rows = 0,
                    grid_offsets = IntPtr.Zero,
                };
                if (columns >= 2 && rows >= 2 && offsets != null && offsets.Count == columns * rows) {
                    pmsdk_vec2_t[] native = new pmsdk_vec2_t[offsets.Count];
                    for (int i = 0; i < offsets.Count; i++) {
                        native[i] = ToNative(offsets[i]);
                    }
                    handle = GCHandle.Alloc(native, GCHandleType.Pinned);
                    Value.grid_columns = columns;
                    Value.grid_rows = rows;
                    Value.grid_offsets = handle.AddrOfPinnedObject();
                }
            }

            public void Dispose()
            {
                if (handle.IsAllocated) {
                    handle.Free();
                }
            }
        }

        private static pmsdk_autoadjust_intrinsics_t ToNative(PMSDKCameraIntrinsics intrinsics)
        {
            pmsdk_autoadjust_intrinsics_t native = new pmsdk_autoadjust_intrinsics_t {
                model = (int)intrinsics.Model,
                fx = intrinsics.Fx,
                fy = intrinsics.Fy,
                cx = intrinsics.Cx,
                cy = intrinsics.Cy,
                distortion = new float[14],
            };
            int count = intrinsics.Distortion != null ? Mathf.Min(intrinsics.Distortion.Length, 14) : 0;
            for (int i = 0; i < count; i++) {
                native.distortion[i] = intrinsics.Distortion[i];
            }
            native.distortion_count = count;
            return native;
        }

        private static pmsdk_autoadjust_checker_t ToNative(PMSDKCheckerSpec spec)
        {
            return new pmsdk_autoadjust_checker_t { squares_x = spec.SquaresX, squares_y = spec.SquaresY, margin = spec.Margin };
        }

        private static pmsdk_autoadjust_options_t ToNative(PMSDKRefineSettings settings)
        {
            pmsdk_autoadjust_options_t options = new pmsdk_autoadjust_options_t();
            NativeAutoAdjust.pmsdk_autoadjust_options_init(ref options);
            options.damping = settings.Damping;
            options.grid_columns = settings.GridColumns;
            options.grid_rows = settings.GridRows;
            options.smoothness = settings.Smoothness;
            options.pin_corners = settings.PinCorners ? 1 : 0;
            options.max_step = settings.MaxStep;
            options.use_target_quad = 0;
            if (settings.UseTargetQuad && settings.TargetQuad != null && settings.TargetQuad.Length == 4) {
                options.use_target_quad = 1;
                options.target0 = ToNative(settings.TargetQuad[0]);
                options.target1 = ToNative(settings.TargetQuad[1]);
                options.target2 = ToNative(settings.TargetQuad[2]);
                options.target3 = ToNative(settings.TargetQuad[3]);
            }
            return options;
        }

        private static pmsdk_vec2_t ToNative(Vector2 v)
        {
            return new pmsdk_vec2_t { x = v.x, y = v.y };
        }

        private static pmsdk_vec2_t[] ToNative(Vector2[] points)
        {
            pmsdk_vec2_t[] native = new pmsdk_vec2_t[points.Length];
            for (int i = 0; i < points.Length; i++) {
                native[i] = ToNative(points[i]);
            }
            return native;
        }

        private static Vector2[] ToVectors(pmsdk_vec2_t[] native)
        {
            Vector2[] points = new Vector2[native.Length];
            for (int i = 0; i < native.Length; i++) {
                points[i] = new Vector2(native[i].x, native[i].y);
            }
            return points;
        }

        private static PMSDKAutoAdjustScore ToScore(pmsdk_autoadjust_score_t native)
        {
            return new PMSDKAutoAdjustScore { ValidCount = native.valid_count, RmsPx = native.rms_px, MaxPx = native.max_px };
        }
    }
}
