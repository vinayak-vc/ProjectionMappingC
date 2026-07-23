namespace vxholotrack
{
    /// <summary>
    /// A source of per-frame detections for <see cref="PMHTHeadTracker"/>. Implemented by the
    /// simulated source (editor/no-hardware) and, later, the OAK device source. Kept allocation-
    /// free: the tracker owns the buffer and the source fills it in place.
    /// </summary>
    public interface IHeadTrackingSource
    {
        /// <summary>
        /// Fill <paramref name="buffer"/> with the latest frame's detections.
        /// </summary>
        /// <param name="buffer">Tracker-owned scratch array; write up to its length.</param>
        /// <param name="count">Number of detections written (0 for an empty frame).</param>
        /// <param name="timestampSeconds">Monotonic timestamp of the frame.</param>
        /// <returns>True if a new frame is available (even if empty); false to skip this tick.</returns>
        bool TryPoll(HtDetection[] buffer, out int count, out double timestampSeconds);
    }
}
