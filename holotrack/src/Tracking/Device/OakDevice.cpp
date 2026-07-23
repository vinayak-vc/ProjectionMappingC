/**
 * @file OakDevice.cpp
 * @brief OAK-D DepthAI detection source. The DepthAI pipeline is compiled only when
 *        HOLOTRACK_HAVE_DEPTHAI is defined (the vcpkg `depthai` feature); otherwise this is an
 *        inert stub so the DLL builds and links with no camera dependency (D-032).
 *
 * Detection modes (OakOptions::detectionMode):
 *   - Person: one MobileNet-SSD spatial net; head is estimated downstream from the body box.
 *   - Face:   one face spatial net; each face is emitted with its centre as a nose keypoint so the
 *             head estimator uses it directly (no body-height lift).
 *   - FaceThenPerson: both nets run; the face is preferred, falling back to the person box after
 *             `faceFallbackFrames` consecutive faceless frames.
 *
 * Targets depthai-core v2.x. DepthAI spatial coordinates are mm, +X right, +Y down, +Z forward;
 * converted here to metres with +Y up (residual axis differences are absorbed by OAK->world calib).
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
    std::shared_ptr<dai::DataOutputQueue> faceQueue;   // null unless Face/FaceThenPerson
    std::shared_ptr<dai::DataOutputQueue> personQueue; // null unless Person/FaceThenPerson
    std::thread worker;
    std::mutex mutex;
    std::vector<Detection> latest;
    double latestTimestamp{0.0};
    std::atomic<bool> running{false};
    std::atomic<bool> hasNew{false};
    int facelessFrames{0};

    static bool NeedsPerson(DetectionMode m) { return m == DetectionMode::Person || m == DetectionMode::FaceThenPerson; }
    static bool NeedsFace(DetectionMode m) { return m == DetectionMode::Face || m == DetectionMode::FaceThenPerson; }

    Detection ToDetection(const dai::SpatialImgDetection& d, bool asFace) const {
        Detection out;
        out.bboxX = d.xmin;
        out.bboxY = d.ymin;
        out.bboxW = d.xmax - d.xmin;
        out.bboxH = d.ymax - d.ymin;
        out.confidence = d.confidence;
        out.spatial = Vector3(d.spatialCoordinates.x / 1000.0f,
                              -d.spatialCoordinates.y / 1000.0f, // +Y up
                              d.spatialCoordinates.z / 1000.0f);
        if (asFace) {
            // A face box already localizes the head: hand its centre to the estimator as a nose
            // keypoint so it is used directly instead of the bbox+body-height fallback.
            out.pose.valid = true;
            out.pose.hasNose = true;
            out.pose.nose = out.spatial;
        }
        return out;
    }

    void Publish(std::vector<Detection>&& frame, double ts) {
        std::lock_guard<std::mutex> lock(mutex);
        latest = std::move(frame);
        latestTimestamp = ts;
        hasNew.store(true, std::memory_order_release);
    }

    void WorkerLoop() {
        const std::chrono::steady_clock::time_point start = std::chrono::steady_clock::now();
        const DetectionMode mode = options.detectionMode;
        while (running.load(std::memory_order_relaxed)) {
            bool published = false;
            const double ts = std::chrono::duration<double>(std::chrono::steady_clock::now() - start).count();

            // --- Face path (preferred when present) ---
            if (faceQueue != nullptr) {
                std::shared_ptr<dai::SpatialImgDetections> packet = faceQueue->tryGet<dai::SpatialImgDetections>();
                if (packet != nullptr) {
                    std::vector<Detection> frame;
                    frame.reserve(packet->detections.size());
                    for (const dai::SpatialImgDetection& d : packet->detections) {
                        if (d.confidence < options.confidenceThreshold) {
                            continue;
                        }
                        frame.push_back(ToDetection(d, /*asFace=*/true));
                    }
                    if (!frame.empty()) {
                        facelessFrames = 0;
                        Publish(std::move(frame), ts);
                        published = true;
                    } else if (facelessFrames < 1000000) {
                        ++facelessFrames;
                    }
                }
            }

            // --- Person path (Person mode, or FaceThenPerson past the fallback threshold) ---
            if (!published && personQueue != nullptr) {
                std::shared_ptr<dai::SpatialImgDetections> packet = personQueue->tryGet<dai::SpatialImgDetections>();
                if (packet != nullptr) {
                    const bool usePerson = (mode == DetectionMode::Person) ||
                                           (mode == DetectionMode::FaceThenPerson && facelessFrames >= options.faceFallbackFrames);
                    if (usePerson) {
                        std::vector<Detection> frame;
                        frame.reserve(packet->detections.size());
                        for (const dai::SpatialImgDetection& d : packet->detections) {
                            if (static_cast<int>(d.label) != options.personLabel || d.confidence < options.confidenceThreshold) {
                                continue;
                            }
                            frame.push_back(ToDetection(d, /*asFace=*/false));
                        }
                        Publish(std::move(frame), ts);
                        published = true;
                    }
                }
            }

            if (!published) {
                std::this_thread::sleep_for(std::chrono::milliseconds(1));
            }
        }
    }

    // Build a spatial-detection net (MobileNet-SSD family: person or face) on a shared preview+depth.
    static void AddSpatialNet(dai::Pipeline& pipeline, std::shared_ptr<dai::node::ColorCamera> cam,
                              std::shared_ptr<dai::node::StereoDepth> stereo, const std::string& blob,
                              float confidence, float lowerMm, float upperMm, const std::string& streamName) {
        auto nn = pipeline.create<dai::node::MobileNetSpatialDetectionNetwork>();
        auto xout = pipeline.create<dai::node::XLinkOut>();
        xout->setStreamName(streamName);
        nn->setBlobPath(blob);
        nn->setConfidenceThreshold(confidence);
        nn->input.setBlocking(false);
        nn->setBoundingBoxScaleFactor(0.5f);
        nn->setDepthLowerThreshold(static_cast<uint32_t>(lowerMm));
        nn->setDepthUpperThreshold(static_cast<uint32_t>(upperMm));
        cam->preview.link(nn->input);
        stereo->depth.link(nn->inputDepth);
        nn->out.link(xout->input);
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
    const DetectionMode mode = impl_->options.detectionMode;
    if (Impl::NeedsPerson(mode) && impl_->options.blobPath.empty()) {
        impl_->lastError = "OakDevice::Start: person blobPath is empty (required for Person/FaceThenPerson)";
        return false;
    }
    if (Impl::NeedsFace(mode) && impl_->options.faceBlobPath.empty()) {
        impl_->lastError = "OakDevice::Start: faceBlobPath is empty (required for Face/FaceThenPerson)";
        return false;
    }
    try {
        dai::Pipeline pipeline;
        auto camRgb = pipeline.create<dai::node::ColorCamera>();
        auto monoLeft = pipeline.create<dai::node::MonoCamera>();
        auto monoRight = pipeline.create<dai::node::MonoCamera>();
        auto stereo = pipeline.create<dai::node::StereoDepth>();

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

        if (Impl::NeedsFace(mode)) {
            Impl::AddSpatialNet(pipeline, camRgb, stereo, impl_->options.faceBlobPath,
                                impl_->options.confidenceThreshold, impl_->options.depthLowerThresholdMm,
                                impl_->options.depthUpperThresholdMm, "det_face");
        }
        if (Impl::NeedsPerson(mode)) {
            Impl::AddSpatialNet(pipeline, camRgb, stereo, impl_->options.blobPath,
                                impl_->options.confidenceThreshold, impl_->options.depthLowerThresholdMm,
                                impl_->options.depthUpperThresholdMm, "det_person");
        }

        impl_->device = std::make_unique<dai::Device>(pipeline);
        if (Impl::NeedsFace(mode)) {
            impl_->faceQueue = impl_->device->getOutputQueue("det_face", 4, false);
        }
        if (Impl::NeedsPerson(mode)) {
            impl_->personQueue = impl_->device->getOutputQueue("det_person", 4, false);
        }
        impl_->facelessFrames = 0;
        impl_->running.store(true);
        impl_->worker = std::thread([this]() { impl_->WorkerLoop(); });
        return true;
    } catch (const std::exception& e) {
        impl_->lastError = std::string("OakDevice::Start: ") + e.what();
        impl_->device.reset();
        impl_->faceQueue.reset();
        impl_->personQueue.reset();
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
    impl_->faceQueue.reset();
    impl_->personQueue.reset();
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
