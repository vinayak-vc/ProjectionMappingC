#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Core/Status.h"
#include "PMSDK/Math/Vector2.h"

#include <vector>

/// Auto Adjust Phase 3 (D-046): dense warp map for non-planar / curved skins.
///
/// Where a corner pin plus a 9 x 9 correction grid cannot follow the surface, the host measures
/// dense correspondences (Gray code projected through the skin) and asks for a per-skin map
/// "projector raster position -> content UV". The renderer then samples content at the mapped UV
/// instead of the mesh UV. Samples are splatted bilinearly onto the map, holes are filled by a
/// pull-push pyramid, and pixels farther than maxFillDistancePx from any sample stay invalid.
namespace pmsdk::AutoAdjust {

struct WarpMapSample {
    /// Projector raster position (normalized, y up).
    Math::Vector2 raster;
    /// Content UV that should appear there.
    Math::Vector2 uv;
};

struct WarpMapRect {
    /// Raster rectangle the map covers (normalized, y up): x, y = bottom-left corner.
    float x = 0.0f;
    float y = 0.0f;
    float width = 1.0f;
    float height = 1.0f;
};

struct WarpMapOptions {
    int width = 256;
    int height = 256;
    /// Holes farther than this (map pixels) from the nearest sample are left invalid.
    float maxFillDistancePx = 12.0f;
};

/// RGBA float map, row-major, row 0 at the rect's bottom (Unity texture order): R = u, G = v,
/// B = 0, A = 1 where valid, 0 elsewhere.
struct WarpMap {
    int width = 0;
    int height = 0;
    WarpMapRect rect;
    std::vector<float> rgba;
    /// Fraction of map pixels that are valid.
    float coverage = 0.0f;
};

[[nodiscard]] PMSDK_API Result<WarpMap> BuildWarpMap(const std::vector<WarpMapSample>& samples, const WarpMapRect& rect,
                                                     const WarpMapOptions& options = WarpMapOptions{});

} // namespace pmsdk::AutoAdjust
