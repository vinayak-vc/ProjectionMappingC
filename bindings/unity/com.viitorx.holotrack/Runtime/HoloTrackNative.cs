using System;
using System.Runtime.InteropServices;

namespace vxholotrack
{
    /// <summary>Status codes returned by the HoloTrack C-API. Mirrors <c>ht_status_t</c>.</summary>
    public enum HtStatus : int
    {
        Success = 0,
        ErrorInvalidArgument = 1,
        ErrorInvalidHandle = 2,
        ErrorUnknown = 3
    }

    /// <summary>Head-position smoothing filter. Mirrors <c>ht_filter_type_t</c>.</summary>
    public enum HtFilterType : int
    {
        None = 0,
        Exponential = 1,
        OneEuro = 2,
        Kalman = 3
    }

    /// <summary>Active-viewer selection mode. Mirrors <c>ht_selection_mode_t</c>.</summary>
    public enum HtSelectionMode : int
    {
        NearestZ = 0,
        LargestBox = 1
    }

    /// <summary>Tracking lifecycle state. Mirrors <c>ht_tracking_state_t</c>.</summary>
    public enum HtTrackingState : int
    {
        Searching = 0,
        Tracking = 1,
        Prediction = 2,
        Lost = 3
    }

    /// <summary>3-component vector, blittable, matches <c>ht_vec3_t</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HtVec3
    {
        public float x, y, z;
        public HtVec3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    /// <summary>Quaternion, matches <c>ht_quat_t</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HtQuat
    {
        public float x, y, z, w;
        public HtQuat(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }

    /// <summary>Flat tracker configuration. Field order MUST match <c>ht_tracker_config_t</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HtTrackerConfig
    {
        public int filterType;
        public float expAlpha;
        public float oneEuroMinCutoff;
        public float oneEuroBeta;
        public float oneEuroDCutoff;
        public float kalmanProcessVar;
        public float kalmanMeasVar;

        public int selection;
        public float hysteresisMargin;
        public int hysteresisFrames;
        public float associationMaxDistance;

        public float minDepth;
        public float maxDepth;

        public float predictionTimeSeconds;
        public float lostTimeoutSeconds;

        public float cameraVFovRad;
        public float headHeightFraction;
        public float bodyHeadOffset;

        public float movementScale;
        public float deadZone;
        public float maxDistance;
        public float minDistanceClamp;

        public HtVec3 calibTranslation;
        public HtQuat calibRotation;
        public float calibScale;
    }

    /// <summary>Flat detection with optional inlined pose keypoints. Matches <c>ht_detection_t</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HtDetection
    {
        public float bboxX, bboxY, bboxW, bboxH;
        public HtVec3 spatial;
        public float confidence;

        public int poseValid;
        public int hasNose, hasLeftEye, hasRightEye, hasNeck;
        public HtVec3 nose, leftEye, rightEye, neck;
    }

    /// <summary>Flat tracked-viewer snapshot. Matches <c>ht_viewer_t</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HtViewer
    {
        public int id;
        public int valid;
        public HtVec3 headCamera;
        public HtVec3 headWorld;
        public HtVec3 velocity;
        public float confidence;
        public int state;
        public double timestampSeconds;
    }

    /// <summary>Off-axis projection result (column-major 4x4 matrices). Matches <c>ht_offaxis_t</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HtOffAxis
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public float[] view;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public float[] projection;
        public float eyeToScreen;
        public int valid;
    }

    /// <summary>OAK detector selection. Matches <c>ht_detection_mode_t</c>.</summary>
    public enum HtDetectionMode : int
    {
        Person = 0,
        Face = 1,
        FaceThenPerson = 2
    }

    /// <summary>OAK device options. Field order MUST match <c>ht_oak_options_t</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HtOakOptions
    {
        public int detectionMode;
        [MarshalAs(UnmanagedType.LPStr)] public string blobPath;
        [MarshalAs(UnmanagedType.LPStr)] public string faceBlobPath;
        public int faceFallbackFrames;
        public int personLabel;
        public float confidenceThreshold;
        public float depthLowerThresholdMm;
        public float depthUpperThresholdMm;
    }

    /// <summary>
    /// Raw P/Invoke bindings to <c>HoloTrackSDK.dll</c>. Consumers should prefer the managed
    /// wrappers (<see cref="PMHTHeadTracker"/>) rather than calling these directly.
    /// </summary>
    public static class HoloTrackNative
    {
        /// <summary>Native library name (no extension); resolves to HoloTrackSDK.dll on Windows.</summary>
        public const string LibraryName = "HoloTrackSDK";

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ht_get_version(out int major, out int minor, out int patch);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ht_get_last_error();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ht_tracker_create(ref HtTrackerConfig config);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ht_tracker_destroy(IntPtr tracker);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern HtStatus ht_tracker_set_config(IntPtr tracker, ref HtTrackerConfig config);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern HtStatus ht_tracker_get_config(IntPtr tracker, out HtTrackerConfig config);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern HtStatus ht_tracker_push_frame(IntPtr tracker, HtDetection[] detections,
                                                            UIntPtr count, double timestampSeconds);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern HtStatus ht_tracker_get_viewer(IntPtr tracker, out HtViewer viewer);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern HtStatus ht_tracker_compute_offaxis(IntPtr tracker,
                                                                ref HtVec3 pa, ref HtVec3 pb, ref HtVec3 pc,
                                                                float nearPlane, float farPlane,
                                                                out HtOffAxis result);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern HtStatus ht_compute_offaxis_eye(ref HtVec3 pa, ref HtVec3 pb, ref HtVec3 pc,
                                                             ref HtVec3 eye, float nearPlane, float farPlane,
                                                             out HtOffAxis result);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern HtStatus ht_tracker_reset(IntPtr tracker);

        // --- OAK-D device source (inert unless the DLL was built with DepthAI) ---

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ht_oak_is_supported();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ht_oak_create(ref HtOakOptions options);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void ht_oak_destroy(IntPtr source);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern HtStatus ht_oak_start(IntPtr source);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern HtStatus ht_oak_stop(IntPtr source);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ht_oak_is_running(IntPtr source);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern HtStatus ht_oak_poll(IntPtr source, [Out] HtDetection[] buffer,
                                                  UIntPtr capacity, out UIntPtr outCount,
                                                  out int outHasFrame, out double outTimestampSeconds);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ht_oak_last_error(IntPtr source);

        /// <summary>Managed copy of the most recent native error message for this thread.</summary>
        public static string GetLastError()
        {
            IntPtr ptr = ht_get_last_error();
            return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(ptr);
        }

        /// <summary>Managed copy of an OAK source's last error message.</summary>
        public static string GetOakLastError(IntPtr source)
        {
            IntPtr ptr = ht_oak_last_error(source);
            return ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(ptr);
        }
    }
}
