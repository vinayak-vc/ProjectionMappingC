#include <gtest/gtest.h>

#include "HoloTrack/Tracking/Filters.h"

using namespace holotrack;
using pmsdk::Math::Vector3;

TEST(FilterTests, PassThroughReturnsMeasurement) {
    PassThroughFilter f;
    const Vector3 m{1.0f, 2.0f, 3.0f};
    const Vector3 out = f.Update(m, 0.016f);
    EXPECT_FLOAT_EQ(out.x, 1.0f);
    EXPECT_FLOAT_EQ(out.y, 2.0f);
    EXPECT_FLOAT_EQ(out.z, 3.0f);
}

TEST(FilterTests, ExponentialFirstSampleIsPassThrough) {
    ExponentialFilter f(0.3f);
    const Vector3 out = f.Update({5.0f, 0.0f, 0.0f}, 0.016f);
    EXPECT_FLOAT_EQ(out.x, 5.0f);
}

TEST(FilterTests, ExponentialConvergesTowardConstant) {
    ExponentialFilter f(0.5f);
    f.Update({0.0f, 0.0f, 0.0f}, 0.016f);
    Vector3 out{};
    for (int i = 0; i < 50; ++i) {
        out = f.Update({10.0f, 0.0f, 0.0f}, 0.016f);
    }
    EXPECT_NEAR(out.x, 10.0f, 1e-3f);
}

TEST(FilterTests, ExponentialLagsBetweenOldAndNew) {
    ExponentialFilter f(0.4f);
    f.Update({0.0f, 0.0f, 0.0f}, 0.016f);
    const Vector3 out = f.Update({10.0f, 0.0f, 0.0f}, 0.016f);
    // One step of alpha=0.4 → 4.0, strictly between old (0) and new (10).
    EXPECT_NEAR(out.x, 4.0f, 1e-4f);
}

TEST(FilterTests, OneEuroFirstSamplePassThroughThenSmooths) {
    OneEuroFilter f(1.0f, 0.007f, 1.0f);
    const Vector3 first = f.Update({0.0f, 0.0f, 0.0f}, 0.016f);
    EXPECT_FLOAT_EQ(first.x, 0.0f);
    // A step should not jump the full distance in one frame at rest cutoff.
    const Vector3 second = f.Update({1.0f, 0.0f, 0.0f}, 0.016f);
    EXPECT_GT(second.x, 0.0f);
    EXPECT_LT(second.x, 1.0f);
}

TEST(FilterTests, OneEuroConvergesToConstant) {
    OneEuroFilter f(1.0f, 0.007f, 1.0f);
    Vector3 out{};
    for (int i = 0; i < 300; ++i) {
        out = f.Update({2.0f, -1.0f, 4.0f}, 0.016f);
    }
    EXPECT_NEAR(out.x, 2.0f, 1e-2f);
    EXPECT_NEAR(out.y, -1.0f, 1e-2f);
    EXPECT_NEAR(out.z, 4.0f, 1e-2f);
}

TEST(FilterTests, OneEuroReducesJitterVersusRawAmplitude) {
    OneEuroFilter f(0.5f, 0.001f, 1.0f);
    f.Update({0.0f, 0.0f, 0.0f}, 0.016f);
    float maxAbs = 0.0f;
    for (int i = 0; i < 40; ++i) {
        const float noisy = (i % 2 == 0) ? 0.5f : -0.5f; // ±0.5 jitter around 0
        const Vector3 out = f.Update({noisy, 0.0f, 0.0f}, 0.016f);
        if (std::fabs(out.x) > maxAbs) maxAbs = std::fabs(out.x);
    }
    EXPECT_LT(maxAbs, 0.5f); // smoothed amplitude strictly below the raw jitter
}

TEST(FilterTests, OneEuroDtZeroDoesNotCrashOrJump) {
    OneEuroFilter f(1.0f, 0.007f, 1.0f);
    f.Update({1.0f, 2.0f, 3.0f}, 0.016f);
    const Vector3 out = f.Update({9.0f, 9.0f, 9.0f}, 0.0f); // dt=0 → hold last
    EXPECT_NEAR(out.x, 1.0f, 1.0f); // stays near the previous output, not the new measurement
}

TEST(FilterTests, KalmanConvergesToConstant) {
    KalmanFilter f(1e-2f, 1e-1f);
    Vector3 out{};
    for (int i = 0; i < 200; ++i) {
        out = f.Update({3.0f, 0.0f, 0.0f}, 0.016f);
    }
    EXPECT_NEAR(out.x, 3.0f, 1e-1f);
}

TEST(FilterTests, KalmanTracksConstantVelocityRamp) {
    KalmanFilter f(1e-1f, 1e-2f);
    const float dt = 0.016f;
    Vector3 out{};
    for (int i = 0; i < 200; ++i) {
        const float pos = static_cast<float>(i) * dt * 2.0f; // 2 m/s ramp
        out = f.Update({pos, 0.0f, 0.0f}, dt);
    }
    const float expected = 199.0f * dt * 2.0f;
    EXPECT_NEAR(out.x, expected, 0.2f);
}

TEST(FilterTests, MakeFilterReturnsSelectedTypeAndNeverNull) {
    TrackerConfig cfg;
    for (FilterType t : {FilterType::None, FilterType::Exponential, FilterType::OneEuro, FilterType::Kalman}) {
        cfg.filterType = t;
        auto f = MakeFilter(cfg);
        ASSERT_NE(f, nullptr);
        (void)f->Update({0.0f, 0.0f, 0.0f}, 0.016f);
    }
}
