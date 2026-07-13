#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"
#include <memory>
#include <vector>
#include <cstdint>

namespace pmsdk::Geometry {

// A topology-aware mesh data structure allowing dynamic modifications and adjacency queries.
class DynamicMesh {
public:
    PMSDK_API DynamicMesh();
    PMSDK_API ~DynamicMesh();
    DynamicMesh(const DynamicMesh&) = delete;
    DynamicMesh& operator=(const DynamicMesh&) = delete;

    // Convert to and from static Mesh
    PMSDK_API void FromMesh(const Mesh& mesh);
    PMSDK_API std::unique_ptr<Mesh> ToMesh() const;

    // Dynamic modifications
    PMSDK_API uint32_t AddVertex(const Vertex& v);
    PMSDK_API void RemoveVertex(uint32_t vertexId);
    
    // Add a triangular face
    PMSDK_API uint32_t AddFace(uint32_t v0, uint32_t v1, uint32_t v2);
    PMSDK_API void RemoveFace(uint32_t faceId);

    // Queries
    PMSDK_API std::vector<uint32_t> GetAdjacentFaces(uint32_t vertexId) const;
    PMSDK_API std::vector<uint32_t> GetAdjacentVertices(uint32_t vertexId) const;
    
    PMSDK_API Vertex* GetVertexMutable(uint32_t vertexId);
    PMSDK_API const Vertex* GetVertex(uint32_t vertexId) const;

    PMSDK_API size_t GetActiveVertexCount() const;
    PMSDK_API size_t GetActiveFaceCount() const;
    PMSDK_API void Clear();

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Geometry
