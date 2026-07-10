#include <gtest/gtest.h>
#include "PMSDK/Math/Matrix4.h"

using namespace pmsdk::Math;

TEST(Matrix4Tests, Identity) {
    Matrix4 m;
    EXPECT_FLOAT_EQ(m[0], 1.0f);
    EXPECT_FLOAT_EQ(m[15], 1.0f);
}

TEST(Matrix4Tests, MultiplyPoint) {
    Matrix4 t = Matrix4::Translation(Vector3(1.0f, 2.0f, 3.0f));
    Vector3 p = t.MultiplyPoint(Vector3(0.0f, 0.0f, 0.0f));
    EXPECT_FLOAT_EQ(p.x, 1.0f);
    EXPECT_FLOAT_EQ(p.y, 2.0f);
    EXPECT_FLOAT_EQ(p.z, 3.0f);
}

TEST(Matrix4Tests, Inverse) {
    Matrix4 t = Matrix4::Translation(Vector3(1.0f, 2.0f, 3.0f));
    Matrix4 inv = t.Inverse();
    Matrix4 ident = t * inv;
    EXPECT_NEAR(ident[0], 1.0f, 1e-5f);
    EXPECT_NEAR(ident[12], 0.0f, 1e-5f); // Translation part should be 0
}

TEST(Matrix4Tests, Rotation) {
    float halfAngle = 3.14159265f / 4.0f;
    Quaternion q(0.0f, std::sin(halfAngle), 0.0f, std::cos(halfAngle));
    Matrix4 r = Matrix4::Rotation(q);
    Vector3 p = r.MultiplyPoint(Vector3(1.0f, 0.0f, 0.0f));
    EXPECT_NEAR(p.x, 0.0f, 1e-5f);
    EXPECT_NEAR(p.z, -1.0f, 1e-5f);
}

TEST(Matrix4Tests, Perspective) {
    Matrix4 p = Matrix4::Perspective(3.14159f / 2.0f, 1.0f, 0.1f, 100.0f);
    // Project point at z=-10
    Vector4 pt(0.0f, 0.0f, -10.0f, 1.0f);
    Vector4 proj = p * pt;
    EXPECT_NEAR(proj.w, 10.0f, 1e-5f);
}
