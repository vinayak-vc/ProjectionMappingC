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

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_projector_set_aspect_ratio(IntPtr projector, float ratio);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_projector_set_throw_ratio(IntPtr projector, float ratio);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_warpnode_create();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_warpnode_destroy(IntPtr node);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_warpnode_add_child(IntPtr parent, IntPtr child);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_warpnode_process_mesh(IntPtr node, IntPtr input_mesh, IntPtr output_mesh);
    }
}
