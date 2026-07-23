/**
 * @file CoordinateTransform.h
 * @brief OAK-camera-space → world-space conversion (spec §6, §9).
 */
#pragma once

#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Math/Quaternion.h"
#include "HoloTrack/Tracking/Config.h"

namespace holotrack {

using pmsdk::Math::Vector3;

/**
 * @brief Applies a rigid OAK->world transform to camera-space points.
 *
 * Pure functions — supports arbitrary, off-centre camera mounting. The transform is
 * world = translation + rotation * (scale * cameraPoint).
 */
struct CoordinateTransform {
    /** @brief Convert a camera-space point to world space under @p c. */
    static Vector3 CameraToWorld(const CalibrationTransform& c, const Vector3& cameraPoint) {
        const Vector3 scaled = cameraPoint * c.scale;
        const Vector3 rotated = c.rotation * scaled; // Quaternion * Vector3 rotates the vector
        return rotated + c.translation;
    }

    /** @brief Convert a world-space point back to camera space under @p c. */
    static Vector3 WorldToCamera(const CalibrationTransform& c, const Vector3& worldPoint) {
        const Vector3 t = worldPoint - c.translation;
        const Vector3 unrotated = c.rotation.Conjugate() * t;
        const float invScale = (c.scale != 0.0f) ? (1.0f / c.scale) : 0.0f;
        return unrotated * invScale;
    }
};

} // namespace holotrack
