#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"
#include "PMSDK/Geometry/BezierPatch.h"
#include "PMSDK/Geometry/GridWarp.h"
#include <memory>
#include <variant>

namespace pmsdk::Warp {

enum class DeformationType {
    None,
    Bezier,
    Grid
};

class DeformationField {
public:
    PMSDK_API DeformationField();
    PMSDK_API ~DeformationField();

    PMSDK_API void SetType(DeformationType type);
    PMSDK_API DeformationType GetType() const;

    // Access the underlying control structures to modify control points
    PMSDK_API Geometry::BezierPatch* GetBezierPatch();
    PMSDK_API Geometry::GridWarp* GetGridWarp();

    // Applies the active deformation to a base mesh.
    // The base mesh's UV coordinates (or normalized X/Y) are used as parameters to evaluate the deformation surface.
    PMSDK_API std::unique_ptr<Geometry::Mesh> ApplyDeformation(const Geometry::Mesh& baseMesh) const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Warp
