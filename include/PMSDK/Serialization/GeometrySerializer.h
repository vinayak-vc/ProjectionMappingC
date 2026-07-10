#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"
#include "PMSDK/Geometry/DynamicMesh.h"
#include "PMSDK/Geometry/BezierPatch.h"
#include "PMSDK/Geometry/GridWarp.h"
#include <string>

namespace pmsdk::Serialization {

/**
 * @brief Serializes a Mesh to a JSON string.
 * @param mesh The mesh to serialize.
 * @return A JSON formatted string containing the mesh data.
 */
PMSDK_API std::string SerializeMesh(const Geometry::Mesh& mesh);

/**
 * @brief Deserializes a Mesh from a JSON string.
 * @param jsonString The JSON string.
 * @param outMesh The mesh to populate.
 * @throw std::invalid_argument if the JSON is malformed.
 */
PMSDK_API void DeserializeMesh(const std::string& jsonString, Geometry::Mesh& outMesh);

/**
 * @brief Serializes a DynamicMesh to a JSON string.
 * @param mesh The dynamic mesh.
 * @return JSON string.
 */
PMSDK_API std::string SerializeDynamicMesh(const Geometry::DynamicMesh& mesh);

/**
 * @brief Deserializes a DynamicMesh from a JSON string.
 * @param jsonString The JSON string.
 * @param outMesh The dynamic mesh to populate.
 */
PMSDK_API void DeserializeDynamicMesh(const std::string& jsonString, Geometry::DynamicMesh& outMesh);

/**
 * @brief Serializes a BezierPatch to a JSON string.
 */
PMSDK_API std::string SerializeBezierPatch(const Geometry::BezierPatch& patch);

/**
 * @brief Deserializes a BezierPatch from a JSON string.
 */
PMSDK_API void DeserializeBezierPatch(const std::string& jsonString, Geometry::BezierPatch& outPatch);

/**
 * @brief Serializes a GridWarp to a JSON string.
 */
PMSDK_API std::string SerializeGridWarp(const Geometry::GridWarp& grid);

/**
 * @brief Deserializes a GridWarp from a JSON string.
 */
PMSDK_API void DeserializeGridWarp(const std::string& jsonString, Geometry::GridWarp& outGrid);

} // namespace pmsdk::Serialization
