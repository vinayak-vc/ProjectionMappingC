#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"

namespace pmsdk::Geometry {

class MeshOptimizer {
public:
    // Welds vertices that are within a certain distance threshold of each other.
    // This updates the mesh in-place by removing duplicate vertices and re-mapping indices.
    PMSDK_API static void WeldVertices(Mesh& mesh, float threshold = 0.0001f);

    // Recalculates normals using area-weighted cross products for smoother shading
    // compared to the basic Mesh::RecalculateNormals().
    PMSDK_API static void RecalculateSmoothNormals(Mesh& mesh);
};

} // namespace pmsdk::Geometry
