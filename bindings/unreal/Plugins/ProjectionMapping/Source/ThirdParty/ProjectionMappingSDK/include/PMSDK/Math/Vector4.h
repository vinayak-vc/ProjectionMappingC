#pragma once

#include <cmath>

namespace pmsdk::Math {

struct Vector4 {
    float x{0.0f};
    float y{0.0f};
    float z{0.0f};
    float w{0.0f};

    constexpr Vector4() = default;
    constexpr Vector4(float x_, float y_, float z_, float w_) : x(x_), y(y_), z(z_), w(w_) {}

    constexpr Vector4 operator+(const Vector4& rhs) const { return {x + rhs.x, y + rhs.y, z + rhs.z, w + rhs.w}; }
    constexpr Vector4 operator-(const Vector4& rhs) const { return {x - rhs.x, y - rhs.y, z - rhs.z, w - rhs.w}; }
    constexpr Vector4 operator*(const Vector4& rhs) const { return {x * rhs.x, y * rhs.y, z * rhs.z, w * rhs.w}; }
    constexpr Vector4 operator/(const Vector4& rhs) const { return {x / rhs.x, y / rhs.y, z / rhs.z, w / rhs.w}; }

    constexpr Vector4 operator+(float scalar) const { return {x + scalar, y + scalar, z + scalar, w + scalar}; }
    constexpr Vector4 operator-(float scalar) const { return {x - scalar, y - scalar, z - scalar, w - scalar}; }
    constexpr Vector4 operator*(float scalar) const { return {x * scalar, y * scalar, z * scalar, w * scalar}; }
    constexpr Vector4 operator/(float scalar) const { return {x / scalar, y / scalar, z / scalar, w / scalar}; }

    constexpr Vector4& operator+=(const Vector4& rhs) { x += rhs.x; y += rhs.y; z += rhs.z; w += rhs.w; return *this; }
    constexpr Vector4& operator-=(const Vector4& rhs) { x -= rhs.x; y -= rhs.y; z -= rhs.z; w -= rhs.w; return *this; }
    constexpr Vector4& operator*=(const Vector4& rhs) { x *= rhs.x; y *= rhs.y; z *= rhs.z; w *= rhs.w; return *this; }
    constexpr Vector4& operator/=(const Vector4& rhs) { x /= rhs.x; y /= rhs.y; z /= rhs.z; w /= rhs.w; return *this; }

    constexpr Vector4& operator+=(float scalar) { x += scalar; y += scalar; z += scalar; w += scalar; return *this; }
    constexpr Vector4& operator-=(float scalar) { x -= scalar; y -= scalar; z -= scalar; w -= scalar; return *this; }
    constexpr Vector4& operator*=(float scalar) { x *= scalar; y *= scalar; z *= scalar; w *= scalar; return *this; }
    constexpr Vector4& operator/=(float scalar) { x /= scalar; y /= scalar; z /= scalar; w /= scalar; return *this; }

    constexpr Vector4 operator-() const { return {-x, -y, -z, -w}; }

    constexpr bool operator==(const Vector4& rhs) const { return x == rhs.x && y == rhs.y && z == rhs.z && w == rhs.w; }
    constexpr bool operator!=(const Vector4& rhs) const { return !(*this == rhs); }

    constexpr float Dot(const Vector4& rhs) const { return x * rhs.x + y * rhs.y + z * rhs.z + w * rhs.w; }
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

    inline Vector4 Normalized() const {
        Vector4 res = *this;
        res.Normalize();
        return res;
    }
};

constexpr Vector4 operator*(float scalar, const Vector4& v) { return v * scalar; }

} // namespace pmsdk::Math
