/**
 * @file HoloTrackHarness.cpp
 * @brief Dependency-free verification harness for the HoloTrack core logic.
 *
 * Mirrors the GoogleTest suites (kept alongside for CI) but links nothing third-party, so it
 * builds and runs under the local MSVC toolset regardless of the prebuilt-gtest toolset gap.
 * Exit code 0 = all checks passed, non-zero = failures (also printed).
 */
#include <cmath>
#include <cstdio>
#include <vector>

#include "HoloTrack/Tracking/Filters.h"
#include "HoloTrack/Tracking/OffAxisProjection.h"
#include "HoloTrack/Tracking/CoordinateTransform.h"
#include "HoloTrack/Tracking/HeadEstimator.h"
#include "HoloTrack/Tracking/ViewerSelector.h"
#include "HoloTrack/Tracking/TrackingStateMachine.h"
#include "HoloTrack/Tracking/Tracker.h"
#include "HoloTrack/C_API/TrackingAPI.h"

using namespace holotrack;
using pmsdk::Math::Vector3;
using pmsdk::Math::Quaternion;

namespace {
int g_checks = 0;
int g_failures = 0;

void CheckTrue(bool cond, const char* expr, const char* file, int line) {
    ++g_checks;
    if (!cond) {
        ++g_failures;
        std::printf("FAIL %s:%d: expected true: %s\n", file, line, expr);
    }
}

void CheckNear(double a, double b, double tol, const char* ea, const char* eb, const char* file, int line) {
    ++g_checks;
    if (std::fabs(a - b) > tol) {
        ++g_failures;
        std::printf("FAIL %s:%d: |%s - %s| > %g  (%.6f vs %.6f)\n", file, line, ea, eb, tol, a, b);
    }
}

constexpr float kPi = 3.14159265358979323846f;
} // namespace

#define CHECK(cond) CheckTrue((cond), #cond, __FILE__, __LINE__)
#define NEAR(a, b, tol) CheckNear((double)(a), (double)(b), (double)(tol), #a, #b, __FILE__, __LINE__)

// ---------------------------------------------------------------------------- Filters
static void TestFilters() {
    PassThroughFilter pt;
    const Vector3 p = pt.Update({1.0f, 2.0f, 3.0f}, 0.016f);
    NEAR(p.x, 1.0f, 1e-6); NEAR(p.y, 2.0f, 1e-6); NEAR(p.z, 3.0f, 1e-6);

    ExponentialFilter e(0.4f);
    NEAR(e.Update({0, 0, 0}, 0.016f).x, 0.0f, 1e-6);          // first = passthrough
    NEAR(e.Update({10, 0, 0}, 0.016f).x, 4.0f, 1e-4);          // one step of alpha 0.4
    Vector3 eo{};
    for (int i = 0; i < 60; ++i) eo = e.Update({10, 0, 0}, 0.016f);
    NEAR(eo.x, 10.0f, 1e-3);                                    // converges

    OneEuroFilter oe(1.0f, 0.007f, 1.0f);
    NEAR(oe.Update({0, 0, 0}, 0.016f).x, 0.0f, 1e-6);
    const float step = oe.Update({1, 0, 0}, 0.016f).x;
    CHECK(step > 0.0f && step < 1.0f);
    Vector3 oeo{};
    for (int i = 0; i < 300; ++i) oeo = oe.Update({2, -1, 4}, 0.016f);
    NEAR(oeo.x, 2.0f, 1e-2); NEAR(oeo.y, -1.0f, 1e-2); NEAR(oeo.z, 4.0f, 1e-2);

    // Jitter reduction: smoothed amplitude below the ±0.5 raw jitter.
    OneEuroFilter oj(0.5f, 0.001f, 1.0f);
    oj.Update({0, 0, 0}, 0.016f);
    float maxAbs = 0.0f;
    for (int i = 0; i < 40; ++i) {
        const float noisy = (i % 2 == 0) ? 0.5f : -0.5f;
        const float y = oj.Update({noisy, 0, 0}, 0.016f).x;
        if (std::fabs(y) > maxAbs) maxAbs = std::fabs(y);
    }
    CHECK(maxAbs < 0.5f);

    // dt=0 holds the last output rather than jumping.
    OneEuroFilter od(1.0f, 0.007f, 1.0f);
    od.Update({1, 2, 3}, 0.016f);
    NEAR(od.Update({9, 9, 9}, 0.0f).x, 1.0f, 1.0);

    KalmanFilter k(1e-2f, 1e-1f);
    Vector3 ko{};
    for (int i = 0; i < 200; ++i) ko = k.Update({3, 0, 0}, 0.016f);
    NEAR(ko.x, 3.0f, 1e-1);

    KalmanFilter kr(1e-1f, 1e-2f);
    const float dt = 0.016f;
    Vector3 kro{};
    for (int i = 0; i < 200; ++i) kro = kr.Update({static_cast<float>(i) * dt * 2.0f, 0, 0}, dt);
    NEAR(kro.x, 199.0f * dt * 2.0f, 0.2);

    TrackerConfig cfg;
    for (FilterType t : {FilterType::None, FilterType::Exponential, FilterType::OneEuro, FilterType::Kalman}) {
        cfg.filterType = t;
        auto f = MakeFilter(cfg);
        CHECK(f != nullptr);
        (void)f->Update({0, 0, 0}, 0.016f);
    }
}

