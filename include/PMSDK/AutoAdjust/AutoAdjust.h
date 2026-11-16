#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Core/Status.h"
#include "PMSDK/Math/Vector2.h"

#include <array>
#include <cstddef>
#include <cstdint>
#include <vector>

/// Camera-assisted verify-and-refine of one planar skin (Auto Adjust, Phase 1, D-044).
///
/// The host projects a checkerboard inside a skin, captures it with a fixed camera, and calls:
///   1. DetectChecker      -> checker inner corners in the camera image (raw pixels)
///   2. UndistortPoints    -> remove the camera's lens distortion (factory intrinsics)
///   3. Measure / Refine   -> residual score and an updated per-skin correction grid
/// and repeats until the score is below the target.
///
/// Definition of "correct" (viewer-perspective, O1): seen from the camera, the content is an exact
/// perspective image of the unit square, i.e. content UV -> camera pixel is a single homography T.
/// By default T is the least-squares fit to the detected corners, which keeps the skin where the user
/// placed it and only straightens the interior (corners stay pinned). A caller may instead give an
/// explicit target quad in camera pixels.
///
/// Coordinate conventions:
///   - content UV: [0,1]^2, origin bottom-left, v up (Unity mesh UV).
///   - projector raster: normalized [0,1]^2, origin bottom-left, y up (PMSDKSkin vertices).
///   - camera: pixels, origin top-left, y down.
namespace pmsdk::AutoAdjust {

/// Checkerboard drawn inside a skin's content: a white margin, then squaresX x squaresY squares.
/// The top-left square (as seen on the content) is black. squaresX + squaresY must be odd so the
/// board's 180-degree rotation is distinguishable by colour.
struct CheckerSpec {
    int squaresX = 10;
    int squaresY = 7;
    /// White border as a fraction of the content size on each side (0..0.4).
    float margin = 0.08f;
};

/// Validates @p spec. Returns InvalidArgument with a message when unusable.
[[nodiscard]] PMSDK_API Status ValidateChecker(const CheckerSpec& spec);

/// Inner-corner count: (squaresX - 1) * (squaresY - 1).
[[nodiscard]] PMSDK_API int CheckerCornerCount(const CheckerSpec& spec);

/// Content UV of every inner corner, row-major: index = j * (squaresX - 1) + i, i left to right,
/// j top to bottom (as seen on the content).
[[nodiscard]] PMSDK_API Result<std::vector<Math::Vector2>> CheckerCornerUVs(const CheckerSpec& spec);

/// Renders the checker as 8-bit grey (0 black, 255 white). Rows are top-to-bottom unless
/// @p bottomRowFirst (Unity Texture2D raw data order).
[[nodiscard]] PMSDK_API Result<std::vector<std::uint8_t>> RenderChecker(const CheckerSpec& spec, int width, int height,
                                                                       bool bottomRowFirst);

/// Detection tunables.
struct DetectOptions {
    /// Minimum grey-level difference between dark and bright quadrants for a corner checked locally.
    float minContrast = 20.0f;
};

/// Checker corners found in a camera image (raw, distorted pixel coordinates).
struct Detection {
    /// Same order as CheckerCornerUVs. Entries with valid[k] == 0 are undefined.
    std::vector<Math::Vector2> corners;
    std::vector<std::uint8_t> valid;
    int validCount = 0;
    /// True when the whole board was found in one piece (global detector).
    bool wholeBoard = false;
};

/// Finds the checker's inner corners in an 8-bit grey image (rows top-to-bottom, @p stride bytes).
/// First tries the whole-board detector (OpenCV findChessboardCornersSB). If that fails and
/// @p predicted (one raw camera point per corner, e.g. the previous iteration's detection) is
/// given, each corner is refined locally around its prediction and accepted only if it shows the
/// checker's saddle pattern, so partly occluded boards still yield the visible corners.
/// Returns NotFound when neither path finds at least 4 corners.
[[nodiscard]] PMSDK_API Result<Detection> DetectChecker(const std::uint8_t* gray, int width, int height, int stride,
                                                        const CheckerSpec& spec,
                                                        const std::vector<Math::Vector2>* predicted = nullptr,
                                                        const DetectOptions& options = DetectOptions{});

/// Lens model of the camera calibration.
enum class LensModel : std::int32_t {
    Perspective = 0, ///< OpenCV pinhole + up to 14 distortion coefficients.
    Fisheye = 1,     ///< OpenCV fisheye (first 4 coefficients k1..k4).
};

/// Camera intrinsics for the image size the detections come from.
struct CameraIntrinsics {
    LensModel model = LensModel::Perspective;
    float fx = 0.0f;
    float fy = 0.0f;
    float cx = 0.0f;
    float cy = 0.0f;
    /// OpenCV order: k1, k2, p1, p2, k3, k4, k5, k6, s1, s2, s3, s4, tx, ty (perspective);
    /// k1..k4 (fisheye). May be empty (no distortion).
    std::vector<float> distortion;
};

/// Removes lens distortion. Output stays in pixels of an ideal pinhole camera with the same
/// fx, fy, cx, cy, so scores remain comparable to raw pixels.
[[nodiscard]] PMSDK_API Result<std::vector<Math::Vector2>> UndistortPoints(const std::vector<Math::Vector2>& points,
                                                                          const CameraIntrinsics& intrinsics);

/// Per-skin correction: offsets (projector raster units) added after the corner pin, bilinear over
/// a columns x rows grid spanning content UV [0,1]^2. Row-major, row 0 at v = 0, column 0 at u = 0.
/// An empty grid (0 x 0) means no correction.
struct CorrectionGrid {
    int columns = 0;
    int rows = 0;
    std::vector<Math::Vector2> offsets;
};

/// Bilinear evaluation (zero for an empty grid). u, v are clamped to [0,1].
[[nodiscard]] PMSDK_API Math::Vector2 EvaluateCorrection(const CorrectionGrid& grid, float u, float v);

/// Resamples @p grid to columns x rows (bilinear). An empty input yields zeros.
[[nodiscard]] PMSDK_API CorrectionGrid ResampleCorrection(const CorrectionGrid& grid, int columns, int rows);

/// The current warp of a quad skin: corner pin (bottom-left, bottom-right, top-right, top-left,
/// projector raster) plus optional correction grid.
struct SkinWarp {
    std::array<Math::Vector2, 4> corners{Math::Vector2(0.0f, 0.0f), Math::Vector2(1.0f, 0.0f), Math::Vector2(1.0f, 1.0f),
                                         Math::Vector2(0.0f, 1.0f)};
    CorrectionGrid correction;
};

/// Projector raster position of content UV (u, v) under @p warp (same homography as
/// Geometry::PerspectiveWarp, plus the correction).
[[nodiscard]] PMSDK_API Math::Vector2 EvaluateWarp(const SkinWarp& warp, float u, float v);

/// Refinement / scoring tunables.
struct RefineOptions {
    /// Fraction of the computed correction applied per iteration (0..1]; < 1 damps oscillation.
    float damping = 0.8f;
    /// Correction grid resolution produced by Refine (2..33 each).
    int gridColumns = 9;
    int gridRows = 9;
    /// Weight of the grid's smoothness term relative to the data term (>= 0).
    float smoothness = 0.05f;
    /// Keep the four skin corners where they are (zero update at the grid's corner nodes).
    bool pinCorners = true;
    /// Largest allowed per-iteration change of any grid offset (projector raster units).
    float maxStep = 0.05f;
    /// Use @ref targetQuad (camera pixels, bottom-left, bottom-right, top-right, top-left of the
    /// content) instead of the least-squares fit as the definition of "correct".
    bool useTargetQuad = false;
    std::array<Math::Vector2, 4> targetQuad{};
};

/// Residual score in camera pixels (undistorted).
struct Score {
    int validCount = 0;
    float rmsPx = 0.0f;
    float maxPx = 0.0f;
};

/// Scores how far the detected corners are from the ideal perspective grid.
/// @param uvs content UV per corner, @param camera undistorted camera pixels, @param valid flags.
[[nodiscard]] PMSDK_API Result<Score> Measure(const std::vector<Math::Vector2>& uvs,
                                              const std::vector<Math::Vector2>& camera,
                                              const std::vector<std::uint8_t>& valid,
                                              const RefineOptions& options = RefineOptions{});

/// Result of one refinement step.
struct RefineResult {
    /// Score of the input (before applying the new correction).
    Score before;
    /// Full new correction grid (gridColumns x gridRows) to apply to the skin.
    CorrectionGrid correction;
};

/// Computes an updated correction grid that moves the observed corners toward the ideal grid.
/// The camera <-> projector relation is estimated from the corners themselves (their current
/// projector positions are known from @p current), so no separate Gray-code pass is needed.
/// Needs at least 8 valid corners.
[[nodiscard]] PMSDK_API Result<RefineResult> Refine(const SkinWarp& current,
                                                    const std::vector<Math::Vector2>& uvs,
                                                    const std::vector<Math::Vector2>& camera,
                                                    const std::vector<std::uint8_t>& valid,
                                                    const RefineOptions& options = RefineOptions{});

} // namespace pmsdk::AutoAdjust
