#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"
#include <memory>

namespace pmsdk::Geometry {

class PMSDK_API MeshBuilder {
public:
    static std::unique_ptr<Mesh> CreatePlane(float width, float height);
    static std::unique_ptr<Mesh> CreateGrid(float width, float height, int columns, int rows);
    static std::unique_ptr<Mesh> CreateCylinder(float radius, float height, int segments);
};

} // namespace pmsdk::Geometry
