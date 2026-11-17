#pragma once

#include "PMSDK/AutoAdjust/AutoAdjust.h"
#include "PMSDK/Core/Export.h"
#include "PMSDK/Core/Status.h"
#include "PMSDK/Math/Vector2.h"
#include "PMSDK/Math/Vector3.h"

#include <array>
#include <cstdint>
#include <vector>

/// Auto Adjust Phase 4 (D-047): automatic surface identification from a depth image (OAK-D stereo
/// depth aligned to the colour camera). Planes are found by sequential RANSAC on the back-projected
/// point cloud, refined by least squares, and split into image-connected pieces, so two parallel
/// facets at different places are separate surfaces. Each plane comes with its image outline and
/// quad (colour-camera pixels, so it can be matched with checker / Gray-code captures) and its
/// metric size, which enables SURFACE-METRIC mode without typed-in sizes.
namespace pmsdk::AutoAdjust {

struct PlaneOptions {
    /// Use every step-th pixel in x and y.
    int step = 4;
    float minDepthMm = 300.0f;
    float maxDepthMm = 8000.0f;
    /// Point-to-plane distance counted as an inlier.
    float inlierThresholdMm = 12.0f;
    int iterations = 400;
    /// A plane needs at least this fraction of all usable points.
    float minInlierFraction = 0.02f;
    int maxPlanes = 8;
    float polygonEpsilonPx = 4.0f;
    std::uint32_t seed = 1;
};

struct Plane {
    /// Unit normal in camera space (x right, y down, z forward), facing the camera (z < 0).
    Math::Vector3 normal;
    /// Plane equation normal . X + d = 0, millimetres.
    float d = 0.0f;
    int inlierCount = 0;
    float rmsMm = 0.0f;
    /// Extent along the plane's two principal directions (2nd-98th percentile), millimetres.
    float widthMm = 0.0f;
    float heightMm = 0.0f;
    /// Outline in full-resolution image pixels.
    std::vector<Math::Vector2> polygon;
    /// Fitted quad (bottom-left, bottom-right, top-right, top-left as seen by an upright camera).
    std::array<Math::Vector2, 4> quad{};
    /// Angle between the normal and the camera's viewing axis, degrees (0 = facing the camera).
    float viewAngleDeg = 0.0f;
};

/// Detects planes in a depth image (millimetres, 0 = no data; @p stride in elements) taken with
/// @p intrinsics. Pixel rays are undistorted with the lens model before back-projection (wide lenses
/// would otherwise bend flat surfaces); outlines and quads stay in raw image pixels.
[[nodiscard]] PMSDK_API Result<std::vector<Plane>> DetectPlanes(const std::uint16_t* depthMm, int width, int height, int stride,
                                                                const CameraIntrinsics& intrinsics,
                                                                const PlaneOptions& options = PlaneOptions{});

} // namespace pmsdk::AutoAdjust
