#include <gtest/gtest.h>
#include "PMSDK/Geometry/GridWarp.h"
#include "PMSDK/Math/Vector3.h"
#include <vector>

using namespace pmsdk;
using namespace pmsdk::Geometry;
using namespace pmsdk::Math;

TEST(DeformationFieldEdgeTests, QueryOutsideGrid) {
    GridWarp field;
    std::vector<Vector3> pts = {
        {-1.0f, -1.0f, 0.0f},
        { 2.0f, -1.0f, 0.0f},
        {-1.0f,  2.0f, 0.0f},
        { 2.0f,  2.0f, 0.0f}
    };
    field.SetControlPoints(2, 2, pts);
    
    // Test points inside
    Vector3 p1 = field.Evaluate(0.5f, 0.5f);
    EXPECT_NEAR(p1.x, 0.5f, 1e-4f);
    EXPECT_NEAR(p1.y, 0.5f, 1e-4f);
    
    // Test points outside (should be clamped by evaluating at 0 and 1)
    Vector3 p2 = field.Evaluate(-0.5f, 0.5f); // u is outside
    EXPECT_NEAR(p2.x, -1.0f, 1e-4f);
    EXPECT_NEAR(p2.y, 0.5f, 1e-4f);
    
    Vector3 p3 = field.Evaluate(1.5f, -0.5f); // u and v outside
    EXPECT_NEAR(p3.x, 2.0f, 1e-4f);
    EXPECT_NEAR(p3.y, -1.0f, 1e-4f);
}

TEST(DeformationFieldEdgeTests, ZeroResolutionThrows) {
    GridWarp field;
    // Empty points should probably be rejected
    std::vector<Vector3> pts;
    EXPECT_ANY_THROW(field.SetControlPoints(0, 0, pts));
}
