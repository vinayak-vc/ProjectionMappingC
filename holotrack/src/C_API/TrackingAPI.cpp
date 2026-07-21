/**
 * @file TrackingAPI.cpp
 * @brief Implementation of the HoloTrackSDK C-API. Translates flat C structs to/from the C++
 *        core and contains all exceptions at the boundary (D-006).
 */
#include "HoloTrack/C_API/TrackingAPI.h"

#include <new>
#include <string>
#include <vector>

#include "HoloTrack/Core/Version.h"
#include "HoloTrack/Tracking/Tracker.h"

using namespace holotrack;

namespace {

thread_local std::string g_lastError;

void SetError(const char* message) { g_lastError = (message != nullptr) ? message : ""; }

Tracker* AsTracker(ht_tracker_t* handle) { return reinterpret_cast<Tracker*>(handle); }

Vector3 ToVec3(const ht_vec3_t& v) { return Vector3{v.x, v.y, v.z}; }
ht_vec3_t FromVec3(const Vector3& v) { return ht_vec3_t{v.x, v.y, v.z}; }

TrackerConfig FromC(const ht_tracker_config_t& c) {
    TrackerConfig t;
    t.filterType = static_cast<FilterType>(c.filterType);
    t.expAlpha = c.expAlpha;
    t.oneEuroMinCutoff = c.oneEuroMinCutoff;
    t.oneEuroBeta = c.oneEuroBeta;
    t.oneEuroDCutoff = c.oneEuroDCutoff;
    t.kalmanProcessVar = c.kalmanProcessVar;
    t.kalmanMeasVar = c.kalmanMeasVar;
    t.selection = static_cast<SelectionMode>(c.selection);
    t.hysteresisMargin = c.hysteresisMargin;
    t.hysteresisFrames = c.hysteresisFrames;
    t.associationMaxDistance = c.associationMaxDistance;
    t.minDepth = c.minDepth;
    t.maxDepth = c.maxDepth;
    t.predictionTimeSeconds = c.predictionTimeSeconds;
    t.lostTimeoutSeconds = c.lostTimeoutSeconds;
    t.cameraVFovRad = c.cameraVFovRad;
    t.headHeightFraction = c.headHeightFraction;
    t.bodyHeadOffset = c.bodyHeadOffset;
    t.movementScale = c.movementScale;
    t.deadZone = c.deadZone;
    t.maxDistance = c.maxDistance;
    t.minDistanceClamp = c.minDistanceClamp;
    t.calibration.translation = ToVec3(c.calibTranslation);
    t.calibration.rotation = Quaternion{c.calibRotation.x, c.calibRotation.y, c.calibRotation.z, c.calibRotation.w};
    t.calibration.scale = c.calibScale;
    return t;
}

void ToC(const TrackerConfig& t, ht_tracker_config_t& c) {
    c.filterType = static_cast<int>(t.filterType);
    c.expAlpha = t.expAlpha;
    c.oneEuroMinCutoff = t.oneEuroMinCutoff;
    c.oneEuroBeta = t.oneEuroBeta;
    c.oneEuroDCutoff = t.oneEuroDCutoff;
    c.kalmanProcessVar = t.kalmanProcessVar;
    c.kalmanMeasVar = t.kalmanMeasVar;
    c.selection = static_cast<int>(t.selection);
    c.hysteresisMargin = t.hysteresisMargin;
    c.hysteresisFrames = t.hysteresisFrames;
    c.associationMaxDistance = t.associationMaxDistance;
    c.minDepth = t.minDepth;
    c.maxDepth = t.maxDepth;
    c.predictionTimeSeconds = t.predictionTimeSeconds;
    c.lostTimeoutSeconds = t.lostTimeoutSeconds;
    c.cameraVFovRad = t.cameraVFovRad;
    c.headHeightFraction = t.headHeightFraction;
    c.bodyHeadOffset = t.bodyHeadOffset;
    c.movementScale = t.movementScale;
    c.deadZone = t.deadZone;
    c.maxDistance = t.maxDistance;
    c.minDistanceClamp = t.minDistanceClamp;
    c.calibTranslation = FromVec3(t.calibration.translation);
    c.calibRotation = ht_quat_t{t.calibration.rotation.x, t.calibration.rotation.y,
                                t.calibration.rotation.z, t.calibration.rotation.w};
    c.calibScale = t.calibration.scale;
}

Detection FromC(const ht_detection_t& d) {
    Detection out;
    out.bboxX = d.bboxX; out.bboxY = d.bboxY; out.bboxW = d.bboxW; out.bboxH = d.bboxH;
    out.spatial = ToVec3(d.spatial);
    out.confidence = d.confidence;
    out.pose.valid = d.poseValid != 0;
    out.pose.hasNose = d.hasNose != 0;
    out.pose.hasLeftEye = d.hasLeftEye != 0;
    out.pose.hasRightEye = d.hasRightEye != 0;
    out.pose.hasNeck = d.hasNeck != 0;
    out.pose.nose = ToVec3(d.nose);
    out.pose.leftEye = ToVec3(d.leftEye);
    out.pose.rightEye = ToVec3(d.rightEye);
    out.pose.neck = ToVec3(d.neck);
    return out;
}

} // namespace

