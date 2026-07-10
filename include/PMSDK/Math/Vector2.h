#pragma once

#include <cmath>
#include <concepts>

namespace pmsdk::Math {

struct Vector2 {
    float x{0.0f};
    float y{0.0f};

    constexpr Vector2() = default;
    constexpr Vector2(float x_, float y_) : x(x_), y(y_) {}

    constexpr Vector2 operator+(const Vector2& rhs) const { return {x + rhs.x, y + rhs.y}; }
    constexpr Vector2 operator-(const Vector2& rhs) const { return {x - rhs.x, y - rhs.y}; }
    constexpr Vector2 operator*(const Vector2& rhs) const { return {x * rhs.x, y * rhs.y}; }
    constexpr Vector2 operator/(const Vector2& rhs) const { return {x / rhs.x, y / rhs.y}; }

    constexpr Vector2 operator+(float scalar) const { return {x + scalar, y + scalar}; }
    constexpr Vector2 operator-(float scalar) const { return {x - scalar, y - scalar}; }
    constexpr Vector2 operator*(float scalar) const { return {x * scalar, y * scalar}; }
    constexpr Vector2 operator/(float scalar) const { return {x / scalar, y / scalar}; }

    constexpr Vector2& operator+=(const Vector2& rhs) { x += rhs.x; y += rhs.y; return *this; }
    constexpr Vector2& operator-=(const Vector2& rhs) { x -= rhs.x; y -= rhs.y; return *this; }
    constexpr Vector2& operator*=(const Vector2& rhs) { x *= rhs.x; y *= rhs.y; return *this; }
    constexpr Vector2& operator/=(const Vector2& rhs) { x /= rhs.x; y /= rhs.y; return *this; }

    constexpr Vector2& operator+=(float scalar) { x += scalar; y += scalar; return *this; }
    constexpr Vector2& operator-=(float scalar) { x -= scalar; y -= scalar; return *this; }
    constexpr Vector2& operator*=(float scalar) { x *= scalar; y *= scalar; return *this; }
    constexpr Vector2& operator/=(float scalar) { x /= scalar; y /= scalar; return *this; }

    constexpr Vector2 operator-() const { return {-x, -y}; }

    constexpr bool operator==(const Vector2& rhs) const { return x == rhs.x && y == rhs.y; }
    constexpr bool operator!=(const Vector2& rhs) const { return !(*this == rhs); }

    constexpr float Dot(const Vector2& rhs) const { return x * rhs.x + y * rhs.y; }
    constexpr float LengthSquared() const { return Dot(*this); }
    inline float Length() const { return std::sqrt(LengthSquared()); }

    inline void Normalize() {
        float len = Length();
        if (len > 0.000001f) {
            float invLen = 1.0f / len;
            x *= invLen;
            y *= invLen;
        }
    }

    inline Vector2 Normalized() const {
        Vector2 res = *this;
        res.Normalize();
        return res;
    }
};

constexpr Vector2 operator*(float scalar, const Vector2& v) { return v * scalar; }

} // namespace pmsdk::Math
