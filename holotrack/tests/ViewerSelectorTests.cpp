#include <gtest/gtest.h>
#include <vector>

#include "HoloTrack/Tracking/ViewerSelector.h"

using namespace holotrack;
using pmsdk::Math::Vector3;

namespace {
Detection Det(float x, float y, float z, float boxW = 0.2f, float boxH = 0.4f) {
    Detection d;
    d.spatial = {x, y, z};
    d.bboxW = boxW;
    d.bboxH = boxH;
    d.confidence = 0.9f;
    return d;
}
} // namespace

TEST(ViewerSelectorTests, NearestZSelectsClosest) {
    ViewerSelector sel;
    TrackerConfig cfg; // NearestZ
    std::vector<Detection> dets{Det(0, 0, 4), Det(1, 0, 2)};
    const SelectionResult r = sel.Select(dets.data(), dets.size(), cfg);
    EXPECT_EQ(r.index, 1);
    EXPECT_EQ(r.id, 1);
}

TEST(ViewerSelectorTests, DepthGateRejectsOutOfRange) {
    ViewerSelector sel;
    TrackerConfig cfg;
    cfg.minDepth = 0.5f; cfg.maxDepth = 6.0f;
    std::vector<Detection> dets{Det(0, 0, 10.0f), Det(0, 0, 3.0f)};
    const SelectionResult r = sel.Select(dets.data(), dets.size(), cfg);
    EXPECT_EQ(r.index, 1); // the z=10 detection is gated out
}

TEST(ViewerSelectorTests, EmptyFrameReturnsNoSelection) {
    ViewerSelector sel;
    TrackerConfig cfg;
    const SelectionResult r = sel.Select(nullptr, 0, cfg);
    EXPECT_EQ(r.index, -1);
    EXPECT_EQ(r.id, -1);
}

TEST(ViewerSelectorTests, LargestBoxModeSelectsLargest) {
    ViewerSelector sel;
    TrackerConfig cfg;
    cfg.selection = SelectionMode::LargestBox;
    std::vector<Detection> dets{Det(0, 0, 2, 0.1f, 0.2f), Det(1, 0, 4, 0.5f, 0.6f)};
    const SelectionResult r = sel.Select(dets.data(), dets.size(), cfg);
    EXPECT_EQ(r.index, 1); // larger box wins despite being farther
}

TEST(ViewerSelectorTests, IdStableWhileViewerPersists) {
    ViewerSelector sel;
    TrackerConfig cfg;
    std::vector<Detection> dets{Det(0, 0, 3)};
    const int id0 = sel.Select(dets.data(), dets.size(), cfg).id;
    for (int i = 0; i < 10; ++i) {
        const SelectionResult r = sel.Select(dets.data(), dets.size(), cfg);
        EXPECT_EQ(r.id, id0);
    }
}

TEST(ViewerSelectorTests, HysteresisDelaysSwitchToChallenger) {
    ViewerSelector sel;
    TrackerConfig cfg;
    cfg.hysteresisFrames = 3;

    // Frame 0: only the incumbent present → adopt id 1.
    std::vector<Detection> only{Det(0, 0, 3)};
    const SelectionResult first = sel.Select(only.data(), only.size(), cfg);
    ASSERT_EQ(first.id, 1);

    // Now a closer challenger appears far from the incumbent; both present each frame.
    std::vector<Detection> both{Det(0, 0, 3), Det(2, 0, 1)};
    const SelectionResult f1 = sel.Select(both.data(), both.size(), cfg);
    EXPECT_EQ(f1.id, 1);      // hysteresis holds the incumbent
    EXPECT_EQ(f1.index, 0);
    const SelectionResult f2 = sel.Select(both.data(), both.size(), cfg);
    EXPECT_EQ(f2.id, 1);
    const SelectionResult f3 = sel.Select(both.data(), both.size(), cfg);
    EXPECT_EQ(f3.index, 1);   // challenger sustained → switch
    EXPECT_EQ(f3.id, 2);      // new identity
}

TEST(ViewerSelectorTests, IncumbentMissingDoesNotHopToOthers) {
    ViewerSelector sel;
    TrackerConfig cfg;
    std::vector<Detection> only{Det(0, 0, 3)};
    const int id0 = sel.Select(only.data(), only.size(), cfg).id;

    // A different person, far outside the association gate, is the only detection.
    std::vector<Detection> other{Det(5, 0, 2)};
    const SelectionResult r = sel.Select(other.data(), other.size(), cfg);
    EXPECT_EQ(r.index, -1);   // no measurement for the incumbent this frame
    EXPECT_EQ(r.id, id0);     // identity preserved for prediction
}

TEST(ViewerSelectorTests, ResetAllowsFreshAdoption) {
    ViewerSelector sel;
    TrackerConfig cfg;
    std::vector<Detection> a{Det(0, 0, 3)};
    const int id0 = sel.Select(a.data(), a.size(), cfg).id;
    sel.Reset();
    std::vector<Detection> b{Det(1, 0, 2)};
    const SelectionResult r = sel.Select(b.data(), b.size(), cfg);
    EXPECT_EQ(r.index, 0);
    EXPECT_GT(r.id, id0); // a new identity after reset
}
