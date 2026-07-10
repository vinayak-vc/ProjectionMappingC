#pragma once
#include "PMSDK/Core/Export.h"
#include <stdint.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

// Status Enum
typedef enum {
    PMSDK_SUCCESS = 0,
    PMSDK_ERROR_INVALID_ARGUMENT = 1,
    PMSDK_ERROR_OUT_OF_MEMORY = 2,
    PMSDK_ERROR_UNKNOWN = 3
} pmsdk_status_t;

// Opaque Handles
typedef struct pmsdk_mesh_t pmsdk_mesh_t;
typedef struct pmsdk_projector_t pmsdk_projector_t;
typedef struct pmsdk_warpnode_t pmsdk_warpnode_t;
typedef struct pmsdk_deformationfield_t pmsdk_deformationfield_t;

// Basic Math Structs matching C++ Memory Layout
typedef struct {
    float x, y;
} pmsdk_vec2_t;

typedef struct {
    float x, y, z;
} pmsdk_vec3_t;

typedef struct {
    float x, y, z, w;
} pmsdk_vec4_t;

typedef struct {
    pmsdk_vec3_t position;
    pmsdk_vec3_t normal;
    pmsdk_vec2_t uv;
    pmsdk_vec4_t color;
} pmsdk_vertex_t;

#ifdef __cplusplus
}
#endif
