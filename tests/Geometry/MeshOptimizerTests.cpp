#include <gtest/gtest.h>
#include "PMSDK/Geometry/MeshOptimizer.h"
#include "PMSDK/Geometry/Mesh.h"

using namespace pmsdk::Geometry;

TEST(MeshOptimizerTests, WeldVertices) {
    Mesh mesh;
    std::vector<Vertex> verts = {
        {{0,0,0}, {0,1,0}, {0,0}, {1,1,1,1}},
        {{1,0,0}, {0,1,0}, {0,0}, {1,1,1,1}},
        {{0,0,0}, {0,1,0}, {0,0}, {1,1,1,1}} // duplicate of 0
    };
    std::vector<uint32_t> idx = {0, 1, 2};
    mesh.SetVertices(verts);
    mesh.SetIndices(idx);

    MeshOptimizer::WeldVertices(mesh);

    auto newVerts = mesh.GetVertices();
    auto newIdx = mesh.GetIndices();

    EXPECT_EQ(newVerts.size(), 2);
    EXPECT_EQ(newIdx.size(), 3);
    EXPECT_EQ(newIdx[0], 0);
    EXPECT_EQ(newIdx[1], 1);
    EXPECT_EQ(newIdx[2], 0); // remapped to 0
}

TEST(MeshOptimizerTests, RecalculateSmoothNormals) {
    Mesh mesh;
    std::vector<Vertex> verts = {
        {{0,0,0}, {0,0,0}, {0,0}, {1,1,1,1}},
        {{1,0,0}, {0,0,0}, {0,0}, {1,1,1,1}},
        {{0,1,0}, {0,0,0}, {0,0}, {1,1,1,1}}
    };
    std::vector<uint32_t> idx = {0, 1, 2};
    mesh.SetVertices(verts);
    mesh.SetIndices(idx);

    MeshOptimizer::RecalculateSmoothNormals(mesh);

    auto newVerts = mesh.GetVertices();
    // Triangle is in XY plane, normal should be +Z
    EXPECT_FLOAT_EQ(newVerts[0].normal.x, 0.0f);
    EXPECT_FLOAT_EQ(newVerts[0].normal.y, 0.0f);
    EXPECT_FLOAT_EQ(newVerts[0].normal.z, 1.0f);
}
