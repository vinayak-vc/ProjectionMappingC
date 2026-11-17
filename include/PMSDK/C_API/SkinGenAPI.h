/**
 * @file SkinGenAPI.h
 * @brief C API for photo -> skin extraction (see pmsdk::SkinGen, decision D-041).
 *
 * Typical use:
 *   pmsdk_skingen_options_t opt; pmsdk_skingen_options_init(&opt); opt.y_up = 1;
 *   pmsdk_skingen_result_t* r = pmsdk_skingen_extract_from_file("statue.png", &opt);
 *   if (!r) { puts(pmsdk_skingen_get_last_error()); }
 *   for each part i: read bounds/quad/polygon/contours/mask, build a skin;
 *   pmsdk_skingen_result_destroy(r);
 *
 * All "copy" functions follow one rule: they return the TOTAL number of elements available and,
 * when out != NULL, write min(total, capacity) of them. Call once with out == NULL to size the
 * buffer. Indices out of range return 0 / PMSDK_ERROR_INVALID_ARGUMENT.
 */

#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/C_API/Types.h"
#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/** @brief Opaque result of an extraction (owns all part data). */
typedef struct pmsdk_skingen_result_t pmsdk_skingen_result_t;

/** @brief How the object is found in the image. */
typedef enum pmsdk_skingen_mode_t {
    PMSDK_SKINGEN_MODE_AUTO = 0,          /**< alpha if present, else colour key, else GrabCut */
    PMSDK_SKINGEN_MODE_ALPHA = 1,         /**< non-transparent pixels (needs 4 channels) */
    PMSDK_SKINGEN_MODE_COLOR_KEY = 2,     /**< differs from the border colour */
    PMSDK_SKINGEN_MODE_GRABCUT = 3,       /**< OpenCV GrabCut with an inset rectangle */
    PMSDK_SKINGEN_MODE_PROVIDED_MASK = 4  /**< caller-supplied mask */
} pmsdk_skingen_mode_t;

/** @brief Extraction tunables. Always initialise with pmsdk_skingen_options_init(). */
typedef struct pmsdk_skingen_options_t {
    int32_t mode;                    /**< pmsdk_skingen_mode_t */
    uint8_t alpha_threshold;         /**< alpha > this is the object (default 8) */
    float color_tolerance;           /**< colour-key tolerance, 0..255 (default 28) */
    float grabcut_margin;            /**< GrabCut rectangle inset fraction (default 0.05) */
    int32_t grabcut_iterations;      /**< default 5 */
    float min_part_area_fraction;    /**< drop pieces smaller than this fraction of the object (default 0.002) */
    float min_hole_area_fraction;    /**< fill holes smaller than this fraction of their part (default 0.0005) */
    int32_t split_parts;             /**< 1 = thick body / thin appendage split + merge (default 1) */
    float thin_part_fraction;        /**< thin feature width / object bbox short side (default 0.05) */
    float appendage_max_fraction;    /**< parts larger than this stay separate (default 0.12) */
    int32_t max_parts;               /**< keep at most this many parts, 0 = unlimited (default 16) */
    int32_t max_polygon_points;      /**< 3..4096, per outline (default 128) */
    int32_t y_up;                    /**< 1 = origin bottom-left, y up; 0 = image space (default 0) */
} pmsdk_skingen_options_t;

/** @brief Fills @p options with the defaults. */
PMSDK_API void pmsdk_skingen_options_init(pmsdk_skingen_options_t* options);

/**
 * @brief Extract parts from an image file (png/jpg/jfif/bmp/tga/tiff/webp). UTF-8 path.
 * @return A result handle, or NULL (see pmsdk_skingen_get_last_error()). Free with pmsdk_skingen_result_destroy().
 */
PMSDK_API pmsdk_skingen_result_t* pmsdk_skingen_extract_from_file(const char* path, const pmsdk_skingen_options_t* options);

