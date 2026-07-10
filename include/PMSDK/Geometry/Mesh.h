#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Vertex.h"
#include "PMSDK/Math/BoundingBox.h"
#include <span>
#include <memory>
#include <cstdint>

namespace pmsdk::Geometry {

/**
 * @brief Represents a 3D mesh composed of vertices and optional indices.
 * 
 * Uses the PImpl idiom to hide internal storage details and provide a stable ABI.
 */
class Mesh {
public:
    /** @brief Default constructor. Creates an empty mesh. */
    PMSDK_API Mesh();
    
    /** @brief Destructor. */
    PMSDK_API ~Mesh();
    
    Mesh(const Mesh& other) = delete;
    Mesh& operator=(const Mesh& other) = delete;
    
    /** @brief Move constructor. */
    PMSDK_API Mesh(Mesh&& other) noexcept;
    
    /** @brief Move assignment operator. */
    PMSDK_API Mesh& operator=(Mesh&& other) noexcept;

    /**
     * @brief Sets the vertex data for this mesh.
     * @param vertices Span of vertices to copy into the mesh.
     */
    PMSDK_API void SetVertices(std::span<const Vertex> vertices);
    
    /**
     * @brief Sets the index data for this mesh.
     * @param indices Span of 32-bit unsigned indices to copy into the mesh.
     */
    PMSDK_API void SetIndices(std::span<const uint32_t> indices);

    /** @return A read-only span of the mesh vertices. */
    PMSDK_API std::span<const Vertex> GetVertices() const;
    
    /** @return A read-only span of the mesh indices. */
    PMSDK_API std::span<const uint32_t> GetIndices() const;

    /** @return A mutable span of the mesh vertices. */
    PMSDK_API std::span<Vertex> GetVerticesMutable();
    
    /** @return A mutable span of the mesh indices. */
    PMSDK_API std::span<uint32_t> GetIndicesMutable();

    /** @return The number of vertices in the mesh. */
    PMSDK_API size_t GetVertexCount() const;
    
    /** @return The number of indices in the mesh. */
    PMSDK_API size_t GetIndexCount() const;

    /** @brief Clears all vertices and indices, leaving the mesh empty. */
    PMSDK_API void Clear();
    
    /** @brief Recalculates vertex normals based on triangle face normals. */
    PMSDK_API void RecalculateNormals();
    
    /** @return The axis-aligned bounding box of the mesh vertices. */
    PMSDK_API Math::BoundingBox CalculateBounds() const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Geometry
