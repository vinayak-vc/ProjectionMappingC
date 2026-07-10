#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Vertex.h"
#include "PMSDK/Math/BoundingBox.h"
#include <span>
#include <memory>
#include <cstdint>

namespace pmsdk::Geometry {

class Mesh {
public:
    PMSDK_API Mesh();
    PMSDK_API ~Mesh();
    Mesh(const Mesh& other) = delete;
    Mesh& operator=(const Mesh& other) = delete;
    PMSDK_API Mesh(Mesh&& other) noexcept;
    PMSDK_API Mesh& operator=(Mesh&& other) noexcept;

    PMSDK_API void SetVertices(std::span<const Vertex> vertices);
    PMSDK_API void SetIndices(std::span<const uint32_t> indices);

    PMSDK_API std::span<const Vertex> GetVertices() const;
    PMSDK_API std::span<const uint32_t> GetIndices() const;

    PMSDK_API std::span<Vertex> GetVerticesMutable();
    PMSDK_API std::span<uint32_t> GetIndicesMutable();

    PMSDK_API size_t GetVertexCount() const;
    PMSDK_API size_t GetIndexCount() const;

    PMSDK_API void Clear();
    PMSDK_API void RecalculateNormals();
    PMSDK_API Math::BoundingBox CalculateBounds() const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Geometry
