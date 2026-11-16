/**
 * @file AutoAdjustAPI.h
 * @brief C API for camera-assisted verify-and-refine of a planar skin (pmsdk::AutoAdjust, D-044).
 *
 * One iteration, as a host runs it:
 *   1. project pmsdk_autoadjust_render_checker() inside the skin, capture a grey camera frame;
 *   2. pmsdk_autoadjust_detect()            -> raw camera corners (+ valid flags);
 *   3. pmsdk_autoadjust_undistort()         -> corners without lens distortion (if intrinsics known);
 *   4. pmsdk_autoadjust_refine()            -> score before + the skin's new correction grid;
 *   5. apply the grid to the skin and repeat; pmsdk_autoadjust_measure() scores a final capture.
 *
 * Conventions: content UV and projector raster are [0,1]^2 with origin bottom-left, y up; camera
 * points are pixels with origin top-left. Corner arrays follow pmsdk_autoadjust_checker_corners().
 * Functions returning pmsdk_status_t set pmsdk_autoadjust_get_last_error() on failure.
 */
#pragma once

#include "PMSDK/C_API/Types.h"
#include "PMSDK/Core/Export.h"
#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/** @brief Checkerboard layout. Initialise with pmsdk_autoadjust_checker_init(). squares_x + squares_y must be odd. */
typedef struct pmsdk_autoadjust_checker_t {
    int32_t squares_x; /**< default 10 */
    int32_t squares_y; /**< default 7 */
    float margin;      /**< white border per side, fraction of the content (default 0.08) */
} pmsdk_autoadjust_checker_t;

/** @brief Camera intrinsics for the detection image size. */
typedef struct pmsdk_autoadjust_intrinsics_t {
    int32_t model;            /**< 0 = perspective (OpenCV), 1 = fisheye */
    float fx;
    float fy;
    float cx;
    float cy;
    int32_t distortion_count; /**< valid entries in distortion (0..14) */
    float distortion[14];     /**< OpenCV order k1,k2,p1,p2,k3,k4,k5,k6,s1..s4,tx,ty (fisheye: k1..k4) */
} pmsdk_autoadjust_intrinsics_t;

/** @brief Current warp of a quad skin. */
typedef struct pmsdk_autoadjust_warp_t {
    pmsdk_vec2_t corners[4];            /**< bottom-left, bottom-right, top-right, top-left (raster) */
    int32_t grid_columns;               /**< 0 = no correction grid */
    int32_t grid_rows;
    const pmsdk_vec2_t* grid_offsets;   /**< grid_columns * grid_rows, row-major, row 0 at v = 0 */
} pmsdk_autoadjust_warp_t;

/** @brief Refinement options. Initialise with pmsdk_autoadjust_options_init(). */
typedef struct pmsdk_autoadjust_options_t {
    float damping;               /**< (0,1], default 0.8 */
    int32_t grid_columns;        /**< 2..33, default 9 */
    int32_t grid_rows;           /**< 2..33, default 9 */
    float smoothness;            /**< >= 0, default 0.05 */
    int32_t pin_corners;         /**< 1 = keep the skin's four corners (default 1) */
    float max_step;              /**< max change per grid node per iteration, raster units (default 0.05) */
    int32_t use_target_quad;     /**< 1 = target_quad defines "correct" instead of the best fit (default 0) */
    pmsdk_vec2_t target_quad[4]; /**< camera pixels: content bottom-left, bottom-right, top-right, top-left */
} pmsdk_autoadjust_options_t;

/** @brief Residual score in (undistorted) camera pixels. */
typedef struct pmsdk_autoadjust_score_t {
    int32_t valid_count;
    float rms_px;
    float max_px;
} pmsdk_autoadjust_score_t;

/** @brief Fills @p checker with the defaults. */
PMSDK_API void pmsdk_autoadjust_checker_init(pmsdk_autoadjust_checker_t* checker);

/** @brief Fills @p options with the defaults. */
PMSDK_API void pmsdk_autoadjust_options_init(pmsdk_autoadjust_options_t* options);

/** @brief Message of the calling thread's last failed autoadjust call; empty if none. Never NULL. */
PMSDK_API const char* pmsdk_autoadjust_get_last_error(void);

/**
 * @brief Content UV of the checker's inner corners (row-major, top row first, left to right).
 * @return The total corner count (0 for an invalid checker); writes min(total, capacity) when out != NULL.
 */
PMSDK_API size_t pmsdk_autoadjust_checker_corners(const pmsdk_autoadjust_checker_t* checker, pmsdk_vec2_t* out, size_t capacity);

/**
 * @brief Renders the checker as 8-bit grey into @p out (width * height bytes).
 * @param bottom_row_first 1 = first row is the bottom (Unity Texture2D raw order), 0 = top first.
 */
PMSDK_API pmsdk_status_t pmsdk_autoadjust_render_checker(const pmsdk_autoadjust_checker_t* checker, int32_t width, int32_t height,
                                                         int32_t bottom_row_first, uint8_t* out, size_t capacity);

