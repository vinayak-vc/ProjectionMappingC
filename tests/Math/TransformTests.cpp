#include <gtest/gtest.h>
#include "PMSDK/Math/Transform.h"

using namespace pmsdk::Math;

TEST(TransformTests, IdentityMatrix) {
    Transform t;
    Matrix4 m = t.ToMatrix();
    EXPECT_FLOAT_EQ(m[0], 1.0f);
    EXPECT_FLOAT_EQ(m[15], 1.0f);
}

TEST(TransformTests, Composition) {
    Transform t;
    t.position = Vector3(1.0f, 0.0f, 0.0f);
    t.scale = Vector3(2.0f, 2.0f, 2.0f);
    Matrix4 m = t.ToMatrix();
    Vector3 p = m.MultiplyPoint(Vector3(1.0f, 0.0f, 0.0f));
    EXPECT_FLOAT_EQ(p.x, 3.0f); // 1 * 2 + 1
}
