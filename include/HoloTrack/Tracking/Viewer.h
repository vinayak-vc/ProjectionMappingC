/**
 * @file Viewer.h
 * @brief Tracked-viewer state and the tracking state-machine enumeration.
 */
#pragma once

#include "PMSDK/Math/Vector3.h"

namespace holotrack {

using pmsdk::Math::Vector3;

/**
 * @brief Lifecycle of the active viewer (spec §4).
 *
 * Searching → Tracking → Prediction → Lost → Searching. Prediction extrapolates from the last
 * velocity through a transient occlusion; Lost is the terminal step that drops the identity.
 */
enum class TrackingState {
    Searching = 0, /**< No viewer held; scanning detections. */
    Tracking = 1,  /**< Viewer held and measured this frame. */
    Prediction = 2,/**< Viewer momentarily unmeasured; extrapolating from velocity. */
    Lost = 3       /**< Prediction window expired; identity dropped this frame. */
};

/**
 * @brief The single active viewer's tracked state.
 *
 * @ref headCamera is the smoothed head centre in OAK camera space; @ref headWorld is the same
 * point after the OAK->world calibration transform. @ref velocity is in camera space (m/s) and
 * drives Prediction.
 */
struct TrackedViewer {
    int id{-1};                                   /**< Stable identity; -1 when none. */
    bool valid{false};                            /**< A usable head position is available (Tracking or Prediction). */
    Vector3 headCamera{};                         /**< Smoothed head centre, camera space (metres). */
    Vector3 headWorld{};                          /**< Head centre in world space (post-calibration). */
    Vector3 velocity{};                           /**< Camera-space velocity (m/s). */
    float confidence{0.0f};                       /**< Confidence of the backing detection [0,1]. */
    TrackingState state{TrackingState::Searching};/**< Current state (see @ref TrackingState). */
    double timestampSeconds{0.0};                 /**< Timestamp of the last update. */
};

} // namespace holotrack
