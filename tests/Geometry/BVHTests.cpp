#include <gtest/gtest.h>
#include "PMSDK/Geometry/BVH.h"
#include "PMSDK/Geometry/MeshBuilder.h"

using namespace pmsdk::Geometry;
using namespace pmsdk::Math;

TEST(BVHTests, BuildAndIntersect) {
    auto mesh = MeshBuilder::CreatePlane(10.0f, 10.0f);
    BVH bvh;
    bvh.Build(*mesh);

    Ray ray(Vector3(0.0f, 0.0f, -10.0f), Vector3(0.0f, 0.0f, 1.0f));
    RayTriangleIntersectionResult result;
    bool hit = bvh.Intersect(ray, result);

    EXPECT_TRUE(hit);
    EXPECT_NEAR(result.distance, 10.0f, 1e-4f);
    EXPECT_NEAR(result.point.x, 0.0f, 1e-4f);
    EXPECT_NEAR(result.point.y, 0.0f, 1e-4f);
}
