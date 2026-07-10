#include <gtest/gtest.h>
#include "PMSDK/Serialization/WarpSerializer.h"

using namespace pmsdk;
using namespace pmsdk::Warp;

TEST(WarpSerializerTests, SerializeWarpNode) {
    WarpNode node("MyNode");
    Math::Transform t;
    t.position = Math::Vector3{1, 2, 3};
    node.SetLocalTransform(t);
    
    std::string jsonStr = Serialization::SerializeWarpNode(node);
    
    WarpNode outNode("");
    Serialization::DeserializeWarpNode(jsonStr, outNode);
    
    EXPECT_EQ(outNode.GetName(), "MyNode");
    EXPECT_FLOAT_EQ(outNode.GetLocalTransform().position.x, 1.0f);
    EXPECT_FLOAT_EQ(outNode.GetLocalTransform().position.y, 2.0f);
    EXPECT_FLOAT_EQ(outNode.GetLocalTransform().position.z, 3.0f);
}

TEST(WarpSerializerTests, SerializeProjector) {
    Projector p;
    p.SetThrowRatio(1.5f);
    p.SetAspectRatio(16.0f / 9.0f);
    
    std::string jsonStr = Serialization::SerializeProjector(p);
    
    Projector out;
    Serialization::DeserializeProjector(jsonStr, out);
    
    EXPECT_FLOAT_EQ(out.GetThrowRatio(), 1.5f);
    EXPECT_FLOAT_EQ(out.GetAspectRatio(), 16.0f / 9.0f);
}
