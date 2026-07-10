#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Math/Matrix4.h"
#include "PMSDK/Math/Transform.h"

namespace pmsdk::Warp {

class Projector {
public:
    PMSDK_API Projector();
    PMSDK_API ~Projector();

    // Extrinsic properties
    PMSDK_API void SetTransform(const Math::Transform& transform);
    PMSDK_API Math::Transform GetTransform() const;

    // Intrinsic properties
    PMSDK_API void SetThrowRatio(float ratio);
    PMSDK_API float GetThrowRatio() const;

    PMSDK_API void SetAspectRatio(float aspect);
    PMSDK_API float GetAspectRatio() const;

    // Lens shift in normalized coordinates (usually -0.5 to 0.5)
    PMSDK_API void SetLensShift(float shiftX, float shiftY);
    PMSDK_API void GetLensShift(float& outShiftX, float& outShiftY) const;

    // Generates the View Matrix (Extrinsic)
    PMSDK_API Math::Matrix4 GetViewMatrix() const;

    // Generates the Projection Matrix (Intrinsic) based on a near and far plane
    PMSDK_API Math::Matrix4 GetProjectionMatrix(float nearPlane = 0.1f, float farPlane = 1000.0f) const;

    // Projects a 3D world point into normalized device coordinates (NDC: -1 to 1)
    PMSDK_API Math::Vector3 ProjectPoint(const Math::Vector3& worldPoint) const;

private:
    Math::Transform m_transform;
    float m_throwRatio{1.0f};
    float m_aspectRatio{16.0f / 9.0f};
    float m_lensShiftX{0.0f};
    float m_lensShiftY{0.0f};
};

} // namespace pmsdk::Warp
