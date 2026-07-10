#include <gtest/gtest.h>
#include "PMSDK/Geometry/DynamicMesh.h"

using namespace pmsdk::Geometry;

TEST(DynamicMeshTests, AddRemoveVertices) {
    DynamicMesh mesh;
    EXPECT_EQ(mesh.GetActiveVertexCount(), 0);

    Vertex v1{{0,0,0}, {0,0,0}, {0,0}, {1,1,1,1}};
    Vertex v2{{1,1,1}, {0,0,0}, {0,0}, {1,1,1,1}};

    uint32_t id1 = mesh.AddVertex(v1);
    uint32_t id2 = mesh.AddVertex(v2);

    EXPECT_EQ(mesh.GetActiveVertexCount(), 2);
    EXPECT_NE(mesh.GetVertex(id1), nullptr);
    EXPECT_NE(mesh.GetVertex(id2), nullptr);

    mesh.RemoveVertex(id1);
    EXPECT_EQ(mesh.GetActiveVertexCount(), 1);
    EXPECT_EQ(mesh.GetVertex(id1), nullptr);
    EXPECT_NE(mesh.GetVertex(id2), nullptr);
}

TEST(DynamicMeshTests, AddRemoveFaces) {
    DynamicMesh mesh;
    Vertex v{{0,0,0}, {0,0,0}, {0,0}, {1,1,1,1}};
    uint32_t id0 = mesh.AddVertex(v);
    uint32_t id1 = mesh.AddVertex(v);
    uint32_t id2 = mesh.AddVertex(v);

    uint32_t faceId = mesh.AddFace(id0, id1, id2);
    EXPECT_EQ(mesh.GetActiveFaceCount(), 1);

    auto adj = mesh.GetAdjacentFaces(id0);
    EXPECT_EQ(adj.size(), 1);
    EXPECT_EQ(adj[0], faceId);

    mesh.RemoveFace(faceId);
    EXPECT_EQ(mesh.GetActiveFaceCount(), 0);
    adj = mesh.GetAdjacentFaces(id0);
    EXPECT_EQ(adj.size(), 0);
}

TEST(DynamicMeshTests, ToMeshConversion) {
    DynamicMesh mesh;
    Vertex v{{0,0,0}, {0,0,0}, {0,0}, {1,1,1,1}};
    uint32_t id0 = mesh.AddVertex(v);
    uint32_t id1 = mesh.AddVertex(v);
    uint32_t id2 = mesh.AddVertex(v);
    mesh.AddFace(id0, id1, id2);

    auto staticMesh = mesh.ToMesh();
    EXPECT_EQ(staticMesh->GetVertices().size(), 3);
    EXPECT_EQ(staticMesh->GetIndices().size(), 3);
}
