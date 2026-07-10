#include "PMSDK/Blend/BlendConfig.h"
#include <algorithm>

namespace pmsdk::Blend {

BlendConfig::BlendConfig() = default;
BlendConfig::~BlendConfig() = default;

EdgeBlend& BlendConfig::GetLeftEdge() { return m_left; }
const EdgeBlend& BlendConfig::GetLeftEdge() const { return m_left; }

EdgeBlend& BlendConfig::GetRightEdge() { return m_right; }
const EdgeBlend& BlendConfig::GetRightEdge() const { return m_right; }

EdgeBlend& BlendConfig::GetTopEdge() { return m_top; }
const EdgeBlend& BlendConfig::GetTopEdge() const { return m_top; }

EdgeBlend& BlendConfig::GetBottomEdge() { return m_bottom; }
const EdgeBlend& BlendConfig::GetBottomEdge() const { return m_bottom; }

void BlendConfig::SetBlackLevel(float level) {
    m_blackLevel = std::clamp(level, 0.0f, 1.0f);
}

float BlendConfig::GetBlackLevel() const {
    return m_blackLevel;
}

float BlendConfig::Evaluate(float u, float v) const {
    // Left edge starts at u=0, grows inward
    float leftMask = m_left.Evaluate(u);
    
    // Right edge starts at u=1, grows inward (distance is 1 - u)
    float rightMask = m_right.Evaluate(1.0f - u);
    
    // Bottom edge starts at v=0, grows inward
    float bottomMask = m_bottom.Evaluate(v);
    
    // Top edge starts at v=1, grows inward (distance is 1 - v)
    float topMask = m_top.Evaluate(1.0f - v);
    
    // Multiply all masks together for corners
    float alpha = leftMask * rightMask * bottomMask * topMask;

    // Apply black level compensation.
    // In areas where alpha is 0 (outside blend, fully masked), the projector still projects "black light".
    // This isn't handled purely by an alpha multiplier (black level compensation usually raises the black floor
    // in unblended areas so they match the double-black of blended areas).
    // For this simple Evaluate function, we return the base multiplier. 
    // The mask generator may choose to return a secondary channel for black level, or bake it in.
    // Let's bake a simple compensation: output = alpha * (1 - black) + black
    // Actually, edge blending alpha scales the image RGB.
    // If we bake black level into the alpha mask, it doesn't work correctly for the image content.
    // Normally, Shader = ImageRGB * AlphaMask + BlackLevelMask.
    // We will just return AlphaMask here.
    return alpha;
}

} // namespace pmsdk::Blend
