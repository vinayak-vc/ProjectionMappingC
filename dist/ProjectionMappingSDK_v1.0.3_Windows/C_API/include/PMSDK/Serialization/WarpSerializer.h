#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Warp/WarpNode.h"
#include "PMSDK/Warp/DeformationField.h"
#include "PMSDK/Warp/Projector.h"
#include <string>
#include <memory>

namespace pmsdk::Serialization {

// Serializes a WarpNode hierarchy to a JSON string
PMSDK_API std::string SerializeWarpNode(const Warp::WarpNode& node);
PMSDK_API void DeserializeWarpNode(const std::string& jsonString, Warp::WarpNode& outNode);

// DeformationField
PMSDK_API std::string SerializeDeformationField(const Warp::DeformationField& field);
PMSDK_API void DeserializeDeformationField(const std::string& jsonString, Warp::DeformationField& outField);

// Projector
PMSDK_API std::string SerializeProjector(const Warp::Projector& projector);
PMSDK_API void DeserializeProjector(const std::string& jsonString, Warp::Projector& outProjector);

} // namespace pmsdk::Serialization
