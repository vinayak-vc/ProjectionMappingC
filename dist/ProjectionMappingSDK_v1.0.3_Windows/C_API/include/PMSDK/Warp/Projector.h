#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Math/Matrix4.h"
#include "PMSDK/Math/Transform.h"

namespace pmsdk::Warp {

/**
 * @brief Represents a physical or virtual projector in the scene.
 * 
 * Handles both extrinsic properties (position, rotation) and intrinsic 
 * properties (throw ratio, aspect ratio, lens shift).
 */
class Projector {
public:
    /** @brief Constructs a projector with default properties. */
    PMSDK_API Projector();
    
    /** @brief Destructor. */
    PMSDK_API ~Projector();

    /**
     * @brief Sets the extrinsic transform of the projector.
     * @param transform The new transform.
     */
    PMSDK_API void SetTransform(const Math::Transform& transform);
    
    /** @return The extrinsic transform of the projector. */
    PMSDK_API Math::Transform GetTransform() const;

    /**
     * @brief Sets the throw ratio (distance / width).
     * @param ratio The throw ratio.
     */
    PMSDK_API void SetThrowRatio(float ratio);
    
    /** @return The throw ratio. */
    PMSDK_API float GetThrowRatio() const;

    /**
     * @brief Sets the aspect ratio (width / height).
     * @param aspect The aspect ratio.
     */
    PMSDK_API void SetAspectRatio(float aspect);
    
    /** @return The aspect ratio. */
    PMSDK_API float GetAspectRatio() const;

    /**
     * @brief Sets the optical lens shift.
     * @param shiftX Horizontal shift in normalized coordinates [-0.5, 0.5].
     * @param shiftY Vertical shift in normalized coordinates [-0.5, 0.5].
     */
    PMSDK_API void SetLensShift(float shiftX, float shiftY);
    
    /**
     * @brief Retrieves the current lens shift.
     * @param outShiftX Populated with the horizontal shift.
     * @param outShiftY Populated with the vertical shift.
     */
    PMSDK_API void GetLensShift(float& outShiftX, float& outShiftY) const;

    /**
     * @brief Generates the View Matrix based on the extrinsic transform.
     * @return A 4x4 View Matrix.
     */
    PMSDK_API Math::Matrix4 GetViewMatrix() const;

    /**
     * @brief Generates the Projection Matrix based on intrinsic properties.
     * @param nearPlane The near clipping plane distance.
     * @param farPlane The far clipping plane distance.
     * @return A 4x4 Projection Matrix.
     */
    PMSDK_API Math::Matrix4 GetProjectionMatrix(float nearPlane = 0.1f, float farPlane = 1000.0f) const;

    /**
     * @brief Projects a 3D world space point into Normalized Device Coordinates (NDC).
     * @param worldPoint The point in world space.
     * @return The projected point in NDC space [-1, 1].
     */
    PMSDK_API Math::Vector3 ProjectPoint(const Math::Vector3& worldPoint) const;

private:
    Math::Transform m_transform;
    float m_throwRatio{1.0f};
    float m_aspectRatio{16.0f / 9.0f};
    float m_lensShiftX{0.0f};
    float m_lensShiftY{0.0f};
};

} // namespace pmsdk::Warp
