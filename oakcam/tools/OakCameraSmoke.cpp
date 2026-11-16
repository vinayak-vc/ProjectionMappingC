// Hardware smoke test for PMSDKOakCamera.dll.
// Usage: pmsdk_oakcam_smoke [output.pgm] [seconds] [depth]
// Opens the first OAK device, streams for a few seconds, prints frame count, size, intrinsics and
// writes the last luma frame as a binary PGM. With "depth" it also streams aligned stereo depth and
// prints the depth frame count and the centre depth. Exit code 0 on success.
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <string>
#include <thread>
#include <vector>

#include "PMSDKOak/OakCameraAPI.h"

int main(int argc, char** argv) {
    const std::string outputPath = argc > 1 ? argv[1] : "pmoak_smoke.pgm";
    const int seconds = argc > 2 ? std::atoi(argv[2]) : 5;
    const bool withDepth = argc > 3 && std::string(argv[3]) == "depth";

    pmoak_options_t options;
    pmoak_options_default(&options);
    options.enableDepth = withDepth ? 1 : 0;
    pmoak_camera_t* camera = pmoak_create(&options);
    if (camera == nullptr) {
        std::printf("FAIL: pmoak_create returned null\n");
        return 1;
    }
    const int startStatus = pmoak_start(camera);
    if (startStatus != PMOAK_OK) {
        std::printf("FAIL: pmoak_start = %d: %s\n", startStatus, pmoak_last_error(camera));
        pmoak_destroy(camera);
        return 2;
    }
    std::printf("device: %s\n", pmoak_device_description(camera));

    int width = 0;
    int height = 0;
    pmoak_get_frame_size(camera, &width, &height);
    std::printf("frame size: %d x %d\n", width, height);

    pmoak_intrinsics_t intrinsics;
    if (pmoak_get_intrinsics(camera, &intrinsics) == PMOAK_OK) {
        std::printf("intrinsics: model=%d fx=%.2f fy=%.2f cx=%.2f cy=%.2f coeffs=%d [",
                    intrinsics.model, intrinsics.fx, intrinsics.fy, intrinsics.cx, intrinsics.cy,
                    intrinsics.distortionCount);
        for (int i = 0; i < intrinsics.distortionCount; ++i) {
            std::printf("%s%.5f", i == 0 ? "" : ", ", intrinsics.distortion[i]);
        }
        std::printf("]\n");
    } else {
        std::printf("intrinsics: unavailable (%s)\n", pmoak_last_error(camera));
    }

    std::this_thread::sleep_for(std::chrono::seconds(seconds));
    const long long frames = static_cast<long long>(pmoak_latest_sequence(camera));
    std::printf("frames in %d s: %lld (%.1f fps)\n", seconds, frames, seconds > 0 ? static_cast<double>(frames) / seconds : 0.0);

    std::vector<uint8_t> gray(static_cast<size_t>(width) * static_cast<size_t>(height));
    int frameWidth = 0;
    int frameHeight = 0;
    int64_t sequence = 0;
    const int copyStatus = pmoak_copy_latest_gray(camera, gray.data(), gray.size(), &frameWidth, &frameHeight, &sequence);
    int exitCode = 0;
    if (copyStatus == PMOAK_OK) {
        FILE* file = nullptr;
#ifdef _MSC_VER
        if (fopen_s(&file, outputPath.c_str(), "wb") != 0) {
            file = nullptr;
        }
#else
        file = std::fopen(outputPath.c_str(), "wb");
#endif
        if (file != nullptr) {
            std::fprintf(file, "P5\n%d %d\n255\n", frameWidth, frameHeight);
            std::fwrite(gray.data(), 1, gray.size(), file);
            std::fclose(file);
            std::printf("wrote %s (frame %lld)\n", outputPath.c_str(), static_cast<long long>(sequence));
        } else {
            std::printf("FAIL: cannot write %s\n", outputPath.c_str());
            exitCode = 3;
        }
    } else {
        std::printf("FAIL: pmoak_copy_latest_gray = %d: %s\n", copyStatus, pmoak_last_error(camera));
        exitCode = 4;
    }
    if (frames == 0 && exitCode == 0) {
        exitCode = 5;
    }
    if (withDepth) {
        const long long depthFrames = static_cast<long long>(pmoak_latest_depth_sequence(camera));
        std::vector<uint16_t> depth(static_cast<size_t>(width) * static_cast<size_t>(height));
        int depthWidth = 0;
        int depthHeight = 0;
        int64_t depthSequence = 0;
        const int depthStatus = pmoak_copy_latest_depth(camera, depth.data(), depth.size(), &depthWidth, &depthHeight, &depthSequence);
        if (depthStatus == PMOAK_OK) {
            size_t valid = 0;
            for (uint16_t d : depth) {
                valid += d != 0 ? 1 : 0;
            }
            std::printf("depth: %lld frames, %d x %d, centre %u mm, %.1f %% valid\n", depthFrames, depthWidth, depthHeight,
                        depth[static_cast<size_t>(depthHeight / 2) * static_cast<size_t>(depthWidth) + static_cast<size_t>(depthWidth / 2)],
                        100.0 * static_cast<double>(valid) / static_cast<double>(depth.size()));
        } else {
            std::printf("FAIL: pmoak_copy_latest_depth = %d: %s\n", depthStatus, pmoak_last_error(camera));
            if (exitCode == 0) {
                exitCode = 6;
            }
        }
    }

    pmoak_stop(camera);
    pmoak_destroy(camera);
    std::printf(exitCode == 0 ? "PASS\n" : "FAIL\n");
    return exitCode;
}
