#include <gtest/gtest.h>
#include <vector>

#include "HoloTrack/Tracking/Tracker.h"

using namespace holotrack;
using pmsdk::Math::Vector3;

namespace {
// A detection whose pose eyes coincide at `head`, so the head estimate is exactly `head`.
Detection HeadAt(const Vector3& head) {
    Detection d;
    d.spatial = head;            // also the centroid/depth
    d.bboxW = 0.2f; d.bboxH = 0.4f;
    d.confidence = 0.95f;
    d.pose.valid = true;
    d.pose.hasLeftEye = d.pose.hasRightEye = true;
    d.pose.leftEye = head;
    d.pose.rightEye = head;
    return d;
}
} // namespace

TEST(TrackerTests, AcquiresViewerOnFirstDetection) {
    TrackerConfig cfg;
    Tracker t(cfg);
    std::vector<Detection> dets{HeadAt({0.3f, 1.6f, 2.5f})};
    t.PushFrame(dets.data(), dets.size(), 0.0);

    const TrackedViewer& v = t.GetViewer();
    EXPECT_TRUE(v.valid);
    EXPECT_EQ(v.state, TrackingState::Tracking);
    EXPECT_EQ(v.id, 1);
    EXPECT_NEAR(v.headWorld.x, 0.3f, 1e-4f); // identity calibration
    EXPECT_NEAR(v.headWorld.y, 1.6f, 1e-4f);
    EXPECT_NEAR(v.headWorld.z, 2.5f, 1e-4f);
}

TEST(TrackerTests, IdStableAcrossManyFrames) {
    TrackerConfig cfg;
    Tracker t(cfg);
    double ts = 0.0;
    for (int i = 0; i < 30; ++i) {
        std::vector<Detection> dets{HeadAt({0.0f, 1.6f, 3.0f})};
        t.PushFrame(dets.data(), dets.size(), ts);
        ts += 0.016;
        EXPECT_EQ(t.GetViewer().id, 1);
    }
}

TEST(TrackerTests, PredictionExtrapolatesFromVelocity) {
    TrackerConfig cfg;
    cfg.filterType = FilterType::None; // deterministic velocity
    Tracker t(cfg);
    t.PushFrame(std::vector<Detection>{HeadAt({0.0f, 1.6f, 3.0f})}.data(), 1, 0.0);
    t.PushFrame(std::vector<Detection>{HeadAt({0.1f, 1.6f, 3.0f})}.data(), 1, 0.1);
    t.PushFrame(std::vector<Detection>{HeadAt({0.2f, 1.6f, 3.0f})}.data(), 1, 0.2);

    // Missed frame → prediction should continue the +1 m/s x motion.
    t.PushFrame(nullptr, 0, 0.3);
    const TrackedViewer& v = t.GetViewer();
    EXPECT_EQ(v.state, TrackingState::Prediction);
    EXPECT_TRUE(v.valid);
    EXPECT_NEAR(v.headWorld.x, 0.3f, 1e-3f);
}

TEST(TrackerTests, LostAfterTimeoutInvalidatesViewer) {
    TrackerConfig cfg;
    cfg.filterType = FilterType::None;
    cfg.lostTimeoutSeconds = 0.5f;
    Tracker t(cfg);
    t.PushFrame(std::vector<Detection>{HeadAt({0.0f, 1.6f, 3.0f})}.data(), 1, 0.0);

    // Advance past the lost timeout with no detections.
    bool sawLost = false;
    double ts = 0.1;
    for (int i = 0; i < 20 && !sawLost; ++i) {
        t.PushFrame(nullptr, 0, ts);
        if (t.GetViewer().state == TrackingState::Lost) sawLost = true;
        ts += 0.1;
    }
    EXPECT_TRUE(sawLost);
    EXPECT_FALSE(t.GetViewer().valid);
    EXPECT_EQ(t.GetViewer().id, -1);

    // A new person afterwards gets a fresh identity.
    t.PushFrame(std::vector<Detection>{HeadAt({0.0f, 1.6f, 3.0f})}.data(), 1, ts);
    EXPECT_TRUE(t.GetViewer().valid);
    EXPECT_GT(t.GetViewer().id, 1);
}

TEST(TrackerTests, ComputeOffAxisUsesTrackedHeadWorld) {
    TrackerConfig cfg;
    cfg.filterType = FilterType::None;
    Tracker t(cfg);
    const Vector3 head{0.2f, 0.0f, 2.0f};
    t.PushFrame(std::vector<Detection>{HeadAt(head)}.data(), 1, 0.0);

    const Vector3 bl{-1, -1, 0}, br{1, -1, 0}, tl{-1, 1, 0};
    const OffAxisResult viaTracker = t.ComputeOffAxis(bl, br, tl, 0.1f, 100.0f);
    const OffAxisResult direct = ComputeOffAxis(bl, br, tl, head, 0.1f, 100.0f);
    ASSERT_TRUE(viaTracker.valid);
    ASSERT_TRUE(direct.valid);
    EXPECT_NEAR(viaTracker.eyeToScreen, direct.eyeToScreen, 1e-5f);
    EXPECT_NEAR(viaTracker.projection[8], direct.projection[8], 1e-5f);
}

TEST(TrackerTests, SetConfigSwitchingFilterKeepsTracking) {
    TrackerConfig cfg;
    cfg.filterType = FilterType::OneEuro;
    Tracker t(cfg);
    t.PushFrame(std::vector<Detection>{HeadAt({0.0f, 1.6f, 3.0f})}.data(), 1, 0.0);

    cfg.filterType = FilterType::Kalman;
    t.SetConfig(cfg);
    t.PushFrame(std::vector<Detection>{HeadAt({0.05f, 1.6f, 3.0f})}.data(), 1, 0.016);
    EXPECT_TRUE(t.GetViewer().valid);
    EXPECT_EQ(t.GetConfig().filterType, FilterType::Kalman);
}
