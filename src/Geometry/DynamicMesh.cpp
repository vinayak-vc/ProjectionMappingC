#include "PMSDK/Geometry/DynamicMesh.h"
#include <unordered_map>
#include <unordered_set>

namespace pmsdk::Geometry {

struct Face {
    uint32_t v0, v1, v2;
    bool active{true};
};

struct DynVertex {
    Vertex data;
    bool active{true};
    std::unordered_set<uint32_t> adjacentFaces;
};

struct DynamicMesh::Impl {
    std::unordered_map<uint32_t, DynVertex> vertices;
    std::unordered_map<uint32_t, Face> faces;
    uint32_t nextVertexId{0};
    uint32_t nextFaceId{0};
    size_t activeVertexCount{0};
    size_t activeFaceCount{0};
};

DynamicMesh::DynamicMesh() : m_impl(std::make_unique<Impl>()) {}
DynamicMesh::~DynamicMesh() = default;

void DynamicMesh::FromMesh(const Mesh& mesh) {
    Clear();
    size_t v_count = 0;
    auto verts = mesh.GetVertices(&v_count);
    size_t i_count = 0;
    auto idx = mesh.GetIndices(&i_count);

    for (size_t i = 0; i < v_count; ++i) AddVertex(verts[i]);
    for (size_t i = 0; i < i_count; i += 3) {
        if (i + 2 < i_count) {
            AddFace(idx[i], idx[i+1], idx[i+2]);
        }
    }
}

std::unique_ptr<Mesh> DynamicMesh::ToMesh() const {
    auto mesh = std::make_unique<Mesh>();
    std::vector<Vertex> verts;
    std::vector<uint32_t> idx;
    
    // Create a compact mapping
    std::unordered_map<uint32_t, uint32_t> oldToNew;
    verts.reserve(m_impl->activeVertexCount);
    
    for (const auto& [id, v] : m_impl->vertices) {
        if (v.active) {
            oldToNew[id] = (uint32_t)verts.size();
            verts.push_back(v.data);
        }
    }

    idx.reserve(m_impl->activeFaceCount * 3);
    for (const auto& [id, f] : m_impl->faces) {
        if (f.active) {
            // Verify vertices still active (they should be, unless topology is broken)
            if (oldToNew.count(f.v0) && oldToNew.count(f.v1) && oldToNew.count(f.v2)) {
                idx.push_back(oldToNew[f.v0]);
                idx.push_back(oldToNew[f.v1]);
                idx.push_back(oldToNew[f.v2]);
            }
        }
    }

    mesh->SetVertices(verts.data(), verts.size());
    mesh->SetIndices(idx.data(), idx.size());
    return mesh;
}

uint32_t DynamicMesh::AddVertex(const Vertex& v) {
    uint32_t id = m_impl->nextVertexId++;
    m_impl->vertices[id] = {v, true, {}};
    m_impl->activeVertexCount++;
    return id;
}

void DynamicMesh::RemoveVertex(uint32_t vertexId) {
    auto it = m_impl->vertices.find(vertexId);
    if (it != m_impl->vertices.end() && it->second.active) {
        // Remove connected faces first
        auto connectedFaces = it->second.adjacentFaces;
        for (uint32_t faceId : connectedFaces) {
            RemoveFace(faceId);
        }
        it->second.active = false;
        m_impl->activeVertexCount--;
    }
}

uint32_t DynamicMesh::AddFace(uint32_t v0, uint32_t v1, uint32_t v2) {
    // Basic validation
    if (!m_impl->vertices.count(v0) || !m_impl->vertices[v0].active) return (uint32_t)-1;
    if (!m_impl->vertices.count(v1) || !m_impl->vertices[v1].active) return (uint32_t)-1;
    if (!m_impl->vertices.count(v2) || !m_impl->vertices[v2].active) return (uint32_t)-1;

    uint32_t id = m_impl->nextFaceId++;
    m_impl->faces[id] = {v0, v1, v2, true};
    m_impl->activeFaceCount++;

    m_impl->vertices[v0].adjacentFaces.insert(id);
    m_impl->vertices[v1].adjacentFaces.insert(id);
    m_impl->vertices[v2].adjacentFaces.insert(id);

    return id;
}

void DynamicMesh::RemoveFace(uint32_t faceId) {
    auto it = m_impl->faces.find(faceId);
    if (it != m_impl->faces.end() && it->second.active) {
        it->second.active = false;
        m_impl->activeFaceCount--;

        m_impl->vertices[it->second.v0].adjacentFaces.erase(faceId);
        m_impl->vertices[it->second.v1].adjacentFaces.erase(faceId);
        m_impl->vertices[it->second.v2].adjacentFaces.erase(faceId);
    }
}

std::vector<uint32_t> DynamicMesh::GetAdjacentFaces(uint32_t vertexId) const {
    auto it = m_impl->vertices.find(vertexId);
    if (it != m_impl->vertices.end() && it->second.active) {
        return {it->second.adjacentFaces.begin(), it->second.adjacentFaces.end()};
    }
    return {};
}

std::vector<uint32_t> DynamicMesh::GetAdjacentVertices(uint32_t vertexId) const {
    std::unordered_set<uint32_t> adjVerts;
    auto faces = GetAdjacentFaces(vertexId);
    for (uint32_t fId : faces) {
        const auto& f = m_impl->faces[fId];
        if (f.v0 != vertexId) adjVerts.insert(f.v0);
        if (f.v1 != vertexId) adjVerts.insert(f.v1);
        if (f.v2 != vertexId) adjVerts.insert(f.v2);
    }
    return {adjVerts.begin(), adjVerts.end()};
}

Vertex* DynamicMesh::GetVertexMutable(uint32_t vertexId) {
    auto it = m_impl->vertices.find(vertexId);
    if (it != m_impl->vertices.end() && it->second.active) {
        return &it->second.data;
    }
    return nullptr;
}

const Vertex* DynamicMesh::GetVertex(uint32_t vertexId) const {
    auto it = m_impl->vertices.find(vertexId);
    if (it != m_impl->vertices.end() && it->second.active) {
        return &it->second.data;
    }
    return nullptr;
}

size_t DynamicMesh::GetActiveVertexCount() const {
    return m_impl->activeVertexCount;
}

size_t DynamicMesh::GetActiveFaceCount() const {
    return m_impl->activeFaceCount;
}

void DynamicMesh::Clear() {
    m_impl->vertices.clear();
    m_impl->faces.clear();
    m_impl->nextVertexId = 0;
    m_impl->nextFaceId = 0;
    m_impl->activeVertexCount = 0;
    m_impl->activeFaceCount = 0;
}

} // namespace pmsdk::Geometry
