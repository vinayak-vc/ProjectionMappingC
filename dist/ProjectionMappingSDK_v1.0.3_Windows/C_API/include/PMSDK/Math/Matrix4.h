#pragma once

#include <cmath>
#include <algorithm>
#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Math/Vector4.h"
#include "PMSDK/Math/Quaternion.h"

namespace pmsdk::Math {

struct Matrix4 {
    // Column-major layout
    float m[16]{
        1.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 1.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 1.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f
    };

    constexpr Matrix4() = default;

    constexpr Matrix4(
        float m00, float m01, float m02, float m03,
        float m10, float m11, float m12, float m13,
        float m20, float m21, float m22, float m23,
        float m30, float m31, float m32, float m33) 
        : m{
            m00, m10, m20, m30, // Col 0
            m01, m11, m21, m31, // Col 1
            m02, m12, m22, m32, // Col 2
            m03, m13, m23, m33  // Col 3
        } {}

    constexpr float& operator[](int index) { return m[index]; }
    constexpr const float& operator[](int index) const { return m[index]; }

    constexpr Matrix4 operator*(const Matrix4& rhs) const {
        Matrix4 res;
        for (int c = 0; c < 4; ++c) {
            for (int r = 0; r < 4; ++r) {
                res.m[c * 4 + r] = 
                    m[0 * 4 + r] * rhs.m[c * 4 + 0] +
                    m[1 * 4 + r] * rhs.m[c * 4 + 1] +
                    m[2 * 4 + r] * rhs.m[c * 4 + 2] +
                    m[3 * 4 + r] * rhs.m[c * 4 + 3];
            }
        }
        return res;
    }

    constexpr Vector4 operator*(const Vector4& v) const {
        return {
            m[0] * v.x + m[4] * v.y + m[8]  * v.z + m[12] * v.w,
            m[1] * v.x + m[5] * v.y + m[9]  * v.z + m[13] * v.w,
            m[2] * v.x + m[6] * v.y + m[10] * v.z + m[14] * v.w,
            m[3] * v.x + m[7] * v.y + m[11] * v.z + m[15] * v.w
        };
    }

    constexpr Vector3 MultiplyPoint(const Vector3& p) const {
        float x = m[0] * p.x + m[4] * p.y + m[8]  * p.z + m[12];
        float y = m[1] * p.x + m[5] * p.y + m[9]  * p.z + m[13];
        float z = m[2] * p.x + m[6] * p.y + m[10] * p.z + m[14];
        float w = m[3] * p.x + m[7] * p.y + m[11] * p.z + m[15];
        if (w != 0.0f && w != 1.0f) {
            return {x / w, y / w, z / w};
        }
        return {x, y, z};
    }
    
    constexpr Vector3 MultiplyDirection(const Vector3& d) const {
        return {
            m[0] * d.x + m[4] * d.y + m[8]  * d.z,
            m[1] * d.x + m[5] * d.y + m[9]  * d.z,
            m[2] * d.x + m[6] * d.y + m[10] * d.z
        };
    }

    constexpr Matrix4 Transpose() const {
        return Matrix4(
            m[0], m[1], m[2], m[3],
            m[4], m[5], m[6], m[7],
            m[8], m[9], m[10], m[11],
            m[12], m[13], m[14], m[15]
        );
    }

