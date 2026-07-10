#include <gtest/gtest.h>
#include "PMSDK/PMSDK_C.h"
#include <vector>

TEST(WarpAPITests, ProjectorNullChecks) {
    EXPECT_EQ(pmsdk_projector_set_aspect_ratio(nullptr, 16.0f/9.0f), PMSDK_ERROR_INVALID_ARGUMENT);
    EXPECT_EQ(pmsdk_projector_set_throw_ratio(nullptr, 1.5f), PMSDK_ERROR_INVALID_ARGUMENT);
    pmsdk_projector_destroy(nullptr); // Should not crash
}

TEST(WarpAPITests, WarpNodeNullChecks) {
    EXPECT_EQ(pmsdk_warpnode_add_child(nullptr, nullptr), PMSDK_ERROR_INVALID_ARGUMENT);
    EXPECT_EQ(pmsdk_warpnode_process_mesh(nullptr, nullptr, nullptr), PMSDK_ERROR_INVALID_ARGUMENT);
    pmsdk_warpnode_destroy(nullptr); // Should not crash
}

TEST(WarpAPITests, ProcessMeshThroughWarpNode) {
    pmsdk_mesh_t* mesh = pmsdk_mesh_create();
    pmsdk_mesh_t* outMesh = pmsdk_mesh_create();
    pmsdk_warpnode_t* node = pmsdk_warpnode_create();
    
    std::vector<pmsdk_vertex_t> verts = {
        { {10.0f, 0.0f, 0.0f}, {0.0f, 0.0f, 1.0f}, {0.0f, 0.0f}, {1.0f, 1.0f, 1.0f, 1.0f} }
    };
    pmsdk_mesh_set_vertices(mesh, verts.data(), verts.size());
    
    pmsdk_status_t status = pmsdk_warpnode_process_mesh(node, mesh, outMesh);
    EXPECT_EQ(status, PMSDK_SUCCESS);
    
    EXPECT_EQ(pmsdk_mesh_get_vertex_count(outMesh), 1);

    pmsdk_warpnode_destroy(node);
    pmsdk_mesh_destroy(outMesh);
    pmsdk_mesh_destroy(mesh);
}
