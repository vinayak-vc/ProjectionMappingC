#pragma once

#include "PMSDK/Math/Vector3.h"
#include <algorithm>

namespace pmsdk::Math {

struct BoundingBox {
    Vector3 min{1e30f, 1e30f, 1e30f};
    Vector3 max{-1e30f, -1e30f, -1e30f};

    constexpr BoundingBox() = default;
    constexpr BoundingBox(const Vector3& min_, const Vector3& max_) : min(min_), max(max_) {}

    constexpr void Expand(const Vector3& point) {
        min.x = std::min(min.x, point.x);
        min.y = std::min(min.y, point.y);
        min.z = std::min(min.z, point.z);
        
        max.x = std::max(max.x, point.x);
        max.y = std::max(max.y, point.y);
        max.z = std::max(max.z, point.z);
    }

    constexpr void Expand(const BoundingBox& box) {
        Expand(box.min);
        Expand(box.max);
    }

    constexpr bool Contains(const Vector3& point) const {
        return (point.x >= min.x && point.x <= max.x) &&
               (point.y >= min.y && point.y <= max.y) &&
               (point.z >= min.z && point.z <= max.z);
    }

    constexpr bool Intersects(const BoundingBox& box) const {
        if (max.x < box.min.x || min.x > box.max.x) return false;
        if (max.y < box.min.y || min.y > box.max.y) return false;
        if (max.z < box.min.z || min.z > box.max.z) return false;
        return true;
    }

    constexpr Vector3 Center() const {
        return (min + max) * 0.5f;
    }

    constexpr Vector3 Extents() const {
        return (max - min) * 0.5f;
    }
    
    constexpr bool IsValid() const {
        return min.x <= max.x && min.y <= max.y && min.z <= max.z;
    }
};

} // namespace pmsdk::Math
