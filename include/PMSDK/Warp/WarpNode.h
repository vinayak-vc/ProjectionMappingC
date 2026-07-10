#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Warp/DeformationField.h"
#include "PMSDK/Math/Transform.h"
#include <memory>
#include <string>
#include <vector>

namespace pmsdk::Warp {

class WarpNode {
public:
    PMSDK_API WarpNode(const std::string& name = "WarpNode");
    PMSDK_API ~WarpNode();

    PMSDK_API std::string GetName() const;
    PMSDK_API void SetName(const std::string& name);

    // Transform relative to parent
    PMSDK_API void SetLocalTransform(const Math::Transform& transform);
    PMSDK_API Math::Transform GetLocalTransform() const;

    // Hierarchy
    PMSDK_API void AddChild(std::shared_ptr<WarpNode> child);
    PMSDK_API void RemoveChild(std::shared_ptr<WarpNode> child);
    PMSDK_API const std::vector<std::shared_ptr<WarpNode>>& GetChildren() const;
    PMSDK_API WarpNode* GetParent() const;

    // Compute the global transform by accumulating parent transforms
    PMSDK_API Math::Transform ComputeGlobalTransform() const;

    // Get the deformation field applied at this node
    PMSDK_API DeformationField& GetDeformationField();
    PMSDK_API const DeformationField& GetDeformationField() const;

    // Apply the deformation field and transform to an input base mesh.
    // Returns the final world-space mesh for this node.
    PMSDK_API std::unique_ptr<Geometry::Mesh> ProcessMesh(const Geometry::Mesh& baseMesh) const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;

    friend class WarpNodeImpl; // to allow setting parent internally
};

} // namespace pmsdk::Warp
