#include "PMSDK/C_API/GeometryAPI.h"
#include "PMSDK/Geometry/Mesh.h"
#include "PMSDK/Geometry/Vertex.h"
#include <new>
#include <span>

using namespace pmsdk;
using namespace pmsdk::Geometry;

extern "C" {

pmsdk_mesh_t* pmsdk_mesh_create(void) {
    try {
        return reinterpret_cast<pmsdk_mesh_t*>(new Mesh());
    } catch (...) {
        return nullptr;
    }
}

void pmsdk_mesh_destroy(pmsdk_mesh_t* mesh) {
    if (mesh) {
        delete reinterpret_cast<Mesh*>(mesh);
    }
}

pmsdk_status_t pmsdk_mesh_set_vertices(pmsdk_mesh_t* mesh, const pmsdk_vertex_t* vertices, size_t count) {
    if (!mesh || !vertices) return PMSDK_ERROR_INVALID_ARGUMENT;
    try {
        auto* cpp_mesh = reinterpret_cast<Mesh*>(mesh);
        // We know that pmsdk_vertex_t has the exact same layout as pmsdk::Geometry::Vertex
        const auto* cpp_verts = reinterpret_cast<const Vertex*>(vertices);
        cpp_mesh->SetVertices(std::span<const Vertex>(cpp_verts, count));
        return PMSDK_SUCCESS;
    } catch (const std::bad_alloc&) {
        return PMSDK_ERROR_OUT_OF_MEMORY;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

pmsdk_status_t pmsdk_mesh_set_indices(pmsdk_mesh_t* mesh, const uint32_t* indices, size_t count) {
    if (!mesh || !indices) return PMSDK_ERROR_INVALID_ARGUMENT;
    try {
        reinterpret_cast<Mesh*>(mesh)->SetIndices(std::span<const uint32_t>(indices, count));
        return PMSDK_SUCCESS;
    } catch (const std::bad_alloc&) {
        return PMSDK_ERROR_OUT_OF_MEMORY;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

size_t pmsdk_mesh_get_vertex_count(const pmsdk_mesh_t* mesh) {
    if (!mesh) return 0;
    return reinterpret_cast<const Mesh*>(mesh)->GetVertexCount();
}

size_t pmsdk_mesh_get_index_count(const pmsdk_mesh_t* mesh) {
    if (!mesh) return 0;
    return reinterpret_cast<const Mesh*>(mesh)->GetIndexCount();
}

pmsdk_status_t pmsdk_mesh_recalculate_normals(pmsdk_mesh_t* mesh) {
    if (!mesh) return PMSDK_ERROR_INVALID_ARGUMENT;
    try {
        reinterpret_cast<Mesh*>(mesh)->RecalculateNormals();
        return PMSDK_SUCCESS;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

pmsdk_status_t pmsdk_mesh_clear(pmsdk_mesh_t* mesh) {
    if (!mesh) return PMSDK_ERROR_INVALID_ARGUMENT;
    try {
        reinterpret_cast<Mesh*>(mesh)->Clear();
        return PMSDK_SUCCESS;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

} // extern "C"
