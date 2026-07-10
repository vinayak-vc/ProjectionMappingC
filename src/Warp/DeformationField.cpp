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
    auto origVerts = baseMesh.GetVertices();
    auto origIdx = baseMesh.GetIndices();

    std::vector<Geometry::Vertex> newVerts(origVerts.begin(), origVerts.end());
    std::vector<uint32_t> newIdx(origIdx.begin(), origIdx.end());

    if (m_impl->type == DeformationType::Bezier) {
        for (auto& v : newVerts) {
            // Assume the mesh's UV coordinates map 0-1 to the full patch area.
            v.position = m_impl->bezierPatch.Evaluate(v.uv.x, v.uv.y);
        }
    } else if (m_impl->type == DeformationType::Grid) {
        for (auto& v : newVerts) {
            v.position = m_impl->gridWarp.Evaluate(v.uv.x, v.uv.y);
        }
    }

    result->SetVertices(newVerts);
    result->SetIndices(newIdx);
    result->RecalculateNormals(); // Recompute normals for the deformed mesh
    return result;
}

} // namespace pmsdk::Warp
