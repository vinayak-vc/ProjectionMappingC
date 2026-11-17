#include "PMSDK/C_API/AutoAdjustAPI.h"
#include "PMSDK/AutoAdjust/AutoAdjust.h"
#include "PMSDK/AutoAdjust/Facets.h"
#include "PMSDK/AutoAdjust/Planes.h"
#include "PMSDK/AutoAdjust/WarpMap.h"

#include <new>

#include <algorithm>
#include <array>
#include <cstring>
#include <exception>
#include <string>
#include <vector>

using namespace pmsdk;

struct pmsdk_autoadjust_facets_t {
    std::vector<AutoAdjust::Facet> facets;
};

struct pmsdk_autoadjust_planes_t {
    std::vector<AutoAdjust::Plane> planes;
};

namespace {

thread_local std::string g_lastError;

void SetError(const std::string& message) {
    g_lastError = message;
}

pmsdk_status_t Fail(const Status& status) {
    SetError(status.message().empty() ? std::string(ToString(status.code())) : status.message());
    return status.code() == ErrorCode::InvalidArgument ? PMSDK_ERROR_INVALID_ARGUMENT : PMSDK_ERROR_UNKNOWN;
}

pmsdk_status_t FailMessage(pmsdk_status_t code, const char* message) {
    SetError(message);
    return code;
}

AutoAdjust::CheckerSpec ToSpec(const pmsdk_autoadjust_checker_t* in) {
    AutoAdjust::CheckerSpec spec;
    if (in != nullptr) {
        spec.squaresX = in->squares_x;
        spec.squaresY = in->squares_y;
        spec.margin = in->margin;
    }
    return spec;
}

AutoAdjust::RefineOptions ToOptions(const pmsdk_autoadjust_options_t* in) {
    AutoAdjust::RefineOptions options;
    if (in == nullptr) {
        return options;
    }
    options.damping = in->damping;
    options.gridColumns = in->grid_columns;
    options.gridRows = in->grid_rows;
    options.smoothness = in->smoothness;
    options.pinCorners = in->pin_corners != 0;
    options.maxStep = in->max_step;
    options.useTargetQuad = in->use_target_quad != 0;
    for (int i = 0; i < 4; ++i) {
        options.targetQuad[static_cast<std::size_t>(i)] = Math::Vector2(in->target_quad[i].x, in->target_quad[i].y);
    }
    return options;
}

bool ToWarp(const pmsdk_autoadjust_warp_t* in, AutoAdjust::SkinWarp& out) {
    if (in == nullptr) {
        return false;
    }
    for (int i = 0; i < 4; ++i) {
        out.corners[static_cast<std::size_t>(i)] = Math::Vector2(in->corners[i].x, in->corners[i].y);
    }
    out.correction = AutoAdjust::CorrectionGrid();
    if (in->grid_columns == 0 && in->grid_rows == 0) {
        return true;
    }
    if (in->grid_columns < 2 || in->grid_rows < 2 || in->grid_columns > 33 || in->grid_rows > 33 ||
        in->grid_offsets == nullptr) {
        return false;
    }
    out.correction.columns = in->grid_columns;
    out.correction.rows = in->grid_rows;
    const std::size_t n = static_cast<std::size_t>(in->grid_columns) * static_cast<std::size_t>(in->grid_rows);
    out.correction.offsets.resize(n);
    for (std::size_t i = 0; i < n; ++i) {
        out.correction.offsets[i] = Math::Vector2(in->grid_offsets[i].x, in->grid_offsets[i].y);
    }
    return true;
}

std::vector<Math::Vector2> ToVectors(const pmsdk_vec2_t* points, std::size_t count) {
    std::vector<Math::Vector2> out;
    out.reserve(count);
    for (std::size_t i = 0; i < count; ++i) {
        out.emplace_back(points[i].x, points[i].y);
    }
    return out;
}

AutoAdjust::CameraIntrinsics ToIntrinsics(const pmsdk_autoadjust_intrinsics_t& in) {
    AutoAdjust::CameraIntrinsics k;
    k.model = in.model == 1 ? AutoAdjust::LensModel::Fisheye : AutoAdjust::LensModel::Perspective;
    k.fx = in.fx;
    k.fy = in.fy;
    k.cx = in.cx;
    k.cy = in.cy;
    const int count = std::clamp(in.distortion_count, 0, 14);
    k.distortion.assign(in.distortion, in.distortion + count);
    return k;
}

std::size_t CopyVectors(const std::vector<Math::Vector2>& points, pmsdk_vec2_t* out, std::size_t capacity) {
    if (out != nullptr) {
        const std::size_t n = std::min(points.size(), capacity);
        for (std::size_t i = 0; i < n; ++i) {
            out[i].x = points[i].x;
            out[i].y = points[i].y;
        }
    }
    return points.size();
}

void WriteQuad(const std::array<Math::Vector2, 4>& quad, pmsdk_vec2_t* out) {
    for (std::size_t i = 0; i < 4; ++i) {
        out[i].x = quad[i].x;
        out[i].y = quad[i].y;
    }
}

void WriteScore(const AutoAdjust::Score& score, pmsdk_autoadjust_score_t* out) {
    if (out == nullptr) {
        return;
    }
    out->valid_count = score.validCount;
    out->rms_px = score.rmsPx;
    out->max_px = score.maxPx;
}

} // namespace