/**
 * @brief Detects the checker's inner corners in an 8-bit grey camera image (rows top-to-bottom).
 * @param predicted Optional raw camera point per corner (e.g. previous detection) used when the
 *        whole board is not visible; NULL to disable.
 * @param out_corners Raw camera pixels per corner (capacity >= corner count).
 * @param out_valid 1 per found corner, 0 otherwise (capacity >= corner count).
 * @param out_whole_board Optional: 1 if the whole board was found in one piece.
 */
PMSDK_API pmsdk_status_t pmsdk_autoadjust_detect(const uint8_t* gray, int32_t width, int32_t height, int32_t stride,
                                                 const pmsdk_autoadjust_checker_t* checker, const pmsdk_vec2_t* predicted,
                                                 float min_contrast, pmsdk_vec2_t* out_corners, uint8_t* out_valid,
                                                 size_t capacity, int32_t* out_valid_count, int32_t* out_whole_board);

/** @brief Removes lens distortion from @p count points (in place allowed: in == out). */
PMSDK_API pmsdk_status_t pmsdk_autoadjust_undistort(const pmsdk_vec2_t* in, pmsdk_vec2_t* out, size_t count,
                                                    const pmsdk_autoadjust_intrinsics_t* intrinsics);

/** @brief Scores undistorted corners against the ideal perspective grid. */
PMSDK_API pmsdk_status_t pmsdk_autoadjust_measure(const pmsdk_vec2_t* uvs, const pmsdk_vec2_t* camera, const uint8_t* valid,
                                                  size_t count, const pmsdk_autoadjust_options_t* options,
                                                  pmsdk_autoadjust_score_t* out_score);

/**
 * @brief One refinement step. Writes the score before the step and the full new correction grid
 *        (options->grid_columns * options->grid_rows offsets, row-major) into @p out_offsets.
 */
PMSDK_API pmsdk_status_t pmsdk_autoadjust_refine(const pmsdk_autoadjust_warp_t* warp, const pmsdk_vec2_t* uvs,
                                                 const pmsdk_vec2_t* camera, const uint8_t* valid, size_t count,
                                                 const pmsdk_autoadjust_options_t* options,
                                                 pmsdk_autoadjust_score_t* out_before, pmsdk_vec2_t* out_offsets,
                                                 size_t capacity);

/** @brief Projector raster position of content UV (u, v) under @p warp (corner pin + correction). */
PMSDK_API pmsdk_status_t pmsdk_autoadjust_evaluate_warp(const pmsdk_autoadjust_warp_t* warp, float u, float v,
                                                        pmsdk_vec2_t* out_point);

/** @brief pmsdk_autoadjust_evaluate_warp for @p count UVs at once (in place allowed: uvs == out_points). */
PMSDK_API pmsdk_status_t pmsdk_autoadjust_evaluate_warp_many(const pmsdk_autoadjust_warp_t* warp, const pmsdk_vec2_t* uvs, size_t count,
                                                             pmsdk_vec2_t* out_points);

/* ------------------------------------------------------------------ Phase 2: facets (D-045) */

/** @brief Facet segmentation options. Initialise with pmsdk_autoadjust_facet_options_init(). */
typedef struct pmsdk_autoadjust_facet_options_t {
    float min_contrast;        /**< white - black needed to count as lit (default 25) */
    float blur_sigma;          /**< default 1.5 */
    float edge_threshold;      /**< log-brightness change per pixel that marks a facet border (default 0.035) */
    int32_t regrow_px;         /**< border band given back to the facets, pixels (default 12) */
    float min_area_fraction;   /**< default 0.002 */
    float polygon_epsilon_px;  /**< default 3 */
    int32_t max_facets;        /**< default 32 */
} pmsdk_autoadjust_facet_options_t;

/** @brief Opaque facet segmentation result. */
typedef struct pmsdk_autoadjust_facets_t pmsdk_autoadjust_facets_t;

PMSDK_API void pmsdk_autoadjust_facet_options_init(pmsdk_autoadjust_facet_options_t* options);

/** @brief Splits the lit area of a white capture (minus a black capture) into facets. NULL on error. */
PMSDK_API pmsdk_autoadjust_facets_t* pmsdk_autoadjust_segment_facets(const uint8_t* white, const uint8_t* black, int32_t width,
                                                                     int32_t height, int32_t stride,
                                                                     const pmsdk_autoadjust_facet_options_t* options);

PMSDK_API void pmsdk_autoadjust_facets_destroy(pmsdk_autoadjust_facets_t* facets);

PMSDK_API size_t pmsdk_autoadjust_facets_count(const pmsdk_autoadjust_facets_t* facets);

/** @brief Quad (camera px, content BL, BR, TR, TL), whether it came from the outline, area and mean contrast. */
PMSDK_API pmsdk_status_t pmsdk_autoadjust_facets_get(const pmsdk_autoadjust_facets_t* facets, size_t index, pmsdk_vec2_t* out_quad4,
                                                     int32_t* out_from_outline, float* out_area_px, float* out_mean_contrast);

/** @brief Outline polygon of a facet. Returns the point count (copy rule as above). */
PMSDK_API size_t pmsdk_autoadjust_facets_copy_polygon(const pmsdk_autoadjust_facets_t* facets, size_t index, pmsdk_vec2_t* out,
                                                      size_t capacity);

