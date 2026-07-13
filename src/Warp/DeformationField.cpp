#include "PMSDK/Warp/DeformationField.h"

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

    std::vector<Geometry::Vertex> deformedVerts(vertices, vertices + v_count);
    std::vector<uint32_t> deformedIndices(indices, indices + i_count);

    if (m_impl->type == DeformationType::Bezier) {
        for (auto& v : deformedVerts) {
            // Assume the mesh's UV coordinates map 0-1 to the full patch area.
            v.position = m_impl->bezierPatch.Evaluate(v.uv.x, v.uv.y);
        }
    } else if (m_impl->type == DeformationType::Grid) {
        for (auto& v : deformedVerts) {
            v.position = m_impl->gridWarp.Evaluate(v.uv.x, v.uv.y);
        }
    }

    result->SetVertices(deformedVerts.data(), deformedVerts.size());
    result->SetIndices(deformedIndices.data(), deformedIndices.size());
    result->RecalculateNormals(); // Recompute normals for the deformed mesh
    return result;
}

} // namespace pmsdk::Warp
