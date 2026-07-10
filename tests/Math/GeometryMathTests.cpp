#include <gtest/gtest.h>
#include "PMSDK/Math/BoundingBox.h"
#include "PMSDK/Math/Ray.h"
#include "PMSDK/Math/Plane.h"

using namespace pmsdk::Math;

TEST(BoundingBoxTests, Containment) {
    BoundingBox b;
    b.Expand(Vector3(1.0f, 1.0f, 1.0f));
    b.Expand(Vector3(-1.0f, -1.0f, -1.0f));
    EXPECT_TRUE(b.Contains(Vector3(0.0f, 0.0f, 0.0f)));
    EXPECT_FALSE(b.Contains(Vector3(2.0f, 0.0f, 0.0f)));
}

TEST(RayTests, PointAt) {
    Ray r(Vector3(1.0f, 1.0f, 1.0f), Vector3(0.0f, 1.0f, 0.0f));
    Vector3 p = r.PointAt(2.0f);
    EXPECT_FLOAT_EQ(p.y, 3.0f);
}

TEST(PlaneTests, Distance) {
    Plane p(Vector3(0.0f, 1.0f, 0.0f), 0.0f);
    EXPECT_FLOAT_EQ(p.DistanceToPoint(Vector3(0.0f, 5.0f, 0.0f)), 5.0f);
}

TEST(PlaneTests, ThreePoints) {
    Plane p(Vector3(0.0f, 0.0f, 0.0f), Vector3(1.0f, 0.0f, 0.0f), Vector3(0.0f, 0.0f, 1.0f));
    EXPECT_FLOAT_EQ(std::abs(p.normal.y), 1.0f);
    EXPECT_FLOAT_EQ(p.distance, 0.0f);
}
