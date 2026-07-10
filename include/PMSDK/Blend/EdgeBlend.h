#pragma once

#include "PMSDK/Core/Export.h"
#include <cmath>

namespace pmsdk::Blend {

enum class CurveType {
    Linear,
    Power,     // x^gamma
    Smoothstep // 3x^2 - 2x^3
};

class EdgeBlend {
public:
    PMSDK_API EdgeBlend();
    PMSDK_API ~EdgeBlend();

    // Size of the blend zone (normalized 0.0 to 1.0 representing percentage of screen width/height)
    PMSDK_API void SetSize(float size);
    PMSDK_API float GetSize() const;

    // Gamma exponent used when CurveType is Power
    PMSDK_API void SetGamma(float gamma);
    PMSDK_API float GetGamma() const;

    PMSDK_API void SetCurveType(CurveType type);
    PMSDK_API CurveType GetCurveType() const;

    // Evaluates the alpha multiplier (0.0 to 1.0) given a distance from the physical edge.
    // distance is normalized: 0.0 is the very edge of the projector, 'size' is the end of the blend zone.
    PMSDK_API float Evaluate(float distance) const;

private:
    float m_size{0.0f};
    float m_gamma{2.2f};
    CurveType m_curveType{CurveType::Power};
};

} // namespace pmsdk::Blend
