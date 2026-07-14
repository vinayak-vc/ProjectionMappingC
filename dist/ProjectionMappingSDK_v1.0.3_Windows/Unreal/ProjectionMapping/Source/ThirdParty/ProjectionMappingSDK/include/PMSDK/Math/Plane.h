#pragma once

#include "PMSDK/Math/Vector3.h"

namespace pmsdk::Math {

struct Plane {
    Vector3 normal{0.0f, 1.0f, 0.0f};
    float distance{0.0f};

    constexpr Plane() = default;
    constexpr Plane(const Vector3& normal_, float distance_) 
        : normal(normal_), distance(distance_) {}
    
    constexpr Plane(const Vector3& normal_, const Vector3& point)
        : normal(normal_), distance(-normal_.Dot(point)) {}

    Plane(const Vector3& p0, const Vector3& p1, const Vector3& p2) {
        Vector3 v0 = p1 - p0;
        Vector3 v1 = p2 - p0;
        normal = v0.Cross(v1).Normalized();
        distance = -normal.Dot(p0);
    }

    constexpr float DistanceToPoint(const Vector3& point) const {
        return normal.Dot(point) + distance;
    }
};

} // namespace pmsdk::Math
