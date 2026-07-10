#include <gtest/gtest.h>
#include "PMSDK/Serialization/BlendSerializer.h"

using namespace pmsdk;
using namespace pmsdk::Blend;

TEST(BlendSerializerTests, SerializeBlendConfig) {
    BlendConfig config;
    config.GetLeftEdge().SetSize(0.25f);
    config.GetLeftEdge().SetCurveType(CurveType::Linear);
    config.SetBlackLevel(0.1f);
    
    std::string jsonStr = Serialization::SerializeBlendConfig(config);
    
    BlendConfig out;
    Serialization::DeserializeBlendConfig(jsonStr, out);
    
    EXPECT_FLOAT_EQ(out.GetLeftEdge().GetSize(), 0.25f);
    EXPECT_EQ(out.GetLeftEdge().GetCurveType(), CurveType::Linear);
    EXPECT_FLOAT_EQ(out.GetBlackLevel(), 0.1f);
}
