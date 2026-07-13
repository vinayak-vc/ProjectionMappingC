#include "PMSDK/C_API/GeometryAPI.h"
#include "PMSDK/Geometry/Mesh.h"
#include "PMSDK/Geometry/Vertex.h"
#include <new>


using namespace pmsdk;
using namespace pmsdk::Geometry;

extern "C" {

PMSDK_API pmsdk_mesh_t* pmsdk_mesh_create(void) {
    try {
        return reinterpret_cast<pmsdk_mesh_t*>(new Mesh());
    } catch (...) {
        return nullptr;
    }
}

PMSDK_API void pmsdk_mesh_destroy(pmsdk_mesh_t* mesh) {
    if (mesh) {
        delete reinterpret_cast<Mesh*>(mesh);
    }
}

PMSDK_API pmsdk_status_t pmsdk_mesh_set_vertices(pmsdk_mesh_t* mesh, const pmsdk_vertex_t* vertices, size_t count) {
    if (!mesh || !vertices) return PMSDK_ERROR_INVALID_ARGUMENT;
    try {
        auto* cpp_mesh = reinterpret_cast<Mesh*>(mesh);
        const auto* cpp_verts = reinterpret_cast<const Vertex*>(vertices);
        cpp_mesh->SetVertices(cpp_verts, count);
        return PMSDK_SUCCESS;
    } catch (const std::bad_alloc&) {
        return PMSDK_ERROR_OUT_OF_MEMORY;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

PMSDK_API pmsdk_status_t pmsdk_mesh_set_indices(pmsdk_mesh_t* mesh, const uint32_t* indices, size_t count) {
    if (!mesh || !indices) return PMSDK_ERROR_INVALID_ARGUMENT;
    try {
        reinterpret_cast<Mesh*>(mesh)->SetIndices(indices, count);
        return PMSDK_SUCCESS;
    } catch (const std::bad_alloc&) {
        return PMSDK_ERROR_OUT_OF_MEMORY;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

PMSDK_API size_t pmsdk_mesh_get_vertex_count(const pmsdk_mesh_t* mesh) {
    if (!mesh) return 0;
    return reinterpret_cast<const Mesh*>(mesh)->GetVertexCount();
}

PMSDK_API size_t pmsdk_mesh_get_index_count(const pmsdk_mesh_t* mesh) {
    if (!mesh) return 0;
    return reinterpret_cast<const Mesh*>(mesh)->GetIndexCount();
}

PMSDK_API pmsdk_status_t pmsdk_mesh_recalculate_normals(pmsdk_mesh_t* mesh) {
    if (!mesh) return PMSDK_ERROR_INVALID_ARGUMENT;
    try {
        reinterpret_cast<Mesh*>(mesh)->RecalculateNormals();
        return PMSDK_SUCCESS;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

PMSDK_API pmsdk_status_t pmsdk_mesh_get_vertices(const pmsdk_mesh_t* mesh, pmsdk_vertex_t* out_vertices, size_t count) {
    if (!mesh || !out_vertices) {
        return PMSDK_ERROR_INVALID_ARGUMENT;
    }

    auto* m = reinterpret_cast<const pmsdk::Geometry::Mesh*>(mesh);
    size_t actual_size = 0;
    auto* vertices = m->GetVertices(&actual_size);
    
    if (count > actual_size) {
        count = actual_size;
    }

    // pmsdk_vertex_t matches pmsdk::Geometry::Vertex layout exactly
    std::memcpy(out_vertices, vertices, count * sizeof(pmsdk_vertex_t));

    return PMSDK_SUCCESS;
}

PMSDK_API pmsdk_status_t pmsdk_mesh_get_indices(const pmsdk_mesh_t* mesh, uint32_t* out_indices, size_t count) {
    if (!mesh || !out_indices) {
        return PMSDK_ERROR_INVALID_ARGUMENT;
    }

    auto* m = reinterpret_cast<const pmsdk::Geometry::Mesh*>(mesh);
    size_t actual_size = 0;
    auto* indices = m->GetIndices(&actual_size);
    
    if (count > actual_size) {
        count = actual_size;
    }

    std::memcpy(out_indices, indices, count * sizeof(uint32_t));

    return PMSDK_SUCCESS;
}

PMSDK_API pmsdk_status_t pmsdk_mesh_clear(pmsdk_mesh_t* mesh) {
    if (!mesh) return PMSDK_ERROR_INVALID_ARGUMENT;
    try {
        reinterpret_cast<Mesh*>(mesh)->Clear();
        return PMSDK_SUCCESS;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

} // extern "C"
