/**
 * @file HeadEstimator.h
 * @brief Turns a selected detection into a head-centre position (spec §2).
 *
 * Pose keypoints are used when present (eyes midpoint → nose → neck), otherwise the head is
 * estimated from the bounding box and stereo depth: a metric body height is derived from the
 * bbox height, the depth, and the camera vertical FOV, and the head is placed above the
 * detection centroid by a configurable fraction of that height (D-031).
 */
#pragma once

#include <cmath>

#include "PMSDK/Math/Vector3.h"
#include "HoloTrack/Tracking/Detection.h"
#include "HoloTrack/Tracking/Config.h"

namespace holotrack {

using pmsdk::Math::Vector3;

/** @brief Stateless head-centre estimator. */
struct HeadEstimator {
    /**
     * @brief Estimate the head centre (camera space, metres) for @p d under @p cfg.
     *
     * Always returns a position. When @ref PoseKeypoints::valid, the most reliable available
     * keypoint combination is used; otherwise the bbox+depth+height fallback applies.
     */
    static Vector3 Estimate(const Detection& d, const TrackerConfig& cfg) {
        if (d.pose.valid) {
            if (d.pose.hasLeftEye && d.pose.hasRightEye) {
                return (d.pose.leftEye + d.pose.rightEye) * 0.5f;
            }
            if (d.pose.hasNose) {
                return d.pose.nose;
            }
            if (d.pose.hasNeck) {
                // Neck sits below the head; lift by a small fraction of the estimated body height.
                Vector3 head = d.pose.neck;
                head.y += EstimateBodyHeight(d, cfg) * 0.20f;
                return head;
            }
        }

        // Fallback: detection centroid lifted toward the top of the body.
        Vector3 head = d.spatial;
        const float bodyHeight = EstimateBodyHeight(d, cfg);
        // headHeightFraction is measured from the feet (1 = crown); the centroid sits at ~0.5.
        head.y += bodyHeight * (cfg.headHeightFraction - 0.5f) + cfg.bodyHeadOffset;
        return head;
    }

    /**
     * @brief Metric body height (metres) implied by the bbox height at the detection's depth.
     *
     * height ≈ 2 · Z · tan(vFov/2) · bboxH. Falls back to 0 for a non-positive depth so the
     * head estimate collapses to the raw centroid rather than producing NaNs.
     */
    static float EstimateBodyHeight(const Detection& d, const TrackerConfig& cfg) {
        const float z = d.spatial.z;
        if (!(z > 0.0f)) {
            return 0.0f;
        }
        const float halfV = std::tan(cfg.cameraVFovRad * 0.5f);
        return 2.0f * z * halfV * d.bboxH;
    }
};

} // namespace holotrack
