#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"
#include "PMSDK/Math/Vector2.h"
#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Geometry/Intersection.h"

namespace pmsdk::Warp {

class Sampler {
public:
    // Calculates the UV coordinate of a world-space point on a given mesh face.
    // Useful for inverse-mapping: finding what pixel from the source media 
    // corresponds to a physical ray hit on the deformed surface.
    PMSDK_API static Math::Vector2 SampleUVAtPoint(const Geometry::Mesh& mesh, uint32_t faceIndex, const Math::Vector3& pointOnFace);
};

} // namespace pmsdk::Warp
