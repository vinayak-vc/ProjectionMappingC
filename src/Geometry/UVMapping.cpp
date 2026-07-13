#include "PMSDK/Geometry/UVMapping.h"
#include <cmath>
#include <execution>
#include <algorithm>

namespace pmsdk::Geometry {

void UVMapping::ApplyPlanarMapping(Mesh& mesh) {
    auto bounds = mesh.CalculateBounds();
    Math::Vector3 size = bounds.max - bounds.min;
    
    size_t v_count = 0;
    auto* verts = mesh.GetVerticesMutable(&v_count);
    std::for_each(std::execution::par_unseq, verts, verts + v_count, [&](auto& v) {
        float u = (size.x > 0.0f) ? (v.position.x - bounds.min.x) / size.x : 0.0f;
        float v_coord = (size.y > 0.0f) ? (v.position.y - bounds.min.y) / size.y : 0.0f;
        v.uv = {u, v_coord};
    });
}

void UVMapping::ApplyCylindricalMapping(Mesh& mesh) {
    auto bounds = mesh.CalculateBounds();
    Math::Vector3 size = bounds.max - bounds.min;

    size_t v_count = 0;
    auto* verts = mesh.GetVerticesMutable(&v_count);
    std::for_each(std::execution::par_unseq, verts, verts + v_count, [&](auto& v) {
        float u = 0.5f + (float)std::atan2(v.position.z, v.position.x) / (2.0f * 3.14159265f);
        float v_coord = (size.y > 0.0f) ? (v.position.y - bounds.min.y) / size.y : 0.0f;
        v.uv = {u, v_coord};
    });
}

void UVMapping::ApplySphericalMapping(Mesh& mesh) {
    auto bounds = mesh.CalculateBounds();
    Math::Vector3 center = bounds.Center();

    size_t v_count = 0;
    auto* verts = mesh.GetVerticesMutable(&v_count);
    std::for_each(std::execution::par_unseq, verts, verts + v_count, [&](auto& v) {
        Math::Vector3 dir = (v.position - center).Normalized();
        float u = 0.5f + (float)std::atan2(dir.z, dir.x) / (2.0f * 3.14159265f);
        float v_coord = 0.5f - (float)std::asin(dir.y) / 3.14159265f;
        v.uv = {u, v_coord};
    });
}

} // namespace pmsdk::Geometry
