#include "PMSDK/Blend/MaskGenerator.h"

namespace pmsdk::Blend {

std::vector<float> MaskGenerator::GenerateFloatMask(const BlendConfig& config, int width, int height) {
    if (width <= 0 || height <= 0) return {};
    
    std::vector<float> mask;
    mask.resize(width * height);

    for (int y = 0; y < height; ++y) {
        // v = 0 at y = 0 (bottom), v = 1 at y = height - 1 (top)
        float v = (height > 1) ? (float)y / (float)(height - 1) : 0.5f;

        for (int x = 0; x < width; ++x) {
            // u = 0 at x = 0 (left), u = 1 at x = width - 1 (right)
            float u = (width > 1) ? (float)x / (float)(width - 1) : 0.5f;

            mask[y * width + x] = config.Evaluate(u, v);
        }
    }

    return mask;
}

std::vector<uint8_t> MaskGenerator::GenerateByteMask(const BlendConfig& config, int width, int height) {
    std::vector<float> floats = GenerateFloatMask(config, width, height);
    std::vector<uint8_t> bytes;
    bytes.reserve(floats.size());

    for (float f : floats) {
        // clamp safely just in case, though Evaluate should return [0, 1]
        float clamped = f;
        if (clamped < 0.0f) clamped = 0.0f;
        if (clamped > 1.0f) clamped = 1.0f;

        bytes.push_back(static_cast<uint8_t>(clamped * 255.0f + 0.5f));
    }

    return bytes;
}

} // namespace pmsdk::Blend
