#include "PMSDK/Warp/Projector.h"
#include <cmath>

namespace pmsdk::Warp {

Projector::Projector() = default;
Projector::~Projector() = default;

void Projector::SetTransform(const Math::Transform& transform) {
    m_transform = transform;
}

Math::Transform Projector::GetTransform() const {
    return m_transform;
}

void Projector::SetThrowRatio(float ratio) {
    if (ratio > 0.0f) m_throwRatio = ratio;
}

float Projector::GetThrowRatio() const {
    return m_throwRatio;
}

void Projector::SetAspectRatio(float aspect) {
    if (aspect > 0.0f) m_aspectRatio = aspect;
}

float Projector::GetAspectRatio() const {
    return m_aspectRatio;
}

void Projector::SetLensShift(float shiftX, float shiftY) {
    m_lensShiftX = shiftX;
    m_lensShiftY = shiftY;
}

void Projector::GetLensShift(float& outShiftX, float& outShiftY) const {
    outShiftX = m_lensShiftX;
    outShiftY = m_lensShiftY;
}

Math::Matrix4 Projector::GetViewMatrix() const {
    // View matrix is the inverse of the projector's world transform
    return m_transform.ToMatrix().Inverse();
}

Math::Matrix4 Projector::GetProjectionMatrix(float nearPlane, float farPlane) const {
    // Throw ratio = Distance / Width
    // FOV can be derived from throw ratio. 
    // width_at_distance = distance / throwRatio
    // half_width = width_at_distance / 2
    // tan(fov/2) = half_width / distance = (distance / throwRatio / 2) / distance = 1 / (2 * throwRatio)
    // fov = 2 * atan(1 / (2 * throwRatio))
    
    // For off-axis projection (lens shift), we construct an asymmetric frustum.
    // width at near plane = nearPlane / m_throwRatio
    // height at near plane = width / m_aspectRatio
    
    float width = nearPlane / m_throwRatio;
    float height = width / m_aspectRatio;

    // Base symmetric frustum bounds at near plane
    float left = -width / 2.0f;
    float right = width / 2.0f;
    float bottom = -height / 2.0f;
    float top = height / 2.0f;

    // Apply lens shift. Shift of 1.0 means shifting by a full width/height.
    float shiftX = m_lensShiftX * width;
    float shiftY = m_lensShiftY * height;

    left += shiftX;
    right += shiftX;
    bottom += shiftY;
    top += shiftY;

    // Construct asymmetric perspective projection matrix (OpenGL style, column-major)
    // We already have Math::Matrix4::Perspective in Math, but it's symmetric.
    // Let's implement an asymmetric frustum here directly.
    Math::Matrix4 m; // initializes to identity
    m.m[0] = (2.0f * nearPlane) / (right - left);
    m.m[1] = 0.0f;
    m.m[2] = 0.0f;
    m.m[3] = 0.0f;

    m.m[4] = 0.0f;
    m.m[5] = (2.0f * nearPlane) / (top - bottom);
    m.m[6] = 0.0f;
    m.m[7] = 0.0f;

    m.m[8] = (right + left) / (right - left);
    m.m[9] = (top + bottom) / (top - bottom);
    m.m[10] = -(farPlane + nearPlane) / (farPlane - nearPlane);
    m.m[11] = -1.0f;

    m.m[12] = 0.0f;
    m.m[13] = 0.0f;
    m.m[14] = -(2.0f * farPlane * nearPlane) / (farPlane - nearPlane);
    m.m[15] = 0.0f;

    return m;
}

Math::Vector3 Projector::ProjectPoint(const Math::Vector3& worldPoint) const {
    Math::Matrix4 view = GetViewMatrix();
    Math::Matrix4 proj = GetProjectionMatrix();

    Math::Matrix4 viewProj = proj * view;
    
    // Manual multiplication by viewProj matrix, including homogeneous divide
    float x = worldPoint.x * viewProj.m[0] + worldPoint.y * viewProj.m[4] + worldPoint.z * viewProj.m[8] + viewProj.m[12];
    float y = worldPoint.x * viewProj.m[1] + worldPoint.y * viewProj.m[5] + worldPoint.z * viewProj.m[9] + viewProj.m[13];
    float z = worldPoint.x * viewProj.m[2] + worldPoint.y * viewProj.m[6] + worldPoint.z * viewProj.m[10] + viewProj.m[14];
    float w = worldPoint.x * viewProj.m[3] + worldPoint.y * viewProj.m[7] + worldPoint.z * viewProj.m[11] + viewProj.m[15];

    if (w != 0.0f) {
        x /= w;
        y /= w;
        z /= w;
    }

    return {x, y, z};
}

} // namespace pmsdk::Warp
