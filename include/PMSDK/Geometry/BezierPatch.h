#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"
#include "PMSDK/Math/Vector3.h"
#include <memory>
#include <vector>

namespace pmsdk::Geometry {

class BezierPatch {
public:
    PMSDK_API BezierPatch();
    PMSDK_API ~BezierPatch();
    BezierPatch(const BezierPatch&) = delete;
    BezierPatch& operator=(const BezierPatch&) = delete;

    // Set control points for a cubic bezier patch (must be 4x4 = 16 points)
    // Points are in row-major order: p[row][col] = points[row * 4 + col]
    PMSDK_API void SetControlPoints(const std::vector<Math::Vector3>& points);
    
    // Generates a mesh with resolution x resolution quads (converted to triangles)
    PMSDK_API std::unique_ptr<Mesh> GenerateMesh(int resolutionX, int resolutionY) const;

    // Evaluates the bezier surface at normalized coordinates (u, v) in [0, 1]
    PMSDK_API Math::Vector3 Evaluate(float u, float v) const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Geometry
