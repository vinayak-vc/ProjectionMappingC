#include <gtest/gtest.h>
#include "PMSDK/Math/Quaternion.h"
#include "PMSDK/Math/Vector3.h"

using namespace pmsdk::Math;

TEST(QuaternionTests, Identity) {
    Quaternion q;
    EXPECT_FLOAT_EQ(q.x, 0.0f);
    EXPECT_FLOAT_EQ(q.y, 0.0f);
    EXPECT_FLOAT_EQ(q.z, 0.0f);
    EXPECT_FLOAT_EQ(q.w, 1.0f);
}

TEST(QuaternionTests, Inverse) {
    Quaternion q(0.0f, 1.0f, 0.0f, 1.0f); // Non-normalized for test
    Quaternion inv = q.Inverse();
    Quaternion ident = q * inv;
    EXPECT_NEAR(ident.w, 1.0f, 1e-5f);
    EXPECT_NEAR(ident.x, 0.0f, 1e-5f);
    EXPECT_NEAR(ident.y, 0.0f, 1e-5f);
    EXPECT_NEAR(ident.z, 0.0f, 1e-5f);
}

TEST(QuaternionTests, RotateVector) {
    // 90 degrees around Y
    float halfAngle = 3.14159265f / 4.0f;
    Quaternion q(0.0f, std::sin(halfAngle), 0.0f, std::cos(halfAngle));
    Vector3 v(1.0f, 0.0f, 0.0f);
    Vector3 r = q * v;
    EXPECT_NEAR(r.x, 0.0f, 1e-5f);
    EXPECT_NEAR(r.y, 0.0f, 1e-5f);
    EXPECT_NEAR(r.z, -1.0f, 1e-5f);
}

TEST(QuaternionTests, Slerp) {
    Quaternion q1(0.0f, 0.0f, 0.0f, 1.0f);
    Quaternion q2(0.0f, 1.0f, 0.0f, 0.0f);
    Quaternion qHalf = Quaternion::Slerp(q1, q2, 0.5f);
    float halfRoot = std::sqrt(2.0f) / 2.0f;
    EXPECT_NEAR(qHalf.w, halfRoot, 1e-5f);
    EXPECT_NEAR(qHalf.y, halfRoot, 1e-5f);
}
