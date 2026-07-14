#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Math/Vector3.h"
#include <memory>
#include <vector>
#include <cstdint>

namespace pmsdk::Geometry {

class KDTree {
public:
    PMSDK_API KDTree();
    PMSDK_API ~KDTree();
    KDTree(const KDTree&) = delete;
    KDTree& operator=(const KDTree&) = delete;

    PMSDK_API void Build(const std::vector<Math::Vector3>& points);
    
    // Returns the index of the nearest point, or -1 if empty
    PMSDK_API int FindNearest(const Math::Vector3& target, float& outDistanceSquared) const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Geometry
