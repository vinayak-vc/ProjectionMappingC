#include "PMSDK/Warp/DeformationField.h"
#include <execution>
#include <algorithm>

namespace pmsdk::Warp {

struct DeformationField::Impl {
    DeformationType type{DeformationType::None};
    Geometry::BezierPatch bezierPatch;
    Geometry::GridWarp gridWarp;
    Geometry::PerspectiveWarp perspectiveWarp;
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

Geometry::PerspectiveWarp* DeformationField::GetPerspectiveWarp() {
    return &m_impl->perspectiveWarp;
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
        m_impl->bezierPatch.ApplyDeformation(verts, v_count);
    } else if (m_impl->type == DeformationType::Grid) {
        m_impl->gridWarp.ApplyDeformation(verts, v_count);
    } else if (m_impl->type == DeformationType::Perspective) {
        m_impl->perspectiveWarp.ApplyDeformation(verts, v_count);
    }

    mesh.RecalculateNormals();
}

} // namespace pmsdk::Warp
