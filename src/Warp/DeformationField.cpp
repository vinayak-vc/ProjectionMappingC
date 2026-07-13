#include "PMSDK/Warp/DeformationField.h"
#include <execution>
#include <algorithm>

namespace pmsdk::Warp {

struct DeformationField::Impl {
    DeformationType type{DeformationType::None};
    Geometry::BezierPatch bezierPatch;
    Geometry::GridWarp gridWarp;
};

DeformationField::DeformationField() : m_impl(std::make_unique<Impl>()) {}
DeformationField::~DeformationField() = default;

void DeformationField::SetType(DeformationType type) {
    m_impl->type = type;
}

DeformationType DeformationField::GetType() const {
    return m_impl->type;
}

Geometry::BezierPatch* DeformationField::GetBezierPatch() {
    return &m_impl->bezierPatch;
}

Geometry::GridWarp* DeformationField::GetGridWarp() {
    return &m_impl->gridWarp;
}

std::unique_ptr<Geometry::Mesh> DeformationField::ApplyDeformation(const Geometry::Mesh& baseMesh) const {
    auto result = std::make_unique<Geometry::Mesh>();
    size_t v_count = 0;
    auto vertices = baseMesh.GetVertices(&v_count);
    size_t i_count = 0;
    auto indices = baseMesh.GetIndices(&i_count);

    if (v_count == 0) return nullptr;

    result->SetVertices(vertices, v_count);
    result->SetIndices(indices, i_count);

    ApplyDeformationInPlace(*result);
    return result;
}

void DeformationField::ApplyDeformationInPlace(Geometry::Mesh& mesh) const {
    size_t v_count = 0;
    auto verts = mesh.GetVerticesMutable(&v_count);
    if (v_count == 0) return;

    if (m_impl->type == DeformationType::Bezier) {
        std::for_each(std::execution::par_unseq, verts, verts + v_count, [this](auto& v) {
            v.position = m_impl->bezierPatch.Evaluate(v.uv.x, v.uv.y);
        });
    } else if (m_impl->type == DeformationType::Grid) {
        std::for_each(std::execution::par_unseq, verts, verts + v_count, [this](auto& v) {
            v.position = m_impl->gridWarp.Evaluate(v.uv.x, v.uv.y);
        });
    }
    
    mesh.RecalculateNormals();
}

} // namespace pmsdk::Warp
