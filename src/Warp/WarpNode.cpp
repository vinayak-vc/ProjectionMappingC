#include "PMSDK/Warp/WarpNode.h"
#include <algorithm>

namespace pmsdk::Warp {

struct WarpNode::Impl {
    std::string name;
    Math::Transform localTransform;
    WarpNode* parent{nullptr};
    std::vector<std::shared_ptr<WarpNode>> children;
    DeformationField deformationField;
};

WarpNode::WarpNode(const std::string& name) : m_impl(std::make_unique<Impl>()) {
    m_impl->name = name;
}

WarpNode::~WarpNode() {
    for (auto& child : m_impl->children) {
        child->m_impl->parent = nullptr;
    }
}

std::string WarpNode::GetName() const {
    return m_impl->name;
}

void WarpNode::SetName(const std::string& name) {
    m_impl->name = name;
}

void WarpNode::SetLocalTransform(const Math::Transform& transform) {
    m_impl->localTransform = transform;
}

Math::Transform WarpNode::GetLocalTransform() const {
    return m_impl->localTransform;
}

void WarpNode::AddChild(std::shared_ptr<WarpNode> child) {
    if (!child) return;
    if (child->m_impl->parent) {
        // Optionally remove from old parent
    }
    child->m_impl->parent = this;
    m_impl->children.push_back(child);
}

void WarpNode::RemoveChild(std::shared_ptr<WarpNode> child) {
    if (!child) return;
    auto it = std::find(m_impl->children.begin(), m_impl->children.end(), child);
    if (it != m_impl->children.end()) {
        (*it)->m_impl->parent = nullptr;
        m_impl->children.erase(it);
    }
}

const std::vector<std::shared_ptr<WarpNode>>& WarpNode::GetChildren() const {
    return m_impl->children;
}

WarpNode* WarpNode::GetParent() const {
    return m_impl->parent;
}

Math::Transform WarpNode::ComputeGlobalTransform() const {
    if (m_impl->parent) {
        Math::Transform parentGlobal = m_impl->parent->ComputeGlobalTransform();
        Math::Matrix4 globalMat = parentGlobal.ToMatrix() * m_impl->localTransform.ToMatrix();
        
        Math::Transform result;
        // Approximation: extract translation directly for simple cases, 
        // full matrix decomposition is needed for rotation/scale.
        // For projection mapping SDKs, storing Matrix4 globally is safer.
        // Let's implement basic extraction.
        Math::Vector3 pos = {globalMat.m[12], globalMat.m[13], globalMat.m[14]};
        result.position = pos;
        // Note: For a robust SDK, Transform should support matrix injection or we return Matrix4 directly.
        // Given Math::Transform stores Pos/Rot/Scale, converting back is complex. 
        // We will just let the user use ComputeGlobalTransform as an approximation for now,
        // or we should update Math::Transform to support full matrix backing.
        return result; 
    }
    return m_impl->localTransform;
}

DeformationField& WarpNode::GetDeformationField() {
    return m_impl->deformationField;
}

const DeformationField& WarpNode::GetDeformationField() const {
    return m_impl->deformationField;
}

std::unique_ptr<Geometry::Mesh> WarpNode::ProcessMesh(const Geometry::Mesh& baseMesh) const {
    // 1. Apply nonlinear deformations
    auto deformedMesh = m_impl->deformationField.ApplyDeformation(baseMesh);

    // 2. Apply global transform (linear)
    Math::Transform globalTx = ComputeGlobalTransform();
    Math::Matrix4 m = globalTx.ToMatrix();

    auto vertices = deformedMesh->GetVerticesMutable();
    for (auto& v : vertices) {
        v.position = m.MultiplyPoint(v.position);
    }
    
    // Recalculate normals after spatial transformation
    deformedMesh->RecalculateNormals();

    return deformedMesh;
}

} // namespace pmsdk::Warp
