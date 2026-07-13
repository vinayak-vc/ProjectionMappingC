#pragma once

#include "PMSDK/Math/Vector3.h"

namespace pmsdk::Geometry {

struct BezierCurve {
    Math::Vector3 p0{0.0f, 0.0f, 0.0f};
    Math::Vector3 p1{0.0f, 0.0f, 0.0f};
    Math::Vector3 p2{0.0f, 0.0f, 0.0f};
    Math::Vector3 p3{0.0f, 0.0f, 0.0f};

    constexpr Math::Vector3 Evaluate(float t) const {
        float u = 1.0f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Math::Vector3 p = p0 * uuu;
        p += p1 * (3.0f * uu * t);
        p += p2 * (3.0f * u * tt);
        p += p3 * ttt;
        return p;
    }
};

} // namespace pmsdk::Geometry
