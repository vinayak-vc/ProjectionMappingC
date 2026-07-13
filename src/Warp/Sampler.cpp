#include "PMSDK/Warp/Sampler.h"
#include <cmath>

namespace pmsdk::Warp {

Math::Vector2 Sampler::SampleUVAtPoint(const Geometry::Mesh& mesh, uint32_t faceIndex, const Math::Vector3& pointOnFace) {
    size_t v_count = 0;
    auto vertices = mesh.GetVertices(&v_count);
    size_t i_count = 0;
    auto indices = mesh.GetIndices(&i_count);

    size_t startIdx = faceIndex * 3;
    if (startIdx + 2 >= i_count) return {0.0f, 0.0f};

    const auto& v0 = vertices[indices[startIdx]];
    const auto& v1 = vertices[indices[startIdx + 1]];
    const auto& v2 = vertices[indices[startIdx + 2]];

    // Calculate barycentric coordinates using area ratios
    Math::Vector3 edge0 = v1.position - v0.position;
    Math::Vector3 edge1 = v2.position - v0.position;
    Math::Vector3 edge2 = pointOnFace - v0.position;

    float d00 = edge0.Dot(edge0);
    float d01 = edge0.Dot(edge1);
    float d11 = edge1.Dot(edge1);
    float d20 = edge2.Dot(edge0);
    float d21 = edge2.Dot(edge1);

    float denom = d00 * d11 - d01 * d01;
    if (std::abs(denom) < 1e-8f) return v0.uv; // degenerate triangle

    float v = (d11 * d20 - d01 * d21) / denom;
    float w = (d00 * d21 - d01 * d20) / denom;
    float u = 1.0f - v - w;

    // Interpolate UV
    return {
        u * v0.uv.x + v * v1.uv.x + w * v2.uv.x,
        u * v0.uv.y + v * v1.uv.y + w * v2.uv.y
    };
}

} // namespace pmsdk::Warp
