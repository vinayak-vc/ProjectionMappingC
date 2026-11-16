/**
 * @file OakCameraAPI.cpp
 * @brief PMSDKOakCamera.dll: OAK-D colour (CAM_A) luma frames + factory intrinsics for camera-based
 *        calibration (D-044). Targets depthai-core v2.x (the same v2.32.0 HoloTrack ships).
 *
 * Pipeline: ColorCamera (smallest sensor mode >= 720 lines, ISP-downscaled to <= maxWidth)
 *   -> video (NV12) -> XLinkOut "pmoak_video"; XLinkIn "pmoak_control" -> ColorCamera.inputControl.
 * A worker thread drains the video queue and keeps the newest frame's Y plane (tightly packed).
 */
#include "PMSDKOak/OakCameraAPI.h"

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstring>
#include <exception>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <tuple>
#include <vector>

#include <depthai/depthai.hpp>

namespace {

constexpr const char* VideoStreamName = "pmoak_video";
constexpr const char* DepthStreamName = "pmoak_depth";
constexpr const char* ControlStreamName = "pmoak_control";

struct SensorMode {
    int width;
    int height;
    dai::ColorCameraProperties::SensorResolution resolution;
};

// Sensor resolutions the ColorCamera node accepts, keyed by the sensor's native output size.
const SensorMode KnownModes[] = {
    {1280, 720, dai::ColorCameraProperties::SensorResolution::THE_720_P},
    {1280, 800, dai::ColorCameraProperties::SensorResolution::THE_800_P},
    {1440, 1080, dai::ColorCameraProperties::SensorResolution::THE_1440X1080},
    {1920, 1080, dai::ColorCameraProperties::SensorResolution::THE_1080_P},
    {1920, 1200, dai::ColorCameraProperties::SensorResolution::THE_1200_P},
    {2024, 1520, dai::ColorCameraProperties::SensorResolution::THE_2024X1520},
    {2592, 1944, dai::ColorCameraProperties::SensorResolution::THE_5_MP},
    {3840, 2160, dai::ColorCameraProperties::SensorResolution::THE_4_K},
    {4056, 3040, dai::ColorCameraProperties::SensorResolution::THE_12_MP},
};

// Picks the smallest supported sensor mode with at least 720 lines; falls back to the largest.
bool ChooseSensorMode(const dai::CameraFeatures& features, SensorMode& outMode) {
    bool found = false;
    SensorMode best{0, 0, dai::ColorCameraProperties::SensorResolution::THE_1080_P};
    for (const dai::CameraSensorConfig& config : features.configs) {
        for (const SensorMode& mode : KnownModes) {
            if (mode.width != config.width || mode.height != config.height) {
                continue;
            }
            const bool bigEnough = mode.height >= 720;
            if (!found) {
                best = mode;
                found = true;
            } else {
                const bool bestBigEnough = best.height >= 720;
                if (bigEnough && (!bestBigEnough || mode.height < best.height)) {
                    best = mode;
                } else if (!bigEnough && !bestBigEnough && mode.height > best.height) {
                    best = mode;
                }
            }
        }
    }
    if (!found) {
        // Older firmware may not report configs: fall back to the native size if it is known.
        for (const SensorMode& mode : KnownModes) {
            if (mode.width == features.width && mode.height == features.height) {
                best = mode;
                found = true;
                break;
            }
        }
    }
    outMode = best;
    return found;
}

// Largest integer downscale n/d (d <= 4) that keeps the output width <= maxWidth and even sizes.
void ChooseIspScale(int sensorWidth, int sensorHeight, int maxWidth, int& outNumerator, int& outDenominator) {
    outNumerator = 1;
    outDenominator = 1;
    if (maxWidth <= 0 || sensorWidth <= maxWidth) {
        return;
    }
    for (int denominator = 2; denominator <= 4; ++denominator) {
        const int width = sensorWidth / denominator;
        const int height = sensorHeight / denominator;
        if (width <= maxWidth && (width % 2) == 0 && (height % 2) == 0) {
            outDenominator = denominator;
            return;
        }
    }
    outDenominator = 4;
}

} // namespace

