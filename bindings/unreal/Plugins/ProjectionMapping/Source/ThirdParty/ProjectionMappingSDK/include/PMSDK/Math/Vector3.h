#pragma once

#include <cmath>

namespace pmsdk::Math {

struct Vector3 {
    float x{0.0f};
    float y{0.0f};
    float z{0.0f};

    constexpr Vector3() = default;
    constexpr Vector3(float x_, float y_, float z_) : x(x_), y(y_), z(z_) {}

    constexpr Vector3 operator+(const Vector3& rhs) const { return {x + rhs.x, y + rhs.y, z + rhs.z}; }
    constexpr Vector3 operator-(const Vector3& rhs) const { return {x - rhs.x, y - rhs.y, z - rhs.z}; }
    constexpr Vector3 operator*(const Vector3& rhs) const { return {x * rhs.x, y * rhs.y, z * rhs.z}; }
    constexpr Vector3 operator/(const Vector3& rhs) const { return {x / rhs.x, y / rhs.y, z / rhs.z}; }

    constexpr Vector3 operator+(float scalar) const { return {x + scalar, y + scalar, z + scalar}; }
    constexpr Vector3 operator-(float scalar) const { return {x - scalar, y - scalar, z - scalar}; }
    constexpr Vector3 operator*(float scalar) const { return {x * scalar, y * scalar, z * scalar}; }
    constexpr Vector3 operator/(float scalar) const { return {x / scalar, y / scalar, z / scalar}; }

    constexpr Vector3& operator+=(const Vector3& rhs) { x += rhs.x; y += rhs.y; z += rhs.z; return *this; }
    constexpr Vector3& operator-=(const Vector3& rhs) { x -= rhs.x; y -= rhs.y; z -= rhs.z; return *this; }
    constexpr Vector3& operator*=(const Vector3& rhs) { x *= rhs.x; y *= rhs.y; z *= rhs.z; return *this; }
    constexpr Vector3& operator/=(const Vector3& rhs) { x /= rhs.x; y /= rhs.y; z /= rhs.z; return *this; }

    constexpr Vector3& operator+=(float scalar) { x += scalar; y += scalar; z += scalar; return *this; }
    constexpr Vector3& operator-=(float scalar) { x -= scalar; y -= scalar; z -= scalar; return *this; }
    constexpr Vector3& operator*=(float scalar) { x *= scalar; y *= scalar; z *= scalar; return *this; }
    constexpr Vector3& operator/=(float scalar) { x /= scalar; y /= scalar; z /= scalar; return *this; }

    constexpr Vector3 operator-() const { return {-x, -y, -z}; }

    constexpr bool operator==(const Vector3& rhs) const { return x == rhs.x && y == rhs.y && z == rhs.z; }
    constexpr bool operator!=(const Vector3& rhs) const { return !(*this == rhs); }

    constexpr float Dot(const Vector3& rhs) const { return x * rhs.x + y * rhs.y + z * rhs.z; }
    constexpr Vector3 Cross(const Vector3& rhs) const {
        return {
            y * rhs.z - z * rhs.y,
            z * rhs.x - x * rhs.z,
            x * rhs.y - y * rhs.x
        };
    }
    
    constexpr float LengthSquared() const { return Dot(*this); }
    inline float Length() const { return std::sqrt(LengthSquared()); }

    inline void Normalize() {
        float len = Length();
        if (len > 0.000001f) {
            float invLen = 1.0f / len;
            x *= invLen;
            y *= invLen;
            z *= invLen;
        }
    }

    inline Vector3 Normalized() const {
        Vector3 res = *this;
        res.Normalize();
        return res;
    }
};

constexpr Vector3 operator*(float scalar, const Vector3& v) { return v * scalar; }

} // namespace pmsdk::Math
