#pragma once

#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Math/Quaternion.h"
#include "PMSDK/Math/Matrix4.h"

namespace pmsdk::Math {

struct Transform {
    Vector3 position{0.0f, 0.0f, 0.0f};
    Quaternion rotation{0.0f, 0.0f, 0.0f, 1.0f};
    Vector3 scale{1.0f, 1.0f, 1.0f};

    constexpr Transform() = default;
    constexpr Transform(const Vector3& pos, const Quaternion& rot, const Vector3& scl)
        : position(pos), rotation(rot), scale(scl) {}

    Matrix4 ToMatrix() const {
        return Matrix4::Translation(position) * Matrix4::Rotation(rotation) * Matrix4::Scale(scale);
    }
};

} // namespace pmsdk::Math