extern "C" {

PMSDK_API void pmsdk_autoadjust_checker_init(pmsdk_autoadjust_checker_t* checker) {
    if (checker == nullptr) {
        return;
    }
    const AutoAdjust::CheckerSpec defaults;
    checker->squares_x = defaults.squaresX;
    checker->squares_y = defaults.squaresY;
    checker->margin = defaults.margin;
}

PMSDK_API void pmsdk_autoadjust_options_init(pmsdk_autoadjust_options_t* options) {
    if (options == nullptr) {
        return;
    }
    const AutoAdjust::RefineOptions defaults;
    options->damping = defaults.damping;
    options->grid_columns = defaults.gridColumns;
    options->grid_rows = defaults.gridRows;
    options->smoothness = defaults.smoothness;
    options->pin_corners = defaults.pinCorners ? 1 : 0;
    options->max_step = defaults.maxStep;
    options->use_target_quad = 0;
    for (int i = 0; i < 4; ++i) {
        options->target_quad[i].x = 0.0f;
        options->target_quad[i].y = 0.0f;
    }
}

PMSDK_API const char* pmsdk_autoadjust_get_last_error(void) {
    return g_lastError.c_str();
}

PMSDK_API size_t pmsdk_autoadjust_checker_corners(const pmsdk_autoadjust_checker_t* checker, pmsdk_vec2_t* out, size_t capacity) {
    try {
        if (checker == nullptr) {
            SetError("checker is NULL");
            return 0;
        }
        Result<std::vector<Math::Vector2>> uvs = AutoAdjust::CheckerCornerUVs(ToSpec(checker));
        if (!uvs.ok()) {
            Fail(uvs.status());
            return 0;
        }
        const std::vector<Math::Vector2>& points = uvs.value();
        if (out != nullptr) {
            const std::size_t n = std::min(points.size(), capacity);
            for (std::size_t i = 0; i < n; ++i) {
                out[i].x = points[i].x;
                out[i].y = points[i].y;
            }
        }
        return points.size();
    } catch (const std::exception& ex) {
        SetError(ex.what());
        return 0;
    } catch (...) {
        SetError("unknown error");
        return 0;
    }
}

PMSDK_API pmsdk_status_t pmsdk_autoadjust_render_checker(const pmsdk_autoadjust_checker_t* checker, int32_t width, int32_t height,
                                                         int32_t bottom_row_first, uint8_t* out, size_t capacity) {
    try {
        if (checker == nullptr || out == nullptr) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "checker and out must not be NULL");
        }
        if (width <= 0 || height <= 0 ||
            capacity < static_cast<std::size_t>(width) * static_cast<std::size_t>(height)) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "output buffer smaller than width * height");
        }
        Result<std::vector<std::uint8_t>> image = AutoAdjust::RenderChecker(ToSpec(checker), width, height, bottom_row_first != 0);
        if (!image.ok()) {
            return Fail(image.status());
        }
        std::memcpy(out, image.value().data(), image.value().size());
        return PMSDK_SUCCESS;
    } catch (const std::exception& ex) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, ex.what());
    } catch (...) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, "unknown error");
    }
}

