#include <gtest/gtest.h>
#include <cmath>

#include "HoloTrack/Tracking/HeadEstimator.h"

using namespace holotrack;
using pmsdk::Math::Vector3;

namespace {
Detection MakeBboxDetection() {
    Detection d;
    d.bboxX = 0.4f; d.bboxY = 0.1f; d.bboxW = 0.2f; d.bboxH = 0.5f;
    d.spatial = {0.0f, 1.0f, 3.0f};
    d.confidence = 0.9f;
    return d;
}
} // namespace

TEST(HeadEstimatorTests, UsesEyeMidpointWhenBothEyesPresent) {
    TrackerConfig cfg;
    Detection d = MakeBboxDetection();
    d.pose.valid = true;
    d.pose.hasLeftEye = d.pose.hasRightEye = true;
    d.pose.leftEye = {0.0f, 1.7f, 2.0f};
    d.pose.rightEye = {0.2f, 1.7f, 2.0f};
    const Vector3 head = HeadEstimator::Estimate(d, cfg);
    EXPECT_NEAR(head.x, 0.1f, 1e-5f);
    EXPECT_NEAR(head.y, 1.7f, 1e-5f);
    EXPECT_NEAR(head.z, 2.0f, 1e-5f);
}

TEST(HeadEstimatorTests, FallsBackToNoseWhenNoEyes) {
    TrackerConfig cfg;
    Detection d = MakeBboxDetection();
    d.pose.valid = true;
    d.pose.hasNose = true;
    d.pose.nose = {0.5f, 1.6f, 2.5f};
    const Vector3 head = HeadEstimator::Estimate(d, cfg);
    EXPECT_NEAR(head.x, 0.5f, 1e-5f);
    EXPECT_NEAR(head.y, 1.6f, 1e-5f);
    EXPECT_NEAR(head.z, 2.5f, 1e-5f);
}

TEST(HeadEstimatorTests, BboxFallbackLiftsHeadAboveCentroid) {
    TrackerConfig cfg; // headHeightFraction 0.9, vFov 1.2
    const Detection d = MakeBboxDetection();
    const Vector3 head = HeadEstimator::Estimate(d, cfg);
    EXPECT_GT(head.y, d.spatial.y);          // head sits above the centroid
    EXPECT_FLOAT_EQ(head.x, d.spatial.x);    // X/Z untouched by the fallback
    EXPECT_FLOAT_EQ(head.z, d.spatial.z);
    // Body height = 2*Z*tan(vFov/2)*bboxH; lift = bodyHeight*(0.9-0.5).
    const float bodyH = 2.0f * 3.0f * std::tan(0.6f) * 0.5f;
    EXPECT_NEAR(head.y, 1.0f + bodyH * 0.4f, 1e-4f);
}

TEST(HeadEstimatorTests, ZeroDepthIsSafe) {
    TrackerConfig cfg;
    Detection d = MakeBboxDetection();
    d.spatial = {1.0f, 2.0f, 0.0f};
    const Vector3 head = HeadEstimator::Estimate(d, cfg);
    EXPECT_FALSE(std::isnan(head.y));
    EXPECT_FLOAT_EQ(head.y, 2.0f); // no body-height lift at zero depth
}

TEST(HeadEstimatorTests, AlwaysReturnsAPositionWithNoPose) {
    TrackerConfig cfg;
    Detection d;
    d.spatial = {0.0f, 0.0f, 2.0f};
    const Vector3 head = HeadEstimator::Estimate(d, cfg);
    EXPECT_FALSE(std::isnan(head.x));
}
