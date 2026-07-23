/**
 * @file Tracker.cpp
 * @brief Implementation of holotrack::Tracker.
 */
#include "HoloTrack/Tracking/Tracker.h"

#include "HoloTrack/Tracking/Filters.h"
#include "HoloTrack/Tracking/HeadEstimator.h"
#include "HoloTrack/Tracking/CoordinateTransform.h"

namespace holotrack {

Tracker::Tracker(const TrackerConfig& cfg) : cfg_(cfg), filter_(MakeFilter(cfg)) {}

Tracker::~Tracker() = default;

void Tracker::SetConfig(const TrackerConfig& cfg) {
    const bool filterChanged = cfg.filterType != cfg_.filterType;
    cfg_ = cfg;
    if (filterChanged || !filter_) {
        filter_ = MakeFilter(cfg_);
    }
}

const TrackerConfig& Tracker::GetConfig() const { return cfg_; }

const TrackedViewer& Tracker::GetViewer() const { return viewer_; }

void Tracker::Reset() {
    selector_.Reset();
    stateMachine_.Reset();
    if (filter_) {
        filter_->Reset();
    }
    viewer_ = TrackedViewer{};
    lastFilteredCamera_ = Vector3{};
    hasMeasured_ = false;
    hasPrevUpdate_ = false;
    lastMeasuredSeconds_ = 0.0;
    lastUpdateSeconds_ = 0.0;
}

void Tracker::PushFrame(const Detection* dets, std::size_t count, double timestampSeconds) {
    if (dets == nullptr) {
        count = 0;
    }

    float dt = 0.0f;
    if (hasPrevUpdate_) {
        const double raw = timestampSeconds - lastUpdateSeconds_;
        dt = raw > 0.0 ? static_cast<float>(raw) : 0.0f;
    }

    const SelectionResult sel = selector_.Select(dets, count, cfg_);
    const bool measured = sel.index >= 0;

    Vector3 headCamera{};
    float confidence = viewer_.confidence;

    if (measured) {
        const Detection& d = dets[static_cast<std::size_t>(sel.index)];
        const Vector3 rawHead = HeadEstimator::Estimate(d, cfg_);
        headCamera = filter_->Update(rawHead, dt);
        confidence = d.confidence;
    }

    const TrackingState state = stateMachine_.Update(measured, timestampSeconds, cfg_);

    // Terminal Lost — release everything exactly once, then report Lost this frame.
    if (state == TrackingState::Lost) {
        selector_.Reset();
        if (filter_) {
            filter_->Reset();
        }
        viewer_ = TrackedViewer{};
        viewer_.state = TrackingState::Lost;
        viewer_.timestampSeconds = timestampSeconds;
        hasMeasured_ = false;
        lastFilteredCamera_ = Vector3{};
        lastUpdateSeconds_ = timestampSeconds;
        hasPrevUpdate_ = true;
        return;
    }

    viewer_.state = state;
    viewer_.timestampSeconds = timestampSeconds;
    viewer_.id = sel.id;

    if (measured) {
        if (hasMeasured_) {
            const double sinceMeasure = timestampSeconds - lastMeasuredSeconds_;
            if (sinceMeasure > 1e-4) {
                viewer_.velocity = (headCamera - lastFilteredCamera_) / static_cast<float>(sinceMeasure);
            }
        }
        lastFilteredCamera_ = headCamera;
        lastMeasuredSeconds_ = timestampSeconds;
        hasMeasured_ = true;

        viewer_.headCamera = headCamera;
        viewer_.confidence = confidence;
        viewer_.valid = true;
    } else if (state == TrackingState::Prediction && hasMeasured_) {
        // Extrapolate from the last measured pose while inside the prediction window; once the
        // window elapses (but before Lost) hold the last pose so the render does not snap.
        if (stateMachine_.IsPredicting(timestampSeconds, cfg_)) {
            const float elapsed = static_cast<float>(timestampSeconds - lastMeasuredSeconds_);
            viewer_.headCamera = lastFilteredCamera_ + viewer_.velocity * elapsed;
        } else {
            viewer_.headCamera = lastFilteredCamera_;
        }
        viewer_.valid = true;
    } else {
        // Searching (no viewer yet) — nothing valid to render.
        viewer_.valid = false;
    }

    if (viewer_.valid) {
        viewer_.headWorld = CoordinateTransform::CameraToWorld(cfg_.calibration, viewer_.headCamera);
    }

    lastUpdateSeconds_ = timestampSeconds;
    hasPrevUpdate_ = true;
}

OffAxisResult Tracker::ComputeOffAxis(const Vector3& pa, const Vector3& pb, const Vector3& pc,
                                      float nearPlane, float farPlane) const {
    return holotrack::ComputeOffAxis(pa, pb, pc, viewer_.headWorld, nearPlane, farPlane);
}

} // namespace holotrack
