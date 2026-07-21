#include <gtest/gtest.h>

#include "HoloTrack/Tracking/TrackingStateMachine.h"

using namespace holotrack;

TEST(StateMachineTests, StartsSearching) {
    TrackingStateMachine sm;
    EXPECT_EQ(sm.State(), TrackingState::Searching);
}

TEST(StateMachineTests, MeasurementEntersTracking) {
    TrackingStateMachine sm;
    TrackerConfig cfg;
    EXPECT_EQ(sm.Update(true, 0.0, cfg), TrackingState::Tracking);
    EXPECT_EQ(sm.Update(true, 0.1, cfg), TrackingState::Tracking);
}

TEST(StateMachineTests, MissEntersPrediction) {
    TrackingStateMachine sm;
    TrackerConfig cfg;
    sm.Update(true, 0.0, cfg);
    EXPECT_EQ(sm.Update(false, 0.05, cfg), TrackingState::Prediction);
}

TEST(StateMachineTests, PredictionTimesOutToLostThenSearching) {
    TrackingStateMachine sm;
    TrackerConfig cfg; // lostTimeout 1.0
    sm.Update(true, 0.1, cfg);          // Tracking, lastSeen=0.1
    sm.Update(false, 0.2, cfg);         // Prediction
    EXPECT_EQ(sm.Update(false, 0.9, cfg), TrackingState::Prediction);
    EXPECT_EQ(sm.Update(false, 1.2, cfg), TrackingState::Lost); // 1.2-0.1 >= 1.0
    EXPECT_EQ(sm.Update(false, 1.3, cfg), TrackingState::Searching);
}

TEST(StateMachineTests, PredictionRecoversToTracking) {
    TrackingStateMachine sm;
    TrackerConfig cfg;
    sm.Update(true, 0.0, cfg);
    sm.Update(false, 0.1, cfg);          // Prediction
    EXPECT_EQ(sm.Update(true, 0.2, cfg), TrackingState::Tracking);
}

TEST(StateMachineTests, IsPredictingOnlyWithinPredictionWindow) {
    TrackingStateMachine sm;
    TrackerConfig cfg; // predictionTime 0.5
    sm.Update(true, 0.1, cfg);           // lastSeen=0.1
    sm.Update(false, 0.2, cfg);          // Prediction
    EXPECT_TRUE(sm.IsPredicting(0.3, cfg));   // 0.2 elapsed < 0.5
    EXPECT_FALSE(sm.IsPredicting(0.7, cfg));  // 0.6 elapsed > 0.5
}

TEST(StateMachineTests, ResetReturnsToSearching) {
    TrackingStateMachine sm;
    TrackerConfig cfg;
    sm.Update(true, 0.0, cfg);
    sm.Reset();
    EXPECT_EQ(sm.State(), TrackingState::Searching);
}
