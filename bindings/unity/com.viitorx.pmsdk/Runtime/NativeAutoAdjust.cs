using System;
using System.Runtime.InteropServices;

namespace vxpmsdk
{
    /// <summary>Mirrors <c>pmsdk_autoadjust_checker_t</c> (PMSDK/C_API/AutoAdjustAPI.h).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_autoadjust_checker_t
    {
        public int squares_x;
        public int squares_y;
        public float margin;
    }

    /// <summary>Mirrors <c>pmsdk_autoadjust_intrinsics_t</c>. <c>distortion</c> holds exactly 14 floats.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_autoadjust_intrinsics_t
    {
        public int model;
        public float fx;
        public float fy;
        public float cx;
        public float cy;
        public int distortion_count;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
        public float[] distortion;
    }

    /// <summary>Mirrors <c>pmsdk_autoadjust_warp_t</c>. <c>grid_offsets</c> points at pinned
    /// <c>pmsdk_vec2_t</c> memory (or is zero when the grid is 0 x 0).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_autoadjust_warp_t
    {
        public pmsdk_vec2_t corner0;
        public pmsdk_vec2_t corner1;
        public pmsdk_vec2_t corner2;
        public pmsdk_vec2_t corner3;
        public int grid_columns;
        public int grid_rows;
        public IntPtr grid_offsets;
    }

    /// <summary>Mirrors <c>pmsdk_autoadjust_options_t</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_autoadjust_options_t
    {
        public float damping;
        public int grid_columns;
        public int grid_rows;
        public float smoothness;
        public int pin_corners;
        public float max_step;
        public int use_target_quad;
        public pmsdk_vec2_t target0;
        public pmsdk_vec2_t target1;
        public pmsdk_vec2_t target2;
        public pmsdk_vec2_t target3;
    }

    /// <summary>Mirrors <c>pmsdk_autoadjust_score_t</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_autoadjust_score_t
    {
        public int valid_count;
        public float rms_px;
        public float max_px;
    }

    /// <summary>Mirrors <c>pmsdk_autoadjust_facet_options_t</c> (Phase 2).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_autoadjust_facet_options_t
    {
        public float min_contrast;
        public float blur_sigma;
        public float edge_threshold;
        public int regrow_px;
        public float min_area_fraction;
        public float polygon_epsilon_px;
        public int max_facets;
    }

    /// <summary>Mirrors <c>pmsdk_autoadjust_warpmap_options_t</c> (Phase 3).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_autoadjust_warpmap_options_t
    {
        public int width;
        public int height;
        public float max_fill_distance_px;
    }

    /// <summary>Mirrors <c>pmsdk_autoadjust_plane_options_t</c> (Phase 4).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_autoadjust_plane_options_t
    {
        public int step;
        public float min_depth_mm;
        public float max_depth_mm;
        public float inlier_threshold_mm;
        public int iterations;
        public float min_inlier_fraction;
        public int max_planes;
        public float polygon_epsilon_px;
        public uint seed;
    }

    /// <summary>Mirrors <c>pmsdk_autoadjust_plane_info_t</c> (Phase 4).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_autoadjust_plane_info_t
    {
        public pmsdk_vec3_t normal;
        public float d;
        public int inlier_count;
        public float rms_mm;
        public float width_mm;
        public float height_mm;
        public float view_angle_deg;
        public pmsdk_vec2_t quad0;
        public pmsdk_vec2_t quad1;
        public pmsdk_vec2_t quad2;
        public pmsdk_vec2_t quad3;
    }