PMSDK_API pmsdk_status_t pmsdk_autoadjust_detect(const uint8_t* gray, int32_t width, int32_t height, int32_t stride,
                                                 const pmsdk_autoadjust_checker_t* checker, const pmsdk_vec2_t* predicted,
                                                 float min_contrast, pmsdk_vec2_t* out_corners, uint8_t* out_valid,
                                                 size_t capacity, int32_t* out_valid_count, int32_t* out_whole_board) {
    try {
        if (checker == nullptr || out_corners == nullptr || out_valid == nullptr) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "checker, out_corners and out_valid must not be NULL");
        }
        const AutoAdjust::CheckerSpec spec = ToSpec(checker);
        const int count = AutoAdjust::CheckerCornerCount(spec);
        if (count <= 0) {
            return Fail(AutoAdjust::ValidateChecker(spec));
        }
        if (capacity < static_cast<std::size_t>(count)) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "capacity smaller than the checker corner count");
        }
        std::vector<Math::Vector2> predictedPoints;
        if (predicted != nullptr) {
            predictedPoints = ToVectors(predicted, static_cast<std::size_t>(count));
        }
        AutoAdjust::DetectOptions options;
        if (min_contrast > 0.0f) {
            options.minContrast = min_contrast;
        }
        Result<AutoAdjust::Detection> detection = AutoAdjust::DetectChecker(
            gray, width, height, stride, spec, predicted != nullptr ? &predictedPoints : nullptr, options);
        if (out_valid_count != nullptr) {
            *out_valid_count = 0;
        }
        if (out_whole_board != nullptr) {
            *out_whole_board = 0;
        }
        if (!detection.ok()) {
            std::memset(out_valid, 0, static_cast<std::size_t>(count));
            return Fail(detection.status());
        }
        const AutoAdjust::Detection& d = detection.value();
        for (int k = 0; k < count; ++k) {
            out_corners[k].x = d.corners[static_cast<std::size_t>(k)].x;
            out_corners[k].y = d.corners[static_cast<std::size_t>(k)].y;
            out_valid[k] = d.valid[static_cast<std::size_t>(k)];
        }
        if (out_valid_count != nullptr) {
            *out_valid_count = d.validCount;
        }
        if (out_whole_board != nullptr) {
            *out_whole_board = d.wholeBoard ? 1 : 0;
        }
        return PMSDK_SUCCESS;
    } catch (const std::exception& ex) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, ex.what());
    } catch (...) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, "unknown error");
    }
}

PMSDK_API pmsdk_status_t pmsdk_autoadjust_undistort(const pmsdk_vec2_t* in, pmsdk_vec2_t* out, size_t count,
                                                    const pmsdk_autoadjust_intrinsics_t* intrinsics) {
    try {
        if (in == nullptr || out == nullptr || intrinsics == nullptr) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "in, out and intrinsics must not be NULL");
        }
        if (intrinsics->distortion_count < 0 || intrinsics->distortion_count > 14) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "distortion_count must be 0..14");
        }
        Result<std::vector<Math::Vector2>> undistorted = AutoAdjust::UndistortPoints(ToVectors(in, count), ToIntrinsics(*intrinsics));
        if (!undistorted.ok()) {
            return Fail(undistorted.status());
        }
        for (std::size_t i = 0; i < count; ++i) {
            out[i].x = undistorted.value()[i].x;
            out[i].y = undistorted.value()[i].y;
        }
        return PMSDK_SUCCESS;
    } catch (const std::exception& ex) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, ex.what());
    } catch (...) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, "unknown error");
    }
}

PMSDK_API pmsdk_status_t pmsdk_autoadjust_measure(const pmsdk_vec2_t* uvs, const pmsdk_vec2_t* camera, const uint8_t* valid,
                                                  size_t count, const pmsdk_autoadjust_options_t* options,
                                                  pmsdk_autoadjust_score_t* out_score) {
    try {
        if (uvs == nullptr || camera == nullptr || valid == nullptr || out_score == nullptr) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "uvs, camera, valid and out_score must not be NULL");
        }
        const std::vector<std::uint8_t> flags(valid, valid + count);
        Result<AutoAdjust::Score> score =
            AutoAdjust::Measure(ToVectors(uvs, count), ToVectors(camera, count), flags, ToOptions(options));
        if (!score.ok()) {
            return Fail(score.status());
        }
        WriteScore(score.value(), out_score);
        return PMSDK_SUCCESS;
    } catch (const std::exception& ex) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, ex.what());
    } catch (...) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, "unknown error");
    }
}

