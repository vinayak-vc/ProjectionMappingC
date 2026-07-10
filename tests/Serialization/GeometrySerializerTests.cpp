#include <gtest/gtest.h>
#include "PMSDK/Serialization/GeometrySerializer.h"

using namespace pmsdk;
using namespace pmsdk::Geometry;

TEST(GeometrySerializerTests, SerializeMesh) {
    std::vector<Vertex> verts = {
        Vertex{ Math::Vector3(0.0f, 0.0f, 0.0f), Math::Vector3(0.0f, 0.0f, 1.0f), Math::Vector2(0.0f, 0.0f) },
        Vertex{ Math::Vector3(1.0f, 0.0f, 0.0f), Math::Vector3(0.0f, 0.0f, 1.0f), Math::Vector2(1.0f, 0.0f) },
        Vertex{ Math::Vector3(0.0f, 1.0f, 0.0f), Math::Vector3(0.0f, 0.0f, 1.0f), Math::Vector2(0.0f, 1.0f) }
    };
    std::vector<uint32_t> idx = {0, 1, 2};
    Mesh m;
    m.SetVertices(verts);
    m.SetIndices(idx);
    
    std::string jsonStr = Serialization::SerializeMesh(m);
    
    Mesh out;
    Serialization::DeserializeMesh(jsonStr, out);
    
    ASSERT_EQ(out.GetVertices().size(), 3);
    ASSERT_EQ(out.GetIndices().size(), 3);
    EXPECT_FLOAT_EQ(out.GetVertices()[1].position.x, 1.0f);
}