struct pmoak_camera_t {
    pmoak_options_t options{};
    std::string mxId;
    std::string lastError;
    std::string description;

    std::unique_ptr<dai::Device> device;
    std::shared_ptr<dai::DataOutputQueue> videoQueue;
    std::shared_ptr<dai::DataOutputQueue> depthQueue;
    std::shared_ptr<dai::DataInputQueue> controlQueue;
    std::vector<uint16_t> latestDepth;
    int depthWidth{0};
    int depthHeight{0};
    std::atomic<int64_t> depthSequence{0};
    std::thread worker;
    std::atomic<bool> running{false};

    std::mutex frameMutex;
    std::vector<uint8_t> latestGray;
    int frameWidth{0};
    int frameHeight{0};
    std::atomic<int64_t> sequence{0};
    pmoak_intrinsics_t intrinsics{};
    bool hasIntrinsics{false};

    void WorkerLoop() {
        while (running.load(std::memory_order_relaxed)) {
            std::shared_ptr<dai::ImgFrame> frame;
            std::shared_ptr<dai::ImgFrame> depth;
            try {
                frame = videoQueue->tryGet<dai::ImgFrame>();
                if (depthQueue != nullptr) {
                    depth = depthQueue->tryGet<dai::ImgFrame>();
                }
            } catch (const std::exception&) {
                running.store(false);
                break;
            }
            if (frame == nullptr && depth == nullptr) {
                std::this_thread::sleep_for(std::chrono::milliseconds(2));
                continue;
            }
            if (frame != nullptr) {
                StoreLuma(*frame);
            }
            if (depth != nullptr) {
                StoreDepth(*depth);
            }
        }
    }

    void StoreDepth(const dai::ImgFrame& frame) {
        const int width = static_cast<int>(frame.getWidth());
        const int height = static_cast<int>(frame.getHeight());
        if (width <= 0 || height <= 0) {
            return;
        }
        std::shared_ptr<dai::RawImgFrame> raw = std::static_pointer_cast<dai::RawImgFrame>(frame.getRaw());
        const std::vector<std::uint8_t>& data = frame.getData();
        size_t strideBytes = static_cast<size_t>(width) * 2;
        size_t offset = 0;
        if (raw != nullptr) {
            if (raw->fb.stride >= static_cast<unsigned int>(width) * 2) {
                strideBytes = raw->fb.stride;
            }
            offset = raw->fb.p1Offset;
        }
        const size_t needed = offset + strideBytes * static_cast<size_t>(height - 1) + static_cast<size_t>(width) * 2;
        if (data.size() < needed) {
            return;
        }
        std::lock_guard<std::mutex> lock(frameMutex);
        latestDepth.resize(static_cast<size_t>(width) * static_cast<size_t>(height));
        for (int row = 0; row < height; ++row) {
            std::memcpy(latestDepth.data() + static_cast<size_t>(row) * static_cast<size_t>(width),
                        data.data() + offset + static_cast<size_t>(row) * strideBytes, static_cast<size_t>(width) * 2);
        }
        depthWidth = width;
        depthHeight = height;
        depthSequence.fetch_add(1, std::memory_order_release);
    }

    void StoreLuma(const dai::ImgFrame& frame) {
        const int width = static_cast<int>(frame.getWidth());
        const int height = static_cast<int>(frame.getHeight());
        if (width <= 0 || height <= 0) {
            return;
        }
        std::shared_ptr<dai::RawImgFrame> raw = std::static_pointer_cast<dai::RawImgFrame>(frame.getRaw());
        const std::vector<std::uint8_t>& data = frame.getData();
        size_t stride = static_cast<size_t>(width);
        size_t offset = 0;
        if (raw != nullptr) {
            if (raw->fb.stride >= static_cast<unsigned int>(width)) {
                stride = raw->fb.stride;
            }
            offset = raw->fb.p1Offset;
        }
        const size_t needed = offset + stride * static_cast<size_t>(height - 1) + static_cast<size_t>(width);
        if (data.size() < needed) {
            return;
        }
        std::lock_guard<std::mutex> lock(frameMutex);
        latestGray.resize(static_cast<size_t>(width) * static_cast<size_t>(height));
        for (int row = 0; row < height; ++row) {
            std::memcpy(latestGray.data() + static_cast<size_t>(row) * static_cast<size_t>(width),
                        data.data() + offset + static_cast<size_t>(row) * stride, static_cast<size_t>(width));
        }
        frameWidth = width;
        frameHeight = height;
        sequence.fetch_add(1, std::memory_order_release);
    }

