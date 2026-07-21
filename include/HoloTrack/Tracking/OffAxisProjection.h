/**
 * @file OffAxisProjection.h
 * @brief Generalized (off-axis) perspective projection for head-tracked holographic rendering.
 *
 * Implements Kooima's "Generalized Perspective Projection" (2008), the same off-axis frustum
 * construction used game-side for the blended wall (D-026). The physical display surface is a
 * fixed rectangle in world space; the tracked eye is the projection centre. The near/far planes
 * stay parallel to the surface — only the frustum skews toward the eye — which is what produces
 * correct motion parallax (D-030), unlike a naive camera translation.
 *
 * Output convention is OpenGL-style (camera looks down -Z, right-handed): assign @ref view to
 * Unity's `Camera.worldToCameraMatrix` and @ref projection to `Camera.projectionMatrix`.
 */
#pragma once

#include <cmath>

#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Math/Matrix4.h"

namespace holotrack {

using pmsdk::Math::Vector3;
using pmsdk::Math::Matrix4;

/** @brief Result of an off-axis projection solve: a view and a projection matrix. */
struct OffAxisResult {
    Matrix4 view{};       /**< World→camera (eye at origin, screen basis). */
    Matrix4 projection{}; /**< Asymmetric perspective frustum. */
    float eyeToScreen{0.0f}; /**< Perpendicular eye→screen distance (metres); 0 if degenerate. */
    bool valid{false};    /**< False when the eye lies on/behind the screen plane (frustum undefined). */
};

/**
 * @brief Build the off-axis view+projection for one eye and one display surface.
 *
 * @param pa Screen bottom-left corner (world space).
 * @param pb Screen bottom-right corner (world space).
 * @param pc Screen top-left corner (world space).
 * @param eye Eye/head position (world space).
 * @param nearPlane Near clip distance (>0).
 * @param farPlane Far clip distance (> near).
 * @return The view+projection; @ref OffAxisResult::valid is false for a degenerate configuration
 *         (eye coincident with or behind the screen), in which case identity matrices are returned.
 */
inline OffAxisResult ComputeOffAxis(const Vector3& pa, const Vector3& pb, const Vector3& pc,
                                    const Vector3& eye, float nearPlane, float farPlane) {
    OffAxisResult out;

    // Orthonormal screen basis: vr (right), vu (up), vn (normal, toward the viewer).
    Vector3 vr = (pb - pa).Normalized();
    Vector3 vu = (pc - pa).Normalized();
    Vector3 vn = vr.Cross(vu).Normalized();

    // Vectors from the eye to three screen corners.
    const Vector3 va = pa - eye;
    const Vector3 vb = pb - eye;
    const Vector3 vc = pc - eye;

    // Perpendicular distance from the eye to the screen plane (along -vn).
    const float d = -va.Dot(vn);
    if (!(d > 1e-5f) || !(nearPlane > 0.0f) || !(farPlane > nearPlane)) {
        return out; // degenerate — leave identity matrices, valid=false
    }
    out.eyeToScreen = d;

    // Frustum extents at the near plane (scale the on-screen projections by near/d).
    const float nOverD = nearPlane / d;
    const float l = vr.Dot(va) * nOverD;
    const float r = vr.Dot(vb) * nOverD;
    const float b = vu.Dot(va) * nOverD;
    const float t = vu.Dot(vc) * nOverD;

    // Asymmetric perspective frustum (OpenGL convention), row-major args (Matrix4 stores column-major).
    const float rl = r - l;
    const float tb = t - b;
    const float fn = farPlane - nearPlane;
    if (!(std::fabs(rl) > 1e-6f) || !(std::fabs(tb) > 1e-6f)) {
        return out;
    }
    const Matrix4 frustum(
        2.0f * nearPlane / rl, 0.0f,                    (r + l) / rl,                    0.0f,
        0.0f,                  2.0f * nearPlane / tb,    (t + b) / tb,                    0.0f,
        0.0f,                  0.0f,                    -(farPlane + nearPlane) / fn,    -2.0f * farPlane * nearPlane / fn,
        0.0f,                  0.0f,                    -1.0f,                            0.0f);

    // Screen-space rotation: rows are the screen basis vectors.
    const Matrix4 rot(
        vr.x, vr.y, vr.z, 0.0f,
        vu.x, vu.y, vu.z, 0.0f,
        vn.x, vn.y, vn.z, 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f);

    out.view = rot * Matrix4::Translation(-eye);
    out.projection = frustum;
    out.valid = true;
    return out;
}

} // namespace holotrack
