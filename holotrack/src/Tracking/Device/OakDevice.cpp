/**
 * @file OakDevice.cpp
 * @brief OAK-D DepthAI detection source. The DepthAI pipeline is compiled only when
 *        HOLOTRACK_HAVE_DEPTHAI is defined (the vcpkg `depthai` feature); otherwise this is an
 *        inert stub so the DLL builds and links with no camera dependency (D-032).
 *
 * Targets depthai-core v2.x. Coordinate note: DepthAI spatial coordinates are millimetres with
 * +X right, +Y down, +Z forward; converted here to metres with +Y up to match the tracker's
 * assumed convention (any residual axis difference is absorbed by the OAK->world calibration).
 */
#include "HoloTrack/Tracking/Device/OakDevice.h"

#include <string>
#include <vector>

#ifdef HOLOTRACK_HAVE_DEPTHAI
#include <atomic>
#include <chrono>
#include <mutex>
#include <thread>
#include <depthai/depthai.hpp>
#endif

namespace holotrack {

struct OakDevice::Impl {
    OakOptions options;
    std::string lastError;
    std::vector<Detection> pollBuffer; // returned to the caller by Poll()

#ifdef HOLOTRACK_HAVE_DEPTHAI
    std::unique_ptr<dai::Device> device;
    std::shared_ptr<dai::DataOutputQueue> queue;
    std::thread worker;
    std::mutex mutex;
    std::vector<Detection> latest;
    double latestTimestamp{0.0};
    std::atomic<bool> running{false};
    std::atomic<bool> hasNew{false};

