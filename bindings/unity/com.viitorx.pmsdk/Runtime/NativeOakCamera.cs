using System;
using System.Runtime.InteropServices;

namespace vxpmsdk
{
    /// <summary>Mirrors <c>pmoak_options_t</c> (PMSDKOak/OakCameraAPI.h).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct pmoak_options_t
    {
        public int maxWidth;
        public float fps;
        public IntPtr mxId;
        public int enableDepth;
    }

    /// <summary>Mirrors <c>pmoak_intrinsics_t</c>. <c>distortion</c> holds exactly 14 floats.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct pmoak_intrinsics_t
    {
        public int width;
        public int height;
        public int model;
        public float fx;
        public float fy;
        public float cx;
        public float cy;
        public int distortionCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
        public float[] distortion;
    }

    /// <summary>
    /// P/Invoke declarations for PMSDKOakCamera.dll (OAK-D colour frames, D-044). The plugin is
    /// optional: it is deployed next to depthai-core.dll, and every caller must handle
    /// <see cref="DllNotFoundException"/> / <see cref="EntryPointNotFoundException"/>.
    /// </summary>
    public static class NativeOakCamera
    {
        public const string LibraryName = "PMSDKOakCamera";

        public const int Ok = 0;
        public const int NoFrame = 1;

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmoak_options_default(ref pmoak_options_t options);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmoak_create(ref pmoak_options_t options);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void pmoak_destroy(IntPtr camera);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pmoak_start(IntPtr camera);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pmoak_stop(IntPtr camera);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pmoak_get_frame_size(IntPtr camera, out int width, out int height);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long pmoak_latest_sequence(IntPtr camera);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pmoak_copy_latest_gray(IntPtr camera, [Out] byte[] buffer, UIntPtr capacity,
            out int width, out int height, out long sequence);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern long pmoak_latest_depth_sequence(IntPtr camera);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pmoak_copy_latest_depth(IntPtr camera, [Out] ushort[] buffer, UIntPtr capacity,
            out int width, out int height, out long sequence);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pmoak_set_manual_exposure(IntPtr camera, uint exposureUs, uint iso);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pmoak_set_auto_exposure(IntPtr camera);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pmoak_set_manual_focus(IntPtr camera, int lensPosition);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int pmoak_get_intrinsics(IntPtr camera, out pmoak_intrinsics_t intrinsics);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmoak_device_description(IntPtr camera);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr pmoak_last_error(IntPtr camera);

        /// <summary>Converts a native UTF-8 string pointer ("" for null).</summary>
        public static string ToText(IntPtr text)
        {
            return text == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(text);
        }
    }
}
