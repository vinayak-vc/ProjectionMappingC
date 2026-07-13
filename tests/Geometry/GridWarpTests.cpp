#include <gtest/gtest.h>
#include "PMSDK/Geometry/GridWarp.h"

using namespace pmsdk::Geometry;

TEST(GridWarpTests, GenerateMesh) {
    GridWarp warp;
    std::vector<pmsdk::Math::Vector3> pts = {
        {0,0,0}, {1,0,0}, {2,0,0},
        {0,1,0}, {1,1,0}, {2,1,0},
        {0,2,0}, {1,2,0}, {2,2,0}
    };
    
    warp.SetControlPoints(3, 3, pts);

    auto mesh = warp.GenerateMesh(4, 4);
    EXPECT_NE(mesh, nullptr);

    size_t v_count = 0;
    auto vertices = mesh->GetVertices(&v_count);
    EXPECT_EQ(v_count, 25); // 5x5 vertices

    size_t i_count = 0;
    auto indices = mesh->GetIndices(&i_count);
    EXPECT_EQ(i_count, 96); // 4x4 quads = 16 quads = 32 tris = 96 indices

    // Corner at top right should be 2,2,0
    EXPECT_FLOAT_EQ(vertices[v_count - 1].position.x, 2.0f);
    EXPECT_FLOAT_EQ(vertices[v_count - 1].position.y, 2.0f);
}
