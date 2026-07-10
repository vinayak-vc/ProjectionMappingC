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

    auto vertices = mesh->GetVertices();
    EXPECT_EQ(vertices.size(), 25); // 5x5 vertices

    auto indices = mesh->GetIndices();
    EXPECT_EQ(indices.size(), 96); // 4x4 quads = 16 quads = 32 tris = 96 indices

    // Corner at top right should be 2,2,0
    EXPECT_FLOAT_EQ(vertices.back().position.x, 2.0f);
    EXPECT_FLOAT_EQ(vertices.back().position.y, 2.0f);
}
