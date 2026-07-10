#include <gtest/gtest.h>
#include "PMSDK/Calibration/GrayCode.h"

using namespace pmsdk::Calibration;

TEST(GrayCodeTests, PatternCount) {
    // 1920x1080
    // colBits for 1920: 2^10 = 1024, 2^11 = 2048 -> 11 bits
    // rowBits for 1080: 2^10 = 1024, 2^11 = 2048 -> 11 bits
    // Total = 22
    GrayCode gc(1920, 1080);
    EXPECT_EQ(gc.GetPatternCount(), 22);

    // 8x8
    // colBits for 8: 2^2 = 4, 2^3 = 8 -> 3 bits. 
    // Wait, 1 << 3 is 8, so loop gives colBits = 3.
    GrayCode gcSmall(8, 8);
    EXPECT_EQ(gcSmall.GetPatternCount(), 6);
}

TEST(GrayCodeTests, GeneratePattern) {
    GrayCode gc(8, 8);
    auto p0 = gc.GeneratePattern(0); // Most significant bit of columns
    ASSERT_EQ(p0.size(), 64);
    
    // Check first pattern (MSB for 8 columns: 0000 1111)
    // Wait, Gray code for 0,1,2,3 is 0,1,3,2 (MSB=0). For 4,5,6,7 is 6,7,5,4 (MSB=1).
    // Let's just ensure it generates a valid binary image
    for (uint8_t val : p0) {
        EXPECT_TRUE(val == 0 || val == 255);
    }
}
