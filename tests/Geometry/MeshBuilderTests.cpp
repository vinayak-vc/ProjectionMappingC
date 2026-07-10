#include <gtest/gtest.h>
#include "PMSDK/Geometry/MeshBuilder.h"

using namespace pmsdk::Geometry;

TEST(MeshBuilderTests, CreatePlane) {
    auto mesh = MeshBuilder::CreatePlane(10.0f, 10.0f);
    EXPECT_EQ(mesh->GetVertexCount(), 4);
    EXPECT_EQ(mesh->GetIndexCount(), 6);

    auto bounds = mesh->CalculateBounds();
    EXPECT_FLOAT_EQ(bounds.min.x, -5.0f);
    EXPECT_FLOAT_EQ(bounds.max.x, 5.0f);
}

TEST(MeshBuilderTests, CreateGrid) {
    auto mesh = MeshBuilder::CreateGrid(10.0f, 10.0f, 2, 2);
    EXPECT_EQ(mesh->GetVertexCount(), 9); // 3x3 vertices
    EXPECT_EQ(mesh->GetIndexCount(), 4 * 6); // 4 quads, 6 indices each
}

TEST(MeshBuilderTests, CreateCylinder) {
    auto mesh = MeshBuilder::CreateCylinder(5.0f, 10.0f, 10);
    // 10 segments + 1 overlapping for UV seam = 11 top, 11 bot = 22 vertices
    EXPECT_EQ(mesh->GetVertexCount(), 22);
    EXPECT_EQ(mesh->GetIndexCount(), 10 * 6);
}