PMSDK_API pmsdk_status_t pmsdk_autoadjust_refine(const pmsdk_autoadjust_warp_t* warp, const pmsdk_vec2_t* uvs,
                                                 const pmsdk_vec2_t* camera, const uint8_t* valid, size_t count,
                                                 const pmsdk_autoadjust_options_t* options,
                                                 pmsdk_autoadjust_score_t* out_before, pmsdk_vec2_t* out_offsets,
                                                 size_t capacity) {
    try {
        if (uvs == nullptr || camera == nullptr || valid == nullptr || out_offsets == nullptr) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "uvs, camera, valid and out_offsets must not be NULL");
        }
        AutoAdjust::SkinWarp current;
        if (!ToWarp(warp, current)) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "invalid warp (NULL, or grid not 0x0 / 2..33 with offsets)");
        }
        const AutoAdjust::RefineOptions refineOptions = ToOptions(options);
        const std::size_t needed =
            static_cast<std::size_t>(std::max(0, refineOptions.gridColumns)) * static_cast<std::size_t>(std::max(0, refineOptions.gridRows));
        if (capacity < needed) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "capacity smaller than grid_columns * grid_rows");
        }
        const std::vector<std::uint8_t> flags(valid, valid + count);
        Result<AutoAdjust::RefineResult> result =
            AutoAdjust::Refine(current, ToVectors(uvs, count), ToVectors(camera, count), flags, refineOptions);
        if (!result.ok()) {
            return Fail(result.status());
        }
        WriteScore(result.value().before, out_before);
        const std::vector<Math::Vector2>& offsets = result.value().correction.offsets;
        for (std::size_t i = 0; i < offsets.size(); ++i) {
            out_offsets[i].x = offsets[i].x;
            out_offsets[i].y = offsets[i].y;
        }
        return PMSDK_SUCCESS;
    } catch (const std::exception& ex) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, ex.what());
    } catch (...) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, "unknown error");
    }
}

PMSDK_API pmsdk_status_t pmsdk_autoadjust_evaluate_warp(const pmsdk_autoadjust_warp_t* warp, float u, float v,
                                                        pmsdk_vec2_t* out_point) {
    try {
        if (out_point == nullptr) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "out_point must not be NULL");
        }
        AutoAdjust::SkinWarp current;
        if (!ToWarp(warp, current)) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "invalid warp (NULL, or grid not 0x0 / 2..33 with offsets)");
        }
        const Math::Vector2 p = AutoAdjust::EvaluateWarp(current, u, v);
        out_point->x = p.x;
        out_point->y = p.y;
        return PMSDK_SUCCESS;
    } catch (const std::exception& ex) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, ex.what());
    } catch (...) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, "unknown error");
    }
}

PMSDK_API pmsdk_status_t pmsdk_autoadjust_evaluate_warp_many(const pmsdk_autoadjust_warp_t* warp, const pmsdk_vec2_t* uvs, size_t count,
                                                             pmsdk_vec2_t* out_points) {
    try {
        if (uvs == nullptr || out_points == nullptr) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "uvs and out_points must not be NULL");
        }
        AutoAdjust::SkinWarp current;
        if (!ToWarp(warp, current)) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "invalid warp (NULL, or grid not 0x0 / 2..33 with offsets)");
        }
        for (std::size_t i = 0; i < count; ++i) {
            const Math::Vector2 p = AutoAdjust::EvaluateWarp(current, uvs[i].x, uvs[i].y);
            out_points[i].x = p.x;
            out_points[i].y = p.y;
        }
        return PMSDK_SUCCESS;
    } catch (const std::exception& ex) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, ex.what());
    } catch (...) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, "unknown error");
    }
}

// ------------------------------------------------------------------ Phase 2: facets

