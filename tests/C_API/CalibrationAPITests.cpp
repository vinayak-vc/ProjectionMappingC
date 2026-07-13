#include "PMSDK/C_API/CalibrationAPI.h"
#include <gtest/gtest.h>
#include <vector>

TEST(CalibrationAPITests, GrayCodeLifecycle) {
    pmsdk_graycode_t* handle = pmsdk_graycode_create(1920, 1080);
    EXPECT_NE(handle, nullptr);

    size_t count = pmsdk_graycode_get_pattern_count(handle);
    EXPECT_GT(count, 0);

    std::vector<uint8_t> pixels(1920 * 1080);
    pmsdk_status_t status = pmsdk_graycode_generate_pattern(handle, 0, pixels.data());
    EXPECT_EQ(status, PMSDK_SUCCESS);

    // Verify some pixels were written (not all zero)
    bool hasData = false;
    for (auto p : pixels) {
        if (p != 0) {
            hasData = true;
            break;
        }
    }
    EXPECT_TRUE(hasData);

    pmsdk_graycode_destroy(handle);
}

TEST(CalibrationAPITests, CalibratorLifecycle) {
    pmsdk_calibrator_t* handle = pmsdk_calibrator_create();
    EXPECT_NE(handle, nullptr);

    // Add a dummy observation
    pmsdk_vec3_t objPts[4] = {
        {0, 0, 0}, {1, 0, 0}, {1, 1, 0}, {0, 1, 0}
    };
    pmsdk_vec2_t imgPts[4] = {
        {10, 10}, {20, 10}, {20, 20}, {10, 20}
    };

    pmsdk_status_t status = pmsdk_calibrator_add_observation(handle, objPts, imgPts, 4);
    EXPECT_EQ(status, PMSDK_SUCCESS);

    float intrinsics[4];
    float distortion[5];
    double rms;

    status = pmsdk_calibrator_calibrate(handle, 1920, 1080, intrinsics, distortion, &rms);
    
    (void)status;

    pmsdk_calibrator_destroy(handle);
}
