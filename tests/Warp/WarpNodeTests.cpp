#include <gtest/gtest.h>
#include "PMSDK/Warp/WarpNode.h"
#include "PMSDK/Geometry/MeshBuilder.h"

using namespace pmsdk;
using namespace pmsdk::Warp;

TEST(WarpNodeTests, HierarchyTransforms) {
    auto root = std::make_shared<WarpNode>("Root");
    auto child = std::make_shared<WarpNode>("Child");
    root->AddChild(child);

    Math::Transform tRoot;
    tRoot.position = {10.0f, 0.0f, 0.0f};
    root->SetLocalTransform(tRoot);

    Math::Transform tChild;
    tChild.position = {5.0f, 0.0f, 0.0f};
    child->SetLocalTransform(tChild);

    Math::Transform globalChild = child->ComputeGlobalTransform();
    // 10 + 5 = 15 on X
    EXPECT_FLOAT_EQ(globalChild.position.x, 15.0f);
    EXPECT_EQ(child->GetParent(), root.get());

    root->RemoveChild(child);
    EXPECT_EQ(child->GetParent(), nullptr);
}

TEST(WarpNodeTests, ProcessMesh) {
    WarpNode node("Slice");
    Math::Transform t;
    t.position = {0.0f, 10.0f, 0.0f};
    node.SetLocalTransform(t);

    auto baseMesh = Geometry::MeshBuilder::CreatePlane(1, 1); // center at 0,0,0
    auto warped = node.ProcessMesh(*baseMesh);

    // Resulting mesh should be shifted by +10 in Y
    auto verts = warped->GetVertices();
    // Original plane bounds are -0.5 to 0.5. Y should now be 9.5 to 10.5
    for(const auto& v : verts) {
        EXPECT_GE(v.position.y, 9.49f);
        EXPECT_LE(v.position.y, 10.51f);
    }
}
