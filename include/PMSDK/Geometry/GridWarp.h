#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"
#include "PMSDK/Math/Vector3.h"
#include <memory>
#include <vector>

namespace pmsdk::Geometry {

class GridWarp {
public:
    PMSDK_API GridWarp();
    PMSDK_API ~GridWarp();
    GridWarp(const GridWarp&) = delete;
    GridWarp& operator=(const GridWarp&) = delete;

    // Set control points for a columns x rows grid
    PMSDK_API void SetControlPoints(int columns, int rows, const std::vector<Math::Vector3>& points);
    
    // Evaluates the grid via bilinear interpolation
    PMSDK_API std::unique_ptr<Mesh> GenerateMesh(int resolutionX, int resolutionY) const;

    // Evaluates the grid surface at normalized coordinates (u, v) in [0, 1]
    PMSDK_API Math::Vector3 Evaluate(float u, float v) const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Geometry
