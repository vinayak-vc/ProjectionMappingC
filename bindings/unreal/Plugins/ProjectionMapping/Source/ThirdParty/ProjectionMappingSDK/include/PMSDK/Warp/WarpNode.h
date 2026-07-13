#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Warp/DeformationField.h"
#include "PMSDK/Math/Transform.h"
#include <memory>
#include <string>
#include <vector>

namespace pmsdk::Warp {

/**
 * @brief Represents a node in the warp hierarchy.
 * 
 * Warp nodes can be arranged in a tree structure. Each node has a local 
 * transform and an optional deformation field (grid or bezier patch) that 
 * applies to any mesh processed through it.
 */
class WarpNode {
public:
    /**
     * @brief Constructs a WarpNode with an optional name.
     * @param name The name of the node.
     */
    PMSDK_API WarpNode(const std::string& name = "WarpNode");
    
    /** @brief Destructor. */
    PMSDK_API ~WarpNode();

    /** @return The name of the node. */
    PMSDK_API std::string GetName() const;
    
    /**
     * @brief Sets the name of the node.
     * @param name The new name.
     */
    PMSDK_API void SetName(const std::string& name);

    /**
     * @brief Sets the local transform relative to the parent node.
     * @param transform The local transform.
     */
    PMSDK_API void SetLocalTransform(const Math::Transform& transform);
    
    /** @return The local transform relative to the parent. */
    PMSDK_API Math::Transform GetLocalTransform() const;

    /**
     * @brief Adds a child node to this node.
     * @param child The child node to add.
     */
    PMSDK_API void AddChild(std::shared_ptr<WarpNode> child);
    
    /**
     * @brief Removes a child node from this node.
     * @param child The child node to remove.
     */
    PMSDK_API void RemoveChild(std::shared_ptr<WarpNode> child);
    
    /** @return A list of child nodes. */
    PMSDK_API const std::vector<std::shared_ptr<WarpNode>>& GetChildren() const;
    
    /** @return The parent node, or nullptr if this is a root node. */
    PMSDK_API WarpNode* GetParent() const;

    /**
     * @brief Computes the global transform by accumulating parent transforms.
     * @return The global transform.
     */
    PMSDK_API Math::Transform ComputeGlobalTransform() const;

    /** @return A mutable reference to the node's deformation field. */
    PMSDK_API DeformationField& GetDeformationField();
    
    /** @return A const reference to the node's deformation field. */
    PMSDK_API const DeformationField& GetDeformationField() const;

    /**
     * @brief Applies the deformation field and transform to an input base mesh.
     * @param baseMesh The source mesh.
     * @return A newly allocated mesh representing the final world-space geometry.
     */
    PMSDK_API std::unique_ptr<Geometry::Mesh> ProcessMesh(const Geometry::Mesh& baseMesh) const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;

    friend class WarpNodeImpl; // to allow setting parent internally
};

} // namespace pmsdk::Warp
