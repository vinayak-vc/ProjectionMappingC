#include <gtest/gtest.h>
#include "PMSDK/Warp/DeformationField.h"
#include "PMSDK/Geometry/MeshBuilder.h"

using namespace pmsdk;
using namespace pmsdk::Warp;

TEST(DeformationFieldTests, ApplyDeformationGrid) {
    DeformationField field;
    field.SetType(DeformationType::Grid);
    
    std::vector<Math::Vector3> pts = {
        {0,0,0}, {1,0,0}, {2,0,0},
        {0,1,0}, {1,1,0}, {2,1,0},
        {0,2,0}, {1,2,0}, {2,2,0}
    };
    field.GetGridWarp()->SetControlPoints(3, 3, pts);

    auto baseMesh = Geometry::MeshBuilder::CreatePlane(1, 1); 
    // Plane from MeshBuilder creates a 1x1 quad mapped from -0.5 to 0.5. 
    // UVs are 0 to 1.
    auto warped = field.ApplyDeformation(*baseMesh);
    
    EXPECT_NE(warped, nullptr);
    EXPECT_EQ(warped->GetVertexCount(), baseMesh->GetVertexCount());

    auto verts = warped->GetVertices();
    // UV (1,1) should map to position {2,2,0} since GridWarp evaluates the grid.
    bool foundMax = false;
    for(const auto& v : verts) {
        if (v.uv.x > 0.99f && v.uv.y > 0.99f) {
            EXPECT_NEAR(v.position.x, 2.0f, 0.001f);
            EXPECT_NEAR(v.position.y, 2.0f, 0.001f);
            foundMax = true;
        }
    }
    EXPECT_TRUE(foundMax);
}
