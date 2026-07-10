#include <gtest/gtest.h>
#include "PMSDK/Geometry/Intersection.h"

using namespace pmsdk::Geometry;
using namespace pmsdk::Math;

TEST(IntersectionTests, RayTriangleHit) {
    Vector3 v0(-1.0f, 0.0f, 0.0f);
    Vector3 v1(1.0f, 0.0f, 0.0f);
    Vector3 v2(0.0f, 1.0f, 0.0f);
    Ray ray(Vector3(0.0f, 0.2f, -1.0f), Vector3(0.0f, 0.0f, 1.0f));

    auto res = Intersection::RayTriangle(ray, v0, v1, v2);
    EXPECT_TRUE(res.hit);
    EXPECT_FLOAT_EQ(res.distance, 1.0f);
    EXPECT_FLOAT_EQ(res.point.z, 0.0f);
}

TEST(IntersectionTests, RayTriangleMiss) {
    Vector3 v0(-1.0f, 0.0f, 0.0f);
    Vector3 v1(1.0f, 0.0f, 0.0f);
    Vector3 v2(0.0f, 1.0f, 0.0f);
    Ray ray(Vector3(0.0f, 2.0f, -1.0f), Vector3(0.0f, 0.0f, 1.0f)); // Shoots above

    auto res = Intersection::RayTriangle(ray, v0, v1, v2);
    EXPECT_FALSE(res.hit);
}

TEST(IntersectionTests, RayPlane) {
    Plane p(Vector3(0.0f, 1.0f, 0.0f), 0.0f);
    Ray ray(Vector3(0.0f, 5.0f, 0.0f), Vector3(0.0f, -1.0f, 0.0f));

    auto res = Intersection::RayPlane(ray, p);
    EXPECT_TRUE(res.hit);
    EXPECT_FLOAT_EQ(res.distance, 5.0f);
}
