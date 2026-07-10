#include <gtest/gtest.h>
#include "PMSDK/Calibration/Intrinsics.h"

using namespace pmsdk::Calibration;

TEST(IntrinsicsTests, DefaultValues) {
    Intrinsics intr;
    float fx, fy, cx, cy;
    intr.GetCameraMatrix(fx, fy, cx, cy);
    EXPECT_FLOAT_EQ(fx, 1.0f);
    EXPECT_FLOAT_EQ(fy, 1.0f);
    EXPECT_FLOAT_EQ(cx, 0.0f);
    EXPECT_FLOAT_EQ(cy, 0.0f);
}

TEST(IntrinsicsTests, SetGetMatrix) {
    Intrinsics intr;
    intr.SetCameraMatrix(1920.0f, 1080.0f, 960.0f, 540.0f);
    float fx, fy, cx, cy;
    intr.GetCameraMatrix(fx, fy, cx, cy);
    EXPECT_FLOAT_EQ(fx, 1920.0f);
    EXPECT_FLOAT_EQ(fy, 1080.0f);
    EXPECT_FLOAT_EQ(cx, 960.0f);
    EXPECT_FLOAT_EQ(cy, 540.0f);
}
