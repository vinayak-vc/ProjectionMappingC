#include <gtest/gtest.h>
#include "PMSDK/Blend/MaskGenerator.h"

using namespace pmsdk::Blend;

TEST(MaskGeneratorTests, GenerateFloatMask) {
    BlendConfig config;
    config.GetLeftEdge().SetSize(0.5f);
    config.GetLeftEdge().SetCurveType(CurveType::Linear);
    
    // Width 3, Height 1 -> u will be 0.0, 0.5, 1.0
    auto mask = MaskGenerator::GenerateFloatMask(config, 3, 1);
    
    ASSERT_EQ(mask.size(), 3);
    EXPECT_FLOAT_EQ(mask[0], 0.0f); // u=0.0 -> edge = 0.0
    EXPECT_FLOAT_EQ(mask[1], 1.0f); // u=0.5 -> edge = 1.0 (since size is 0.5)
    EXPECT_FLOAT_EQ(mask[2], 1.0f); // u=1.0 -> outside blend = 1.0
}

TEST(MaskGeneratorTests, GenerateByteMask) {
    BlendConfig config;
    config.GetTopEdge().SetSize(0.5f);
    config.GetTopEdge().SetCurveType(CurveType::Linear);
    
    // Width 1, Height 3 -> v will be 0.0, 0.5, 1.0
    // Top edge distance is (1.0 - v).
    // y=0 (v=0) -> dist=1.0 -> fully visible (255)
    // y=1 (v=0.5) -> dist=0.5 -> edge of blend -> fully visible (255)
    // y=2 (v=1.0) -> dist=0.0 -> fully masked (0)
    auto mask = MaskGenerator::GenerateByteMask(config, 1, 3);
    
    ASSERT_EQ(mask.size(), 3);
    EXPECT_EQ(mask[0], 255);
    EXPECT_EQ(mask[1], 255);
    EXPECT_EQ(mask[2], 0);
}
