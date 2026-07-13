#include "PMSDK/Geometry/MeshOptimizer.h"
#include <unordered_map>
#include <vector>

namespace pmsdk::Geometry {

void MeshOptimizer::WeldVertices(Mesh& mesh, float threshold) {
    size_t v_count = 0;
    auto vertices = mesh.GetVertices(&v_count);
    size_t i_count = 0;
    auto indices = mesh.GetIndices(&i_count);

    if (v_count == 0) return;

    std::vector<Vertex> newVertices;
    std::vector<uint32_t> newIndices;
    std::unordered_map<uint32_t, uint32_t> remap;
    
    float thresholdSq = threshold * threshold;

    for (uint32_t i = 0; i < v_count; ++i) {
        int foundIdx = -1;
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

    newIndices.reserve(i_count);
    for (size_t i = 0; i < i_count; ++i) {
        newIndices.push_back(remap[indices[i]]);
    }

    mesh.SetVertices(newVertices.data(), newVertices.size());
    mesh.SetIndices(newIndices.data(), newIndices.size());
}

void MeshOptimizer::RecalculateSmoothNormals(Mesh& mesh) {
    size_t v_count = 0;
    auto vertices = mesh.GetVerticesMutable(&v_count);
    size_t i_count = 0;
    auto indices = mesh.GetIndices(&i_count);

    for (size_t i = 0; i < v_count; ++i) {
        vertices[i].normal = {0.0f, 0.0f, 0.0f};
    }

    for (size_t i = 0; i < i_count; i += 3) {
        if (i + 2 >= i_count) break;
        uint32_t i0 = indices[i];
        uint32_t i1 = indices[i+1];
        uint32_t i2 = indices[i+2];

        Math::Vector3 v0 = vertices[i0].position;
        Math::Vector3 v1 = vertices[i1].position;
        Math::Vector3 v2 = vertices[i2].position;

        Math::Vector3 e1 = v1 - v0;
        Math::Vector3 e2 = v2 - v0;
        Math::Vector3 cross = e1.Cross(e2);
        
        vertices[i0].normal = vertices[i0].normal + cross;
        vertices[i1].normal = vertices[i1].normal + cross;
        vertices[i2].normal = vertices[i2].normal + cross;
    }

    for (size_t i = 0; i < v_count; ++i) {
        vertices[i].normal.Normalize();
    }
}

} // namespace pmsdk::Geometry
