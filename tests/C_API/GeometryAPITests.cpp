#include <gtest/gtest.h>
#include "PMSDK/PMSDK_C.h"
#include <vector>

TEST(GeometryAPITests, CreateAndDestroyMesh) {
    pmsdk_mesh_t* mesh = pmsdk_mesh_create();
    ASSERT_NE(mesh, nullptr);
    pmsdk_mesh_destroy(mesh);
}

TEST(GeometryAPITests, SetAndGetVertices) {
    pmsdk_mesh_t* mesh = pmsdk_mesh_create();
    ASSERT_NE(mesh, nullptr);

    std::vector<pmsdk_vertex_t> verts = {
        { {0.0f, 0.0f, 0.0f}, {0.0f, 0.0f, 1.0f}, {0.0f, 0.0f}, {1.0f, 1.0f, 1.0f, 1.0f} },
        { {1.0f, 0.0f, 0.0f}, {0.0f, 0.0f, 1.0f}, {1.0f, 0.0f}, {1.0f, 1.0f, 1.0f, 1.0f} },
        { {0.0f, 1.0f, 0.0f}, {0.0f, 0.0f, 1.0f}, {0.0f, 1.0f}, {1.0f, 1.0f, 1.0f, 1.0f} }
    };

    pmsdk_status_t status = pmsdk_mesh_set_vertices(mesh, verts.data(), verts.size());
    EXPECT_EQ(status, PMSDK_SUCCESS);

    size_t vCount = pmsdk_mesh_get_vertex_count(mesh);
    EXPECT_EQ(vCount, 3);

    pmsdk_mesh_destroy(mesh);
}

TEST(WarpAPITests, ProcessMeshThroughWarpNode) {
    pmsdk_mesh_t* mesh = pmsdk_mesh_create();
    pmsdk_mesh_t* outMesh = pmsdk_mesh_create();
    pmsdk_warpnode_t* node = pmsdk_warpnode_create();
    
    // Add some vertices
    std::vector<pmsdk_vertex_t> verts = {
        { {10.0f, 0.0f, 0.0f}, {0.0f, 0.0f, 1.0f}, {0.0f, 0.0f}, {1.0f, 1.0f, 1.0f, 1.0f} }
    };
    pmsdk_mesh_set_vertices(mesh, verts.data(), verts.size());
    
    pmsdk_status_t status = pmsdk_warpnode_process_mesh(node, mesh, outMesh);
    EXPECT_EQ(status, PMSDK_SUCCESS);
    
    // Identity node should preserve vertex count
    EXPECT_EQ(pmsdk_mesh_get_vertex_count(outMesh), 1);

    pmsdk_warpnode_destroy(node);
    pmsdk_mesh_destroy(outMesh);
    pmsdk_mesh_destroy(mesh);
}