/**
 * @brief Extract parts from raw 8-bit pixels (R,G,B[,A] order, rows top-to-bottom).
 * @param channels 1, 3 or 4.
 * @param mask Optional width*height bytes (non-zero = object); used by PMSDK_SKINGEN_MODE_PROVIDED_MASK.
 */
PMSDK_API pmsdk_skingen_result_t* pmsdk_skingen_extract_from_pixels(const uint8_t* pixels, int32_t width, int32_t height,
                                                                    int32_t channels, const uint8_t* mask,
                                                                    const pmsdk_skingen_options_t* options);

/** @brief Extract parts from the bytes of an encoded image file. */
PMSDK_API pmsdk_skingen_result_t* pmsdk_skingen_extract_from_memory(const uint8_t* bytes, size_t size,
                                                                    const pmsdk_skingen_options_t* options);

/** @brief Destroys a result (NULL is ignored). */
PMSDK_API void pmsdk_skingen_result_destroy(pmsdk_skingen_result_t* result);

/** @brief Message of the calling thread's last failed skingen call; empty string if none. Never NULL. */
PMSDK_API const char* pmsdk_skingen_get_last_error(void);

/** @brief Source image size in pixels. */
PMSDK_API pmsdk_status_t pmsdk_skingen_get_image_size(const pmsdk_skingen_result_t* result, int32_t* width, int32_t* height);

/** @brief The segmentation mode actually used (resolves AUTO), a pmsdk_skingen_mode_t; -1 on bad handle. */
PMSDK_API int32_t pmsdk_skingen_get_used_mode(const pmsdk_skingen_result_t* result);

/** @brief Number of parts, largest first. */
PMSDK_API size_t pmsdk_skingen_get_part_count(const pmsdk_skingen_result_t* result);

/** @brief Part area / image area. */
PMSDK_API float pmsdk_skingen_get_part_area_fraction(const pmsdk_skingen_result_t* result, size_t part);

/** @brief Axis-aligned bounds: out4 = {minX, minY, maxX, maxY} (normalised, see y_up). */
PMSDK_API pmsdk_status_t pmsdk_skingen_get_part_bounds(const pmsdk_skingen_result_t* result, size_t part, float* out4);

/** @brief Bounding quad, visual order bottom-left, bottom-right, top-right, top-left (4 points). */
PMSDK_API pmsdk_status_t pmsdk_skingen_get_part_quad(const pmsdk_skingen_result_t* result, size_t part, pmsdk_vec2_t* out4);

/** @brief Simplified outer polygon (no holes). Returns the point count (see header comment). */
PMSDK_API size_t pmsdk_skingen_copy_polygon(const pmsdk_skingen_result_t* result, size_t part, pmsdk_vec2_t* out, size_t capacity);

/** @brief Number of outlines (outer pieces first, then holes) of a part. */
PMSDK_API size_t pmsdk_skingen_get_contour_count(const pmsdk_skingen_result_t* result, size_t part);

/** @brief 1 if the outline is a hole, 0 if outer, -1 on bad index. */
PMSDK_API int32_t pmsdk_skingen_get_contour_is_hole(const pmsdk_skingen_result_t* result, size_t part, size_t contour);

/** @brief Copy an outline's points. Returns the point count (see header comment). */
PMSDK_API size_t pmsdk_skingen_copy_contour(const pmsdk_skingen_result_t* result, size_t part, size_t contour,
                                            pmsdk_vec2_t* out, size_t capacity);

/** @brief Cropped mask rectangle in SOURCE image pixels (origin top-left): out4 = {x, y, width, height}. */
PMSDK_API pmsdk_status_t pmsdk_skingen_get_mask_rect(const pmsdk_skingen_result_t* result, size_t part, int32_t* out4);

/** @brief Copy the cropped mask (width*height bytes, rows top-to-bottom, 255 = part). Returns bytes (see header comment). */
PMSDK_API size_t pmsdk_skingen_copy_mask(const pmsdk_skingen_result_t* result, size_t part, uint8_t* out, size_t capacity);

#ifdef __cplusplus
}
#endif
