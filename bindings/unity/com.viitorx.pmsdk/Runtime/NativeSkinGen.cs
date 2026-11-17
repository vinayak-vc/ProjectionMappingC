using System;
using System.Runtime.InteropServices;

namespace vxpmsdk
{
    /// <summary>Mirrors <c>pmsdk_skingen_options_t</c> (PMSDK/C_API/SkinGenAPI.h). Field order and
    /// types must match the C struct exactly.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct pmsdk_skingen_options_t
    {
        public int mode;
        public byte alpha_threshold;
        public float color_tolerance;
        public float grabcut_margin;
        public int grabcut_iterations;
        public float min_part_area_fraction;
        public float min_hole_area_fraction;
        public int split_parts;
        public float thin_part_fraction;
        public float appendage_max_fraction;
        public int max_parts;
        public int max_polygon_points;
        public int y_up;
    }

    /// <summary>P/Invoke declarations for the photo -> skin C API (SkinGenAPI.h).</summary>
    public static class NativeSkinGen
    {
        private const string Lib = NativeBindings.LibraryName;

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_skingen_options_init(ref pmsdk_skingen_options_t options);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_skingen_extract_from_file(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path, ref pmsdk_skingen_options_t options);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmsdk_skingen_result_destroy(IntPtr result);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmsdk_skingen_get_last_error();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_skingen_get_image_size(IntPtr result, out int width, out int height);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pmsdk_skingen_get_used_mode(IntPtr result);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_skingen_get_part_count(IntPtr result);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern float pmsdk_skingen_get_part_area_fraction(IntPtr result, UIntPtr part);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_skingen_get_part_bounds(IntPtr result, UIntPtr part, [Out] float[] out4);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_skingen_get_part_quad(IntPtr result, UIntPtr part, [Out] pmsdk_vec2_t[] out4);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_skingen_copy_polygon(IntPtr result, UIntPtr part, [Out] pmsdk_vec2_t[] points, UIntPtr capacity);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_skingen_get_contour_count(IntPtr result, UIntPtr part);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pmsdk_skingen_get_contour_is_hole(IntPtr result, UIntPtr part, UIntPtr contour);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_skingen_copy_contour(IntPtr result, UIntPtr part, UIntPtr contour, [Out] pmsdk_vec2_t[] points, UIntPtr capacity);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern pmsdk_status_t pmsdk_skingen_get_mask_rect(IntPtr result, UIntPtr part, [Out] int[] out4);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr pmsdk_skingen_copy_mask(IntPtr result, UIntPtr part, [Out] byte[] pixels, UIntPtr capacity);
    }
}
