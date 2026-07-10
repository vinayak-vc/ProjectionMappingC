#pragma once
#include "PMSDK/C_API/Types.h"

#ifdef __cplusplus
extern "C" {
#endif

// Projector
PMSDK_API pmsdk_projector_t* pmsdk_projector_create(void);
PMSDK_API void pmsdk_projector_destroy(pmsdk_projector_t* projector);

PMSDK_API pmsdk_status_t pmsdk_projector_set_aspect_ratio(pmsdk_projector_t* projector, float aspect);
PMSDK_API pmsdk_status_t pmsdk_projector_set_throw_ratio(pmsdk_projector_t* projector, float ratio);

// WarpNode
PMSDK_API pmsdk_warpnode_t* pmsdk_warpnode_create(void);
PMSDK_API void pmsdk_warpnode_destroy(pmsdk_warpnode_t* node);

PMSDK_API pmsdk_status_t pmsdk_warpnode_add_child(pmsdk_warpnode_t* parent, pmsdk_warpnode_t* child);

// Process a mesh through a warp node. The input_mesh is read, and the deformed result is written to output_mesh.
PMSDK_API pmsdk_status_t pmsdk_warpnode_process_mesh(pmsdk_warpnode_t* node, const pmsdk_mesh_t* input_mesh, pmsdk_mesh_t* output_mesh);

#ifdef __cplusplus
}
#endif
