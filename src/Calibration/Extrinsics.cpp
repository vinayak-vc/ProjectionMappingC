#include "PMSDK/Calibration/Extrinsics.h"
#include <cmath>

namespace pmsdk::Calibration {

Extrinsics::Extrinsics() = default;
Extrinsics::~Extrinsics() = default;

void Extrinsics::SetRotationVector(const Math::Vector3& rvec) {
    m_rvec = rvec;
}

Math::Vector3 Extrinsics::GetRotationVector() const {
    return m_rvec;
}

void Extrinsics::SetTranslationVector(const Math::Vector3& tvec) {
    m_tvec = tvec;
}

Math::Vector3 Extrinsics::GetTranslationVector() const {
    return m_tvec;
}

Math::Matrix4 Extrinsics::ToMatrix() const {
    // Convert Rodrigues vector to 3x3 rotation matrix.
    // If we have OpenCV available here, we could use cv::Rodrigues.
    // But since this is simple math, we can do it directly.
    float theta = std::sqrt(m_rvec.x * m_rvec.x + m_rvec.y * m_rvec.y + m_rvec.z * m_rvec.z);
    
    Math::Matrix4 result;
    if (theta < 1e-6f) {
        // No rotation
        result.m[12] = m_tvec.x;
        result.m[13] = m_tvec.y;
        result.m[14] = m_tvec.z;
    } else {
        float cx = m_rvec.x / theta;
        float cy = m_rvec.y / theta;
        float cz = m_rvec.z / theta;
        
        float c = std::cos(theta);
        float s = std::sin(theta);
        float t = 1.0f - c;
        
        float m00 = t * cx * cx + c;
        float m01 = t * cx * cy - s * cz;
        float m02 = t * cx * cz + s * cy;
        
        float m10 = t * cx * cy + s * cz;
        float m11 = t * cy * cy + c;
        float m12 = t * cy * cz - s * cx;
        
        float m20 = t * cx * cz - s * cy;
        float m21 = t * cy * cz + s * cx;
        float m22 = t * cz * cz + c;
        
        result = Math::Matrix4(
            m00, m01, m02, m_tvec.x,
            m10, m11, m12, m_tvec.y,
            m20, m21, m22, m_tvec.z,
            0.0f, 0.0f, 0.0f, 1.0f
        );
    }
    
    return result;
}

} // namespace pmsdk::Calibration
