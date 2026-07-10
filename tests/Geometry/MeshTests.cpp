#include <gtest/gtest.h>
#include "PMSDK/Geometry/Mesh.h"

using namespace pmsdk::Geometry;
using namespace pmsdk::Math;

TEST(MeshTests, DefaultConstructionIsEmpty) {
    Mesh mesh;
    EXPECT_EQ(mesh.GetVertexCount(), 0);
    EXPECT_EQ(mesh.GetIndexCount(), 0);
}

TEST(MeshTests, SetAndGetVertices) {
    Mesh mesh;
    std::vector<Vertex> verts = {
        {{0.0f, 0.0f, 0.0f}, {0.0f, 1.0f, 0.0f}, {0.0f, 0.0f}, {1.0f, 1.0f, 1.0f, 1.0f}}
    };
    mesh.SetVertices(verts);
    EXPECT_EQ(mesh.GetVertexCount(), 1);
    EXPECT_EQ(mesh.GetVertices()[0].position.x, 0.0f);
}

TEST(MeshTests, RecalculateNormals) {
    Mesh mesh;
    std::vector<Vertex> verts(3);
    verts[0].position = {0.0f, 0.0f, 0.0f};
    verts[1].position = {1.0f, 0.0f, 0.0f};
    verts[2].position = {0.0f, 0.0f, 1.0f};
    std::vector<uint32_t> idx = {0, 1, 2};
    mesh.SetVertices(verts);
    mesh.SetIndices(idx);

    mesh.RecalculateNormals();

    auto outVerts = mesh.GetVertices();
    // Normal should point to -Y given right hand rule: (1,0,0) x (0,0,1) = (0,-1,0)
    EXPECT_NEAR(outVerts[0].normal.y, -1.0f, 1e-5f);
}

TEST(MeshTests, CalculateBounds) {
    Mesh mesh;
    std::vector<Vertex> verts(2);
    verts[0].position = {-1.0f, -1.0f, -1.0f};
    verts[1].position = {1.0f, 1.0f, 1.0f};
    mesh.SetVertices(verts);

    auto bounds = mesh.CalculateBounds();
    EXPECT_FLOAT_EQ(bounds.min.x, -1.0f);
    EXPECT_FLOAT_EQ(bounds.max.x, 1.0f);
}
