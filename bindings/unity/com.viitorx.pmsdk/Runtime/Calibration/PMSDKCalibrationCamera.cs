using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// A camera that observes the projected surfaces for camera-assisted auto-align.
    /// Two implementations exist:
    ///   - PMSDKSimulatedCamera: renders an in-scene Unity Camera to a texture. Works
    ///     with no hardware — for virtual/LED-volume rigs and for testing the whole
    ///     pipeline deterministically.
    ///   - (real hardware) a physical webcam via the native OpenCV Decoder
    ///     (pmsdk_decoder_open_camera / capture_frame). The auto-align algorithm here
    ///     is source-agnostic; a native-backed source can implement this interface once
    ///     per-frame pixel readback is exposed on the C API. See docs.
    /// </summary>
    public interface IPMSDKCalibrationCamera
    {
        int Width { get; }
        int Height { get; }
        bool Begin();
        /// <summary>Capture the currently-displayed frame as row-major luminance (0..255), top-left origin.</summary>
        byte[] CaptureLuminance();
        void End();
    }

    /// <summary>
    /// Observes projected surfaces through an in-scene Unity Camera. The camera should
    /// be positioned to see the projection the way a physical calibration camera would
    /// (ideally off-axis, so the recovered mapping is a genuine perspective homography).
    /// </summary>
    public class PMSDKSimulatedCamera : IPMSDKCalibrationCamera
    {
        private readonly Camera camera;
        private readonly int width;
        private readonly int height;
        private RenderTexture rt;
        private Texture2D readback;

        public int Width => width;
        public int Height => height;

        public PMSDKSimulatedCamera(Camera camera, int width, int height)
        {
            this.camera = camera;
            this.width = width;
            this.height = height;
        }

        public bool Begin()
        {
            if (camera == null) return false;
            rt = new RenderTexture(width, height, 24) { name = "PMSDK_CalibCameraRT" };
            readback = new Texture2D(width, height, TextureFormat.RGB24, false);
            return true;
        }

        public byte[] CaptureLuminance()
        {
            var prevTarget = camera.targetTexture;
            var prevActive = RenderTexture.active;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readback.Apply(false);
            camera.targetTexture = prevTarget;
            RenderTexture.active = prevActive;

            var px = readback.GetPixels32();
            var lum = new byte[width * height];
            // GetPixels32 is bottom-up; flip to top-left origin so decoded projector
            // coordinates line up with the displayed patterns.
            for (int y = 0; y < height; y++)
            {
                int srcRow = (height - 1 - y) * width;
                int dstRow = y * width;
                for (int x = 0; x < width; x++)
                {
                    var c = px[srcRow + x];
                    lum[dstRow + x] = (byte)((c.r * 77 + c.g * 150 + c.b * 29) >> 8);
                }
            }
            return lum;
        }

        public void End()
        {
            if (rt != null) { rt.Release(); Object.Destroy(rt); rt = null; }
            if (readback != null) { Object.Destroy(readback); readback = null; }
        }
    }
}
