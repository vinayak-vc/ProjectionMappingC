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

    // --- Robust sequence (references + inverse patterns) ---------------------
    // Real-camera decoding with a single global threshold is fragile (ambient
    // light, surface albedo, projector falloff). The robust sequence follows
    // standard structured-light practice (cf. OpenCV structured_light): an
    // all-white and all-black reference frame for a per-pixel shadow mask, and
    // every Gray-code pattern followed by its inverse so each bit is decided by
    // comparing pattern vs inverse per pixel instead of against a threshold.
    //
    // Sequence layout:
    //   index 0                     -> all-white reference
    //   index 1                     -> all-black reference
    //   index 2 + 2k                -> pattern k   (same k-order as GeneratePattern)
    //   index 3 + 2k                -> inverse of pattern k
    // Count = 2 + 2 * GetPatternCount().

    PMSDK_API size_t GetRobustPatternCount() const;

    // Generates the robust-sequence pattern at the given index (layout above).
    PMSDK_API std::vector<uint8_t> GenerateRobustPattern(size_t index) const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Calibration
