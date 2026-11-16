namespace vxpmsdk.Components
{
    /// <summary>
    /// Optional companion of <see cref="IPMSDKCalibrationCamera"/>: a camera that also delivers depth
    /// aligned to its colour frames (same size, same intrinsics), e.g. the OAK-D. Used by Auto Adjust
    /// Phase 4 to find flat surfaces automatically (D-047).
    /// </summary>
    public interface IPMSDKDepthSource
    {
        /// <summary>True when depth is streaming (valid after <see cref="IPMSDKCalibrationCamera.Begin"/>).</summary>
        bool HasDepth { get; }

        /// <summary>
        /// Depth in millimetres per pixel (0 = no data), rows top first, the same size as the colour
        /// frame. Implementations may combine several frames to reduce noise. Null on failure.
        /// </summary>
        ushort[] CaptureDepth();
    }
}
