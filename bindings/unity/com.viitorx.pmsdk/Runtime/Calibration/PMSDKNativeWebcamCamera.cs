using System;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Physical-webcam capture source for camera-assisted auto-align, backed by the
    /// native SDK's OpenCV VideoCapture (pmsdk_decoder_*). Frames are captured with a
    /// buffer flush first: VideoCapture queues frames internally, so without flushing
    /// the capture after a pattern change would show the PREVIOUS pattern and corrupt
    /// the decode.
    ///
    /// The native decoder is used purely as a camera here — decoding stays in
    /// PMSDKGrayCodeDecode so the same pipeline runs against simulated and physical
    /// sources. (Hosts that want the native decode can use pmsdk_decoder_decode_robust.)
    /// </summary>
    public class PMSDKNativeWebcamCamera : IPMSDKCalibrationCamera
    {
        private readonly int cameraIndex;
        private readonly int flushFrames;
        private IntPtr decoder = IntPtr.Zero;

        public int Width { get; private set; }
        public int Height { get; private set; }

        public PMSDKNativeWebcamCamera(int cameraIndex = 0, int flushFrames = 2)
        {
            this.cameraIndex = cameraIndex;
            this.flushFrames = flushFrames;
        }

        public bool Begin()
        {
            // Projector resolution is irrelevant for capture-only use.
            decoder = NativeBindings.pmsdk_decoder_create(2, 2);
            if (decoder == IntPtr.Zero)
            {
                return false;
            }
            if (NativeBindings.pmsdk_decoder_open_camera(decoder, cameraIndex) != pmsdk_status_t.PMSDK_SUCCESS)
            {
                Debug.LogWarning($"[PMSDK] Could not open webcam index {cameraIndex}.");
                End();
                return false;
            }

            // Probe one frame to learn the camera resolution.
            if (NativeBindings.pmsdk_decoder_capture_frame_flushed(decoder, flushFrames) != pmsdk_status_t.PMSDK_SUCCESS)
            {
                Debug.LogWarning("[PMSDK] Webcam opened but produced no frame.");
                End();
                return false;
            }
            int w = 0, h = 0;
            if (NativeBindings.pmsdk_decoder_get_last_frame(decoder, null, ref w, ref h) != pmsdk_status_t.PMSDK_SUCCESS)
            {
                End();
                return false;
            }
            Width = w;
            Height = h;
            NativeBindings.pmsdk_decoder_clear_images(decoder);
            return true;
        }

        public byte[] CaptureLuminance()
        {
            // Callers hold on to returned frames for the whole sweep, so every
            // capture must be a fresh array — never the reused staging buffer.
            var frame = new byte[Width * Height];
            if (decoder == IntPtr.Zero)
            {
                return frame;
            }
            if (NativeBindings.pmsdk_decoder_capture_frame_flushed(decoder, flushFrames) != pmsdk_status_t.PMSDK_SUCCESS)
            {
                Debug.LogWarning("[PMSDK] Webcam frame capture failed mid-sweep.");
                return frame;
            }
            int w = Width, h = Height;
            NativeBindings.pmsdk_decoder_get_last_frame(decoder, frame, ref w, ref h);
            // Keep the decoder's internal list empty — we only use it as a camera.
            NativeBindings.pmsdk_decoder_clear_images(decoder);
            return frame;
        }

        public void End()
        {
            if (decoder != IntPtr.Zero)
            {
                NativeBindings.pmsdk_decoder_close_camera(decoder);
                NativeBindings.pmsdk_decoder_destroy(decoder);
                decoder = IntPtr.Zero;
            }
        }
    }
}
