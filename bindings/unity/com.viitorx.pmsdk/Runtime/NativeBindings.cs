using System;
using System.Runtime.InteropServices;

namespace vxpmsdk
{
    public enum pmsdk_status_t : int
    {
        PMSDK_SUCCESS = 0,
        PMSDK_ERROR_INVALID_ARGUMENT = 1,
        PMSDK_ERROR_OUT_OF_MEMORY = 2,
        PMSDK_ERROR_UNKNOWN = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_vec2_t
    {
        public float x, y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_vec3_t
    {
        public float x, y, z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_vec4_t
    {
        public float x, y, z, w;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_vertex_t
    {
        public pmsdk_vec3_t position;
        public pmsdk_vec3_t normal;
        public pmsdk_vec2_t uv;
        public pmsdk_vec4_t color;
    }

    public static class NativeBindings
    {
        public const string LibraryName = "ProjectionMappingSDK";

        // --- Mesh ---
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_mesh_create();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_mesh_destroy(IntPtr mesh);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_mesh_set_vertices(IntPtr mesh, pmsdk_vertex_t[] vertices, UIntPtr count);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_mesh_get_vertices(IntPtr mesh, [Out] pmsdk_vertex_t[] out_vertices, UIntPtr count);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_mesh_set_indices(IntPtr mesh, uint[] indices, UIntPtr count);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_mesh_get_indices(IntPtr mesh, [Out] uint[] out_indices, UIntPtr count);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_mesh_get_vertex_count(IntPtr mesh);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_mesh_get_index_count(IntPtr mesh);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_mesh_recalculate_normals(IntPtr mesh);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_mesh_clear(IntPtr mesh);

        // --- BlendConfig ---
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_blendconfig_create();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_blendconfig_destroy(IntPtr config);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_blendconfig_set_black_level(IntPtr config, float level);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float pmsdk_blendconfig_get_black_level(IntPtr config);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_blendconfig_get_left_edge(IntPtr config);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_blendconfig_get_right_edge(IntPtr config);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_blendconfig_get_top_edge(IntPtr config);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_blendconfig_get_bottom_edge(IntPtr config);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float pmsdk_blendconfig_evaluate(IntPtr config, float u, float v);

        // --- EdgeBlend ---
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_edgeblend_set_size(IntPtr edge, float size);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float pmsdk_edgeblend_get_size(IntPtr edge);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_edgeblend_set_gamma(IntPtr edge, float gamma);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float pmsdk_edgeblend_get_gamma(IntPtr edge);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_edgeblend_set_curve_type(IntPtr edge, int type);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pmsdk_edgeblend_get_curve_type(IntPtr edge);


        // WarpAPI.h
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_projector_create();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_projector_destroy(IntPtr projector);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_projector_set_aspect_ratio(IntPtr projector, float ratio);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_projector_set_throw_ratio(IntPtr projector, float ratio);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_warpnode_create();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_warpnode_destroy(IntPtr node);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_warpnode_add_child(IntPtr parent, IntPtr child);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_warpnode_set_deformation_type(IntPtr node, int type);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_warpnode_get_gridwarp(IntPtr node);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_gridwarp_set_control_points(IntPtr gridwarp, int columns, int rows, pmsdk_vec3_t[] points);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_warpnode_get_perspectivewarp(IntPtr node);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_perspectivewarp_set_corners(
            IntPtr perspectivewarp,
            pmsdk_vec2_t bottomLeft, pmsdk_vec2_t bottomRight,
            pmsdk_vec2_t topRight, pmsdk_vec2_t topLeft);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_warpnode_process_mesh(IntPtr node, IntPtr input_mesh, IntPtr output_mesh);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_blendconfig_apply_to_mesh(IntPtr config, IntPtr mesh);

        // --- CalibrationAPI.h ---
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_graycode_create(int width, int height);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_graycode_destroy(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_graycode_get_pattern_count(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_graycode_generate_pattern(IntPtr handle, UIntPtr index, byte[] outPixels);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_calibrator_create();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_calibrator_destroy(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_calibrator_add_observation(IntPtr handle, pmsdk_vec3_t[] objectPoints, pmsdk_vec2_t[] imagePoints, UIntPtr pointCount);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_calibrator_calibrate(IntPtr handle, int imageWidth, int imageHeight, float[] outIntrinsics, float[] outDistortion, out double outRmsError);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_decoder_create(int projectorWidth, int projectorHeight);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_decoder_destroy(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_decoder_open_camera(IntPtr handle, int cameraIndex);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_decoder_capture_frame(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_decoder_close_camera(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_decoder_add_image(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string filepath);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_decoder_decode_and_triangulate(
            IntPtr handle,
            int threshold,
            float[] camIntrinsics, float[] camExtrinsics,
            float[] projIntrinsics, float[] projExtrinsics,
            [Out] pmsdk_vec3_t[] outPoints, out UIntPtr outCount, UIntPtr maxPoints);

        // --- Robust calibration additions (see CalibrationAPI.h) ---
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_graycode_get_robust_pattern_count(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_graycode_generate_robust_pattern(IntPtr handle, UIntPtr index, byte[] outPixels);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_decoder_capture_frame_flushed(IntPtr handle, int flushFrames);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_decoder_add_image_memory(IntPtr handle, byte[] pixels, int width, int height);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_decoder_get_last_frame(IntPtr handle, [Out] byte[] outPixels, ref int inOutWidth, ref int inOutHeight);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_decoder_get_image_count(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_decoder_clear_images(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_decoder_decode_robust(
            IntPtr handle,
            int minContrast,
            [Out] pmsdk_vec2_t[] outCameraPoints,
            [Out] pmsdk_vec2_t[] outProjectorPoints,
            out UIntPtr outCount,
            UIntPtr maxPoints);
    }
}
