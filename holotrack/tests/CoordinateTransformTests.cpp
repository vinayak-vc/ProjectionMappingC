#include <gtest/gtest.h>
#include <cmath>

#include "HoloTrack/Tracking/CoordinateTransform.h"

using namespace holotrack;
using pmsdk::Math::Vector3;
using pmsdk::Math::Quaternion;

TEST(CoordinateTransformTests, IdentityIsNoOp) {
    CalibrationTransform c;
    const Vector3 p{1.0f, 2.0f, 3.0f};
    const Vector3 w = CoordinateTransform::CameraToWorld(c, p);
    EXPECT_FLOAT_EQ(w.x, 1.0f);
    EXPECT_FLOAT_EQ(w.y, 2.0f);
    EXPECT_FLOAT_EQ(w.z, 3.0f);
}

TEST(CoordinateTransformTests, TranslationApplies) {
    CalibrationTransform c;
    c.translation = {10.0f, -5.0f, 2.0f};
    const Vector3 w = CoordinateTransform::CameraToWorld(c, {1.0f, 1.0f, 1.0f});
    EXPECT_FLOAT_EQ(w.x, 11.0f);
    EXPECT_FLOAT_EQ(w.y, -4.0f);
    EXPECT_FLOAT_EQ(w.z, 3.0f);
}

TEST(CoordinateTransformTests, ScaleApplies) {
    CalibrationTransform c;
    c.scale = 2.0f;
    const Vector3 w = CoordinateTransform::CameraToWorld(c, {1.0f, 2.0f, 3.0f});
    EXPECT_FLOAT_EQ(w.x, 2.0f);
    EXPECT_FLOAT_EQ(w.y, 4.0f);
    EXPECT_FLOAT_EQ(w.z, 6.0f);
}

TEST(CoordinateTransformTests, YawRotationRotatesXToZ) {
    CalibrationTransform c;
    // 90° about +Y: +X → -Z (right-handed).
    const float half = 3.14159265358979323846f * 0.25f;
    c.rotation = Quaternion(0.0f, std::sin(half), 0.0f, std::cos(half));
    const Vector3 w = CoordinateTransform::CameraToWorld(c, {1.0f, 0.0f, 0.0f});
    EXPECT_NEAR(w.x, 0.0f, 1e-5f);
    EXPECT_NEAR(w.y, 0.0f, 1e-5f);
    EXPECT_NEAR(w.z, -1.0f, 1e-5f);
}

TEST(CoordinateTransformTests, RoundTripWorldCameraIsIdentity) {
    CalibrationTransform c;
    c.translation = {3.0f, -1.0f, 4.0f};
    c.scale = 1.5f;
    const float half = 3.14159265358979323846f * 0.2f;
    c.rotation = Quaternion(std::sin(half) * 0.0f, std::sin(half), 0.0f, std::cos(half));
    const Vector3 p{0.7f, -0.3f, 2.2f};
    const Vector3 w = CoordinateTransform::CameraToWorld(c, p);
    const Vector3 back = CoordinateTransform::WorldToCamera(c, w);
    EXPECT_NEAR(back.x, p.x, 1e-4f);
    EXPECT_NEAR(back.y, p.y, 1e-4f);
    EXPECT_NEAR(back.z, p.z, 1e-4f);
}
