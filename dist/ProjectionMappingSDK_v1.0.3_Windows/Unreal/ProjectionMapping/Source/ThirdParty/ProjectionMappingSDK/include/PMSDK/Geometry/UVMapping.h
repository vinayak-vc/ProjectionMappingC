#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"

namespace pmsdk::Geometry {

class PMSDK_API UVMapping {
public:
    static void ApplyPlanarMapping(Mesh& mesh);
    static void ApplyCylindricalMapping(Mesh& mesh);
    static void ApplySphericalMapping(Mesh& mesh);
};

} // namespace pmsdk::Geometry
