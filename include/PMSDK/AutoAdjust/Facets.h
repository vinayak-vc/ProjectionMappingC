#pragma once

#include "PMSDK/AutoAdjust/AutoAdjust.h"
#include "PMSDK/Core/Export.h"
#include "PMSDK/Core/Status.h"
#include "PMSDK/Math/Vector2.h"

#include <array>
#include <cstdint>
#include <vector>

/// Auto Adjust Phase 2 (D-045): several facets of a folded surface.
///
/// A folded surface lit evenly by the projector shows each flat facet at its own brightness (the
/// light hits it at its own angle). SegmentFacets splits the lit area of a white capture (minus a
/// black capture) at brightness discontinuities into facet regions, each with an outline polygon
/// and a fitted quad. A skin is then matched to the facet that contains its detected checker
/// corners, and SolveCornersForTarget moves the skin's corner pin so its content corners land on
/// that facet's quad ("snap to facet edges"). Camera pixels, origin top-left.
namespace pmsdk::AutoAdjust {

struct FacetOptions {
    /// Minimum white - black grey-level difference for a pixel to count as lit by the projector.
    float minContrast = 25.0f;
    /// Gaussian blur sigma (pixels) applied before edge detection.
    float blurSigma = 1.5f;
    /// A pixel is a facet border where the blurred log-brightness changes faster than this per pixel.
    /// A 30 % brightness step with the default blur peaks near 0.07; sensor noise stays below 0.01.
    float edgeThreshold = 0.035f;
    /// Border pixels are given back to the neighbouring facets over at most this many pixels.
    int regrowPx = 12;
    /// Regions smaller than this fraction of the image are dropped.
    float minAreaFraction = 0.002f;
    /// Outline simplification tolerance in pixels.
    float polygonEpsilonPx = 3.0f;
    /// Keep at most this many facets (largest first).
    int maxFacets = 32;
};

struct Facet {
    /// Simplified outline, camera pixels.
    std::vector<Math::Vector2> polygon;
    /// Fitted quad in content order as seen by an upright camera: bottom-left, bottom-right,
    /// top-right, top-left (image "bottom" = larger y).
    std::array<Math::Vector2, 4> quad{};
    /// True when the outline simplified to exactly 4 corners; false when the quad is the minimum-area
    /// rectangle fallback (the facet is not quadrilateral).
    bool quadFromOutline = false;
    /// Region area in pixels.
    float areaPx = 0.0f;
    /// Mean white - black difference inside the region.
    float meanContrast = 0.0f;
};

/// Splits the lit area into facets. Images are 8-bit grey, same size, rows top-to-bottom.
[[nodiscard]] PMSDK_API Result<std::vector<Facet>> SegmentFacets(const std::uint8_t* white, const std::uint8_t* black, int width,
                                                                 int height, int stride, const FacetOptions& options = FacetOptions{});

/// Quad that best fits an outline (camera pixels): the convex hull simplified to 4 corners, or the
/// minimum-area rectangle when that fails (@p fromOutline = false). Ordered like OrderQuad.
[[nodiscard]] PMSDK_API std::array<Math::Vector2, 4> FitQuadToOutline(const std::vector<Math::Vector2>& outline, bool* fromOutline);

/// Orders 4 image points as bottom-left, bottom-right, top-right, top-left (upright camera).
[[nodiscard]] PMSDK_API std::array<Math::Vector2, 4> OrderQuad(const std::array<Math::Vector2, 4>& corners);

/// Index of the facet whose outline contains the most valid points, or -1 when none contains any.
[[nodiscard]] PMSDK_API int FindFacetContaining(const std::vector<Facet>& facets, const std::vector<Math::Vector2>& points,
                                                const std::vector<std::uint8_t>& valid);

/// New corner-pin corners (raster, BL, BR, TR, TL) so that the content corners appear at
/// @p targetQuad (camera pixels, content BL, BR, TR, TL). The camera -> projector relation is fitted to
/// the current corner correspondences (projector side from @p current), so the correction grid
/// should be cleared after applying the result. Needs at least 4 valid corners.
[[nodiscard]] PMSDK_API Result<std::array<Math::Vector2, 4>> SolveCornersForTarget(const SkinWarp& current,
                                                                                   const std::vector<Math::Vector2>& uvs,
                                                                                   const std::vector<Math::Vector2>& camera,
                                                                                   const std::vector<std::uint8_t>& valid,
                                                                                   const std::array<Math::Vector2, 4>& targetQuad);

/// Millimetres per camera pixel on a facet of known size, from its quad (BL, BR, TR, TL): the mean
/// of the four edge ratios (bottom/top edges against the width, left/right against the height).
/// Returns 0 for degenerate input. An approximation: perspective makes the scale vary across the facet.
[[nodiscard]] PMSDK_API float MetricScaleMmPerPx(const std::array<Math::Vector2, 4>& quad, float widthMm, float heightMm);

} // namespace pmsdk::AutoAdjust
