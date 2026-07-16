#include "PMSDK/Calibration/GrayCode.h"

// We can implement basic Gray code generation directly without depending on OpenCV, 
// ensuring the SDK functions perfectly even in lightweight builds.

namespace pmsdk::Calibration {

struct GrayCode::Impl {
    int width;
    int height;
    int colBits;
    int rowBits;
    
    Impl(int w, int h) : width(w), height(h) {
        colBits = 0;
        int pw = 1;
        while (pw < width) {
            pw *= 2;
            colBits++;
        }
        
        rowBits = 0;
        int ph = 1;
        while (ph < height) {
            ph *= 2;
            rowBits++;
        }
    }
};

GrayCode::GrayCode(int width, int height) 
    : m_impl(std::make_unique<Impl>(width, height)) {}

GrayCode::~GrayCode() = default;

size_t GrayCode::GetPatternCount() const {
    // We typically need colBits + rowBits patterns for a full sequence,
    // plus optionally full black and full white patterns. 
    // Here we'll just return the required sequence count.
    return m_impl->colBits + m_impl->rowBits;
}

namespace {

std::vector<uint8_t> GenerateBitPattern(int width, int height, int colBits, int rowBits,
                                        size_t index, bool inverted) {
    std::vector<uint8_t> buffer(static_cast<size_t>(width) * height, 0);

    bool isColumn = (int)index < colBits;
    int bitIndex = isColumn ? colBits - 1 - (int)index : rowBits - 1 - ((int)index - colBits);

    for (int y = 0; y < height; ++y) {
        for (int x = 0; x < width; ++x) {
            int val = isColumn ? x : y;

            // Convert to Gray code: G = (B >> 1) ^ B
            int grayVal = (val >> 1) ^ val;

            bool bit = (grayVal & (1 << bitIndex)) != 0;
            if (inverted) bit = !bit;
            buffer[y * width + x] = bit ? 255 : 0;
        }
    }

    return buffer;
}

} // namespace

std::vector<uint8_t> GrayCode::GeneratePattern(size_t index) const {
    if (index >= GetPatternCount()) {
        return std::vector<uint8_t>(static_cast<size_t>(m_impl->width) * m_impl->height, 0);
    }
    return GenerateBitPattern(m_impl->width, m_impl->height, m_impl->colBits, m_impl->rowBits,
                              index, /*inverted=*/false);
}

size_t GrayCode::GetRobustPatternCount() const {
    return 2 + 2 * GetPatternCount();
}

std::vector<uint8_t> GrayCode::GenerateRobustPattern(size_t index) const {
    const size_t pixelCount = static_cast<size_t>(m_impl->width) * m_impl->height;

    if (index >= GetRobustPatternCount()) {
        return std::vector<uint8_t>(pixelCount, 0);
    }
    if (index == 0) {
        return std::vector<uint8_t>(pixelCount, 255); // all-white reference
    }
    if (index == 1) {
        return std::vector<uint8_t>(pixelCount, 0);   // all-black reference
    }

    size_t patternIndex = (index - 2) / 2;
    bool inverted = ((index - 2) % 2) == 1;
    return GenerateBitPattern(m_impl->width, m_impl->height, m_impl->colBits, m_impl->rowBits,
                              patternIndex, inverted);
}

} // namespace pmsdk::Calibration
