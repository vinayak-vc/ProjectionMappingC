#include "PMSDK/Geometry/MeshOptimizer.h"
#include <unordered_map>
#include <vector>

namespace pmsdk::Geometry {

void MeshOptimizer::WeldVertices(Mesh& mesh, float threshold) {
    auto vertices = mesh.GetVertices();
    auto indices = mesh.GetIndices();

    if (vertices.empty()) return;

    std::vector<Vertex> newVertices;
    std::vector<uint32_t> newIndices;
    std::unordered_map<uint32_t, uint32_t> remap;
    
    float thresholdSq = threshold * threshold;

    for (uint32_t i = 0; i < vertices.size(); ++i) {
        int foundIdx = -1;
        // Simple O(N^2) search. For production, use KDTree or Octree.
        // Since meshes in mapping are usually < 10k vertices, this might be fine,
        // but KDTree exists in our library. Let's just do simple search for now 
        // to avoid dependency circularity in this single function.
        for (uint32_t j = 0; j < newVertices.size(); ++j) {
            float dx = vertices[i].position.x - newVertices[j].position.x;
            float dy = vertices[i].position.y - newVertices[j].position.y;
            float dz = vertices[i].position.z - newVertices[j].position.z;
            float distSq = dx*dx + dy*dy + dz*dz;
            if (distSq <= thresholdSq) {
                foundIdx = j;
                break;
            }
        }

        if (foundIdx != -1) {
            remap[i] = (uint32_t)foundIdx;
        } else {
            remap[i] = (uint32_t)newVertices.size();
            newVertices.push_back(vertices[i]);
        }
    }

    newIndices.reserve(indices.size());
    for (uint32_t idx : indices) {
        newIndices.push_back(remap[idx]);
    }

    mesh.SetVertices(newVertices);
    mesh.SetIndices(newIndices);
}

void MeshOptimizer::RecalculateSmoothNormals(Mesh& mesh) {
    auto vertices = mesh.GetVerticesMutable();
    auto indices = mesh.GetIndices();

    for (auto& v : vertices) {
        v.normal = {0.0f, 0.0f, 0.0f};
    }

    for (size_t i = 0; i < indices.size(); i += 3) {
        if (i + 2 >= indices.size()) break;
        uint32_t i0 = indices[i];
        uint32_t i1 = indices[i+1];
        uint32_t i2 = indices[i+2];

        Math::Vector3 v0 = vertices[i0].position;
        Math::Vector3 v1 = vertices[i1].position;
        Math::Vector3 v2 = vertices[i2].position;

        Math::Vector3 e1 = v1 - v0;
        Math::Vector3 e2 = v2 - v0;
        Math::Vector3 cross = e1.Cross(e2);
        
        // Accumulate area-weighted normal (cross product magnitude is 2x area)
        vertices[i0].normal = vertices[i0].normal + cross;
        vertices[i1].normal = vertices[i1].normal + cross;
        vertices[i2].normal = vertices[i2].normal + cross;
    }

    for (auto& v : vertices) {
        v.normal.Normalize();
    }
}

} // namespace pmsdk::Geometry
