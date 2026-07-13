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
    m.SetVertices(verts.data(), verts.size());
    m.SetIndices(idx.data(), idx.size());
    
    std::string jsonStr = Serialization::SerializeMesh(m);
    
    Mesh out;
    Serialization::DeserializeMesh(jsonStr, out);
    
    size_t v_count = 0;
    auto outVerts = out.GetVertices(&v_count);
    size_t i_count = 0;
    auto outIdx = out.GetIndices(&i_count);
    
    ASSERT_EQ(v_count, 3);
    ASSERT_EQ(i_count, 3);
    EXPECT_FLOAT_EQ(outVerts[1].position.x, 1.0f);
}
