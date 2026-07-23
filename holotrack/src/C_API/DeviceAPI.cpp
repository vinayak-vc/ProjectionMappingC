/**
 * @file DeviceAPI.cpp
 * @brief Implementation of the OAK-D C-API — thin wrapper over holotrack::OakDevice.
 */
#include "HoloTrack/C_API/DeviceAPI.h"

#include <string>

#include "HoloTrack/Tracking/Device/OakDevice.h"

using namespace holotrack;

namespace {

OakDevice* AsDevice(ht_oak_source_t* handle) { return reinterpret_cast<OakDevice*>(handle); }

thread_local std::string g_fallbackError;

ht_detection_t ToC(const Detection& d) {
    ht_detection_t out{};
    out.bboxX = d.bboxX;
    out.bboxY = d.bboxY;
    out.bboxW = d.bboxW;
    out.bboxH = d.bboxH;
    out.spatial = ht_vec3_t{d.spatial.x, d.spatial.y, d.spatial.z};
    out.confidence = d.confidence;
    out.poseValid = d.pose.valid ? 1 : 0;
    out.hasNose = d.pose.hasNose ? 1 : 0;
    out.hasLeftEye = d.pose.hasLeftEye ? 1 : 0;
    out.hasRightEye = d.pose.hasRightEye ? 1 : 0;
    out.hasNeck = d.pose.hasNeck ? 1 : 0;
    out.nose = ht_vec3_t{d.pose.nose.x, d.pose.nose.y, d.pose.nose.z};
    out.leftEye = ht_vec3_t{d.pose.leftEye.x, d.pose.leftEye.y, d.pose.leftEye.z};
    out.rightEye = ht_vec3_t{d.pose.rightEye.x, d.pose.rightEye.y, d.pose.rightEye.z};
    out.neck = ht_vec3_t{d.pose.neck.x, d.pose.neck.y, d.pose.neck.z};
    return out;
}

} // namespace

extern "C" {

HOLOTRACK_API int ht_oak_is_supported(void) { return OakDevice::IsSupported() ? 1 : 0; }

HOLOTRACK_API ht_oak_source_t* ht_oak_create(const ht_oak_options_t* options) {
    try {
        OakDevice* dev;
        if (options != nullptr) {
            OakOptions opt;
            opt.detectionMode = static_cast<DetectionMode>(options->detectionMode);
            opt.blobPath = (options->blobPath != nullptr) ? options->blobPath : "";
            opt.faceBlobPath = (options->faceBlobPath != nullptr) ? options->faceBlobPath : "";
            opt.faceFallbackFrames = options->faceFallbackFrames;
            opt.personLabel = options->personLabel;
            opt.confidenceThreshold = options->confidenceThreshold;
            opt.depthLowerThresholdMm = options->depthLowerThresholdMm;
            opt.depthUpperThresholdMm = options->depthUpperThresholdMm;
            dev = new OakDevice(opt);
        } else {
            dev = new OakDevice();
        }
        return reinterpret_cast<ht_oak_source_t*>(dev);
    } catch (...) {
        g_fallbackError = "ht_oak_create: allocation failed";
        return nullptr;
    }
}

HOLOTRACK_API void ht_oak_destroy(ht_oak_source_t* source) { delete AsDevice(source); }

HOLOTRACK_API ht_status_t ht_oak_start(ht_oak_source_t* source) {
    OakDevice* dev = AsDevice(source);
    if (dev == nullptr) { g_fallbackError = "ht_oak_start: null handle"; return HT_ERROR_INVALID_HANDLE; }
    return dev->Start() ? HT_SUCCESS : HT_ERROR_UNKNOWN;
}

HOLOTRACK_API ht_status_t ht_oak_stop(ht_oak_source_t* source) {
    OakDevice* dev = AsDevice(source);
    if (dev == nullptr) { g_fallbackError = "ht_oak_stop: null handle"; return HT_ERROR_INVALID_HANDLE; }
    dev->Stop();
    return HT_SUCCESS;
}

HOLOTRACK_API int ht_oak_is_running(ht_oak_source_t* source) {
    OakDevice* dev = AsDevice(source);
    return (dev != nullptr && dev->IsRunning()) ? 1 : 0;
}

HOLOTRACK_API ht_status_t ht_oak_poll(ht_oak_source_t* source, ht_detection_t* buffer,
                                      size_t capacity, size_t* outCount, int* outHasFrame,
                                      double* outTimestampSeconds) {
    OakDevice* dev = AsDevice(source);
    if (dev == nullptr) { g_fallbackError = "ht_oak_poll: null handle"; return HT_ERROR_INVALID_HANDLE; }
    if (outCount != nullptr) *outCount = 0;
    if (outHasFrame != nullptr) *outHasFrame = 0;

    const Detection* dets = nullptr;
    size_t count = 0;
    double ts = 0.0;
    const bool hasFrame = dev->Poll(&dets, &count, &ts);
    if (!hasFrame) {
        return HT_SUCCESS; // no new frame; not an error
    }
    if (outHasFrame != nullptr) *outHasFrame = 1;
    if (outTimestampSeconds != nullptr) *outTimestampSeconds = ts;

    const size_t n = (buffer != nullptr && count > capacity) ? capacity : count;
    if (buffer != nullptr && dets != nullptr) {
        for (size_t i = 0; i < n; ++i) {
            buffer[i] = ToC(dets[i]);
        }
    }
    if (outCount != nullptr) *outCount = (buffer != nullptr) ? n : 0;
    return HT_SUCCESS;
}

HOLOTRACK_API const char* ht_oak_last_error(ht_oak_source_t* source) {
    OakDevice* dev = AsDevice(source);
    return (dev != nullptr) ? dev->LastError() : g_fallbackError.c_str();
}

} // extern "C"
