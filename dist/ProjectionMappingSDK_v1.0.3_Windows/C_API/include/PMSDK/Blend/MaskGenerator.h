#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Blend/BlendConfig.h"
#include <vector>
#include <cstdint>

namespace pmsdk::Blend {

class MaskGenerator {
public:
    // Generates a 2D float array (width * height) where each pixel is the alpha multiplier.
    // Indexing is: y * width + x
    PMSDK_API static std::vector<float> GenerateFloatMask(const BlendConfig& config, int width, int height);

    // Generates a 2D byte array (width * height) where each pixel is [0, 255].
    PMSDK_API static std::vector<uint8_t> GenerateByteMask(const BlendConfig& config, int width, int height);
};

} // namespace pmsdk::Blend
