#pragma once
#include "PMSDK/Core/Export.h"
#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Math/Matrix4.h"

namespace pmsdk::Calibration {

class Extrinsics {
public:
    PMSDK_API Extrinsics();
    PMSDK_API ~Extrinsics();

    // Rotation vector (Rodrigues) as produced by OpenCV
    PMSDK_API void SetRotationVector(const Math::Vector3& rvec);
    PMSDK_API Math::Vector3 GetRotationVector() const;

    // Translation vector
    PMSDK_API void SetTranslationVector(const Math::Vector3& tvec);
    PMSDK_API Math::Vector3 GetTranslationVector() const;

    // Convert to a standard 4x4 view matrix
    PMSDK_API Math::Matrix4 ToMatrix() const;

private:
    Math::Vector3 m_rvec{0.0f, 0.0f, 0.0f};
    Math::Vector3 m_tvec{0.0f, 0.0f, 0.0f};
};

} // namespace pmsdk::Calibration
