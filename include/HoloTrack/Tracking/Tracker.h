/**
 * @file Tracker.h
 * @brief Single-viewer head tracker — orchestrates selection, estimation, filtering, the state
 *        machine, the OAK->world transform, and off-axis projection.
 *
 * This is the one compiled/exported unit of HoloTrackSDK's core; everything it composes is pure
 * header-only logic. Detections are *pushed* in (from the OAK device thread or a simulated
 * source), so the tracker itself has no hardware dependency (D-032).
 *
 * Export follows PMSDK D-009: the class is not dllexported wholesale (its private members use
 * std::unique_ptr); each public method carries HOLOTRACK_API and the destructor is exported so
 * member destruction happens inside the SDK binary.
 */
#pragma once

#include <cstddef>
#include <memory>

#include "HoloTrack/Core/Export.h"
#include "HoloTrack/Tracking/Config.h"
#include "HoloTrack/Tracking/Detection.h"
#include "HoloTrack/Tracking/Viewer.h"
#include "HoloTrack/Tracking/IHeadFilter.h"
#include "HoloTrack/Tracking/ViewerSelector.h"
#include "HoloTrack/Tracking/TrackingStateMachine.h"
#include "HoloTrack/Tracking/OffAxisProjection.h"

namespace holotrack {

/** @brief Orchestrating single-viewer head tracker (spec §1–§10). */
class Tracker {
public:
    /** @brief Construct with an initial configuration (builds the selected filter). */
    HOLOTRACK_API explicit Tracker(const TrackerConfig& cfg);
    HOLOTRACK_API ~Tracker();

    Tracker(const Tracker&) = delete;
    Tracker& operator=(const Tracker&) = delete;

    /** @brief Replace the configuration; rebuilds the filter if the filter type changed. */
    HOLOTRACK_API void SetConfig(const TrackerConfig& cfg);

    /** @brief Current configuration. */
    HOLOTRACK_API const TrackerConfig& GetConfig() const;

    /**
     * @brief Process one detection frame and advance all tracking state.
     * @param dets Detection span for this frame (may be empty for an unmeasured frame).
     * @param count Number of detections.
     * @param timestampSeconds Monotonic frame timestamp (seconds); drives dt, velocity, and the
     *        state-machine timeouts.
     */
    HOLOTRACK_API void PushFrame(const Detection* dets, std::size_t count, double timestampSeconds);

    /** @brief The current active viewer (check @ref TrackedViewer::valid before use). */
    HOLOTRACK_API const TrackedViewer& GetViewer() const;

    /**
     * @brief Off-axis view+projection for the current viewer against a display surface.
     * @param pa Screen bottom-left (world). @param pb bottom-right. @param pc top-left.
     * @param nearPlane Near clip (>0). @param farPlane Far clip (> near).
     * @see holotrack::ComputeOffAxis
     */
    HOLOTRACK_API OffAxisResult ComputeOffAxis(const Vector3& pa, const Vector3& pb,
                                               const Vector3& pc, float nearPlane,
                                               float farPlane) const;

    /** @brief Drop the viewer and clear filter/selector state (back to Searching). */
    HOLOTRACK_API void Reset();

private:
    TrackerConfig cfg_;
    std::unique_ptr<IHeadFilter> filter_;
    ViewerSelector selector_;
    TrackingStateMachine stateMachine_;
    TrackedViewer viewer_;

    Vector3 lastFilteredCamera_{};
    double lastMeasuredSeconds_{0.0};
    double lastUpdateSeconds_{0.0};
    bool hasMeasured_{false};
    bool hasPrevUpdate_{false};
};

} // namespace holotrack