PMSDK_API void pmsdk_autoadjust_facet_options_init(pmsdk_autoadjust_facet_options_t* options) {
    if (options == nullptr) {
        return;
    }
    const AutoAdjust::FacetOptions d;
    options->min_contrast = d.minContrast;
    options->blur_sigma = d.blurSigma;
    options->edge_threshold = d.edgeThreshold;
    options->regrow_px = d.regrowPx;
    options->min_area_fraction = d.minAreaFraction;
    options->polygon_epsilon_px = d.polygonEpsilonPx;
    options->max_facets = d.maxFacets;
}

PMSDK_API pmsdk_autoadjust_facets_t* pmsdk_autoadjust_segment_facets(const uint8_t* white, const uint8_t* black, int32_t width,
                                                                     int32_t height, int32_t stride,
                                                                     const pmsdk_autoadjust_facet_options_t* options) {
    try {
        AutoAdjust::FacetOptions o;
        if (options != nullptr) {
            o.minContrast = options->min_contrast;
            o.blurSigma = options->blur_sigma;
            o.edgeThreshold = options->edge_threshold;
            o.regrowPx = options->regrow_px;
            o.minAreaFraction = options->min_area_fraction;
            o.polygonEpsilonPx = options->polygon_epsilon_px;
            o.maxFacets = options->max_facets;
        }
        Result<std::vector<AutoAdjust::Facet>> facets = AutoAdjust::SegmentFacets(white, black, width, height, stride, o);
        if (!facets.ok()) {
            Fail(facets.status());
            return nullptr;
        }
        pmsdk_autoadjust_facets_t* handle = new (std::nothrow) pmsdk_autoadjust_facets_t{std::move(facets).value()};
        if (handle == nullptr) {
            SetError("out of memory");
        }
        return handle;
    } catch (const std::exception& ex) {
        SetError(ex.what());
        return nullptr;
    } catch (...) {
        SetError("unknown error");
        return nullptr;
    }
}

PMSDK_API void pmsdk_autoadjust_facets_destroy(pmsdk_autoadjust_facets_t* facets) {
    delete facets;
}

PMSDK_API size_t pmsdk_autoadjust_facets_count(const pmsdk_autoadjust_facets_t* facets) {
    return facets != nullptr ? facets->facets.size() : 0;
}

PMSDK_API pmsdk_status_t pmsdk_autoadjust_facets_get(const pmsdk_autoadjust_facets_t* facets, size_t index, pmsdk_vec2_t* out_quad4,
                                                     int32_t* out_from_outline, float* out_area_px, float* out_mean_contrast) {
    if (facets == nullptr || index >= facets->facets.size() || out_quad4 == nullptr) {
        return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "invalid facets handle, index or output");
    }
    const AutoAdjust::Facet& f = facets->facets[index];
    WriteQuad(f.quad, out_quad4);
    if (out_from_outline != nullptr) {
        *out_from_outline = f.quadFromOutline ? 1 : 0;
    }
    if (out_area_px != nullptr) {
        *out_area_px = f.areaPx;
    }
    if (out_mean_contrast != nullptr) {
        *out_mean_contrast = f.meanContrast;
    }
    return PMSDK_SUCCESS;
}

PMSDK_API size_t pmsdk_autoadjust_facets_copy_polygon(const pmsdk_autoadjust_facets_t* facets, size_t index, pmsdk_vec2_t* out,
                                                      size_t capacity) {
    if (facets == nullptr || index >= facets->facets.size()) {
        return 0;
    }
    return CopyVectors(facets->facets[index].polygon, out, capacity);
}

PMSDK_API int32_t pmsdk_autoadjust_facets_find_containing(const pmsdk_autoadjust_facets_t* facets, const pmsdk_vec2_t* points,
                                                          const uint8_t* valid, size_t count) {
    if (facets == nullptr || points == nullptr) {
        return -1;
    }
    try {
        std::vector<std::uint8_t> flags = valid != nullptr ? std::vector<std::uint8_t>(valid, valid + count)
                                                           : std::vector<std::uint8_t>(count, 1);
        return AutoAdjust::FindFacetContaining(facets->facets, ToVectors(points, count), flags);
    } catch (...) {
        return -1;
    }
}

