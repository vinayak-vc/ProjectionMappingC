#include <gtest/gtest.h>
#include "PMSDK/Math/Vector2.h"
#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Math/Vector4.h"

using namespace pmsdk::Math;

TEST(Vector2Tests, Construction) {
    Vector2 v1;
    EXPECT_FLOAT_EQ(v1.x, 0.0f);
    EXPECT_FLOAT_EQ(v1.y, 0.0f);

    Vector2 v2(1.5f, 2.5f);
    EXPECT_FLOAT_EQ(v2.x, 1.5f);
    EXPECT_FLOAT_EQ(v2.y, 2.5f);
}

TEST(Vector2Tests, Arithmetic) {
    Vector2 a(1.0f, 2.0f);
    Vector2 b(3.0f, 4.0f);
    Vector2 c = a + b;
    EXPECT_FLOAT_EQ(c.x, 4.0f);
    EXPECT_FLOAT_EQ(c.y, 6.0f);

    Vector2 d = a * 2.0f;
    EXPECT_FLOAT_EQ(d.x, 2.0f);
    EXPECT_FLOAT_EQ(d.y, 4.0f);

    EXPECT_FLOAT_EQ(a.Dot(b), 11.0f);
    EXPECT_FLOAT_EQ(a.LengthSquared(), 5.0f);
}

TEST(Vector3Tests, Construction) {
    Vector3 v1(1.0f, 2.0f, 3.0f);
    EXPECT_FLOAT_EQ(v1.z, 3.0f);
}

TEST(Vector3Tests, CrossProduct) {
    Vector3 i(1.0f, 0.0f, 0.0f);
    Vector3 j(0.0f, 1.0f, 0.0f);
    Vector3 k = i.Cross(j);
    EXPECT_FLOAT_EQ(k.x, 0.0f);
    EXPECT_FLOAT_EQ(k.y, 0.0f);
    EXPECT_FLOAT_EQ(k.z, 1.0f);
}

TEST(Vector4Tests, Normalization) {
    Vector4 v(1.0f, 1.0f, 1.0f, 1.0f);
    Vector4 n = v.Normalized();
    EXPECT_FLOAT_EQ(n.Length(), 1.0f);
}
