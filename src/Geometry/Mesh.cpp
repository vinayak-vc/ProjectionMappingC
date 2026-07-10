#include "PMSDK/Geometry/Mesh.h"
#include <vector>

namespace pmsdk::Geometry {

struct Mesh::Impl {
    std::vector<Vertex> vertices;
    std::vector<uint32_t> indices;
};

Mesh::Mesh() : m_impl(std::make_unique<Impl>()) {}
Mesh::~Mesh() = default;
Mesh::Mesh(Mesh&& other) noexcept = default;
Mesh& Mesh::operator=(Mesh&& other) noexcept = default;

void Mesh::SetVertices(std::span<const Vertex> vertices) {
    m_impl->vertices.assign(vertices.begin(), vertices.end());
}

void Mesh::SetIndices(std::span<const uint32_t> indices) {
    m_impl->indices.assign(indices.begin(), indices.end());
}

std::span<const Vertex> Mesh::GetVertices() const {
    return m_impl->vertices;
}

std::span<const uint32_t> Mesh::GetIndices() const {
    return m_impl->indices;
}

std::span<Vertex> Mesh::GetVerticesMutable() {
    return m_impl->vertices;
}

std::span<uint32_t> Mesh::GetIndicesMutable() {
    return m_impl->indices;
}

size_t Mesh::GetVertexCount() const {
    return m_impl->vertices.size();
}

size_t Mesh::GetIndexCount() const {
    return m_impl->indices.size();
}

void Mesh::Clear() {
    m_impl->vertices.clear();
    m_impl->indices.clear();
}

void Mesh::RecalculateNormals() {
    if (m_impl->indices.empty() || m_impl->vertices.empty()) return;

    for (auto& v : m_impl->vertices) {
        v.normal = Math::Vector3(0.0f, 0.0f, 0.0f);
    }

    for (size_t i = 0; i < m_impl->indices.size(); i += 3) {
        if (i + 2 >= m_impl->indices.size()) break;

        uint32_t i0 = m_impl->indices[i];
        uint32_t i1 = m_impl->indices[i + 1];
        uint32_t i2 = m_impl->indices[i + 2];

        if (i0 >= m_impl->vertices.size() || i1 >= m_impl->vertices.size() || i2 >= m_impl->vertices.size()) continue;

        Math::Vector3 v0 = m_impl->vertices[i0].position;
        Math::Vector3 v1 = m_impl->vertices[i1].position;
        Math::Vector3 v2 = m_impl->vertices[i2].position;

        Math::Vector3 edge1 = v1 - v0;
        Math::Vector3 edge2 = v2 - v0;
        Math::Vector3 normal = edge1.Cross(edge2).Normalized();

        m_impl->vertices[i0].normal += normal;
        m_impl->vertices[i1].normal += normal;
        m_impl->vertices[i2].normal += normal;
    }

    for (auto& v : m_impl->vertices) {
        v.normal.Normalize();
    }
}

Math::BoundingBox Mesh::CalculateBounds() const {
    Math::BoundingBox bounds;
    for (const auto& v : m_impl->vertices) {
        bounds.Expand(v.position);
    }
    return bounds;
}

} // namespace pmsdk::Geometry