PMSDK_API pmsdk_status_t pmsdk_autoadjust_solve_corners(const pmsdk_autoadjust_warp_t* warp, const pmsdk_vec2_t* uvs,
                                                        const pmsdk_vec2_t* camera, const uint8_t* valid, size_t count,
                                                        const pmsdk_vec2_t* target_quad4, pmsdk_vec2_t* out_corners4) {
    try {
        if (uvs == nullptr || camera == nullptr || valid == nullptr || target_quad4 == nullptr || out_corners4 == nullptr) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "inputs and outputs must not be NULL");
        }
        AutoAdjust::SkinWarp current;
        if (!ToWarp(warp, current)) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "invalid warp (NULL, or grid not 0x0 / 2..33 with offsets)");
        }
        std::array<Math::Vector2, 4> target{};
        for (std::size_t i = 0; i < 4; ++i) {
            target[i] = Math::Vector2(target_quad4[i].x, target_quad4[i].y);
        }
        Result<std::array<Math::Vector2, 4>> corners = AutoAdjust::SolveCornersForTarget(
            current, ToVectors(uvs, count), ToVectors(camera, count), std::vector<std::uint8_t>(valid, valid + count), target);
        if (!corners.ok()) {
            return Fail(corners.status());
        }
        WriteQuad(corners.value(), out_corners4);
        return PMSDK_SUCCESS;
    } catch (const std::exception& ex) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, ex.what());
    } catch (...) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, "unknown error");
    }
}

PMSDK_API float pmsdk_autoadjust_metric_scale(const pmsdk_vec2_t* quad4, float width_mm, float height_mm) {
    if (quad4 == nullptr) {
        return 0.0f;
    }
    std::array<Math::Vector2, 4> quad{};
    for (std::size_t i = 0; i < 4; ++i) {
        quad[i] = Math::Vector2(quad4[i].x, quad4[i].y);
    }
    return AutoAdjust::MetricScaleMmPerPx(quad, width_mm, height_mm);
}

// ------------------------------------------------------------------ Phase 3: warp map

PMSDK_API void pmsdk_autoadjust_warpmap_options_init(pmsdk_autoadjust_warpmap_options_t* options) {
    if (options == nullptr) {
        return;
    }
    const AutoAdjust::WarpMapOptions d;
    options->width = d.width;
    options->height = d.height;
    options->max_fill_distance_px = d.maxFillDistancePx;
}

PMSDK_API pmsdk_status_t pmsdk_autoadjust_build_warp_map(const pmsdk_vec2_t* raster, const pmsdk_vec2_t* uv, size_t count,
                                                         float rect_x, float rect_y, float rect_width, float rect_height,
                                                         const pmsdk_autoadjust_warpmap_options_t* options, float* out_rgba,
                                                         size_t capacity_floats, float* out_coverage) {
    try {
        if (raster == nullptr || uv == nullptr || out_rgba == nullptr) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "raster, uv and out_rgba must not be NULL");
        }
        AutoAdjust::WarpMapOptions o;
        if (options != nullptr) {
            o.width = options->width;
            o.height = options->height;
            o.maxFillDistancePx = options->max_fill_distance_px;
        }
        if (o.width <= 0 || o.height <= 0 ||
            capacity_floats < static_cast<std::size_t>(o.width) * static_cast<std::size_t>(o.height) * 4) {
            return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "capacity smaller than width * height * 4");
        }
        std::vector<AutoAdjust::WarpMapSample> samples(count);
        for (std::size_t i = 0; i < count; ++i) {
            samples[i].raster = Math::Vector2(raster[i].x, raster[i].y);
            samples[i].uv = Math::Vector2(uv[i].x, uv[i].y);
        }
        AutoAdjust::WarpMapRect rect;
        rect.x = rect_x;
        rect.y = rect_y;
        rect.width = rect_width;
        rect.height = rect_height;
        Result<AutoAdjust::WarpMap> map = AutoAdjust::BuildWarpMap(samples, rect, o);
        if (!map.ok()) {
            return Fail(map.status());
        }
        std::memcpy(out_rgba, map.value().rgba.data(), map.value().rgba.size() * sizeof(float));
        if (out_coverage != nullptr) {
            *out_coverage = map.value().coverage;
        }
        return PMSDK_SUCCESS;
    } catch (const std::exception& ex) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, ex.what());
    } catch (...) {
        return FailMessage(PMSDK_ERROR_UNKNOWN, "unknown error");
    }
}

