#include <gtest/gtest.h>
#include "PMSDK/Calibration/Extrinsics.h"

using namespace pmsdk::Calibration;

TEST(ExtrinsicsTests, ToMatrixNoRotation) {
    Extrinsics ext;
    ext.SetTranslationVector({1.0f, 2.0f, 3.0f});
    auto m = ext.ToMatrix();
    
    // Identity rotation
    EXPECT_FLOAT_EQ(m[0], 1.0f); // m00
    EXPECT_FLOAT_EQ(m[5], 1.0f); // m11
    EXPECT_FLOAT_EQ(m[10], 1.0f); // m22
    
    // Translation
    EXPECT_FLOAT_EQ(m[12], 1.0f); // m03
    EXPECT_FLOAT_EQ(m[13], 2.0f); // m13
    EXPECT_FLOAT_EQ(m[14], 3.0f); // m23
}

TEST(ExtrinsicsTests, RotationConversion) {
    Extrinsics ext;
    // 90 degrees around X axis
    // theta = pi/2, axis = (1, 0, 0)
    // Rodrigues vector = axis * theta
    ext.SetRotationVector({3.14159265f / 2.0f, 0.0f, 0.0f});
    auto m = ext.ToMatrix();
    
    // cos(90) = 0, sin(90) = 1
    EXPECT_FLOAT_EQ(m[0], 1.0f); // m00
    EXPECT_NEAR(m[5], 0.0f, 1e-5f); // m11
    EXPECT_NEAR(m[10], 0.0f, 1e-5f); // m22
    EXPECT_NEAR(m[6], 1.0f, 1e-5f); // m21 -> col 1, row 2 -> m[6]
    EXPECT_NEAR(m[9], -1.0f, 1e-5f); // m12 -> col 2, row 1 -> m[9]
}