    /// <summary>P/Invoke declarations for the Auto Adjust C API (AutoAdjustAPI.h, D-044..D-047).</summary>
    public static class NativeAutoAdjust
    {
        private const string Lib = NativeBindings.LibraryName;

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_autoadjust_checker_init(ref pmsdk_autoadjust_checker_t checker);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_autoadjust_options_init(ref pmsdk_autoadjust_options_t options);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_autoadjust_get_last_error();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_autoadjust_checker_corners(ref pmsdk_autoadjust_checker_t checker,
            [Out] pmsdk_vec2_t[] output, UIntPtr capacity);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_autoadjust_render_checker(ref pmsdk_autoadjust_checker_t checker,
            int width, int height, int bottomRowFirst, [Out] byte[] output, UIntPtr capacity);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_autoadjust_detect(byte[] gray, int width, int height, int stride,
            ref pmsdk_autoadjust_checker_t checker, pmsdk_vec2_t[] predicted, float minContrast,
            [Out] pmsdk_vec2_t[] outCorners, [Out] byte[] outValid, UIntPtr capacity,
            out int outValidCount, out int outWholeBoard);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_autoadjust_undistort(pmsdk_vec2_t[] input, [Out] pmsdk_vec2_t[] output,
            UIntPtr count, ref pmsdk_autoadjust_intrinsics_t intrinsics);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_autoadjust_measure(pmsdk_vec2_t[] uvs, pmsdk_vec2_t[] camera, byte[] valid,
            UIntPtr count, ref pmsdk_autoadjust_options_t options, out pmsdk_autoadjust_score_t outScore);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_autoadjust_refine(ref pmsdk_autoadjust_warp_t warp, pmsdk_vec2_t[] uvs,
            pmsdk_vec2_t[] camera, byte[] valid, UIntPtr count, ref pmsdk_autoadjust_options_t options,
            out pmsdk_autoadjust_score_t outBefore, [Out] pmsdk_vec2_t[] outOffsets, UIntPtr capacity);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_autoadjust_evaluate_warp(ref pmsdk_autoadjust_warp_t warp, float u, float v,
            out pmsdk_vec2_t outPoint);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_autoadjust_evaluate_warp_many(ref pmsdk_autoadjust_warp_t warp, pmsdk_vec2_t[] uvs,
            UIntPtr count, [Out] pmsdk_vec2_t[] outPoints);

        // ---- Phase 2: facets ----

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_autoadjust_facet_options_init(ref pmsdk_autoadjust_facet_options_t options);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_autoadjust_segment_facets(byte[] white, byte[] black, int width, int height, int stride,
            ref pmsdk_autoadjust_facet_options_t options);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_autoadjust_facets_destroy(IntPtr facets);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_autoadjust_facets_count(IntPtr facets);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_autoadjust_facets_get(IntPtr facets, UIntPtr index, [Out] pmsdk_vec2_t[] outQuad4,
            out int outFromOutline, out float outAreaPx, out float outMeanContrast);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_autoadjust_facets_copy_polygon(IntPtr facets, UIntPtr index, [Out] pmsdk_vec2_t[] output,
            UIntPtr capacity);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pmsdk_autoadjust_facets_find_containing(IntPtr facets, pmsdk_vec2_t[] points, byte[] valid, UIntPtr count);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_autoadjust_solve_corners(ref pmsdk_autoadjust_warp_t warp, pmsdk_vec2_t[] uvs,
            pmsdk_vec2_t[] camera, byte[] valid, UIntPtr count, pmsdk_vec2_t[] targetQuad4, [Out] pmsdk_vec2_t[] outCorners4);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern float pmsdk_autoadjust_metric_scale(pmsdk_vec2_t[] quad4, float widthMm, float heightMm);

        // ---- Phase 3: warp map ----

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_autoadjust_warpmap_options_init(ref pmsdk_autoadjust_warpmap_options_t options);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_autoadjust_build_warp_map(pmsdk_vec2_t[] raster, pmsdk_vec2_t[] uv, UIntPtr count,
            float rectX, float rectY, float rectWidth, float rectHeight, ref pmsdk_autoadjust_warpmap_options_t options,
            [Out] float[] outRgba, UIntPtr capacityFloats, out float outCoverage);

        // ---- Phase 4: planes ----

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_autoadjust_plane_options_init(ref pmsdk_autoadjust_plane_options_t options);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_autoadjust_detect_planes(ushort[] depthMm, int width, int height, int stride,
            ref pmsdk_autoadjust_intrinsics_t intrinsics, ref pmsdk_autoadjust_plane_options_t options);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_autoadjust_planes_destroy(IntPtr planes);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_autoadjust_planes_count(IntPtr planes);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_autoadjust_planes_get(IntPtr planes, UIntPtr index, out pmsdk_autoadjust_plane_info_t outInfo);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_autoadjust_planes_copy_polygon(IntPtr planes, UIntPtr index, [Out] pmsdk_vec2_t[] output,
            UIntPtr capacity);

        /// <summary>The calling thread's last Auto Adjust error message ("" when none).</summary>
        public static string LastError()
        {
            IntPtr message = pmsdk_autoadjust_get_last_error();
            return message == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(message);
        }
    }
}