// ---------------------------------------------------------------- Off-axis projection
static void TestOffAxis() {
    const Vector3 bl{-1, -1, 0}, br{1, -1, 0}, tl{-1, 1, 0};
    const float n = 0.1f, f = 100.0f;

    OffAxisResult c = ComputeOffAxis(bl, br, tl, {0, 0, 2}, n, f);
    CHECK(c.valid);
    NEAR(c.eyeToScreen, 2.0f, 1e-4);
    NEAR(c.projection[8], 0.0f, 1e-5);   // symmetric frustum, no horizontal skew
    NEAR(c.projection[9], 0.0f, 1e-5);

    // View maps the eye to the origin.
    const Vector3 eye{0.4f, -0.2f, 3.0f};
    OffAxisResult v = ComputeOffAxis(bl, br, tl, eye, n, f);
    CHECK(v.valid);
    const Vector3 ve = v.view.MultiplyPoint(eye);
    NEAR(ve.x, 0.0f, 1e-4); NEAR(ve.y, 0.0f, 1e-4); NEAR(ve.z, 0.0f, 1e-4);

    // Centered eye → screen centre at NDC origin.
    const Vector3 ndcC = c.projection.MultiplyPoint(c.view.MultiplyPoint({0, 0, 0}));
    NEAR(ndcC.x, 0.0f, 1e-4); NEAR(ndcC.y, 0.0f, 1e-4);

    // Off-centre eye → skewed (asymmetric) frustum.
    OffAxisResult o = ComputeOffAxis(bl, br, tl, {0.6f, 0, 2}, n, f);
    CHECK(o.valid);
    CHECK(std::fabs(o.projection[8]) > 1e-3f);
    // The on-plane screen centre stays anchored at NDC origin for ANY eye (the window is fixed).
    const Vector3 ndcOnPlane = o.projection.MultiplyPoint(o.view.MultiplyPoint({0, 0, 0}));
    NEAR(ndcOnPlane.x, 0.0f, 1e-4);
    // A point OFF the screen plane shifts in NDC as the eye moves — this is the motion parallax
    // a naive camera translation cannot reproduce.
    const Vector3 qOff{0, 0, 1};
    const float ndcCenterOff = c.projection.MultiplyPoint(c.view.MultiplyPoint(qOff)).x;
    const float ndcSkewOff = o.projection.MultiplyPoint(o.view.MultiplyPoint(qOff)).x;
    CHECK(std::fabs(ndcSkewOff - ndcCenterOff) > 1e-3f);

    // Degenerate: eye on / behind the screen plane.
    CHECK(!ComputeOffAxis(bl, br, tl, {0, 0, 0}, n, f).valid);
    CHECK(!ComputeOffAxis(bl, br, tl, {0, 0, -1}, n, f).valid);
}

