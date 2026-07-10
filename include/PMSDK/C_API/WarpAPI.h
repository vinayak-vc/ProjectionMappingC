#pragma once
#include "PMSDK/C_API/Types.h"

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief Creates a new Projector object.
 * @return A handle to the created Projector, or nullptr on failure.
 */
PMSDK_API pmsdk_projector_t* pmsdk_projector_create(void);

/**
 * @brief Destroys a Projector object and frees its resources.
 * @param projector The handle to the Projector. Safe to call with nullptr.
 */
PMSDK_API void pmsdk_projector_destroy(pmsdk_projector_t* projector);

/**
 * @brief Sets the aspect ratio of the projector.
 * @param projector The handle to the projector.
 * @param ratio The aspect ratio (e.g., 16.0f / 9.0f).
 * @return PMSDK_SUCCESS on success, or an error code on failure.
 */
PMSDK_API pmsdk_status_t pmsdk_projector_set_aspect_ratio(pmsdk_projector_t* projector, float ratio);

/**
 * @brief Sets the throw ratio of the projector.
 * @param projector The handle to the projector.
 * @param ratio The throw ratio.
 * @return PMSDK_SUCCESS on success, or an error code on failure.
 */
PMSDK_API pmsdk_status_t pmsdk_projector_set_throw_ratio(pmsdk_projector_t* projector, float ratio);

/**
 * @brief Creates a new WarpNode object.
 * @return A handle to the created WarpNode, or nullptr on failure.
 */
PMSDK_API pmsdk_warpnode_t* pmsdk_warpnode_create(void);

/**
 * @brief Destroys a WarpNode object and frees its resources.
 * @param node The handle to the WarpNode. Safe to call with nullptr.
 */
PMSDK_API void pmsdk_warpnode_destroy(pmsdk_warpnode_t* node);

/**
 * @brief Adds a child WarpNode to a parent WarpNode, forming a hierarchy.
 * @param parent The parent WarpNode handle.
 * @param child The child WarpNode handle.
 * @return PMSDK_SUCCESS on success, or an error code on failure.
 */
PMSDK_API pmsdk_status_t pmsdk_warpnode_add_child(pmsdk_warpnode_t* parent, pmsdk_warpnode_t* child);

/**
 * @brief Processes an input mesh through a warp node hierarchy.
 * The input mesh is read, and the deformed result is written to the output mesh.
 * @param node The warp node to process through.
 * @param input_mesh The source mesh.
 * @param output_mesh The destination mesh.
 * @return PMSDK_SUCCESS on success, or an error code on failure.
 */
PMSDK_API pmsdk_status_t pmsdk_warpnode_process_mesh(pmsdk_warpnode_t* node, const pmsdk_mesh_t* input_mesh, pmsdk_mesh_t* output_mesh);

#ifdef __cplusplus
}
#endif
