/**
 * @file Config.h
 * @brief All tunable tracker parameters and the OAK->world calibration transform.
 *
 * Every runtime value lives here (spec §13: no hardcoded values). The struct is a plain value
 * type so it serializes trivially (JSON on the C# side) and crosses the C-API as a flat POD.
 */
#pragma once

#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Math/Quaternion.h"

namespace holotrack {

using pmsdk::Math::Vector3;
using pmsdk::Math::Quaternion;

/** @brief Head-position smoothing filter selection (spec §5). */
enum class FilterType {
    None = 0,        /**< Pass-through (no smoothing). */
    Exponential = 1, /**< One-pole exponential moving average. */
    OneEuro = 2,     /**< Adaptive 1€ filter — low jitter at rest, low lag when moving. */
    Kalman = 3       /**< Constant-velocity Kalman filter. */
};

/** @brief Active-viewer selection criterion when multiple people are present (spec §3). */
enum class SelectionMode {
    NearestZ = 0,  /**< Prefer the closest viewer (smallest depth). */
    LargestBox = 1 /**< Prefer the largest bounding box. */
};

/**
 * @brief Rigid OAK-camera-space → world-space transform (spec §6, §9).
 *
 * Applied as world = translation + rotation * (scale * cameraPoint). Supports arbitrary,
 * off-centre camera mounting. Serialized as part of the calibration JSON.
 */
struct CalibrationTransform {
    Vector3 translation{};          /**< World-space position of the camera origin. */
    Quaternion rotation{};          /**< Camera → world rotation (identity by default). */
    float scale{1.0f};              /**< Uniform scale (metres→world units; usually 1). */
};

/**
 * @brief Complete tracker configuration.
 *
 * Groups: filtering, selection/hysteresis, depth gating, state-machine timing, head estimation,
 * the safety/tuning layer applied to the world-space eye, and the calibration transform.
 */
struct TrackerConfig {
    // --- Filtering (spec §5) ---
    FilterType filterType{FilterType::OneEuro}; /**< Active filter. */
    float expAlpha{0.5f};                        /**< Exponential filter smoothing [0,1]. */
    float oneEuroMinCutoff{1.0f};                /**< 1€ minimum cutoff frequency (Hz). */
    float oneEuroBeta{0.007f};                   /**< 1€ speed coefficient. */
    float oneEuroDCutoff{1.0f};                  /**< 1€ derivative cutoff frequency (Hz). */
    float kalmanProcessVar{1e-2f};               /**< Kalman process (model) variance. */
    float kalmanMeasVar{1e-1f};                  /**< Kalman measurement variance. */

    // --- Active-viewer selection (spec §3) ---
    SelectionMode selection{SelectionMode::NearestZ}; /**< Selection criterion. */
    float hysteresisMargin{0.15f};   /**< Challenger must beat the incumbent's score by this fraction... */
    int hysteresisFrames{8};         /**< ...for this many consecutive frames before the id switches. */
    float associationMaxDistance{0.6f}; /**< Max metres between frames to keep the same viewer id. */

    // --- Depth gating (spec §13) ---
    float minDepth{0.3f};            /**< Reject detections closer than this (metres). */
    float maxDepth{6.0f};            /**< Reject detections farther than this (metres). */

    // --- State machine (spec §4, §10) ---
    float predictionTimeSeconds{0.5f}; /**< Extrapolate from velocity for up to this long. */
    float lostTimeoutSeconds{1.0f};    /**< Total time unmeasured before Lost (>= prediction time). */

    // --- Head estimation (spec §2) ---
    float cameraVFovRad{1.20f};      /**< Camera vertical FOV (radians) — metric body-height estimate. */
    float headHeightFraction{0.9f};  /**< Where the head sits within the body height (0=feet,1=top). */
    float bodyHeadOffset{0.0f};      /**< Extra metric offset added to the head Y estimate. */

    // --- Safety / tuning layer on the world eye (spec §7) ---
    float movementScale{1.0f};       /**< Multiplier on head displacement from the rest pose. */
    float deadZone{0.0f};            /**< Ignore head displacement below this magnitude (metres). */
    float maxDistance{3.0f};         /**< Clamp head displacement magnitude to this (metres). */
    float minDistanceClamp{0.0f};    /**< Minimum allowed distance from the rest pose (metres). */

    // --- Calibration ---
    CalibrationTransform calibration{}; /**< OAK->world transform. */
};

} // namespace holotrack