// -------------------------------------------------------------- Coordinate transform
static void TestCoordinateTransform() {
    CalibrationTransform id;
    const Vector3 a = CoordinateTransform::CameraToWorld(id, {1, 2, 3});
    NEAR(a.x, 1.0f, 1e-6); NEAR(a.y, 2.0f, 1e-6); NEAR(a.z, 3.0f, 1e-6);

    CalibrationTransform tr;
    tr.translation = {10, -5, 2};
    const Vector3 b = CoordinateTransform::CameraToWorld(tr, {1, 1, 1});
    NEAR(b.x, 11.0f, 1e-6); NEAR(b.y, -4.0f, 1e-6); NEAR(b.z, 3.0f, 1e-6);

    CalibrationTransform sc; sc.scale = 2.0f;
    const Vector3 s = CoordinateTransform::CameraToWorld(sc, {1, 2, 3});
    NEAR(s.x, 2.0f, 1e-6); NEAR(s.z, 6.0f, 1e-6);

    CalibrationTransform yaw;
    const float h = kPi * 0.25f;
    yaw.rotation = Quaternion(0.0f, std::sin(h), 0.0f, std::cos(h)); // 90° about +Y
    const Vector3 w = CoordinateTransform::CameraToWorld(yaw, {1, 0, 0});
    NEAR(w.x, 0.0f, 1e-5); NEAR(w.z, -1.0f, 1e-5);

    CalibrationTransform full;
    full.translation = {3, -1, 4}; full.scale = 1.5f;
    const float h2 = kPi * 0.2f;
    full.rotation = Quaternion(0.0f, std::sin(h2), 0.0f, std::cos(h2));
    const Vector3 p{0.7f, -0.3f, 2.2f};
    const Vector3 back = CoordinateTransform::WorldToCamera(full, CoordinateTransform::CameraToWorld(full, p));
    NEAR(back.x, p.x, 1e-4); NEAR(back.y, p.y, 1e-4); NEAR(back.z, p.z, 1e-4);
}

// ------------------------------------------------------------------- Head estimator
static void TestHeadEstimator() {
    TrackerConfig cfg;
    Detection d;
    d.bboxX = 0.4f; d.bboxY = 0.1f; d.bboxW = 0.2f; d.bboxH = 0.5f;
    d.spatial = {0, 1, 3};

    Detection eyes = d;
    eyes.pose.valid = true; eyes.pose.hasLeftEye = eyes.pose.hasRightEye = true;
    eyes.pose.leftEye = {0.0f, 1.7f, 2.0f}; eyes.pose.rightEye = {0.2f, 1.7f, 2.0f};
    const Vector3 he = HeadEstimator::Estimate(eyes, cfg);
    NEAR(he.x, 0.1f, 1e-5); NEAR(he.y, 1.7f, 1e-5); NEAR(he.z, 2.0f, 1e-5);

    Detection nose = d;
    nose.pose.valid = true; nose.pose.hasNose = true; nose.pose.nose = {0.5f, 1.6f, 2.5f};
    const Vector3 hn = HeadEstimator::Estimate(nose, cfg);
    NEAR(hn.x, 0.5f, 1e-5); NEAR(hn.z, 2.5f, 1e-5);

    const Vector3 hf = HeadEstimator::Estimate(d, cfg);   // bbox fallback
    CHECK(hf.y > d.spatial.y);
    NEAR(hf.x, d.spatial.x, 1e-6); NEAR(hf.z, d.spatial.z, 1e-6);
    const float bodyH = 2.0f * 3.0f * std::tan(0.6f) * 0.5f;
    NEAR(hf.y, 1.0f + bodyH * 0.4f, 1e-4);

    Detection zero = d; zero.spatial = {1, 2, 0};
    const Vector3 hz = HeadEstimator::Estimate(zero, cfg);
    CHECK(!std::isnan(hz.y));
    NEAR(hz.y, 2.0f, 1e-6);
}

// ------------------------------------------------------------------ Viewer selector
static Detection Det(float x, float y, float z, float w = 0.2f, float h = 0.4f) {
    Detection d; d.spatial = {x, y, z}; d.bboxW = w; d.bboxH = h; d.confidence = 0.9f; return d;
}

