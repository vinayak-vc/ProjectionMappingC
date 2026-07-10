#include <gtest/gtest.h>
#include "PMSDK/Warp/Projector.h"

using namespace pmsdk;
using namespace pmsdk::Warp;

TEST(ProjectorTests, IntrinsicProperties) {
    Projector proj;
    proj.SetThrowRatio(2.0f);
    EXPECT_FLOAT_EQ(proj.GetThrowRatio(), 2.0f);

    proj.SetAspectRatio(1.5f);
    EXPECT_FLOAT_EQ(proj.GetAspectRatio(), 1.5f);

    proj.SetLensShift(0.5f, -0.2f);
    float sx, sy;
    proj.GetLensShift(sx, sy);
    EXPECT_FLOAT_EQ(sx, 0.5f);
    EXPECT_FLOAT_EQ(sy, -0.2f);
}

TEST(ProjectorTests, ProjectionMatrix) {
    Projector proj;
    proj.SetThrowRatio(1.0f);
    proj.SetAspectRatio(1.0f);
    proj.SetLensShift(0.0f, 0.0f);

    Math::Matrix4 p = proj.GetProjectionMatrix(0.1f, 100.0f);
    // Symmetric projection: m[8] and m[9] should be 0
    EXPECT_FLOAT_EQ(p.m[8], 0.0f);
    EXPECT_FLOAT_EQ(p.m[9], 0.0f);
    EXPECT_FLOAT_EQ(p.m[11], -1.0f); // perspective divide
}

TEST(ProjectorTests, ProjectPoint) {
    Projector proj;
    proj.SetThrowRatio(1.0f);
    proj.SetAspectRatio(1.0f);
    proj.SetLensShift(0.0f, 0.0f);

    // Default transform is at origin looking -Z. 
    // Wait, Transform default constructs to Identity, so looking +Z? 
    // Usually Identity looks down +Z if right-handed. Wait, our Matrix4 is purely a math construct.
    // Let's set explicitly:
    Math::Transform t;
    t.position = {0.0f, 0.0f, 0.0f};
    proj.SetTransform(t);

    Math::Vector3 pt = proj.ProjectPoint({0.0f, 0.0f, -10.0f});
    // In standard graphics, looking down -Z, X and Y should project near 0 for point (0,0,-10)
    EXPECT_NEAR(pt.x, 0.0f, 0.001f);
    EXPECT_NEAR(pt.y, 0.0f, 0.001f);
}
