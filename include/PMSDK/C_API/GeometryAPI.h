#pragma once
#include "PMSDK/C_API/Types.h"

#ifdef __cplusplus
extern "C" {
#endif

PMSDK_API pmsdk_mesh_t* pmsdk_mesh_create(void);
PMSDK_API void pmsdk_mesh_destroy(pmsdk_mesh_t* mesh);

PMSDK_API pmsdk_status_t pmsdk_mesh_set_vertices(pmsdk_mesh_t* mesh, const pmsdk_vertex_t* vertices, size_t count);
PMSDK_API pmsdk_status_t pmsdk_mesh_set_indices(pmsdk_mesh_t* mesh, const uint32_t* indices, size_t count);

PMSDK_API size_t pmsdk_mesh_get_vertex_count(const pmsdk_mesh_t* mesh);
PMSDK_API size_t pmsdk_mesh_get_index_count(const pmsdk_mesh_t* mesh);

PMSDK_API pmsdk_status_t pmsdk_mesh_recalculate_normals(pmsdk_mesh_t* mesh);
PMSDK_API pmsdk_status_t pmsdk_mesh_clear(pmsdk_mesh_t* mesh);

#ifdef __cplusplus
}
#endif