static void TestViewerSelector() {
    {
        ViewerSelector sel; TrackerConfig cfg;
        std::vector<Detection> dets{Det(0, 0, 4), Det(1, 0, 2)};
        const SelectionResult r = sel.Select(dets.data(), dets.size(), cfg);
        CHECK(r.index == 1); CHECK(r.id == 1);
    }
    {
        ViewerSelector sel; TrackerConfig cfg; cfg.minDepth = 0.5f; cfg.maxDepth = 6.0f;
        std::vector<Detection> dets{Det(0, 0, 10.0f), Det(0, 0, 3.0f)};
        CHECK(sel.Select(dets.data(), dets.size(), cfg).index == 1);
    }
    {
        ViewerSelector sel; TrackerConfig cfg;
        const SelectionResult r = sel.Select(nullptr, 0, cfg);
        CHECK(r.index == -1); CHECK(r.id == -1);
    }
    {
        ViewerSelector sel; TrackerConfig cfg; cfg.selection = SelectionMode::LargestBox;
        std::vector<Detection> dets{Det(0, 0, 2, 0.1f, 0.2f), Det(1, 0, 4, 0.5f, 0.6f)};
        CHECK(sel.Select(dets.data(), dets.size(), cfg).index == 1);
    }
    {
        ViewerSelector sel; TrackerConfig cfg;
        std::vector<Detection> dets{Det(0, 0, 3)};
        const int id0 = sel.Select(dets.data(), dets.size(), cfg).id;
        for (int i = 0; i < 10; ++i) CHECK(sel.Select(dets.data(), dets.size(), cfg).id == id0);
    }
    {
        ViewerSelector sel; TrackerConfig cfg; cfg.hysteresisFrames = 3;
        std::vector<Detection> only{Det(0, 0, 3)};
        CHECK(sel.Select(only.data(), only.size(), cfg).id == 1);
        std::vector<Detection> both{Det(0, 0, 3), Det(2, 0, 1)};
        CHECK(sel.Select(both.data(), both.size(), cfg).id == 1);
        CHECK(sel.Select(both.data(), both.size(), cfg).id == 1);
        const SelectionResult f3 = sel.Select(both.data(), both.size(), cfg);
        CHECK(f3.index == 1); CHECK(f3.id == 2);
    }
    {
        ViewerSelector sel; TrackerConfig cfg;
        std::vector<Detection> only{Det(0, 0, 3)};
        const int id0 = sel.Select(only.data(), only.size(), cfg).id;
        std::vector<Detection> other{Det(5, 0, 2)};
        const SelectionResult r = sel.Select(other.data(), other.size(), cfg);
        CHECK(r.index == -1); CHECK(r.id == id0);
    }
    {
        ViewerSelector sel; TrackerConfig cfg;
        std::vector<Detection> a{Det(0, 0, 3)};
        const int id0 = sel.Select(a.data(), a.size(), cfg).id;
        sel.Reset();
        std::vector<Detection> b{Det(1, 0, 2)};
        const SelectionResult r = sel.Select(b.data(), b.size(), cfg);
        CHECK(r.index == 0); CHECK(r.id > id0);
    }
}

// ------------------------------------------------------------------- State machine
static void TestStateMachine() {
    TrackerConfig cfg;
    {
        TrackingStateMachine sm;
        CHECK(sm.State() == TrackingState::Searching);
        CHECK(sm.Update(true, 0.0, cfg) == TrackingState::Tracking);
        CHECK(sm.Update(true, 0.1, cfg) == TrackingState::Tracking);
    }
    {
        TrackingStateMachine sm;
        sm.Update(true, 0.0, cfg);
        CHECK(sm.Update(false, 0.05, cfg) == TrackingState::Prediction);
    }
    {
        TrackingStateMachine sm;
        sm.Update(true, 0.1, cfg);
        sm.Update(false, 0.2, cfg);
        CHECK(sm.Update(false, 0.9, cfg) == TrackingState::Prediction);
        CHECK(sm.Update(false, 1.2, cfg) == TrackingState::Lost);
        CHECK(sm.Update(false, 1.3, cfg) == TrackingState::Searching);
    }
    {
        TrackingStateMachine sm;
        sm.Update(true, 0.0, cfg);
        sm.Update(false, 0.1, cfg);
        CHECK(sm.Update(true, 0.2, cfg) == TrackingState::Tracking);
    }
    {
        TrackingStateMachine sm;
        sm.Update(true, 0.1, cfg);
        sm.Update(false, 0.2, cfg);
        CHECK(sm.IsPredicting(0.3, cfg));
        CHECK(!sm.IsPredicting(0.7, cfg));
    }
}