    void ReadIntrinsics(int width, int height) {
        hasIntrinsics = false;
        try {
            dai::CalibrationHandler calibration = device->readCalibration();
            const std::vector<std::vector<float>> matrix =
                calibration.getCameraIntrinsics(dai::CameraBoardSocket::CAM_A, width, height);
            const std::vector<float> coefficients = calibration.getDistortionCoefficients(dai::CameraBoardSocket::CAM_A);
            const dai::CameraModel model = calibration.getDistortionModel(dai::CameraBoardSocket::CAM_A);
            if (matrix.size() < 3 || matrix[0].size() < 3 || matrix[1].size() < 3) {
                return;
            }
            pmoak_intrinsics_t result{};
            result.width = width;
            result.height = height;
            result.model = static_cast<int>(model);
            result.fx = matrix[0][0];
            result.fy = matrix[1][1];
            result.cx = matrix[0][2];
            result.cy = matrix[1][2];
            const size_t count = std::min<size_t>(coefficients.size(), 14);
            result.distortionCount = static_cast<int>(count);
            for (size_t i = 0; i < count; ++i) {
                result.distortion[i] = coefficients[i];
            }
            intrinsics = result;
            hasIntrinsics = true;
        } catch (const std::exception& e) {
            lastError = std::string("pmoak_start: calibration read failed (frames still stream): ") + e.what();
        }
    }

    void Teardown() {
        running.store(false);
        if (worker.joinable()) {
            worker.join();
        }
        videoQueue.reset();
        depthQueue.reset();
        controlQueue.reset();
        device.reset();
    }
};

