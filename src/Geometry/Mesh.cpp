#include "PMSDK/Geometry/Mesh.h"
#include <vector>
#include <execution>
#include <numeric>

namespace pmsdk::Geometry {

struct Mesh::Impl {
    std::vector<Vertex> vertices;
    std::vector<uint32_t> indices;
};

Mesh::Mesh() : m_impl(std::make_unique<Impl>()) {}
Mesh::~Mesh() = default;
Mesh::Mesh(Mesh&& other) noexcept = default;
Mesh& Mesh::operator=(Mesh&& other) noexcept = default;

void Mesh::SetVertices(const Vertex* vertices, size_t count) {
    if (count > 0 && vertices != nullptr) {
        m_impl->vertices.assign(vertices, vertices + count);
    }
}

void Mesh::SetIndices(const uint32_t* indices, size_t count) {
    if (count > 0 && indices != nullptr) {
        m_impl->indices.assign(indices, indices + count);
    }
}

const Vertex* Mesh::GetVertices(size_t* out_count) const {
    if (out_count) *out_count = m_impl->vertices.size();
    return m_impl->vertices.data();
}

const uint32_t* Mesh::GetIndices(size_t* out_count) const {
    if (out_count) *out_count = m_impl->indices.size();
    return m_impl->indices.data();
}

Vertex* Mesh::GetVerticesMutable(size_t* out_count) {
    if (out_count) *out_count = m_impl->vertices.size();
    return m_impl->vertices.data();
}

uint32_t* Mesh::GetIndicesMutable(size_t* out_count) {
    if (out_count) *out_count = m_impl->indices.size();
    return m_impl->indices.data();
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

    std::for_each(std::execution::par_unseq, m_impl->vertices.begin(), m_impl->vertices.end(), [](auto& v) {
        v.normal = Math::Vector3(0.0f, 0.0f, 0.0f);
    });

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

    std::for_each(std::execution::par_unseq, m_impl->vertices.begin(), m_impl->vertices.end(), [](auto& v) {
        v.normal.Normalize();
    });
}

Math::BoundingBox Mesh::CalculateBounds() const {
    if (m_impl->vertices.empty()) return Math::BoundingBox{};

    return std::transform_reduce(
        std::execution::par_unseq,
        m_impl->vertices.begin(), m_impl->vertices.end(),
        Math::BoundingBox{},
        [](const Math::BoundingBox& a, const Math::BoundingBox& b) {
            Math::BoundingBox res = a;
            if (b.min.x < res.min.x) res.min.x = b.min.x;
            if (b.min.y < res.min.y) res.min.y = b.min.y;
            if (b.min.z < res.min.z) res.min.z = b.min.z;
            if (b.max.x > res.max.x) res.max.x = b.max.x;
            if (b.max.y > res.max.y) res.max.y = b.max.y;
            if (b.max.z > res.max.z) res.max.z = b.max.z;
            return res;
        },
        [](const Vertex& v) {
            Math::BoundingBox b;
            b.Expand(v.position);
            return b;
        }
    );
}

} // namespace pmsdk::Geometry
