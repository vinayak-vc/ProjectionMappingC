#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"
#include "PMSDK/Math/Ray.h"
#include "PMSDK/Geometry/Intersection.h"
#include <memory>
#include <vector>

namespace pmsdk::Geometry {

class BVH {
public:
    PMSDK_API BVH();
    PMSDK_API ~BVH();
    BVH(const BVH&) = delete;
    BVH& operator=(const BVH&) = delete;

    PMSDK_API void Build(const Mesh& mesh);
    PMSDK_API bool Intersect(const Math::Ray& ray, RayTriangleIntersectionResult& outResult) const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Geometry