    // A standard 4x4 matrix inverse
    constexpr Matrix4 Inverse() const {
        float inv[16]{};

        inv[0] = m[5]  * m[10] * m[15] - m[5]  * m[11] * m[14] - m[9]  * m[6]  * m[15] + 
                 m[9]  * m[7]  * m[14] + m[13] * m[6]  * m[11] - m[13] * m[7]  * m[10];
        inv[4] = -m[4]  * m[10] * m[15] + m[4]  * m[11] * m[14] + m[8]  * m[6]  * m[15] - 
                 m[8]  * m[7]  * m[14] - m[12] * m[6]  * m[11] + m[12] * m[7]  * m[10];
        inv[8] = m[4]  * m[9] * m[15] - m[4]  * m[11] * m[13] - m[8]  * m[5] * m[15] + 
                 m[8]  * m[7] * m[13] + m[12] * m[5] * m[11] - m[12] * m[7] * m[9];
        inv[12] = -m[4]  * m[9] * m[14] + m[4]  * m[10] * m[13] + m[8]  * m[5] * m[14] - 
                  m[8]  * m[6] * m[13] - m[12] * m[5] * m[10] + m[12] * m[6] * m[9];
        inv[1] = -m[1]  * m[10] * m[15] + m[1]  * m[11] * m[14] + m[9]  * m[2] * m[15] - 
                 m[9]  * m[3] * m[14] - m[13] * m[2] * m[11] + m[13] * m[3] * m[10];
        inv[5] = m[0]  * m[10] * m[15] - m[0]  * m[11] * m[14] - m[8]  * m[2] * m[15] + 
                 m[8]  * m[3] * m[14] + m[12] * m[2] * m[11] - m[12] * m[3] * m[10];
        inv[9] = -m[0]  * m[9] * m[15] + m[0]  * m[11] * m[13] + m[8]  * m[1] * m[15] - 
                 m[8]  * m[3] * m[13] - m[12] * m[1] * m[11] + m[12] * m[3] * m[9];
        inv[13] = m[0]  * m[9] * m[14] - m[0]  * m[10] * m[13] - m[8]  * m[1] * m[14] + 
                  m[8]  * m[2] * m[13] + m[12] * m[1] * m[10] - m[12] * m[2] * m[9];
        inv[2] = m[1]  * m[6] * m[15] - m[1]  * m[7] * m[14] - m[5]  * m[2] * m[15] + 
                 m[5]  * m[3] * m[14] + m[13] * m[2] * m[7] - m[13] * m[3] * m[6];
        inv[6] = -m[0]  * m[6] * m[15] + m[0]  * m[7] * m[14] + m[4]  * m[2] * m[15] - 
                 m[4]  * m[3] * m[14] - m[12] * m[2] * m[7] + m[12] * m[3] * m[6];
        inv[10] = m[0]  * m[5] * m[15] - m[0]  * m[7] * m[13] - m[4]  * m[1] * m[15] + 
                  m[4]  * m[3] * m[13] + m[12] * m[1] * m[7] - m[12] * m[3] * m[5];
        inv[14] = -m[0]  * m[5] * m[14] + m[0]  * m[6] * m[13] + m[4]  * m[1] * m[14] - 
                  m[4]  * m[2] * m[13] - m[12] * m[1] * m[6] + m[12] * m[2] * m[5];
        inv[3] = -m[1] * m[6] * m[11] + m[1] * m[7] * m[10] + m[5] * m[2] * m[11] - 
                 m[5] * m[3] * m[10] - m[9] * m[2] * m[7] + m[9] * m[3] * m[6];
        inv[7] = m[0] * m[6] * m[11] - m[0] * m[7] * m[10] - m[4] * m[2] * m[11] + 
                 m[4] * m[3] * m[10] + m[8] * m[2] * m[7] - m[8] * m[3] * m[6];
        inv[11] = -m[0] * m[5] * m[11] + m[0] * m[7] * m[9] + m[4] * m[1] * m[11] - 
                  m[4] * m[3] * m[9] - m[8] * m[1] * m[7] + m[8] * m[3] * m[5];
        inv[15] = m[0] * m[5] * m[10] - m[0] * m[6] * m[9] - m[4] * m[1] * m[10] + 
                  m[4] * m[2] * m[9] + m[8] * m[1] * m[6] - m[8] * m[2] * m[5];

        float det = m[0] * inv[0] + m[1] * inv[4] + m[2] * inv[8] + m[3] * inv[12];
        
        if (det == 0.0f) {
            return Matrix4(); // Degenerate: return identity or zeros. Returning identity for now.
        }
        
        float invDet = 1.0f / det;
        Matrix4 res;
        for (int i = 0; i < 16; ++i) {
            res.m[i] = inv[i] * invDet;
        }
        return res;
    }

    constexpr bool operator==(const Matrix4& rhs) const {
        for (int i = 0; i < 16; ++i) {
            if (m[i] != rhs.m[i]) return false;
        }
        return true;
    }

    constexpr bool operator!=(const Matrix4& rhs) const { return !(*this == rhs); }

    static constexpr Matrix4 Translation(const Vector3& t) {
        Matrix4 res;
        res.m[12] = t.x;
        res.m[13] = t.y;
        res.m[14] = t.z;
        return res;
    }

    static constexpr Matrix4 Scale(const Vector3& s) {
        Matrix4 res;
        res.m[0] = s.x;
        res.m[5] = s.y;
        res.m[10] = s.z;
        return res;
    }

    static Matrix4 Rotation(const Quaternion& q) {
        Matrix4 res;
        float xx = q.x * q.x;
        float yy = q.y * q.y;
        float zz = q.z * q.z;
        float xy = q.x * q.y;
        float xz = q.x * q.z;
        float yz = q.y * q.z;
        float wx = q.w * q.x;
        float wy = q.w * q.y;
        float wz = q.w * q.z;

        res.m[0] = 1.0f - 2.0f * (yy + zz);
        res.m[1] = 2.0f * (xy + wz);
        res.m[2] = 2.0f * (xz - wy);

        res.m[4] = 2.0f * (xy - wz);
        res.m[5] = 1.0f - 2.0f * (xx + zz);
        res.m[6] = 2.0f * (yz + wx);

        res.m[8] = 2.0f * (xz + wy);
        res.m[9] = 2.0f * (yz - wx);
        res.m[10] = 1.0f - 2.0f * (xx + yy);

        return res;
    }

    static constexpr Matrix4 Orthographic(float left, float right, float bottom, float top, float zNear, float zFar) {
        Matrix4 res;
        res.m[0] = 2.0f / (right - left);
        res.m[5] = 2.0f / (top - bottom);
        res.m[10] = -2.0f / (zFar - zNear);
        res.m[12] = -(right + left) / (right - left);
        res.m[13] = -(top + bottom) / (top - bottom);
        res.m[14] = -(zFar + zNear) / (zFar - zNear);
        return res;
    }

    static inline Matrix4 Perspective(float fovY, float aspect, float zNear, float zFar) {
        float f = 1.0f / std::tan(fovY * 0.5f);
        Matrix4 res;
        res.m[0] = f / aspect;
        res.m[5] = f;
        res.m[10] = (zFar + zNear) / (zNear - zFar);
        res.m[11] = -1.0f;
        res.m[14] = (2.0f * zFar * zNear) / (zNear - zFar);
        res.m[15] = 0.0f;
        return res;
    }
};

} // namespace pmsdk::Math
