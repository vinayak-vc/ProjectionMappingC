#include <gtest/gtest.h>

#include "HoloTrack/Tracking/OffAxisProjection.h"

using namespace holotrack;
using pmsdk::Math::Vector3;
using pmsdk::Math::Matrix4;

namespace {
// A 2x2 screen centred at the origin in the z=0 plane; normal points toward +z (the viewer).
constexpr Vector3 kBL{-1.0f, -1.0f, 0.0f};
constexpr Vector3 kBR{1.0f, -1.0f, 0.0f};
constexpr Vector3 kTL{-1.0f, 1.0f, 0.0f};
constexpr float kNear = 0.1f;
constexpr float kFar = 100.0f;
} // namespace

TEST(OffAxisTests, CenteredEyeProducesSymmetricFrustum) {
    const OffAxisResult res = ComputeOffAxis(kBL, kBR, kTL, {0.0f, 0.0f, 2.0f}, kNear, kFar);
    ASSERT_TRUE(res.valid);
    EXPECT_NEAR(res.eyeToScreen, 2.0f, 1e-4f);
    // Frustum column entries (col-major): (r+l)/(r-l) at index 8, (t+b)/(t-b) at index 9.
    EXPECT_NEAR(res.projection[8], 0.0f, 1e-5f);
    EXPECT_NEAR(res.projection[9], 0.0f, 1e-5f);
}

TEST(OffAxisTests, ViewMatrixMapsEyeToOrigin) {
    const Vector3 eye{0.4f, -0.2f, 3.0f};
    const OffAxisResult res = ComputeOffAxis(kBL, kBR, kTL, eye, kNear, kFar);
    ASSERT_TRUE(res.valid);
    const Vector3 v = res.view.MultiplyPoint(eye);
    EXPECT_NEAR(v.x, 0.0f, 1e-4f);
    EXPECT_NEAR(v.y, 0.0f, 1e-4f);
    EXPECT_NEAR(v.z, 0.0f, 1e-4f);
}

TEST(OffAxisTests, CenteredEyeProjectsScreenCentreToNdcOrigin) {
    const OffAxisResult res = ComputeOffAxis(kBL, kBR, kTL, {0.0f, 0.0f, 2.0f}, kNear, kFar);
    ASSERT_TRUE(res.valid);
    const Vector3 viewPt = res.view.MultiplyPoint({0.0f, 0.0f, 0.0f}); // screen centre
    const Vector3 ndc = res.projection.MultiplyPoint(viewPt);
    EXPECT_NEAR(ndc.x, 0.0f, 1e-4f);
    EXPECT_NEAR(ndc.y, 0.0f, 1e-4f);
}

TEST(OffAxisTests, OffCenterEyeSkewsFrustumButKeepsScreenAnchored) {
    const OffAxisResult centered = ComputeOffAxis(kBL, kBR, kTL, {0.0f, 0.0f, 2.0f}, kNear, kFar);
    const OffAxisResult off = ComputeOffAxis(kBL, kBR, kTL, {0.6f, 0.0f, 2.0f}, kNear, kFar);
    ASSERT_TRUE(centered.valid);
    ASSERT_TRUE(off.valid);
    // Asymmetric frustum → non-zero horizontal offset term.
    EXPECT_GT(std::fabs(off.projection[8]), 1e-3f);
    // On-plane screen centre stays anchored at NDC origin for any eye (fixed window).
    const Vector3 ndcOnPlane = off.projection.MultiplyPoint(off.view.MultiplyPoint({0.0f, 0.0f, 0.0f}));
    EXPECT_NEAR(ndcOnPlane.x, 0.0f, 1e-4f);
    // A point OFF the plane shifts in NDC with the eye — the motion parallax a naive camera
    // translation cannot produce.
    const Vector3 qOff{0.0f, 0.0f, 1.0f};
    const float dCenter = centered.projection.MultiplyPoint(centered.view.MultiplyPoint(qOff)).x;
    const float dOff = off.projection.MultiplyPoint(off.view.MultiplyPoint(qOff)).x;
    EXPECT_GT(std::fabs(dOff - dCenter), 1e-3f);
}

TEST(OffAxisTests, EyeOnScreenPlaneIsDegenerate) {
    const OffAxisResult res = ComputeOffAxis(kBL, kBR, kTL, {0.0f, 0.0f, 0.0f}, kNear, kFar);
    EXPECT_FALSE(res.valid);
}

TEST(OffAxisTests, EyeBehindScreenIsDegenerate) {
    const OffAxisResult res = ComputeOffAxis(kBL, kBR, kTL, {0.0f, 0.0f, -1.0f}, kNear, kFar);
    EXPECT_FALSE(res.valid);
}

TEST(OffAxisTests, MovingEyeRightShiftsFrustumConsistently) {
    const OffAxisResult c = ComputeOffAxis(kBL, kBR, kTL, {0.0f, 0.0f, 2.0f}, kNear, kFar);
    const OffAxisResult r = ComputeOffAxis(kBL, kBR, kTL, {0.5f, 0.0f, 2.0f}, kNear, kFar);
    ASSERT_TRUE(c.valid);
    ASSERT_TRUE(r.valid);
    // Eye moved +x → the screen appears shifted, i.e. the frustum offset term changes sign/value.
    EXPECT_NE(c.projection[8], r.projection[8]);
}
