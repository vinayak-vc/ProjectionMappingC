#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Blend/EdgeBlend.h"

namespace pmsdk::Blend {

/**
 * @brief Represents the total edge blending configuration for a projector.
 * 
 * Contains 4 EdgeBlend instances (Left, Right, Top, Bottom) and 
 * global properties like black level.
 */
class BlendConfig {
public:
    /** @brief Constructs a new BlendConfig with default settings. */
    PMSDK_API BlendConfig();
    
    /** @brief Destructor. */
    PMSDK_API ~BlendConfig();

    /** @return A mutable reference to the left edge blend settings. */
    PMSDK_API EdgeBlend& GetLeftEdge();
    /** @return A const reference to the left edge blend settings. */
    PMSDK_API const EdgeBlend& GetLeftEdge() const;

    /** @return A mutable reference to the right edge blend settings. */
    PMSDK_API EdgeBlend& GetRightEdge();
    /** @return A const reference to the right edge blend settings. */
    PMSDK_API const EdgeBlend& GetRightEdge() const;

    /** @return A mutable reference to the top edge blend settings. */
    PMSDK_API EdgeBlend& GetTopEdge();
    /** @return A const reference to the top edge blend settings. */
    PMSDK_API const EdgeBlend& GetTopEdge() const;

    /** @return A mutable reference to the bottom edge blend settings. */
    PMSDK_API EdgeBlend& GetBottomEdge();
    /** @return A const reference to the bottom edge blend settings. */
    PMSDK_API const EdgeBlend& GetBottomEdge() const;

    /**
     * @brief Sets the global black level offset for the projector.
     * @param level The black level.
     */
    PMSDK_API void SetBlackLevel(float level);
    
    /** @return The current black level offset. */
    PMSDK_API float GetBlackLevel() const;

    /**
     * @brief Evaluates the final blend alpha at a normalized coordinate.
     * 
     * u=0 is Left, u=1 is Right.
     * v=0 is Bottom, v=1 is Top.
     * The final alpha is the multiplication of the four edge masks.
     * 
     * @param u Horizontal coordinate [0, 1].
     * @param v Vertical coordinate [0, 1].
     * @return The final evaluated alpha transparency [0, 1].
     */
    PMSDK_API float Evaluate(float u, float v) const;

private:
    EdgeBlend m_left;
    EdgeBlend m_right;
    EdgeBlend m_top;
    EdgeBlend m_bottom;
    
    float m_blackLevel{0.0f};
};

} // namespace pmsdk::Blend
