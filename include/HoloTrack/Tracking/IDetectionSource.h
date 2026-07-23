/**
 * @file IDetectionSource.h
 * @brief Abstraction over "where detections come from" (spec §1).
 *
 * Decouples the tracker from the OAK device: the concrete @c OakDevice (DepthAI, feature-gated,
 * D-032) and a @c SimulatedSource / recorded source implement this same interface, so the whole
 * pipeline runs on CI and on machines with no camera. This is also the extension point for
 * networked and multi-OAK sources.
 */
#pragma once

#include <cstddef>

#include "HoloTrack/Tracking/Detection.h"

namespace holotrack {

/** @brief A source of per-frame detections. */
class IDetectionSource {
public:
    virtual ~IDetectionSource() = default;

    /**
     * @brief Fetch the latest frame's detections, if a new one is available.
     * @param outDetections Set to a pointer to the source-owned detection array (valid until the
     *        next Poll on the same source). May be null when @p outCount is 0.
     * @param outCount Set to the number of detections.
     * @param outTimestampSeconds Set to the frame's monotonic timestamp.
     * @return True if a (possibly empty) new frame was produced; false if nothing new since the
     *         last call — the caller should not advance the tracker on a false return.
     */
    virtual bool Poll(const Detection** outDetections, std::size_t* outCount,
                      double* outTimestampSeconds) = 0;

    /** @brief Begin producing frames (open the device / start the thread). @return success. */
    virtual bool Start() = 0;

    /** @brief Stop producing frames and release resources. */
    virtual void Stop() = 0;

    /** @brief True while the source is running and healthy. */
    virtual bool IsRunning() const = 0;
};

} // namespace holotrack
