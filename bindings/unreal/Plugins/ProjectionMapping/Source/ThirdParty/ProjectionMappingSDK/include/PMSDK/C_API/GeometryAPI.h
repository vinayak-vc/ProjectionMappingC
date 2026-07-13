#pragma once
#include "PMSDK/C_API/Types.h"

#ifdef __cplusplus
extern "C" {
#endif

/**
 * @brief Creates a new mesh object.
 * @return A handle to the created mesh, or nullptr on failure.
 */
PMSDK_API pmsdk_mesh_t* pmsdk_mesh_create(void);

/**
 * @brief Destroys a mesh object and frees its resources.
 * @param mesh The handle to the mesh. Safe to call with nullptr.
 */
PMSDK_API void pmsdk_mesh_destroy(pmsdk_mesh_t* mesh);

/**
 * @brief Sets the vertices of the mesh.
 * @param mesh The handle to the mesh.
 * @param vertices Array of vertices.
 * @param count Number of vertices in the array.
 * @return PMSDK_SUCCESS on success, or an error code on failure.
 */
PMSDK_API pmsdk_status_t pmsdk_mesh_set_vertices(pmsdk_mesh_t* mesh, const pmsdk_vertex_t* vertices, size_t count);

/**
 * @brief Sets the indices of the mesh for indexed drawing.
 * @param mesh The handle to the mesh.
 * @param indices Array of 32-bit unsigned integer indices.
 * @param count Number of indices in the array.
 * @return PMSDK_SUCCESS on success, or an error code on failure.
 */
PMSDK_API pmsdk_status_t pmsdk_mesh_set_indices(pmsdk_mesh_t* mesh, const uint32_t* indices, size_t count);

/**
 * @brief Retrieves the number of vertices in the mesh.
 * @param mesh The handle to the mesh.
 * @return The vertex count, or 0 if mesh is null.
 */
PMSDK_API size_t pmsdk_mesh_get_vertex_count(const pmsdk_mesh_t* mesh);

/**
 * @brief Retrieves the number of indices in the mesh.
 * @param mesh The handle to the mesh.
 * @return The index count, or 0 if mesh is null.
 */
PMSDK_API size_t pmsdk_mesh_get_index_count(const pmsdk_mesh_t* mesh);

/**
 * @brief Recalculates the normals of the mesh based on its triangles.
 * @param mesh The handle to the mesh.
 * @return PMSDK_SUCCESS on success, or an error code on failure.
 */
PMSDK_API pmsdk_status_t pmsdk_mesh_recalculate_normals(pmsdk_mesh_t* mesh);

/**
 * @brief Retrieves the vertices of the mesh.
 * @param mesh The handle to the mesh.
 * @param out_vertices Array to store the vertices. Must be pre-allocated to at least `count`.
 * @param count Number of vertices to retrieve.
 * @return PMSDK_SUCCESS on success, or an error code on failure.
 */
PMSDK_API pmsdk_status_t pmsdk_mesh_get_vertices(const pmsdk_mesh_t* mesh, pmsdk_vertex_t* out_vertices, size_t count);

/**
 * @brief Retrieves the indices of the mesh.
 * @param mesh The handle to the mesh.
 * @param out_indices Array to store the indices. Must be pre-allocated to at least `count`.
 * @param count Number of indices to retrieve.
 * @return PMSDK_SUCCESS on success, or an error code on failure.
 */
PMSDK_API pmsdk_status_t pmsdk_mesh_get_indices(const pmsdk_mesh_t* mesh, uint32_t* out_indices, size_t count);

/**
 * @brief Clears all vertices and indices from the mesh.
 * @param mesh The handle to the mesh.
 * @return PMSDK_SUCCESS on success, or an error code on failure.
 */
PMSDK_API pmsdk_status_t pmsdk_mesh_clear(pmsdk_mesh_t* mesh);

#ifdef __cplusplus
}
#endif
