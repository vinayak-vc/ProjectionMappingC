#include <gtest/gtest.h>
#include "PMSDK/Geometry/BezierPatch.h"

using namespace pmsdk::Geometry;

TEST(BezierPatchTests, GenerateMesh) {
    BezierPatch patch;
    std::vector<pmsdk::Math::Vector3> pts(16);
    
    // Flat patch spanning (0,0) to (3,3) on XY plane
    for (int y = 0; y < 4; ++y) {
        for (int x = 0; x < 4; ++x) {
            pts[y*4 + x] = {(float)x, (float)y, 0.0f};
        }
    }
    patch.SetControlPoints(pts);

    auto mesh = patch.GenerateMesh(10, 10);
    EXPECT_NE(mesh, nullptr);

    // 10x10 resolution = 11x11 vertices = 121 vertices
    auto vertices = mesh->GetVertices();
    EXPECT_EQ(vertices.size(), 121);

    // 10x10 = 100 quads = 200 triangles = 600 indices
    auto indices = mesh->GetIndices();
    EXPECT_EQ(indices.size(), 600);

    // Test corners
    EXPECT_FLOAT_EQ(vertices[0].position.x, 0.0f);
    EXPECT_FLOAT_EQ(vertices[0].position.y, 0.0f);

    EXPECT_FLOAT_EQ(vertices.back().position.x, 3.0f);
    EXPECT_FLOAT_EQ(vertices.back().position.y, 3.0f);
}
