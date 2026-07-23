/**
 * @file Detection.h
 * @brief Per-frame person detection produced by a detection source (OAK device or simulated).
 *
 * Pure value type — no hardware or DepthAI dependency (see docs/decisions.md D-032).
 * All spatial coordinates are in OAK camera space, metres, before the OAK->world transform.
 */
#pragma once

#include "PMSDK/Math/Vector3.h"

namespace holotrack {

using pmsdk::Math::Vector3;

/**
 * @brief Optional pose keypoints for one detection.
 *
 * Present (@ref valid true) only when a pose-estimation network ran on-device. Each keypoint
 * carries its own validity flag because networks routinely drop occluded joints. Positions are
 * in OAK camera space (metres); a keypoint's spatial value is meaningful only when its flag is
 * set. Consumed by @ref HeadEstimator as a refinement over the bbox+depth estimate (D-031).
 */
struct PoseKeypoints {
    bool valid{false};      /**< Any keypoint in this set is present. */
    bool hasNose{false};    /**< @ref nose is valid. */
    bool hasLeftEye{false}; /**< @ref leftEye is valid. */
    bool hasRightEye{false};/**< @ref rightEye is valid. */
    bool hasNeck{false};    /**< @ref neck is valid. */
    Vector3 nose{};         /**< Nose position (camera space, metres). */
    Vector3 leftEye{};      /**< Left-eye position (camera space, metres). */
    Vector3 rightEye{};     /**< Right-eye position (camera space, metres). */
    Vector3 neck{};         /**< Neck position (camera space, metres). */
};

/**
 * @brief One detected person in one camera frame.
 *
 * The 2D bounding box is normalized [0,1] with a top-left origin. @ref spatial is the metric
 * position of the detection (typically the bbox centroid at the fused stereo depth), in OAK
 * camera space. Axis convention assumed by the pure logic: +X right, +Y up, +Z forward (away
 * from the camera); any real device convention is reconciled by the OAK->world calibration
 * transform, so the tracker math stays convention-agnostic.
 */
struct Detection {
    float bboxX{0.0f};      /**< Normalized left edge [0,1]. */
    float bboxY{0.0f};      /**< Normalized top edge [0,1]. */
    float bboxW{0.0f};      /**< Normalized width [0,1]. */
    float bboxH{0.0f};      /**< Normalized height [0,1]. */
    Vector3 spatial{};      /**< Metric position from stereo depth (camera space). */
    float confidence{0.0f}; /**< Detector confidence [0,1]. */
    PoseKeypoints pose{};   /**< Optional pose keypoints (see @ref PoseKeypoints). */

    /** @brief Bounding-box area (normalized). Used by the largest-box selection mode. */
    constexpr float BoxArea() const { return bboxW * bboxH; }
};

} // namespace holotrack
