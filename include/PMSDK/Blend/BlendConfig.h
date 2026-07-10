#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Blend/EdgeBlend.h"

namespace pmsdk::Blend {

class BlendConfig {
public:
    PMSDK_API BlendConfig();
    PMSDK_API ~BlendConfig();

    PMSDK_API EdgeBlend& GetLeftEdge();
    PMSDK_API const EdgeBlend& GetLeftEdge() const;

    PMSDK_API EdgeBlend& GetRightEdge();
    PMSDK_API const EdgeBlend& GetRightEdge() const;

    PMSDK_API EdgeBlend& GetTopEdge();
    PMSDK_API const EdgeBlend& GetTopEdge() const;

    PMSDK_API EdgeBlend& GetBottomEdge();
    PMSDK_API const EdgeBlend& GetBottomEdge() const;

    // Global properties across the whole projector
    PMSDK_API void SetBlackLevel(float level);
    PMSDK_API float GetBlackLevel() const;

    // Evaluates the final blend alpha at a normalized coordinate (u, v) in [0, 1].
    // u=0 is Left, u=1 is Right
    // v=0 is Bottom, v=1 is Top
    // The final alpha is the multiplication of the four edge masks.
    PMSDK_API float Evaluate(float u, float v) const;

private:
    EdgeBlend m_left;
    EdgeBlend m_right;
    EdgeBlend m_top;
    EdgeBlend m_bottom;
    
    float m_blackLevel{0.0f};
};

} // namespace pmsdk::Blend