/** @brief Index of the facet containing most of the valid points, -1 if none. */
PMSDK_API int32_t pmsdk_autoadjust_facets_find_containing(const pmsdk_autoadjust_facets_t* facets, const pmsdk_vec2_t* points,
                                                          const uint8_t* valid, size_t count);

/**
 * @brief New corner-pin corners (raster BL, BR, TR, TL) so the content corners land on @p target_quad4
 *        (camera px, content BL, BR, TR, TL). Clear the skin's correction grid after applying them.
 */
PMSDK_API pmsdk_status_t pmsdk_autoadjust_solve_corners(const pmsdk_autoadjust_warp_t* warp, const pmsdk_vec2_t* uvs,
                                                        const pmsdk_vec2_t* camera, const uint8_t* valid, size_t count,
                                                        const pmsdk_vec2_t* target_quad4, pmsdk_vec2_t* out_corners4);

/** @brief Millimetres per camera pixel on a facet of known size (0 for degenerate input). */
PMSDK_API float pmsdk_autoadjust_metric_scale(const pmsdk_vec2_t* quad4, float width_mm, float height_mm);

/* ------------------------------------------------------------------ Phase 3: warp map (D-046) */

typedef struct pmsdk_autoadjust_warpmap_options_t {
    int32_t width;               /**< default 256 */
    int32_t height;              /**< default 256 */
    float max_fill_distance_px;  /**< default 12 */
} pmsdk_autoadjust_warpmap_options_t;

PMSDK_API void pmsdk_autoadjust_warpmap_options_init(pmsdk_autoadjust_warpmap_options_t* options);

/**
 * @brief Builds a raster -> content UV map from scattered samples over the raster rect (x, y = bottom-left).
 * @param out_rgba width * height * 4 floats, row 0 at the rect's bottom: u, v, 0, valid (0/1).
 */
PMSDK_API pmsdk_status_t pmsdk_autoadjust_build_warp_map(const pmsdk_vec2_t* raster, const pmsdk_vec2_t* uv, size_t count,
                                                         float rect_x, float rect_y, float rect_width, float rect_height,
                                                         const pmsdk_autoadjust_warpmap_options_t* options, float* out_rgba,
                                                         size_t capacity_floats, float* out_coverage);

/* ------------------------------------------------------------------ Phase 4: planes from depth (D-047) */

typedef struct pmsdk_autoadjust_plane_options_t {
    int32_t step;                /**< default 4 */
    float min_depth_mm;          /**< default 300 */
    float max_depth_mm;          /**< default 8000 */
    float inlier_threshold_mm;   /**< default 12 */
    int32_t iterations;          /**< default 400 */
    float min_inlier_fraction;   /**< default 0.02 */
    int32_t max_planes;          /**< default 8 */
    float polygon_epsilon_px;    /**< default 4 */
    uint32_t seed;               /**< default 1 */
} pmsdk_autoadjust_plane_options_t;

typedef struct pmsdk_autoadjust_plane_info_t {
    pmsdk_vec3_t normal;         /**< camera space (x right, y down, z forward), facing the camera */
    float d;                     /**< normal . X + d = 0 (mm) */
    int32_t inlier_count;
    float rms_mm;
    float width_mm;
    float height_mm;
    float view_angle_deg;
    pmsdk_vec2_t quad[4];        /**< image px, BL, BR, TR, TL */
} pmsdk_autoadjust_plane_info_t;

typedef struct pmsdk_autoadjust_planes_t pmsdk_autoadjust_planes_t;

PMSDK_API void pmsdk_autoadjust_plane_options_init(pmsdk_autoadjust_plane_options_t* options);

/**
 * @brief Detects planes in a depth image (mm, 0 = none, stride in elements) taken with @p intrinsics
 *        (lens distortion is removed from the pixel rays). NULL on error.
 */
PMSDK_API pmsdk_autoadjust_planes_t* pmsdk_autoadjust_detect_planes(const uint16_t* depth_mm, int32_t width, int32_t height,
                                                                    int32_t stride, const pmsdk_autoadjust_intrinsics_t* intrinsics,
                                                                    const pmsdk_autoadjust_plane_options_t* options);

PMSDK_API void pmsdk_autoadjust_planes_destroy(pmsdk_autoadjust_planes_t* planes);

PMSDK_API size_t pmsdk_autoadjust_planes_count(const pmsdk_autoadjust_planes_t* planes);

PMSDK_API pmsdk_status_t pmsdk_autoadjust_planes_get(const pmsdk_autoadjust_planes_t* planes, size_t index,
                                                     pmsdk_autoadjust_plane_info_t* out_info);

/** @brief Outline polygon of a plane (image px). Returns the point count (copy rule as above). */
PMSDK_API size_t pmsdk_autoadjust_planes_copy_polygon(const pmsdk_autoadjust_planes_t* planes, size_t index, pmsdk_vec2_t* out,
                                                      size_t capacity);

#ifdef __cplusplus
}
#endif
