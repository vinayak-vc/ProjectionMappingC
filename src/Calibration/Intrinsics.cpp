#include "PMSDK/Calibration/Intrinsics.h"

namespace pmsdk::Calibration {

Intrinsics::Intrinsics() = default;
Intrinsics::~Intrinsics() = default;

void Intrinsics::SetCameraMatrix(float fx, float fy, float cx, float cy) {
    m_fx = fx;
    m_fy = fy;
    m_cx = cx;
    m_cy = cy;
}

void Intrinsics::GetCameraMatrix(float& fx, float& fy, float& cx, float& cy) const {
    fx = m_fx;
    fy = m_fy;
    cx = m_cx;
    cy = m_cy;
}

void Intrinsics::SetDistortionCoefficients(const std::vector<float>& coeffs) {
    m_distCoeffs = coeffs;
}

std::vector<float> Intrinsics::GetDistortionCoefficients() const {
    return m_distCoeffs;
}

} // namespace pmsdk::Calibration
