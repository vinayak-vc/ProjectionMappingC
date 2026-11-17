using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace vxpmsdk.Components
{
    /// <summary>
    /// OAK-D colour camera as a calibration camera (D-044), backed by PMSDKOakCamera.dll. The OAK is
    /// not a UVC webcam, so it cannot go through <see cref="PMSDKNativeWebcamCamera"/>.
    ///
    /// <see cref="CaptureLuminance"/> blocks until <see cref="SettleFrames"/> new frames have
    /// arrived after the call, so the returned image was exposed after the caller changed the
    /// projected pattern (the device pipeline has a few frames of latency). Exposure and focus can be
    /// locked so the brightness does not drift between captures. Only one process may own the
    /// device: close OAK Viewer and stop HoloTrack's OAK source first.
    /// </summary>
    public sealed class PMSDKOakCalibrationCamera : IPMSDKCalibrationCamera, IPMSDKCameraIntrinsicsSource, IPMSDKDepthSource
    {
        private readonly int maxWidth;
        private readonly float fps;
        private readonly bool enableDepth;

        /// <summary>Depth frames combined (per-pixel median) by <see cref="CaptureDepth"/> (default 5).</summary>
        public int DepthFramesToCombine = 5;
        private IntPtr handle = IntPtr.Zero;
        private PMSDKCameraIntrinsics intrinsics;

        /// <summary>New frames to wait for after a capture request (default 3).</summary>
        public int SettleFrames = 3;

        /// <summary>Give up waiting for frames after this many milliseconds (default 3000).</summary>
        public int CaptureTimeoutMs = 3000;

        /// <summary>When &gt; 0, exposure is locked to this time (microseconds) at <see cref="Begin"/>.</summary>
        public uint ManualExposureUs;

        /// <summary>ISO used with <see cref="ManualExposureUs"/> (100..1600).</summary>
        public uint ManualIso = 400;

        /// <summary>When 0..255, focus is locked to this lens position at <see cref="Begin"/>; -1 = leave auto.</summary>
        public int ManualFocus = -1;

        /// <summary>Error of the last failed operation ("" when none).</summary>
        public string LastError { get; private set; } = "";

        /// <summary>"&lt;MXID&gt; &lt;sensor&gt;" after a successful <see cref="Begin"/>.</summary>
        public string DeviceDescription { get; private set; } = "";

        public int Width { get; private set; }

        public int Height { get; private set; }

        public PMSDKOakCalibrationCamera(int maxWidth = 1920, float fps = 15f, bool enableDepth = false)
        {
            this.maxWidth = maxWidth;
            this.fps = fps;
            this.enableDepth = enableDepth;
        }

        public bool HasDepth
        {
            get { return enableDepth && handle != IntPtr.Zero; }
        }

        /// <summary>Per-pixel median of <see cref="DepthFramesToCombine"/> fresh depth frames (mm, 0 = none), or null.</summary>
        public ushort[] CaptureDepth()
        {
            if (!HasDepth) {
                LastError = "OAK depth is not streaming (create the camera with enableDepth).";
                return null;
            }
            int frames = Mathf.Clamp(DepthFramesToCombine, 1, 15);
            int pixels = Width * Height;
            ushort[][] stack = new ushort[frames][];
            for (int f = 0; f < frames; f++) {
                if (!WaitForDepthFrames(1)) {
                    LastError = "OAK delivered no depth frame within " + CaptureTimeoutMs + " ms.";
                    return null;
                }
                ushort[] frame = new ushort[pixels];
                int width;
                int height;
                long sequence;
                int status = NativeOakCamera.pmoak_copy_latest_depth(handle, frame, (UIntPtr)frame.Length, out width, out height, out sequence);
                if (status != NativeOakCamera.Ok || width != Width || height != Height) {
                    LastError = "OAK depth copy failed: " + NativeOakCamera.ToText(NativeOakCamera.pmoak_last_error(handle));
                    return null;
                }
                stack[f] = frame;
            }
            if (frames == 1) {
                return stack[0];
            }
            ushort[] result = new ushort[pixels];
            ushort[] values = new ushort[frames];
            for (int i = 0; i < pixels; i++) {
                int count = 0;
                for (int f = 0; f < frames; f++) {
                    ushort v = stack[f][i];
                    if (v != 0) {
                        values[count++] = v;
                    }
                }
                // Keep pixels seen in at least half the frames; take their median.
                if (count * 2 < frames) {
                    continue;
                }
                Array.Sort(values, 0, count);
                result[i] = values[count / 2];
            }
            return result;
        }

        /// <summary>True when PMSDKOakCamera.dll (and its depthai-core.dll) can be loaded.</summary>
        public static bool IsAvailable()
        {
            try {
                pmoak_options_t options = new pmoak_options_t();
                NativeOakCamera.pmoak_options_default(ref options);
                return options.maxWidth > 0;
            } catch (DllNotFoundException) {
                return false;
            } catch (EntryPointNotFoundException) {
                return false;
            }
        }

        public bool Begin()
        {
            LastError = "";
            if (handle != IntPtr.Zero) {
                return true;
            }
            try {
                pmoak_options_t options = new pmoak_options_t();
                NativeOakCamera.pmoak_options_default(ref options);
                options.maxWidth = maxWidth;
                options.fps = fps;
                options.enableDepth = enableDepth ? 1 : 0;
                handle = NativeOakCamera.pmoak_create(ref options);
                if (handle == IntPtr.Zero) {
                    LastError = "Could not create the OAK camera handle.";
                    return false;
                }
                int status = NativeOakCamera.pmoak_start(handle);
                if (status != NativeOakCamera.Ok) {
                    LastError = "OAK camera did not start: " + NativeOakCamera.ToText(NativeOakCamera.pmoak_last_error(handle));
                    End();
                    return false;
                }
                int width;
                int height;
                NativeOakCamera.pmoak_get_frame_size(handle, out width, out height);
                Width = width;
                Height = height;
                DeviceDescription = NativeOakCamera.ToText(NativeOakCamera.pmoak_device_description(handle));
                ReadIntrinsics();
                ApplyLocks();
                if (!WaitForFrames(1)) {
                    LastError = "OAK camera started but delivered no frame within " + CaptureTimeoutMs + " ms.";
                    End();
                    return false;
                }
                return true;
            } catch (DllNotFoundException ex) {
                LastError = "PMSDKOakCamera.dll (or depthai-core.dll) is not installed: " + ex.Message;
                handle = IntPtr.Zero;
                return false;
            } catch (EntryPointNotFoundException ex) {
                LastError = "PMSDKOakCamera.dll is out of date: " + ex.Message;
                handle = IntPtr.Zero;
                return false;
            }
        }

        /// <summary>Waits for fresh frames and returns the newest as luminance (top-left origin), or null on failure.</summary>
        public byte[] CaptureLuminance()
        {
            if (handle == IntPtr.Zero) {
                LastError = "OAK camera is not started.";
                return null;
            }
            if (!WaitForFrames(Mathf.Max(1, SettleFrames))) {
                LastError = "OAK camera delivered no new frame within " + CaptureTimeoutMs + " ms.";
                Debug.LogWarning("[PMSDK] " + LastError);
                return null;
            }
            byte[] frame = new byte[Width * Height];
            int width;
            int height;
            long sequence;
            int status = NativeOakCamera.pmoak_copy_latest_gray(handle, frame, (UIntPtr)frame.Length, out width, out height, out sequence);
            if (status != NativeOakCamera.Ok) {
                LastError = "OAK frame copy failed: " + NativeOakCamera.ToText(NativeOakCamera.pmoak_last_error(handle));
                Debug.LogWarning("[PMSDK] " + LastError);
                return null;
            }
            return frame;
        }

        public void End()
        {
            if (handle == IntPtr.Zero) {
                return;
            }
            try {
                NativeOakCamera.pmoak_stop(handle);
                NativeOakCamera.pmoak_destroy(handle);
            } catch (DllNotFoundException) {
                // Nothing was opened if the plugin is missing.
            }
            handle = IntPtr.Zero;
        }

        public bool TryGetIntrinsics(out PMSDKCameraIntrinsics result)
        {
            result = intrinsics;
            return intrinsics != null;
        }

        private void ReadIntrinsics()
        {
            intrinsics = null;
            pmoak_intrinsics_t native;
            if (NativeOakCamera.pmoak_get_intrinsics(handle, out native) != NativeOakCamera.Ok) {
                Debug.LogWarning("[PMSDK] OAK factory intrinsics unavailable; lens distortion will not be removed. " +
                                 NativeOakCamera.ToText(NativeOakCamera.pmoak_last_error(handle)));
                return;
            }
            if (native.model != (int)PMSDKLensModel.Perspective && native.model != (int)PMSDKLensModel.Fisheye) {
                Debug.LogWarning("[PMSDK] OAK lens model " + native.model + " is not supported for undistortion; ignoring it.");
                return;
            }
            int count = Mathf.Clamp(native.distortionCount, 0, 14);
            float[] coefficients = new float[count];
            if (native.distortion != null) {
                Array.Copy(native.distortion, coefficients, Mathf.Min(count, native.distortion.Length));
            }
            intrinsics = new PMSDKCameraIntrinsics {
                Model = (PMSDKLensModel)native.model,
                Fx = native.fx,
                Fy = native.fy,
                Cx = native.cx,
                Cy = native.cy,
                Distortion = coefficients,
            };
        }

        private void ApplyLocks()
        {
            if (ManualExposureUs > 0) {
                int status = NativeOakCamera.pmoak_set_manual_exposure(handle, ManualExposureUs, ManualIso);
                if (status != NativeOakCamera.Ok) {
                    Debug.LogWarning("[PMSDK] OAK exposure lock failed: " + NativeOakCamera.ToText(NativeOakCamera.pmoak_last_error(handle)));
                }
            }
            if (ManualFocus >= 0 && ManualFocus <= 255) {
                int status = NativeOakCamera.pmoak_set_manual_focus(handle, ManualFocus);
                if (status != NativeOakCamera.Ok) {
                    Debug.LogWarning("[PMSDK] OAK focus lock failed: " + NativeOakCamera.ToText(NativeOakCamera.pmoak_last_error(handle)));
                }
            }
        }

        private bool WaitForDepthFrames(int count)
        {
            long start = NativeOakCamera.pmoak_latest_depth_sequence(handle);
            Stopwatch watch = Stopwatch.StartNew();
            while (NativeOakCamera.pmoak_latest_depth_sequence(handle) < start + count) {
                if (watch.ElapsedMilliseconds > CaptureTimeoutMs) {
                    return false;
                }
                Thread.Sleep(5);
            }
            return true;
        }

        private bool WaitForFrames(int count)
        {
            long start = NativeOakCamera.pmoak_latest_sequence(handle);
            Stopwatch watch = Stopwatch.StartNew();
            while (NativeOakCamera.pmoak_latest_sequence(handle) < start + count) {
                if (watch.ElapsedMilliseconds > CaptureTimeoutMs) {
                    return false;
                }
                Thread.Sleep(5);
            }
            return true;
        }
    }
}
