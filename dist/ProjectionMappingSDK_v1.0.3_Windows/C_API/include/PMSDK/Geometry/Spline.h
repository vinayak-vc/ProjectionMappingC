#pragma once

#include "PMSDK/Math/Vector3.h"

namespace pmsdk::Geometry {

struct Spline {
    // Basic Catmull-Rom spline evaluation
    static constexpr Math::Vector3 EvaluateCatmullRom(
        const Math::Vector3& p0,
        const Math::Vector3& p1,
        const Math::Vector3& p2,
        const Math::Vector3& p3,
        float t) 
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return (
            p1 * 2.0f +
            (p2 - p0) * t +
            (p0 * 2.0f - p1 * 5.0f + p2 * 4.0f - p3) * t2 +
            (p1 * 3.0f - p0 - p2 * 3.0f + p3) * t3
        ) * 0.5f;
    }
};

} // namespace pmsdk::Geometry
