#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"
#include "PMSDK/Geometry/DynamicMesh.h"
#include "PMSDK/Geometry/BezierPatch.h"
#include "PMSDK/Geometry/GridWarp.h"
#include <string>

namespace pmsdk::Serialization {

// Serializes a Mesh to a JSON string
PMSDK_API std::string SerializeMesh(const Geometry::Mesh& mesh);
PMSDK_API void DeserializeMesh(const std::string& jsonString, Geometry::Mesh& outMesh);

// Serializes a DynamicMesh to a JSON string
PMSDK_API std::string SerializeDynamicMesh(const Geometry::DynamicMesh& mesh);
PMSDK_API void DeserializeDynamicMesh(const std::string& jsonString, Geometry::DynamicMesh& outMesh);

// Control grids
PMSDK_API std::string SerializeBezierPatch(const Geometry::BezierPatch& patch);
PMSDK_API void DeserializeBezierPatch(const std::string& jsonString, Geometry::BezierPatch& outPatch);

PMSDK_API std::string SerializeGridWarp(const Geometry::GridWarp& grid);
PMSDK_API void DeserializeGridWarp(const std::string& jsonString, Geometry::GridWarp& outGrid);

} // namespace pmsdk::Serialization