// ------------------------------------------------------------------------- Tracker
static Detection HeadAt(const Vector3& head) {
    Detection d; d.spatial = head; d.bboxW = 0.2f; d.bboxH = 0.4f; d.confidence = 0.95f;
    d.pose.valid = true; d.pose.hasLeftEye = d.pose.hasRightEye = true;
    d.pose.leftEye = head; d.pose.rightEye = head;
    return d;
}

static void TestTracker() {
    {
        Tracker t{TrackerConfig{}};
        std::vector<Detection> dets{HeadAt({0.3f, 1.6f, 2.5f})};
        t.PushFrame(dets.data(), dets.size(), 0.0);
        const TrackedViewer& v = t.GetViewer();
        CHECK(v.valid); CHECK(v.state == TrackingState::Tracking); CHECK(v.id == 1);
        NEAR(v.headWorld.x, 0.3f, 1e-4); NEAR(v.headWorld.y, 1.6f, 1e-4); NEAR(v.headWorld.z, 2.5f, 1e-4);
    }
    {
        Tracker t{TrackerConfig{}};
        double ts = 0.0;
        for (int i = 0; i < 30; ++i) {
            std::vector<Detection> dets{HeadAt({0, 1.6f, 3.0f})};
            t.PushFrame(dets.data(), dets.size(), ts);
            ts += 0.016;
            CHECK(t.GetViewer().id == 1);
        }
    }
    {
        TrackerConfig cfg; cfg.filterType = FilterType::None;
        Tracker t{cfg};
        std::vector<Detection> a{HeadAt({0.0f, 1.6f, 3.0f})}; t.PushFrame(a.data(), 1, 0.0);
        std::vector<Detection> b{HeadAt({0.1f, 1.6f, 3.0f})}; t.PushFrame(b.data(), 1, 0.1);
        std::vector<Detection> c{HeadAt({0.2f, 1.6f, 3.0f})}; t.PushFrame(c.data(), 1, 0.2);
        t.PushFrame(nullptr, 0, 0.3);
        const TrackedViewer& v = t.GetViewer();
        CHECK(v.state == TrackingState::Prediction); CHECK(v.valid);
        NEAR(v.headWorld.x, 0.3f, 1e-3);
    }
    {
        TrackerConfig cfg; cfg.filterType = FilterType::None; cfg.lostTimeoutSeconds = 0.5f;
        Tracker t{cfg};
        std::vector<Detection> a{HeadAt({0.0f, 1.6f, 3.0f})}; t.PushFrame(a.data(), 1, 0.0);
        bool sawLost = false; double ts = 0.1;
        for (int i = 0; i < 20 && !sawLost; ++i) {
            t.PushFrame(nullptr, 0, ts);
            if (t.GetViewer().state == TrackingState::Lost) sawLost = true;
            ts += 0.1;
        }
        CHECK(sawLost); CHECK(!t.GetViewer().valid); CHECK(t.GetViewer().id == -1);
        std::vector<Detection> again{HeadAt({0.0f, 1.6f, 3.0f})};
        t.PushFrame(again.data(), 1, ts);
        CHECK(t.GetViewer().valid); CHECK(t.GetViewer().id > 1);
    }
    {
        TrackerConfig cfg; cfg.filterType = FilterType::None;
        Tracker t{cfg};
        const Vector3 head{0.2f, 0.0f, 2.0f};
        std::vector<Detection> d{HeadAt(head)}; t.PushFrame(d.data(), 1, 0.0);
        const Vector3 bl{-1, -1, 0}, br{1, -1, 0}, tl{-1, 1, 0};
        const OffAxisResult viaT = t.ComputeOffAxis(bl, br, tl, 0.1f, 100.0f);
        const OffAxisResult direct = ComputeOffAxis(bl, br, tl, head, 0.1f, 100.0f);
        CHECK(viaT.valid); CHECK(direct.valid);
        NEAR(viaT.eyeToScreen, direct.eyeToScreen, 1e-5);
        NEAR(viaT.projection[8], direct.projection[8], 1e-5);
    }
    {
        TrackerConfig cfg; cfg.filterType = FilterType::OneEuro;
        Tracker t{cfg};
        std::vector<Detection> a{HeadAt({0.0f, 1.6f, 3.0f})}; t.PushFrame(a.data(), 1, 0.0);
        cfg.filterType = FilterType::Kalman; t.SetConfig(cfg);
        std::vector<Detection> b{HeadAt({0.05f, 1.6f, 3.0f})}; t.PushFrame(b.data(), 1, 0.016);
        CHECK(t.GetViewer().valid); CHECK(t.GetConfig().filterType == FilterType::Kalman);
    }
}

