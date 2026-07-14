#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Blend/BlendConfig.h"
#include <string>

namespace pmsdk::Serialization {

PMSDK_API std::string SerializeBlendConfig(const Blend::BlendConfig& config);
PMSDK_API void DeserializeBlendConfig(const std::string& jsonString, Blend::BlendConfig& outConfig);

} // namespace pmsdk::Serialization
