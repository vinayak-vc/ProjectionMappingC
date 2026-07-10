#pragma once
#include "PMSDK/Core/Export.h"
#include <vector>
#include <cstdint>
#include <memory>

namespace pmsdk::Calibration {

class GrayCode {
public:
    PMSDK_API GrayCode(int width, int height);
    PMSDK_API ~GrayCode();

    // Returns the total number of patterns required to encode the width and height
    PMSDK_API size_t GetPatternCount() const;

    // Generates the pattern at the given index. Returns a flat 8-bit grayscale array (width * height).
    PMSDK_API std::vector<uint8_t> GeneratePattern(size_t index) const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Calibration
