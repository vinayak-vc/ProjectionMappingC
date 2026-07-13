#pragma once

#include "PMSDK/Math/Vector3.h"

namespace pmsdk::Math {

struct Ray {
    Vector3 origin{0.0f, 0.0f, 0.0f};
    Vector3 direction{0.0f, 0.0f, 1.0f};

    constexpr Ray() = default;
    constexpr Ray(const Vector3& origin_, const Vector3& direction_) 
        : origin(origin_), direction(direction_) {}

    constexpr Vector3 PointAt(float t) const {
        return origin + direction * t;
    }
};

} // namespace pmsdk::Math
