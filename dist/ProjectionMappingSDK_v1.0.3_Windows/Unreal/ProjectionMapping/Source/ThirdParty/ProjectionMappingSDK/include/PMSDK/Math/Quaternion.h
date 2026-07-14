#pragma once

#include <cmath>
#include "PMSDK/Math/Vector3.h"

namespace pmsdk::Math {

struct Quaternion {
    float x{0.0f};
    float y{0.0f};
    float z{0.0f};
    float w{1.0f};

    constexpr Quaternion() = default;
    constexpr Quaternion(float x_, float y_, float z_, float w_) : x(x_), y(y_), z(z_), w(w_) {}

    constexpr Quaternion operator*(const Quaternion& rhs) const {
        return {
            w * rhs.x + x * rhs.w + y * rhs.z - z * rhs.y,
            w * rhs.y - x * rhs.z + y * rhs.w + z * rhs.x,
            w * rhs.z + x * rhs.y - y * rhs.x + z * rhs.w,
            w * rhs.w - x * rhs.x - y * rhs.y - z * rhs.z
        };
    }

    constexpr Vector3 operator*(const Vector3& v) const {
        Vector3 qVec(x, y, z);
        Vector3 uv = qVec.Cross(v);
        Vector3 uuv = qVec.Cross(uv);
        return v + (uv * (2.0f * w)) + (uuv * 2.0f);
    }

    constexpr Quaternion& operator*=(const Quaternion& rhs) {
        *this = *this * rhs;
        return *this;
    }

    constexpr bool operator==(const Quaternion& rhs) const { return x == rhs.x && y == rhs.y && z == rhs.z && w == rhs.w; }
    constexpr bool operator!=(const Quaternion& rhs) const { return !(*this == rhs); }

    constexpr float Dot(const Quaternion& rhs) const { return x * rhs.x + y * rhs.y + z * rhs.z + w * rhs.w; }
    constexpr float LengthSquared() const { return Dot(*this); }
    inline float Length() const { return std::sqrt(LengthSquared()); }

    inline void Normalize() {
        float len = Length();
        if (len > 0.000001f) {
            float invLen = 1.0f / len;
            x *= invLen;
            y *= invLen;
            z *= invLen;
            w *= invLen;
        }
    }

    inline Quaternion Normalized() const {
        Quaternion res = *this;
        res.Normalize();
        return res;
    }

    constexpr Quaternion Inverse() const {
        float ls = LengthSquared();
        if (ls > 0.000001f) {
            float invLs = 1.0f / ls;
            return {-x * invLs, -y * invLs, -z * invLs, w * invLs};
        }
        return {0.0f, 0.0f, 0.0f, 0.0f};
    }

    constexpr Quaternion Conjugate() const {
        return {-x, -y, -z, w};
    }

    static Quaternion Slerp(const Quaternion& q1, const Quaternion& q2, float t) {
        float cosTheta = q1.Dot(q2);
        Quaternion q2Adjusted = q2;

        if (cosTheta < 0.0f) {
            q2Adjusted = {-q2.x, -q2.y, -q2.z, -q2.w};
            cosTheta = -cosTheta;
        }

        if (cosTheta > 0.9995f) {
            Quaternion res = {
                q1.x + t * (q2Adjusted.x - q1.x),
                q1.y + t * (q2Adjusted.y - q1.y),
                q1.z + t * (q2Adjusted.z - q1.z),
                q1.w + t * (q2Adjusted.w - q1.w)
            };
            res.Normalize();
            return res;
        }

        float theta = std::acos(cosTheta);
        float sinTheta = std::sin(theta);
        
        float w1 = std::sin((1.0f - t) * theta) / sinTheta;
        float w2 = std::sin(t * theta) / sinTheta;

        return {
            q1.x * w1 + q2Adjusted.x * w2,
            q1.y * w1 + q2Adjusted.y * w2,
            q1.z * w1 + q2Adjusted.z * w2,
            q1.w * w1 + q2Adjusted.w * w2
        };
    }
};

} // namespace pmsdk::Math