extern "C" {

PMOAK_API void pmoak_options_default(pmoak_options_t* options) {
    if (options == nullptr) {
        return;
    }
    options->maxWidth = 1920;
    options->fps = 15.0f;
    options->mxId = nullptr;
    options->enableDepth = 0;
}

PMOAK_API pmoak_camera_t* pmoak_create(const pmoak_options_t* options) {
    try {
        pmoak_camera_t* camera = new pmoak_camera_t();
        pmoak_options_default(&camera->options);
        if (options != nullptr) {
            camera->options.maxWidth = options->maxWidth > 0 ? options->maxWidth : 1920;
            camera->options.fps = options->fps > 0.0f ? options->fps : 15.0f;
            if (options->mxId != nullptr) {
                camera->mxId = options->mxId;
            }
            camera->options.enableDepth = options->enableDepth != 0 ? 1 : 0;
        }
        camera->options.mxId = nullptr; // the copied string lives in camera->mxId
        return camera;
    } catch (...) {
        return nullptr;
    }
}

PMOAK_API void pmoak_destroy(pmoak_camera_t* camera) {
    if (camera == nullptr) {
        return;
    }
    camera->Teardown();
    delete camera;
}

PMOAK_API int pmoak_start(pmoak_camera_t* camera) {
    if (camera == nullptr) {
        return PMOAK_ERROR_INVALID_ARGUMENT;
    }
    if (camera->running.load()) {
        return PMOAK_OK;
    }
    camera->lastError.clear();
    try {
        if (camera->mxId.empty()) {
            camera->device = std::make_unique<dai::Device>();
        } else {
            camera->device = std::make_unique<dai::Device>(dai::DeviceInfo(camera->mxId));
        }

        dai::CameraFeatures colourFeatures;
        bool hasColour = false;
        for (const dai::CameraFeatures& features : camera->device->getConnectedCameraFeatures()) {
            if (features.socket == dai::CameraBoardSocket::CAM_A) {
                colourFeatures = features;
                hasColour = true;
                break;
            }
        }
        if (!hasColour) {
            camera->lastError = "pmoak_start: the device reports no camera on socket CAM_A";
            camera->Teardown();
            return PMOAK_ERROR_DEVICE;
        }
        SensorMode mode{};
        if (!ChooseSensorMode(colourFeatures, mode)) {
            camera->lastError = "pmoak_start: no supported sensor mode for CAM_A sensor '" + colourFeatures.sensorName + "'";
            camera->Teardown();
            return PMOAK_ERROR_DEVICE;
        }
        int numerator = 1;
        int denominator = 1;
        ChooseIspScale(mode.width, mode.height, camera->options.maxWidth, numerator, denominator);
        const int outWidth = mode.width * numerator / denominator;
        const int outHeight = mode.height * numerator / denominator;

        dai::Pipeline pipeline;
        std::shared_ptr<dai::node::ColorCamera> colour = pipeline.create<dai::node::ColorCamera>();
        std::shared_ptr<dai::node::XLinkOut> videoOut = pipeline.create<dai::node::XLinkOut>();
        std::shared_ptr<dai::node::XLinkIn> controlIn = pipeline.create<dai::node::XLinkIn>();
        colour->setBoardSocket(dai::CameraBoardSocket::CAM_A);
        colour->setResolution(mode.resolution);
        colour->setIspScale(numerator, denominator);
        colour->setVideoSize(outWidth, outHeight);
        colour->setFps(camera->options.fps);
        videoOut->setStreamName(VideoStreamName);
        controlIn->setStreamName(ControlStreamName);
        colour->video.link(videoOut->input);
        controlIn->out.link(colour->inputControl);

        bool depthEnabled = false;
        if (camera->options.enableDepth != 0) {
            bool hasLeft = false;
            bool hasRight = false;
            for (const dai::CameraFeatures& features : camera->device->getConnectedCameraFeatures()) {
                hasLeft = hasLeft || features.socket == dai::CameraBoardSocket::CAM_B;
                hasRight = hasRight || features.socket == dai::CameraBoardSocket::CAM_C;
            }
            if (!hasLeft || !hasRight) {
                camera->lastError = "pmoak_start: depth requested but the device has no stereo pair (CAM_B/CAM_C)";
                camera->Teardown();
                return PMOAK_ERROR_DEVICE;
            }
            // Stereo depth aligned to the colour camera at the colour output size, so a depth pixel
            // and a colour pixel are the same ray and the colour intrinsics apply to both.
            std::shared_ptr<dai::node::MonoCamera> left = pipeline.create<dai::node::MonoCamera>();
            std::shared_ptr<dai::node::MonoCamera> right = pipeline.create<dai::node::MonoCamera>();
            std::shared_ptr<dai::node::StereoDepth> stereo = pipeline.create<dai::node::StereoDepth>();
            std::shared_ptr<dai::node::XLinkOut> depthOut = pipeline.create<dai::node::XLinkOut>();
            left->setBoardSocket(dai::CameraBoardSocket::CAM_B);
            right->setBoardSocket(dai::CameraBoardSocket::CAM_C);
            left->setResolution(dai::MonoCameraProperties::SensorResolution::THE_400_P);
            right->setResolution(dai::MonoCameraProperties::SensorResolution::THE_400_P);
            left->setFps(camera->options.fps);
            right->setFps(camera->options.fps);
            stereo->setDefaultProfilePreset(dai::node::StereoDepth::PresetMode::DEFAULT);
            stereo->setLeftRightCheck(true);
            stereo->setSubpixel(true);
            stereo->setDepthAlign(dai::CameraBoardSocket::CAM_A);
            stereo->setOutputSize(outWidth, outHeight);
            depthOut->setStreamName(DepthStreamName);
            left->out.link(stereo->left);
            right->out.link(stereo->right);
            stereo->depth.link(depthOut->input);
            depthEnabled = true;
        }

        camera->device->startPipeline(pipeline);
        camera->videoQueue = camera->device->getOutputQueue(VideoStreamName, 2, false);
        if (depthEnabled) {
            camera->depthQueue = camera->device->getOutputQueue(DepthStreamName, 2, false);
        }
        camera->depthSequence.store(0);
        camera->controlQueue = camera->device->getInputQueue(ControlStreamName);
        camera->description = camera->device->getMxId() + " " + colourFeatures.sensorName;
        camera->ReadIntrinsics(outWidth, outHeight);

        {
            std::lock_guard<std::mutex> lock(camera->frameMutex);
            camera->latestGray.clear();
            camera->frameWidth = outWidth;
            camera->frameHeight = outHeight;
        }
        camera->sequence.store(0);
        camera->running.store(true);
        camera->worker = std::thread([camera]() { camera->WorkerLoop(); });
        return PMOAK_OK;
    } catch (const std::exception& e) {
        camera->lastError = std::string("pmoak_start: ") + e.what();
        camera->Teardown();
        return PMOAK_ERROR_DEVICE;
    } catch (...) {
        camera->lastError = "pmoak_start: unknown error";
        camera->Teardown();
        return PMOAK_ERROR_DEVICE;
    }
}

PMOAK_API int pmoak_stop(pmoak_camera_t* camera) {
    if (camera == nullptr) {
        return PMOAK_ERROR_INVALID_ARGUMENT;
    }
    try {
        camera->Teardown();
        return PMOAK_OK;
    } catch (const std::exception& e) {
        camera->lastError = std::string("pmoak_stop: ") + e.what();
        return PMOAK_ERROR_DEVICE;
    }
}

PMOAK_API int pmoak_is_running(pmoak_camera_t* camera) {
    if (camera == nullptr) {
        return 0;
    }
    return camera->running.load() ? 1 : 0;
}

PMOAK_API int pmoak_get_frame_size(pmoak_camera_t* camera, int* outWidth, int* outHeight) {
    if (camera == nullptr || outWidth == nullptr || outHeight == nullptr) {
        return PMOAK_ERROR_INVALID_ARGUMENT;
    }
    std::lock_guard<std::mutex> lock(camera->frameMutex);
    *outWidth = camera->frameWidth;
    *outHeight = camera->frameHeight;
    return camera->frameWidth > 0 ? PMOAK_OK : PMOAK_ERROR_NOT_RUNNING;
}

PMOAK_API int64_t pmoak_latest_sequence(pmoak_camera_t* camera) {
    if (camera == nullptr) {
        return 0;
    }
    return camera->sequence.load(std::memory_order_acquire);
}

PMOAK_API int pmoak_copy_latest_gray(pmoak_camera_t* camera, uint8_t* buffer, size_t capacity,
                                     int* outWidth, int* outHeight, int64_t* outSequence) {
    if (camera == nullptr || buffer == nullptr) {
        return PMOAK_ERROR_INVALID_ARGUMENT;
    }
    std::lock_guard<std::mutex> lock(camera->frameMutex);
    if (camera->latestGray.empty()) {
        return camera->running.load() ? PMOAK_NO_FRAME : PMOAK_ERROR_NOT_RUNNING;
    }
    if (capacity < camera->latestGray.size()) {
        camera->lastError = "pmoak_copy_latest_gray: buffer too small";
        return PMOAK_ERROR_BUFFER_TOO_SMALL;
    }
    std::memcpy(buffer, camera->latestGray.data(), camera->latestGray.size());
    if (outWidth != nullptr) {
        *outWidth = camera->frameWidth;
    }
    if (outHeight != nullptr) {
        *outHeight = camera->frameHeight;
    }
    if (outSequence != nullptr) {
        *outSequence = camera->sequence.load(std::memory_order_acquire);
    }
    return PMOAK_OK;
}

PMOAK_API int64_t pmoak_latest_depth_sequence(pmoak_camera_t* camera) {
    if (camera == nullptr) {
        return 0;
    }
    return camera->depthSequence.load(std::memory_order_acquire);
}

PMOAK_API int pmoak_copy_latest_depth(pmoak_camera_t* camera, uint16_t* buffer, size_t capacity, int* outWidth, int* outHeight,
                                      int64_t* outSequence) {
    if (camera == nullptr || buffer == nullptr) {
        return PMOAK_ERROR_INVALID_ARGUMENT;
    }
    if (camera->depthQueue == nullptr) {
        camera->lastError = "pmoak_copy_latest_depth: depth is not enabled (set enableDepth before start)";
        return PMOAK_ERROR_NOT_RUNNING;
    }
    std::lock_guard<std::mutex> lock(camera->frameMutex);
    if (camera->latestDepth.empty()) {
        return camera->running.load() ? PMOAK_NO_FRAME : PMOAK_ERROR_NOT_RUNNING;
    }
    if (capacity < camera->latestDepth.size()) {
        camera->lastError = "pmoak_copy_latest_depth: buffer too small";
        return PMOAK_ERROR_BUFFER_TOO_SMALL;
    }
    std::memcpy(buffer, camera->latestDepth.data(), camera->latestDepth.size() * sizeof(uint16_t));
    if (outWidth != nullptr) {
        *outWidth = camera->depthWidth;
    }
    if (outHeight != nullptr) {
        *outHeight = camera->depthHeight;
    }
    if (outSequence != nullptr) {
        *outSequence = camera->depthSequence.load(std::memory_order_acquire);
    }
    return PMOAK_OK;
}

namespace {

int SendControl(pmoak_camera_t* camera, const dai::CameraControl& control, const char* what) {
    if (camera == nullptr) {
        return PMOAK_ERROR_INVALID_ARGUMENT;
    }
    if (!camera->running.load() || camera->controlQueue == nullptr) {
        return PMOAK_ERROR_NOT_RUNNING;
    }
    try {
        camera->controlQueue->send(control);
        return PMOAK_OK;
    } catch (const std::exception& e) {
        camera->lastError = std::string(what) + ": " + e.what();
        return PMOAK_ERROR_DEVICE;
    }
}

} // namespace

PMOAK_API int pmoak_set_manual_exposure(pmoak_camera_t* camera, uint32_t exposureUs, uint32_t iso) {
    if (exposureUs < 1 || exposureUs > 33000 || iso < 100 || iso > 1600) {
        if (camera != nullptr) {
            camera->lastError = "pmoak_set_manual_exposure: exposure must be 1..33000 us and ISO 100..1600";
        }
        return PMOAK_ERROR_INVALID_ARGUMENT;
    }
    dai::CameraControl control;
    control.setManualExposure(exposureUs, iso);
    return SendControl(camera, control, "pmoak_set_manual_exposure");
}

PMOAK_API int pmoak_set_auto_exposure(pmoak_camera_t* camera) {
    dai::CameraControl control;
    control.setAutoExposureEnable();
    return SendControl(camera, control, "pmoak_set_auto_exposure");
}

PMOAK_API int pmoak_set_manual_focus(pmoak_camera_t* camera, int lensPosition) {
    if (lensPosition < 0 || lensPosition > 255) {
        if (camera != nullptr) {
            camera->lastError = "pmoak_set_manual_focus: lens position must be 0..255";
        }
        return PMOAK_ERROR_INVALID_ARGUMENT;
    }
    dai::CameraControl control;
    control.setManualFocus(static_cast<uint8_t>(lensPosition));
    return SendControl(camera, control, "pmoak_set_manual_focus");
}

PMOAK_API int pmoak_get_intrinsics(pmoak_camera_t* camera, pmoak_intrinsics_t* outIntrinsics) {
    if (camera == nullptr || outIntrinsics == nullptr) {
        return PMOAK_ERROR_INVALID_ARGUMENT;
    }
    if (!camera->hasIntrinsics) {
        return camera->running.load() ? PMOAK_ERROR_DEVICE : PMOAK_ERROR_NOT_RUNNING;
    }
    *outIntrinsics = camera->intrinsics;
    return PMOAK_OK;
}

PMOAK_API const char* pmoak_device_description(pmoak_camera_t* camera) {
    if (camera == nullptr) {
        return "";
    }
    return camera->description.c_str();
}

PMOAK_API const char* pmoak_last_error(pmoak_camera_t* camera) {
    if (camera == nullptr) {
        return "invalid camera handle";
    }
    return camera->lastError.c_str();
}

} // extern "C"
