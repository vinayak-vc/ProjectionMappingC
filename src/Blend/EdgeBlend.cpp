#include "PMSDK/Blend/EdgeBlend.h"
#include <algorithm>

namespace pmsdk::Blend {

EdgeBlend::EdgeBlend() = default;
EdgeBlend::~EdgeBlend() = default;

void EdgeBlend::SetSize(float size) {
    m_size = std::clamp(size, 0.0f, 1.0f);
}

float EdgeBlend::GetSize() const {
    return m_size;
}

void EdgeBlend::SetGamma(float gamma) {
    if (gamma > 0.0f) {
        m_gamma = gamma;
    }
}

float EdgeBlend::GetGamma() const {
    return m_gamma;
}

void EdgeBlend::SetCurveType(CurveType type) {
    m_curveType = type;
}

CurveType EdgeBlend::GetCurveType() const {
    return m_curveType;
}

float EdgeBlend::Evaluate(float distance) const {
    if (m_size <= 0.0f) return 1.0f; // No blend zone
    
    if (distance >= m_size) return 1.0f; // Outside blend zone (fully visible)
    if (distance <= 0.0f) return 0.0f; // At the absolute edge (fully transparent)

    // Normalize distance within the blend zone [0, 1]
    float x = distance / m_size;

    switch (m_curveType) {
        case CurveType::Linear:
            return x;
        case CurveType::Smoothstep:
            return x * x * (3.0f - 2.0f * x);
        case CurveType::Power:
        default:
            return std::pow(x, m_gamma);
    }
}

} // namespace pmsdk::Blend
