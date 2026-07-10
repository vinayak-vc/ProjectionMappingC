#include <gtest/gtest.h>
#include "PMSDK/Blend/EdgeBlend.h"

using namespace pmsdk::Blend;

TEST(EdgeBlendTests, DefaultProperties) {
    EdgeBlend blend;
    EXPECT_FLOAT_EQ(blend.GetSize(), 0.0f);
    EXPECT_FLOAT_EQ(blend.GetGamma(), 2.2f);
    EXPECT_EQ(blend.GetCurveType(), CurveType::Power);
}

TEST(EdgeBlendTests, LinearCurve) {
    EdgeBlend blend;
    blend.SetSize(0.2f);
    blend.SetCurveType(CurveType::Linear);

    // Outside blend zone (fully visible)
    EXPECT_FLOAT_EQ(blend.Evaluate(0.5f), 1.0f);
    EXPECT_FLOAT_EQ(blend.Evaluate(0.2f), 1.0f);
    
    // Exactly at edge (fully black/transparent)
    EXPECT_FLOAT_EQ(blend.Evaluate(0.0f), 0.0f);
    
    // Middle of blend zone
    EXPECT_FLOAT_EQ(blend.Evaluate(0.1f), 0.5f);
}

TEST(EdgeBlendTests, PowerCurve) {
    EdgeBlend blend;
    blend.SetSize(0.5f);
    blend.SetGamma(2.0f); // x^2
    blend.SetCurveType(CurveType::Power);

    EXPECT_FLOAT_EQ(blend.Evaluate(0.0f), 0.0f);
    EXPECT_FLOAT_EQ(blend.Evaluate(0.5f), 1.0f);
    
    // x = 0.25 is halfway into the 0.5 size blend zone -> normalized x = 0.5
    // 0.5^2 = 0.25
    EXPECT_FLOAT_EQ(blend.Evaluate(0.25f), 0.25f);
}
