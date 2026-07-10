#pragma once
#include "PMSDK/Core/Export.h"
#include <vector>

namespace pmsdk::Calibration {

class Intrinsics {
public:
    PMSDK_API Intrinsics();
    PMSDK_API ~Intrinsics();

    PMSDK_API void SetCameraMatrix(float fx, float fy, float cx, float cy);
    PMSDK_API void GetCameraMatrix(float& fx, float& fy, float& cx, float& cy) const;

    PMSDK_API void SetDistortionCoefficients(const std::vector<float>& coeffs);
    PMSDK_API std::vector<float> GetDistortionCoefficients() const;

private:
    float m_fx{1.0f}, m_fy{1.0f}, m_cx{0.0f}, m_cy{0.0f};
    std::vector<float> m_distCoeffs; // k1, k2, p1, p2, [k3, k4, k5, k6]
};

} // namespace pmsdk::Calibration
