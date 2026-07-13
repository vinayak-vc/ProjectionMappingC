#include "PMSDK/Geometry/MeshSubdivision.h"
#include <vector>
#include <map>
#include <tuple>

namespace pmsdk::Geometry {

std::unique_ptr<Mesh> MeshSubdivision::SubdivideLinear(const Mesh& mesh) {
    auto result = std::make_unique<Mesh>();
    
    size_t v_count = 0;
    auto srcVerts = mesh.GetVertices(&v_count);
    size_t i_count = 0;
    auto srcIdx = mesh.GetIndices(&i_count);

    if (i_count == 0 || v_count == 0) return result;

    std::vector<Vertex> newVerts(srcVerts, srcVerts + v_count);
    std::vector<uint32_t> newIdx;

    // Cache to avoid duplicating vertices on shared edges
    std::map<std::pair<uint32_t, uint32_t>, uint32_t> edgeCache;

    auto getEdgeVertex = [&](uint32_t i1, uint32_t i2) -> uint32_t {
        std::pair<uint32_t, uint32_t> edge(std::min(i1, i2), std::max(i1, i2));
        auto it = edgeCache.find(edge);
        if (it != edgeCache.end()) return it->second;

        Vertex v;
        const Vertex& v1 = newVerts[i1];
        const Vertex& v2 = newVerts[i2];
        v.position = (v1.position + v2.position) * 0.5f;
        v.normal = (v1.normal + v2.normal).Normalized();
        v.uv = (v1.uv + v2.uv) * 0.5f;
        v.color = (v1.color + v2.color) * 0.5f;

        uint32_t idx = (uint32_t)newVerts.size();
        newVerts.push_back(v);
        edgeCache[edge] = idx;
        return idx;
    };

    for (size_t i = 0; i < i_count; i += 3) {
        if (i + 2 >= i_count) break;

        uint32_t i0 = srcIdx[i];
        uint32_t i1 = srcIdx[i + 1];
        uint32_t i2 = srcIdx[i + 2];

        uint32_t a = getEdgeVertex(i0, i1);
        uint32_t b = getEdgeVertex(i1, i2);
        uint32_t c = getEdgeVertex(i2, i0);

        newIdx.push_back(i0); newIdx.push_back(a); newIdx.push_back(c);
        newIdx.push_back(a);  newIdx.push_back(i1); newIdx.push_back(b);
        newIdx.push_back(c);  newIdx.push_back(b);  newIdx.push_back(i2);
        newIdx.push_back(a);  newIdx.push_back(b);  newIdx.push_back(c);
    }

    result->SetVertices(newVerts.data(), newVerts.size());
    result->SetIndices(newIdx.data(), newIdx.size());
    return result;
}

} // namespace pmsdk::Geometry
