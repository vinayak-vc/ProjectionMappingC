#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"
#include <memory>

namespace pmsdk::Geometry {

class PMSDK_API MeshSubdivision {
public:
    // Subdivides each triangle into 4 smaller triangles
    static std::unique_ptr<Mesh> SubdivideLinear(const Mesh& mesh);
};

} // namespace pmsdk::Geometry
