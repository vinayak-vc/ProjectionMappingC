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
 * @brief Sets the deformation type for the warp node.
 * @param node The warp node.
 * @param type 0 = None, 1 = Bezier, 2 = Grid, 3 = Perspective.
 * @return PMSDK_SUCCESS on success.
 */
PMSDK_API pmsdk_status_t pmsdk_warpnode_set_deformation_type(pmsdk_warpnode_t* node, int type);

/**
 * @brief Retrieves the GridWarp handle from a WarpNode.
 * @param node The warp node (must have type Grid).
 * @return Handle to the GridWarp, or nullptr if unavailable.
 */
PMSDK_API pmsdk_gridwarp_t* pmsdk_warpnode_get_gridwarp(pmsdk_warpnode_t* node);

/**
 * @brief Sets the control points for a GridWarp.
 * @param gridwarp The GridWarp handle.
 * @param columns Number of columns in the grid.
 * @param rows Number of rows in the grid.
 * @param points Array of 3D control points (length = columns * rows).
 * @return PMSDK_SUCCESS on success.
 */
PMSDK_API pmsdk_status_t pmsdk_gridwarp_set_control_points(pmsdk_gridwarp_t* gridwarp, int columns, int rows, const pmsdk_vec3_t* points);

/**
 * @brief Retrieves the PerspectiveWarp handle from a WarpNode.
 * @param node The warp node (must have type Perspective).
 * @return Handle to the PerspectiveWarp, or nullptr if unavailable.
 */
PMSDK_API pmsdk_perspectivewarp_t* pmsdk_warpnode_get_perspectivewarp(pmsdk_warpnode_t* node);

/**
 * @brief Sets the four target corners of a perspective (projective) corner pin.
 *
 * Corners map to the UV corners of the source quad:
 *   bottomLeft -> (0,0), bottomRight -> (1,0), topRight -> (1,1), topLeft -> (0,1).
 * Unlike a 2x2 grid warp (bilinear), this produces a perspective-correct mapping.
 *
 * @param perspectivewarp The PerspectiveWarp handle.
 * @return PMSDK_SUCCESS on success.
 */
PMSDK_API pmsdk_status_t pmsdk_perspectivewarp_set_corners(
    pmsdk_perspectivewarp_t* perspectivewarp,
    pmsdk_vec2_t bottomLeft, pmsdk_vec2_t bottomRight,
    pmsdk_vec2_t topRight, pmsdk_vec2_t topLeft);

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
