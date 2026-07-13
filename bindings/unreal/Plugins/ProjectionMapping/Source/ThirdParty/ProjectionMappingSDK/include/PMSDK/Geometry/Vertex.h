#pragma once

#include "PMSDK/Math/Vector2.h"
#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Math/Vector4.h"

namespace pmsdk::Geometry {

struct Vertex {
    Math::Vector3 position{0.0f, 0.0f, 0.0f};
    Math::Vector3 normal{0.0f, 1.0f, 0.0f};
    Math::Vector2 uv{0.0f, 0.0f};
    Math::Vector4 color{1.0f, 1.0f, 1.0f, 1.0f};

    constexpr bool operator==(const Vertex& rhs) const {
        return position == rhs.position &&
               normal == rhs.normal &&
               uv == rhs.uv &&
               color == rhs.color;
    }

    constexpr bool operator!=(const Vertex& rhs) const {
        return !(*this == rhs);
    }
};

} // namespace pmsdk::Geometry
