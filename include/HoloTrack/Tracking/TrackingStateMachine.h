/**
 * @file TrackingStateMachine.h
 * @brief Searching → Tracking → Prediction → Lost lifecycle (spec §4, §10).
 *
 * Drives recovery through transient occlusion: while unmeasured the machine stays in Prediction
 * (the tracker extrapolates from velocity) up to @ref TrackerConfig::predictionTimeSeconds, then
 * Lost once @ref TrackerConfig::lostTimeoutSeconds elapses, then back to Searching. Header-only,
 * time supplied by the caller (no clock dependency — keeps it deterministic and testable).
 */
#pragma once

#include "HoloTrack/Tracking/Viewer.h"
#include "HoloTrack/Tracking/Config.h"

namespace holotrack {

/** @brief Pure state machine for the active viewer's lifecycle. */
class TrackingStateMachine {
public:
    /**
     * @brief Advance the state for one frame.
     * @param hasMeasurement True when a viewer was measured this frame.
     * @param nowSeconds Monotonic timestamp of this frame.
     * @param cfg Timing configuration.
     * @return The new state. A returned @ref TrackingState::Lost signals the caller to Reset()
     *         downstream state (selector, filter) exactly once; the machine itself then reports
     *         Searching on the following frame.
     */
    TrackingState Update(bool hasMeasurement, double nowSeconds, const TrackerConfig& cfg) {
        switch (state_) {
            case TrackingState::Searching:
                if (hasMeasurement) {
                    lastSeenSeconds_ = nowSeconds;
                    state_ = TrackingState::Tracking;
                }
                break;

            case TrackingState::Tracking:
                if (hasMeasurement) {
                    lastSeenSeconds_ = nowSeconds;
                } else {
                    state_ = TrackingState::Prediction;
                }
                break;

            case TrackingState::Prediction:
                if (hasMeasurement) {
                    lastSeenSeconds_ = nowSeconds;
                    state_ = TrackingState::Tracking;
                } else if ((nowSeconds - lastSeenSeconds_) >= cfg.lostTimeoutSeconds) {
                    state_ = TrackingState::Lost;
                }
                break;

            case TrackingState::Lost:
                // Terminal for one frame; resume searching (or immediately track if measured).
                state_ = hasMeasurement ? TrackingState::Tracking : TrackingState::Searching;
                if (hasMeasurement) {
                    lastSeenSeconds_ = nowSeconds;
                }
                break;
        }
        return state_;
    }

    /**
     * @brief Whether the tracker should still extrapolate a position this frame.
     *
     * True in Prediction while inside the prediction window; false once prediction time has
     * elapsed (the estimate goes stale before the Lost timeout).
     */
    bool IsPredicting(double nowSeconds, const TrackerConfig& cfg) const {
        return state_ == TrackingState::Prediction &&
               (nowSeconds - lastSeenSeconds_) < cfg.predictionTimeSeconds;
    }

    /** @brief Current state. */
    TrackingState State() const { return state_; }

    /** @brief Reset to Searching. */
    void Reset() {
        state_ = TrackingState::Searching;
        lastSeenSeconds_ = 0.0;
    }

private:
    TrackingState state_{TrackingState::Searching};
    double lastSeenSeconds_{0.0};
};

} // namespace holotrack