// ------------------------------------------------------------------ Phase 4: planes

PMSDK_API void pmsdk_autoadjust_plane_options_init(pmsdk_autoadjust_plane_options_t* options) {
    if (options == nullptr) {
        return;
    }
    const AutoAdjust::PlaneOptions d;
    options->step = d.step;
    options->min_depth_mm = d.minDepthMm;
    options->max_depth_mm = d.maxDepthMm;
    options->inlier_threshold_mm = d.inlierThresholdMm;
    options->iterations = d.iterations;
    options->min_inlier_fraction = d.minInlierFraction;
    options->max_planes = d.maxPlanes;
    options->polygon_epsilon_px = d.polygonEpsilonPx;
    options->seed = d.seed;
}

PMSDK_API pmsdk_autoadjust_planes_t* pmsdk_autoadjust_detect_planes(const uint16_t* depth_mm, int32_t width, int32_t height,
                                                                    int32_t stride, const pmsdk_autoadjust_intrinsics_t* intrinsics,
                                                                    const pmsdk_autoadjust_plane_options_t* options) {
    try {
        if (intrinsics == nullptr || intrinsics->distortion_count < 0 || intrinsics->distortion_count > 14) {
            SetError("intrinsics must not be NULL and distortion_count must be 0..14");
            return nullptr;
        }
        AutoAdjust::PlaneOptions o;
        if (options != nullptr) {
            o.step = options->step;
            o.minDepthMm = options->min_depth_mm;
            o.maxDepthMm = options->max_depth_mm;
            o.inlierThresholdMm = options->inlier_threshold_mm;
            o.iterations = options->iterations;
            o.minInlierFraction = options->min_inlier_fraction;
            o.maxPlanes = options->max_planes;
            o.polygonEpsilonPx = options->polygon_epsilon_px;
            o.seed = options->seed;
        }
        Result<std::vector<AutoAdjust::Plane>> planes =
            AutoAdjust::DetectPlanes(depth_mm, width, height, stride, ToIntrinsics(*intrinsics), o);
        if (!planes.ok()) {
            Fail(planes.status());
            return nullptr;
        }
        pmsdk_autoadjust_planes_t* handle = new (std::nothrow) pmsdk_autoadjust_planes_t{std::move(planes).value()};
        if (handle == nullptr) {
            SetError("out of memory");
        }
        return handle;
    } catch (const std::exception& ex) {
        SetError(ex.what());
        return nullptr;
    } catch (...) {
        SetError("unknown error");
        return nullptr;
    }
}

PMSDK_API void pmsdk_autoadjust_planes_destroy(pmsdk_autoadjust_planes_t* planes) {
    delete planes;
}

PMSDK_API size_t pmsdk_autoadjust_planes_count(const pmsdk_autoadjust_planes_t* planes) {
    return planes != nullptr ? planes->planes.size() : 0;
}

PMSDK_API pmsdk_status_t pmsdk_autoadjust_planes_get(const pmsdk_autoadjust_planes_t* planes, size_t index,
                                                     pmsdk_autoadjust_plane_info_t* out_info) {
    if (planes == nullptr || index >= planes->planes.size() || out_info == nullptr) {
        return FailMessage(PMSDK_ERROR_INVALID_ARGUMENT, "invalid planes handle, index or output");
    }
    const AutoAdjust::Plane& p = planes->planes[index];
    out_info->normal.x = p.normal.x;
    out_info->normal.y = p.normal.y;
    out_info->normal.z = p.normal.z;
    out_info->d = p.d;
    out_info->inlier_count = p.inlierCount;
    out_info->rms_mm = p.rmsMm;
    out_info->width_mm = p.widthMm;
    out_info->height_mm = p.heightMm;
    out_info->view_angle_deg = p.viewAngleDeg;
    WriteQuad(p.quad, out_info->quad);
    return PMSDK_SUCCESS;
}

PMSDK_API size_t pmsdk_autoadjust_planes_copy_polygon(const pmsdk_autoadjust_planes_t* planes, size_t index, pmsdk_vec2_t* out,
                                                      size_t capacity) {
    if (planes == nullptr || index >= planes->planes.size()) {
        return 0;
    }
    return CopyVectors(planes->planes[index].polygon, out, capacity);
}

} // extern "C"