// --------------------------------------------------------------------------- C-API
static void TestCApi() {
    // A default config round-trips through the C boundary and drives tracking + off-axis.
    ht_tracker_config_t cfg{};
    cfg.filterType = HT_FILTER_NONE;
    cfg.selection = HT_SELECT_NEAREST_Z;
    cfg.hysteresisMargin = 0.15f; cfg.hysteresisFrames = 8; cfg.associationMaxDistance = 0.6f;
    cfg.minDepth = 0.3f; cfg.maxDepth = 6.0f;
    cfg.predictionTimeSeconds = 0.5f; cfg.lostTimeoutSeconds = 1.0f;
    cfg.cameraVFovRad = 1.2f; cfg.headHeightFraction = 0.9f;
    cfg.movementScale = 1.0f; cfg.maxDistance = 3.0f;
    cfg.calibRotation.w = 1.0f; cfg.calibScale = 1.0f;

    int major = -1, minor = -1, patch = -1;
    ht_get_version(&major, &minor, &patch);
    CHECK(major >= 1);

    ht_tracker_t* t = ht_tracker_create(&cfg);
    CHECK(t != nullptr);

    // Null-argument guards.
    CHECK(ht_tracker_create(nullptr) == nullptr);
    CHECK(ht_tracker_push_frame(nullptr, nullptr, 0, 0.0) == HT_ERROR_INVALID_HANDLE);

    ht_detection_t d{};
    d.bboxW = 0.2f; d.bboxH = 0.4f; d.confidence = 0.9f;
    d.spatial = {0.2f, 0.0f, 2.0f};
    d.poseValid = 1; d.hasLeftEye = 1; d.hasRightEye = 1;
    d.leftEye = {0.2f, 0.0f, 2.0f}; d.rightEye = {0.2f, 0.0f, 2.0f};

    CHECK(ht_tracker_push_frame(t, &d, 1, 0.0) == HT_SUCCESS);

    ht_viewer_t v{};
    CHECK(ht_tracker_get_viewer(t, &v) == HT_SUCCESS);
    CHECK(v.valid == 1);
    CHECK(v.state == HT_STATE_TRACKING);
    CHECK(v.id == 1);
    NEAR(v.headWorld.x, 0.2f, 1e-4);
    NEAR(v.headWorld.z, 2.0f, 1e-4);

    const ht_vec3_t pa{-1, -1, 0}, pb{1, -1, 0}, pc{-1, 1, 0};
    ht_offaxis_t off{};
    CHECK(ht_tracker_compute_offaxis(t, &pa, &pb, &pc, 0.1f, 100.0f, &off) == HT_SUCCESS);
    CHECK(off.valid == 1);
    NEAR(off.eyeToScreen, 2.0f, 1e-4);

    // Config round-trip preserves the filter type.
    ht_tracker_config_t got{};
    CHECK(ht_tracker_get_config(t, &got) == HT_SUCCESS);
    CHECK(got.filterType == HT_FILTER_NONE);

    CHECK(ht_tracker_reset(t) == HT_SUCCESS);
    ht_tracker_destroy(t);
    ht_tracker_destroy(nullptr); // no-op, must not crash
}

int main() {
    TestFilters();
    TestOffAxis();
    TestCoordinateTransform();
    TestHeadEstimator();
    TestViewerSelector();
    TestStateMachine();
    TestTracker();
    TestCApi();

    std::printf("\nHoloTrack harness: %d checks, %d failure(s)\n", g_checks, g_failures);
    return g_failures == 0 ? 0 : 1;
}
