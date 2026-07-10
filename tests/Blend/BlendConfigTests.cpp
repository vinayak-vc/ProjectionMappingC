#include <gtest/gtest.h>
#include "PMSDK/Blend/BlendConfig.h"

using namespace pmsdk::Blend;

TEST(BlendConfigTests, GlobalProperties) {
    BlendConfig config;
    EXPECT_FLOAT_EQ(config.GetBlackLevel(), 0.0f);
    config.SetBlackLevel(0.1f);
    EXPECT_FLOAT_EQ(config.GetBlackLevel(), 0.1f);
}

TEST(BlendConfigTests, EvaluateCorners) {
    BlendConfig config;
    // Set left and bottom blend sizes
    config.GetLeftEdge().SetSize(0.2f);
    config.GetLeftEdge().SetCurveType(CurveType::Linear);
    
    config.GetBottomEdge().SetSize(0.2f);
    config.GetBottomEdge().SetCurveType(CurveType::Linear);

    // Center should be 1.0
    EXPECT_FLOAT_EQ(config.Evaluate(0.5f, 0.5f), 1.0f);

    // Only left edge is affecting (x=0.1, y=0.5)
    // u=0.1 -> normalized x = 0.1 / 0.2 = 0.5
    EXPECT_FLOAT_EQ(config.Evaluate(0.1f, 0.5f), 0.5f);

    // Corner intersection (x=0.1, y=0.1)
    // left alpha = 0.5, bottom alpha = 0.5 -> final = 0.25
    EXPECT_FLOAT_EQ(config.Evaluate(0.1f, 0.1f), 0.25f);
}