    void WorkerLoop() {
        const std::chrono::steady_clock::time_point start = std::chrono::steady_clock::now();
        while (running.load(std::memory_order_relaxed)) {
            std::shared_ptr<dai::SpatialImgDetections> packet = queue->tryGet<dai::SpatialImgDetections>();
            if (packet == nullptr) {
                std::this_thread::sleep_for(std::chrono::milliseconds(1));
                continue;
            }
            std::vector<Detection> frame;
            frame.reserve(packet->detections.size());
            for (const dai::SpatialImgDetection& d : packet->detections) {
                if (static_cast<int>(d.label) != options.personLabel || d.confidence < options.confidenceThreshold) {
                    continue;
                }
                Detection out;
                out.bboxX = d.xmin;
                out.bboxY = d.ymin;
                out.bboxW = d.xmax - d.xmin;
                out.bboxH = d.ymax - d.ymin;
                out.confidence = d.confidence;
                out.spatial = Vector3(d.spatialCoordinates.x / 1000.0f,
                                      -d.spatialCoordinates.y / 1000.0f, // +Y up
                                      d.spatialCoordinates.z / 1000.0f);
                frame.push_back(out);
            }
            const double ts = std::chrono::duration<double>(std::chrono::steady_clock::now() - start).count();
            {
                std::lock_guard<std::mutex> lock(mutex);
                latest = std::move(frame);
                latestTimestamp = ts;
                hasNew.store(true, std::memory_order_release);
            }
        }
    }
#endif
};

OakDevice::OakDevice() : impl_(std::make_unique<Impl>()) {}

OakDevice::OakDevice(const OakOptions& options) : impl_(std::make_unique<Impl>()) {
    impl_->options = options;
}

OakDevice::~OakDevice() { Stop(); }

bool OakDevice::IsSupported() {
#ifdef HOLOTRACK_HAVE_DEPTHAI
    return true;
#else
    return false;
#endif
}

const char* OakDevice::LastError() const { return impl_->lastError.c_str(); }

#ifdef HOLOTRACK_HAVE_DEPTHAI

bool OakDevice::Start() {
    if (impl_->running.load()) {
        return true;
    }
    if (impl_->options.blobPath.empty()) {
        impl_->lastError = "OakDevice::Start: OakOptions.blobPath is empty (spatial-detection blob required)";
        return false;
    }
    try {
        dai::Pipeline pipeline;
        auto camRgb = pipeline.create<dai::node::ColorCamera>();
        auto monoLeft = pipeline.create<dai::node::MonoCamera>();
        auto monoRight = pipeline.create<dai::node::MonoCamera>();
        auto stereo = pipeline.create<dai::node::StereoDepth>();
        auto spatialNN = pipeline.create<dai::node::MobileNetSpatialDetectionNetwork>();
        auto xoutNN = pipeline.create<dai::node::XLinkOut>();
        xoutNN->setStreamName("detections");

        camRgb->setPreviewSize(300, 300);
        camRgb->setResolution(dai::ColorCameraProperties::SensorResolution::THE_1080_P);
        camRgb->setInterleaved(false);
        camRgb->setBoardSocket(dai::CameraBoardSocket::CAM_A);

        monoLeft->setResolution(dai::MonoCameraProperties::SensorResolution::THE_400_P);
        monoLeft->setBoardSocket(dai::CameraBoardSocket::CAM_B);
        monoRight->setResolution(dai::MonoCameraProperties::SensorResolution::THE_400_P);
        monoRight->setBoardSocket(dai::CameraBoardSocket::CAM_C);

        stereo->setDefaultProfilePreset(dai::node::StereoDepth::PresetMode::HIGH_DENSITY);
        stereo->setDepthAlign(dai::CameraBoardSocket::CAM_A);
        stereo->setSubpixel(true);
        monoLeft->out.link(stereo->left);
        monoRight->out.link(stereo->right);

        spatialNN->setBlobPath(impl_->options.blobPath);
        spatialNN->setConfidenceThreshold(impl_->options.confidenceThreshold);
        spatialNN->input.setBlocking(false);
        spatialNN->setBoundingBoxScaleFactor(0.5f);
        spatialNN->setDepthLowerThreshold(static_cast<uint32_t>(impl_->options.depthLowerThresholdMm));
        spatialNN->setDepthUpperThreshold(static_cast<uint32_t>(impl_->options.depthUpperThresholdMm));

        camRgb->preview.link(spatialNN->input);
        stereo->depth.link(spatialNN->inputDepth);
        spatialNN->out.link(xoutNN->input);

        impl_->device = std::make_unique<dai::Device>(pipeline);
        impl_->queue = impl_->device->getOutputQueue("detections", 4, false);
        impl_->running.store(true);
        impl_->worker = std::thread([this]() { impl_->WorkerLoop(); });
        return true;
    } catch (const std::exception& e) {
        impl_->lastError = std::string("OakDevice::Start: ") + e.what();
        impl_->device.reset();
        impl_->running.store(false);
        return false;
    }
}

void OakDevice::Stop() {
    if (impl_->running.exchange(false)) {
        if (impl_->worker.joinable()) {
            impl_->worker.join();
        }
    }
    impl_->queue.reset();
    impl_->device.reset();
}

bool OakDevice::IsRunning() const { return impl_->running.load(); }

bool OakDevice::Poll(const Detection** outDetections, std::size_t* outCount, double* outTimestampSeconds) {
    if (!impl_->hasNew.exchange(false, std::memory_order_acquire)) {
        return false;
    }
    {
        std::lock_guard<std::mutex> lock(impl_->mutex);
        impl_->pollBuffer = impl_->latest;
        if (outTimestampSeconds != nullptr) {
            *outTimestampSeconds = impl_->latestTimestamp;
        }
    }
    if (outDetections != nullptr) {
        *outDetections = impl_->pollBuffer.empty() ? nullptr : impl_->pollBuffer.data();
    }
    if (outCount != nullptr) {
        *outCount = impl_->pollBuffer.size();
    }
    return true;
}

#else // ---- No DepthAI: inert stub ----

bool OakDevice::Start() {
    impl_->lastError = "OakDevice: this build has no DepthAI support (rebuild with HOLOTRACK_WITH_DEPTHAI)";
    return false;
}

void OakDevice::Stop() {}

bool OakDevice::IsRunning() const { return false; }

bool OakDevice::Poll(const Detection** outDetections, std::size_t* outCount, double* outTimestampSeconds) {
    if (outDetections != nullptr) *outDetections = nullptr;
    if (outCount != nullptr) *outCount = 0;
    if (outTimestampSeconds != nullptr) *outTimestampSeconds = 0.0;
    return false;
}

#endif

} // namespace holotrack
