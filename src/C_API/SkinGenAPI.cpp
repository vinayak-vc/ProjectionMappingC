#include "PMSDK/C_API/SkinGenAPI.h"
#include "PMSDK/SkinGen/SkinGen.h"

#include <algorithm>
#include <cstring>
#include <new>
#include <string>
#include <utility>

using namespace pmsdk;

struct pmsdk_skingen_result_t {
    SkinGen::Extraction extraction;
};

namespace {

thread_local std::string g_lastError;

void SetError(const std::string& message) {
    g_lastError = message;
}

SkinGen::Options ToOptions(const pmsdk_skingen_options_t* in) {
    SkinGen::Options o;
    if (in == nullptr) {
        return o;
    }
    o.mode = static_cast<SkinGen::SegmentationMode>(in->mode);
    o.alphaThreshold = in->alpha_threshold;
    o.colorTolerance = in->color_tolerance;
    o.grabCutMargin = in->grabcut_margin;
    o.grabCutIterations = in->grabcut_iterations;
    o.minPartAreaFraction = in->min_part_area_fraction;
    o.minHoleAreaFraction = in->min_hole_area_fraction;
    o.splitParts = in->split_parts != 0;
    o.thinPartFraction = in->thin_part_fraction;
    o.appendageMaxFraction = in->appendage_max_fraction;
    o.maxParts = in->max_parts;
    o.maxPolygonPoints = in->max_polygon_points;
    o.yUp = in->y_up != 0;
    return o;
}

pmsdk_skingen_result_t* Wrap(Result<SkinGen::Extraction>&& result) {
    if (!result.ok()) {
        SetError(result.status().message());
        return nullptr;
    }
    pmsdk_skingen_result_t* handle = new (std::nothrow) pmsdk_skingen_result_t{std::move(result).value()};
    if (handle == nullptr) {
        SetError("out of memory");
    }
    return handle;
}

const SkinGen::Part* GetPart(const pmsdk_skingen_result_t* result, size_t part) {
    if (result == nullptr || part >= result->extraction.parts.size()) {
        return nullptr;
    }
    return &result->extraction.parts[part];
}

size_t CopyPoints(const std::vector<Math::Vector2>& points, pmsdk_vec2_t* out, size_t capacity) {
    if (out != nullptr) {
        const size_t n = std::min(points.size(), capacity);
        for (size_t i = 0; i < n; ++i) {
            out[i].x = points[i].x;
            out[i].y = points[i].y;
        }
    }
    return points.size();
}

} // namespace

