namespace vxpmsdk.Components
{
    /// <summary>Lens model of a calibration camera.</summary>
    public enum PMSDKLensModel
    {
        Perspective = 0,
        Fisheye = 1,
    }

    /// <summary>Camera intrinsics for the frame size a calibration camera delivers.</summary>
    public sealed class PMSDKCameraIntrinsics
    {
        public PMSDKLensModel Model = PMSDKLensModel.Perspective;
        public float Fx;
        public float Fy;
        public float Cx;
        public float Cy;
        /// <summary>OpenCV order k1, k2, p1, p2, k3, k4, k5, k6, s1..s4, tx, ty (fisheye: k1..k4). Up to 14.</summary>
        public float[] Distortion = new float[0];
    }

    /// <summary>
    /// Optional companion of <see cref="IPMSDKCalibrationCamera"/>: a camera that knows its own lens
    /// calibration (e.g. the OAK-D factory calibration). Auto Adjust removes lens distortion from
    /// detections when the camera implements this.
    /// </summary>
    public interface IPMSDKCameraIntrinsicsSource
    {
        /// <summary>True and the intrinsics when known (valid after <see cref="IPMSDKCalibrationCamera.Begin"/>).</summary>
        bool TryGetIntrinsics(out PMSDKCameraIntrinsics intrinsics);
    }
}
