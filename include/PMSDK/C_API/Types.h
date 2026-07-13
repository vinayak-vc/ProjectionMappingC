/**
 * @file Types.h
 * @brief Common types and enumerations for the C-API.
 */

#pragma once

#include "PMSDK/Core/Export.h"
#include <stdint.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief Status codes returned by all C-API functions.
 */
typedef enum {
    PMSDK_SUCCESS = 0,                  /**< The operation completed successfully. */
    PMSDK_ERROR_INVALID_ARGUMENT = 1,   /**< One or more arguments are invalid. */
    PMSDK_ERROR_OUT_OF_MEMORY = 2,      /**< An allocation failed. */
    PMSDK_ERROR_UNKNOWN = 3             /**< An unknown error occurred. */
} pmsdk_status_t;

/** @brief Opaque handle to a Mesh object. */
typedef struct pmsdk_mesh_t pmsdk_mesh_t;
typedef struct pmsdk_warpnode_t pmsdk_warpnode_t;
typedef struct pmsdk_projector_t pmsdk_projector_t;
typedef struct pmsdk_blendconfig_t pmsdk_blendconfig_t;
typedef struct pmsdk_edgeblend_t pmsdk_edgeblend_t;

/**
 * @brief Interpolation curve types for edge blending.
 */
typedef enum pmsdk_curve_type_t {
    PMSDK_CURVE_LINEAR = 0,
    PMSDK_CURVE_POWER = 1,
    PMSDK_CURVE_SMOOTHSTEP = 2
} pmsdk_curve_type_t;

/** @brief Opaque handle to a DeformationField object. */
typedef struct pmsdk_deformationfield_t pmsdk_deformationfield_t;

/** @brief 2D vector for UV coordinates. */
typedef struct {
    float x, y;
} pmsdk_vec2_t;

/** @brief 3D vector for position and normal data. */
typedef struct {
    float x, y, z;
} pmsdk_vec3_t;

/** @brief 4D vector for colors and homogenous coordinates. */
typedef struct {
    float x, y, z, w;
} pmsdk_vec4_t;

/**
 * @brief Represents a single vertex in a projection mapping mesh.
 */
typedef struct {
    pmsdk_vec3_t position; /**< 3D position of the vertex */
    pmsdk_vec3_t normal;   /**< 3D normal vector */
    pmsdk_vec2_t uv;       /**< 2D texture coordinate */
    pmsdk_vec4_t color;    /**< RGBA color, typically in [0, 1] */
} pmsdk_vertex_t;

#ifdef __cplusplus
}
#endif