extern "C" {

PMSDK_API void pmsdk_skingen_options_init(pmsdk_skingen_options_t* options) {
    if (options == nullptr) {
        return;
    }
    const SkinGen::Options d;
    options->mode = static_cast<int32_t>(d.mode);
    options->alpha_threshold = d.alphaThreshold;
    options->color_tolerance = d.colorTolerance;
    options->grabcut_margin = d.grabCutMargin;
    options->grabcut_iterations = d.grabCutIterations;
    options->min_part_area_fraction = d.minPartAreaFraction;
    options->min_hole_area_fraction = d.minHoleAreaFraction;
    options->split_parts = d.splitParts ? 1 : 0;
    options->thin_part_fraction = d.thinPartFraction;
    options->appendage_max_fraction = d.appendageMaxFraction;
    options->max_parts = d.maxParts;
    options->max_polygon_points = d.maxPolygonPoints;
    options->y_up = d.yUp ? 1 : 0;
}

PMSDK_API pmsdk_skingen_result_t* pmsdk_skingen_extract_from_file(const char* path, const pmsdk_skingen_options_t* options) {
    try {
        if (path == nullptr || path[0] == '\0') {
            SetError("no file path supplied");
            return nullptr;
        }
        return Wrap(SkinGen::ExtractSkinsFromFile(path, ToOptions(options)));
    } catch (const std::exception& ex) {
        SetError(ex.what());
        return nullptr;
    } catch (...) {
        SetError("unknown error");
        return nullptr;
    }
}

PMSDK_API pmsdk_skingen_result_t* pmsdk_skingen_extract_from_pixels(const uint8_t* pixels, int32_t width, int32_t height,
                                                                    int32_t channels, const uint8_t* mask,
                                                                    const pmsdk_skingen_options_t* options) {
    try {
        if (pixels == nullptr || width <= 0 || height <= 0 || (channels != 1 && channels != 3 && channels != 4)) {
            SetError("invalid pixel buffer arguments");
            return nullptr;
        }
        SkinGen::ImageData image;
        image.width = width;
        image.height = height;
        image.channels = channels;
        const size_t total = static_cast<size_t>(width) * static_cast<size_t>(height) * static_cast<size_t>(channels);
        image.pixels.assign(pixels, pixels + total);
        return Wrap(SkinGen::ExtractSkins(image, ToOptions(options), mask));
    } catch (const std::exception& ex) {
        SetError(ex.what());
        return nullptr;
    } catch (...) {
        SetError("unknown error");
        return nullptr;
    }
}

PMSDK_API pmsdk_skingen_result_t* pmsdk_skingen_extract_from_memory(const uint8_t* bytes, size_t size,
                                                                    const pmsdk_skingen_options_t* options) {
    try {
        Result<SkinGen::ImageData> image = SkinGen::DecodeImage(bytes, size);
        if (!image.ok()) {
            SetError(image.status().message());
            return nullptr;
        }
        return Wrap(SkinGen::ExtractSkins(image.value(), ToOptions(options), nullptr));
    } catch (const std::exception& ex) {
        SetError(ex.what());
        return nullptr;
    } catch (...) {
        SetError("unknown error");
        return nullptr;
    }
}

PMSDK_API void pmsdk_skingen_result_destroy(pmsdk_skingen_result_t* result) {
    delete result;
}

PMSDK_API const char* pmsdk_skingen_get_last_error(void) {
    return g_lastError.c_str();
}

PMSDK_API pmsdk_status_t pmsdk_skingen_get_image_size(const pmsdk_skingen_result_t* result, int32_t* width, int32_t* height) {
    if (result == nullptr || width == nullptr || height == nullptr) {
        return PMSDK_ERROR_INVALID_ARGUMENT;
    }
    *width = result->extraction.imageWidth;
    *height = result->extraction.imageHeight;
    return PMSDK_SUCCESS;
}

PMSDK_API int32_t pmsdk_skingen_get_used_mode(const pmsdk_skingen_result_t* result) {
    return result == nullptr ? -1 : static_cast<int32_t>(result->extraction.usedMode);
}

PMSDK_API size_t pmsdk_skingen_get_part_count(const pmsdk_skingen_result_t* result) {
    return result == nullptr ? 0 : result->extraction.parts.size();
}

PMSDK_API float pmsdk_skingen_get_part_area_fraction(const pmsdk_skingen_result_t* result, size_t part) {
    const SkinGen::Part* p = GetPart(result, part);
    return p == nullptr ? 0.0f : p->areaFraction;
}

PMSDK_API pmsdk_status_t pmsdk_skingen_get_part_bounds(const pmsdk_skingen_result_t* result, size_t part, float* out4) {
    const SkinGen::Part* p = GetPart(result, part);
    if (p == nullptr || out4 == nullptr) {
        return PMSDK_ERROR_INVALID_ARGUMENT;
    }
    out4[0] = p->boundsMin.x;
    out4[1] = p->boundsMin.y;
    out4[2] = p->boundsMax.x;
    out4[3] = p->boundsMax.y;
    return PMSDK_SUCCESS;
}

PMSDK_API pmsdk_status_t pmsdk_skingen_get_part_quad(const pmsdk_skingen_result_t* result, size_t part, pmsdk_vec2_t* out4) {
    const SkinGen::Part* p = GetPart(result, part);
    if (p == nullptr || out4 == nullptr) {
        return PMSDK_ERROR_INVALID_ARGUMENT;
    }
    for (int i = 0; i < 4; ++i) {
        out4[i].x = p->quad[i].x;
        out4[i].y = p->quad[i].y;
    }
    return PMSDK_SUCCESS;
}

PMSDK_API size_t pmsdk_skingen_copy_polygon(const pmsdk_skingen_result_t* result, size_t part, pmsdk_vec2_t* out, size_t capacity) {
    const SkinGen::Part* p = GetPart(result, part);
    return p == nullptr ? 0 : CopyPoints(p->polygon, out, capacity);
}

PMSDK_API size_t pmsdk_skingen_get_contour_count(const pmsdk_skingen_result_t* result, size_t part) {
    const SkinGen::Part* p = GetPart(result, part);
    return p == nullptr ? 0 : p->contours.size();
}

PMSDK_API int32_t pmsdk_skingen_get_contour_is_hole(const pmsdk_skingen_result_t* result, size_t part, size_t contour) {
    const SkinGen::Part* p = GetPart(result, part);
    if (p == nullptr || contour >= p->contours.size()) {
        return -1;
    }
    return p->contours[contour].isHole ? 1 : 0;
}

PMSDK_API size_t pmsdk_skingen_copy_contour(const pmsdk_skingen_result_t* result, size_t part, size_t contour,
                                            pmsdk_vec2_t* out, size_t capacity) {
    const SkinGen::Part* p = GetPart(result, part);
    if (p == nullptr || contour >= p->contours.size()) {
        return 0;
    }
    return CopyPoints(p->contours[contour].points, out, capacity);
}

PMSDK_API pmsdk_status_t pmsdk_skingen_get_mask_rect(const pmsdk_skingen_result_t* result, size_t part, int32_t* out4) {
    const SkinGen::Part* p = GetPart(result, part);
    if (p == nullptr || out4 == nullptr) {
        return PMSDK_ERROR_INVALID_ARGUMENT;
    }
    out4[0] = p->maskX;
    out4[1] = p->maskY;
    out4[2] = p->maskWidth;
    out4[3] = p->maskHeight;
    return PMSDK_SUCCESS;
}

PMSDK_API size_t pmsdk_skingen_copy_mask(const pmsdk_skingen_result_t* result, size_t part, uint8_t* out, size_t capacity) {
    const SkinGen::Part* p = GetPart(result, part);
    if (p == nullptr) {
        return 0;
    }
    if (out != nullptr) {
        std::memcpy(out, p->mask.data(), std::min(p->mask.size(), capacity));
    }
    return p->mask.size();
}

} // extern "C"
