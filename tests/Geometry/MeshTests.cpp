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
    mesh.SetVertices(verts.data(), verts.size());
    
    size_t v_count = 0;
    auto retrievedVerts = mesh.GetVertices(&v_count);
    EXPECT_EQ(v_count, 1);
    EXPECT_EQ(retrievedVerts[0].position.x, 0.0f);
}

TEST(MeshTests, RecalculateNormals) {
    Mesh mesh;
    std::vector<Vertex> verts(3);
    verts[0].position = {0.0f, 0.0f, 0.0f};
    verts[1].position = {1.0f, 0.0f, 0.0f};
    verts[2].position = {0.0f, 0.0f, 1.0f};
    std::vector<uint32_t> idx = {0, 1, 2};
    mesh.SetVertices(verts.data(), verts.size());
    mesh.SetIndices(idx.data(), idx.size());

    mesh.RecalculateNormals();

    size_t v_count = 0;
    auto outVerts = mesh.GetVertices(&v_count);
    // Normal should point to -Y given right hand rule: (1,0,0) x (0,0,1) = (0,-1,0)
    EXPECT_NEAR(outVerts[0].normal.y, -1.0f, 1e-5f);
}

TEST(MeshTests, CalculateBounds) {
    Mesh mesh;
    // Bounds of empty mesh should be something sensible like min=max=0, or min>max
    BoundingBox emptyBounds = mesh.CalculateBounds();
    EXPECT_FLOAT_EQ(emptyBounds.min.x, 1e30f);
    EXPECT_FLOAT_EQ(emptyBounds.max.x, -1e30f);
    
    std::vector<Vertex> verts = {
        {{0.0f, 0.0f, 0.0f}, {0.0f, 0.0f, 1.0f}, {0.0f, 0.0f}, {1.0f, 1.0f, 1.0f, 1.0f}},
        {{1.0f, 0.0f, 0.0f}, {0.0f, 0.0f, 1.0f}, {1.0f, 0.0f}, {1.0f, 1.0f, 1.0f, 1.0f}},
        {{0.0f, 2.0f, -1.0f}, {0.0f, 0.0f, 1.0f}, {0.0f, 1.0f}, {1.0f, 1.0f, 1.0f, 1.0f}}
    };

    mesh.SetVertices(verts.data(), verts.size());
    BoundingBox bounds = mesh.CalculateBounds();

    EXPECT_FLOAT_EQ(bounds.min.x, 0.0f);
    EXPECT_FLOAT_EQ(bounds.min.y, 0.0f);
    EXPECT_FLOAT_EQ(bounds.min.z, -1.0f);

    EXPECT_FLOAT_EQ(bounds.max.x, 1.0f);
    EXPECT_FLOAT_EQ(bounds.max.y, 2.0f);
    EXPECT_FLOAT_EQ(bounds.max.z, 0.0f);
}
