#pragma once

#include "PMSDK/Core/Export.h"
#include <cmath>

namespace pmsdk::Blend {

/**
 * @brief Interpolation curve types for edge blending.
 */
enum class CurveType {
    Linear,     /**< Standard linear interpolation. */
    Power,      /**< Exponential interpolation x^gamma. */
    Smoothstep  /**< Hermite interpolation 3x^2 - 2x^3. */
};

/**
 * @brief Represents the blending parameters for a single edge of a projector.
 */
class EdgeBlend {
public:
    /** @brief Constructs a new EdgeBlend with default settings. */
    PMSDK_API EdgeBlend();
    
    /** @brief Destructor. */
    PMSDK_API ~EdgeBlend();

    /**
     * @brief Sets the size of the blend zone.
     * @param size Normalized size [0, 1] representing percentage of screen width/height.
     */
    PMSDK_API void SetSize(float size);
    
    /** @return The size of the blend zone. */
    PMSDK_API float GetSize() const;

    /**
     * @brief Sets the gamma exponent used when CurveType is Power.
     * @param gamma The gamma value (e.g., 2.2).
     */
    PMSDK_API void SetGamma(float gamma);
    
    /** @return The gamma value. */
    PMSDK_API float GetGamma() const;

    /**
     * @brief Sets the interpolation curve type.
     * @param type The curve type.
     */
    PMSDK_API void SetCurveType(CurveType type);
    
    /** @return The curve type. */
    PMSDK_API CurveType GetCurveType() const;

    /**
     * @brief Evaluates the alpha multiplier (0.0 to 1.0) given a distance from the physical edge.
     * 
     * Distance is normalized: 0.0 is the very edge of the projector, 'size' is the end of the blend zone.
     * 
     * @param distance The normalized distance from the edge.
     * @return The evaluated alpha multiplier [0, 1].
     */
    PMSDK_API float Evaluate(float distance) const;

private:
    float m_size{0.0f};
    float m_gamma{2.2f};
    CurveType m_curveType{CurveType::Power};
};

} // namespace pmsdk::Blend
