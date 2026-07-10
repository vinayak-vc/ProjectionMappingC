#include <gtest/gtest.h>
#include "PMSDK/PMSDK_C.h"
#include <vector>

TEST(GeometryAPITests, CreateAndDestroyMesh) {
    pmsdk_mesh_t* mesh = pmsdk_mesh_create();
    ASSERT_NE(mesh, nullptr);
    pmsdk_mesh_destroy(mesh);
}

TEST(GeometryAPITests, NullChecks) {
    EXPECT_EQ(pmsdk_mesh_set_vertices(nullptr, nullptr, 0), PMSDK_ERROR_INVALID_ARGUMENT);
    EXPECT_EQ(pmsdk_mesh_set_indices(nullptr, nullptr, 0), PMSDK_ERROR_INVALID_ARGUMENT);
    EXPECT_EQ(pmsdk_mesh_recalculate_normals(nullptr), PMSDK_ERROR_INVALID_ARGUMENT);
    EXPECT_EQ(pmsdk_mesh_clear(nullptr), PMSDK_ERROR_INVALID_ARGUMENT);
    EXPECT_EQ(pmsdk_mesh_get_vertex_count(nullptr), 0);
    EXPECT_EQ(pmsdk_mesh_get_index_count(nullptr), 0);
    
    // Destroying nullptr should be safe and no-op
    pmsdk_mesh_destroy(nullptr);
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