extern "C" {

HOLOTRACK_API void ht_get_version(int* major, int* minor, int* patch) {
    if (major != nullptr) *major = kVersionMajor;
    if (minor != nullptr) *minor = kVersionMinor;
    if (patch != nullptr) *patch = kVersionPatch;
}

HOLOTRACK_API const char* ht_get_last_error(void) { return g_lastError.c_str(); }

HOLOTRACK_API ht_tracker_t* ht_tracker_create(const ht_tracker_config_t* config) {
    if (config == nullptr) {
        SetError("ht_tracker_create: config is null");
        return nullptr;
    }
    try {
        Tracker* t = new Tracker(FromC(*config));
        return reinterpret_cast<ht_tracker_t*>(t);
    } catch (const std::exception& e) {
        SetError(e.what());
    } catch (...) {
        SetError("ht_tracker_create: unknown error");
    }
    return nullptr;
}

HOLOTRACK_API void ht_tracker_destroy(ht_tracker_t* tracker) {
    delete AsTracker(tracker);
}

HOLOTRACK_API ht_status_t ht_tracker_set_config(ht_tracker_t* tracker, const ht_tracker_config_t* config) {
    Tracker* t = AsTracker(tracker);
    if (t == nullptr) { SetError("ht_tracker_set_config: null handle"); return HT_ERROR_INVALID_HANDLE; }
    if (config == nullptr) { SetError("ht_tracker_set_config: null config"); return HT_ERROR_INVALID_ARGUMENT; }
    try {
        t->SetConfig(FromC(*config));
        return HT_SUCCESS;
    } catch (...) { SetError("ht_tracker_set_config: unknown error"); return HT_ERROR_UNKNOWN; }
}

HOLOTRACK_API ht_status_t ht_tracker_get_config(ht_tracker_t* tracker, ht_tracker_config_t* out) {
    Tracker* t = AsTracker(tracker);
    if (t == nullptr) { SetError("ht_tracker_get_config: null handle"); return HT_ERROR_INVALID_HANDLE; }
    if (out == nullptr) { SetError("ht_tracker_get_config: null out"); return HT_ERROR_INVALID_ARGUMENT; }
    ToC(t->GetConfig(), *out);
    return HT_SUCCESS;
}

HOLOTRACK_API ht_status_t ht_tracker_push_frame(ht_tracker_t* tracker, const ht_detection_t* detections,
                                                size_t count, double timestampSeconds) {
    Tracker* t = AsTracker(tracker);
    if (t == nullptr) { SetError("ht_tracker_push_frame: null handle"); return HT_ERROR_INVALID_HANDLE; }
    if (detections == nullptr && count != 0) {
        SetError("ht_tracker_push_frame: null detections with non-zero count");
        return HT_ERROR_INVALID_ARGUMENT;
    }
    try {
        if (count == 0) {
            t->PushFrame(nullptr, 0, timestampSeconds);
            return HT_SUCCESS;
        }
        // Translate flat detections into the C++ type on a small stack/heap buffer.
        std::vector<Detection> buf;
        buf.reserve(count);
        for (size_t i = 0; i < count; ++i) {
            buf.push_back(FromC(detections[i]));
        }
        t->PushFrame(buf.data(), buf.size(), timestampSeconds);
        return HT_SUCCESS;
    } catch (const std::exception& e) {
        SetError(e.what());
        return HT_ERROR_UNKNOWN;
    } catch (...) {
        SetError("ht_tracker_push_frame: unknown error");
        return HT_ERROR_UNKNOWN;
    }
}

HOLOTRACK_API ht_status_t ht_tracker_get_viewer(ht_tracker_t* tracker, ht_viewer_t* out) {
    Tracker* t = AsTracker(tracker);
    if (t == nullptr) { SetError("ht_tracker_get_viewer: null handle"); return HT_ERROR_INVALID_HANDLE; }
    if (out == nullptr) { SetError("ht_tracker_get_viewer: null out"); return HT_ERROR_INVALID_ARGUMENT; }
    const TrackedViewer& v = t->GetViewer();
    out->id = v.id;
    out->valid = v.valid ? 1 : 0;
    out->headCamera = FromVec3(v.headCamera);
    out->headWorld = FromVec3(v.headWorld);
    out->velocity = FromVec3(v.velocity);
    out->confidence = v.confidence;
    out->state = static_cast<int>(v.state);
    out->timestampSeconds = v.timestampSeconds;
    return HT_SUCCESS;
}

HOLOTRACK_API ht_status_t ht_tracker_compute_offaxis(ht_tracker_t* tracker, const ht_vec3_t* pa,
                                                     const ht_vec3_t* pb, const ht_vec3_t* pc,
                                                     float nearPlane, float farPlane, ht_offaxis_t* out) {
    Tracker* t = AsTracker(tracker);
    if (t == nullptr) { SetError("ht_tracker_compute_offaxis: null handle"); return HT_ERROR_INVALID_HANDLE; }
    if (pa == nullptr || pb == nullptr || pc == nullptr || out == nullptr) {
        SetError("ht_tracker_compute_offaxis: null argument");
        return HT_ERROR_INVALID_ARGUMENT;
    }
    const OffAxisResult r = t->ComputeOffAxis(ToVec3(*pa), ToVec3(*pb), ToVec3(*pc), nearPlane, farPlane);
    for (int i = 0; i < 16; ++i) {
        out->view[i] = r.view[i];
        out->projection[i] = r.projection[i];
    }
    out->eyeToScreen = r.eyeToScreen;
    out->valid = r.valid ? 1 : 0;
    return HT_SUCCESS;
}

HOLOTRACK_API ht_status_t ht_tracker_reset(ht_tracker_t* tracker) {
    Tracker* t = AsTracker(tracker);
    if (t == nullptr) { SetError("ht_tracker_reset: null handle"); return HT_ERROR_INVALID_HANDLE; }
    t->Reset();
    return HT_SUCCESS;
}

} // extern "C"
