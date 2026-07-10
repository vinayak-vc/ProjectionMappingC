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

std::vector<uint8_t> GrayCode::GeneratePattern(size_t index) const {
    std::vector<uint8_t> buffer(m_impl->width * m_impl->height, 0);
    
    if (index >= GetPatternCount()) {
        return buffer;
    }

    bool isColumn = (int)index < m_impl->colBits;
    int bitIndex = isColumn ? m_impl->colBits - 1 - (int)index : m_impl->rowBits - 1 - ((int)index - m_impl->colBits);

    for (int y = 0; y < m_impl->height; ++y) {
        for (int x = 0; x < m_impl->width; ++x) {
            int val = isColumn ? x : y;
            
            // Convert to Gray code: G = (B >> 1) ^ B
            int grayVal = (val >> 1) ^ val;
            
            bool bit = (grayVal & (1 << bitIndex)) != 0;
            buffer[y * m_impl->width + x] = bit ? 255 : 0;
        }
    }

    return buffer;
}

} // namespace pmsdk::Calibration
