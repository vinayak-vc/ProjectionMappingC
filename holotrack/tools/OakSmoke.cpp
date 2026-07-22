#include "HoloTrack/C_API/DeviceAPI.h"

#include <chrono>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <string>
#include <thread>

namespace {

enum class SmokeMode {
    Person,
    Face,
    FaceThenPerson
};

struct SmokeOptions {
    SmokeMode mode{SmokeMode::FaceThenPerson};
    std::string personBlob;
    std::string faceBlob;
    int seconds{10};
};

void PrintUsage() {
    std::cout
        << "Usage: holotrack_oak_smoke [--mode person|face|both] "
        << "[--person-blob path] [--face-blob path] [--seconds n]\n";
}

bool Equals(const char* a, const char* b) {
    return std::strcmp(a, b) == 0;
}

bool ParseArgs(int argc, char** argv, SmokeOptions* out) {
    if (out == nullptr) {
        return false;
    }
    for (int i = 1; i < argc; ++i) {
        const char* arg = argv[i];
        if (Equals(arg, "--help") || Equals(arg, "-h")) {
            PrintUsage();
            return false;
        }
        if (Equals(arg, "--mode") && i + 1 < argc) {
            const char* value = argv[++i];
            if (Equals(value, "person")) {
                out->mode = SmokeMode::Person;
            } else if (Equals(value, "face")) {
                out->mode = SmokeMode::Face;
            } else if (Equals(value, "both") || Equals(value, "face-then-person")) {
                out->mode = SmokeMode::FaceThenPerson;
            } else {
                std::cerr << "Invalid --mode: " << value << "\n";
                return false;
            }
        } else if (Equals(arg, "--person-blob") && i + 1 < argc) {
            out->personBlob = argv[++i];
        } else if (Equals(arg, "--face-blob") && i + 1 < argc) {
            out->faceBlob = argv[++i];
        } else if (Equals(arg, "--seconds") && i + 1 < argc) {
            out->seconds = std::atoi(argv[++i]);
            if (out->seconds <= 0) {
                std::cerr << "--seconds must be positive\n";
                return false;
            }
        } else {
            std::cerr << "Unknown or incomplete argument: " << arg << "\n";
            PrintUsage();
            return false;
        }
    }
    return true;
}

int ToNativeMode(SmokeMode mode) {
    switch (mode) {
        case SmokeMode::Person:
            return HT_DETECT_PERSON;
        case SmokeMode::Face:
            return HT_DETECT_FACE;
        case SmokeMode::FaceThenPerson:
            return HT_DETECT_FACE_THEN_PERSON;
        default:
            return HT_DETECT_FACE_THEN_PERSON;
    }
}

bool HasRequiredBlobs(const SmokeOptions& options) {
    if ((options.mode == SmokeMode::Person || options.mode == SmokeMode::FaceThenPerson) &&
        options.personBlob.empty()) {
        std::cerr << "Missing --person-blob for the selected mode\n";
        return false;
    }
    if ((options.mode == SmokeMode::Face || options.mode == SmokeMode::FaceThenPerson) &&
        options.faceBlob.empty()) {
        std::cerr << "Missing --face-blob for the selected mode\n";
        return false;
    }
    return true;
}

} // namespace

int main(int argc, char** argv) {
    SmokeOptions options;
    if (!ParseArgs(argc, argv, &options)) {
        return 2;
    }
    if (!HasRequiredBlobs(options)) {
        PrintUsage();
        return 2;
    }
    if (ht_oak_is_supported() == 0) {
        std::cerr << "HoloTrackSDK was built without DepthAI support.\n";
        return 3;
    }

    ht_oak_options_t nativeOptions{};
    nativeOptions.detectionMode = ToNativeMode(options.mode);
    nativeOptions.blobPath = options.personBlob.c_str();
    nativeOptions.faceBlobPath = options.faceBlob.c_str();
    nativeOptions.faceFallbackFrames = 15;
    nativeOptions.personLabel = 15;
    nativeOptions.confidenceThreshold = 0.5f;
    nativeOptions.depthLowerThresholdMm = 300.0f;
    nativeOptions.depthUpperThresholdMm = 8000.0f;

    ht_oak_source_t* source = ht_oak_create(&nativeOptions);
    if (source == nullptr) {
        std::cerr << "ht_oak_create failed: " << ht_oak_last_error(nullptr) << "\n";
        return 4;
    }

    if (ht_oak_start(source) != HT_SUCCESS) {
        std::cerr << "ht_oak_start failed: " << ht_oak_last_error(source) << "\n";
        ht_oak_destroy(source);
        return 5;
    }

    ht_detection_t detections[32]{};
    int frames = 0;
    int framesWithDetections = 0;
    int totalDetections = 0;
    const std::chrono::steady_clock::time_point end =
        std::chrono::steady_clock::now() + std::chrono::seconds(options.seconds);

    while (std::chrono::steady_clock::now() < end) {
        size_t count = 0;
        int hasFrame = 0;
        double timestampSeconds = 0.0;
        const ht_status_t status = ht_oak_poll(source, detections, 32, &count, &hasFrame, &timestampSeconds);
        if (status != HT_SUCCESS) {
            std::cerr << "ht_oak_poll failed: " << ht_oak_last_error(source) << "\n";
            ht_oak_stop(source);
            ht_oak_destroy(source);
            return 6;
        }
        if (hasFrame != 0) {
            ++frames;
            totalDetections += static_cast<int>(count);
            if (count > 0) {
                ++framesWithDetections;
                const ht_detection_t& first = detections[0];
                std::cout << "frame=" << frames
                          << " detections=" << count
                          << " first=(" << first.spatial.x << ", "
                          << first.spatial.y << ", " << first.spatial.z << ")"
                          << " confidence=" << first.confidence
                          << " ts=" << timestampSeconds << "\n";
            }
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(2));
    }

    ht_oak_stop(source);
    ht_oak_destroy(source);

    std::cout << "OAK smoke complete: frames=" << frames
              << " framesWithDetections=" << framesWithDetections
              << " totalDetections=" << totalDetections << "\n";
    return frames > 0 ? 0 : 7;
}
