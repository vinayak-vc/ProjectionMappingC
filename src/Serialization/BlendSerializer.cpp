#include "PMSDK/Serialization/BlendSerializer.h"
#include "JsonHelpers.h"

namespace pmsdk::Blend {
    void to_json(nlohmann::json& j, const EdgeBlend& b) {
        j = nlohmann::json{
            {"size", b.GetSize()},
            {"gamma", b.GetGamma()},
            {"curveType", static_cast<int>(b.GetCurveType())}
        };
    }
    void from_json(const nlohmann::json& j, EdgeBlend& b) {
        b.SetSize(j.at("size").get<float>());
        b.SetGamma(j.at("gamma").get<float>());
        b.SetCurveType(static_cast<CurveType>(j.at("curveType").get<int>()));
    }

    void to_json(nlohmann::json& j, const BlendConfig& c) {
        j = nlohmann::json{
            {"left", c.GetLeftEdge()},
            {"right", c.GetRightEdge()},
            {"top", c.GetTopEdge()},
            {"bottom", c.GetBottomEdge()},
            {"blackLevel", c.GetBlackLevel()}
        };
    }
    void from_json(const nlohmann::json& j, BlendConfig& c) {
        c.GetLeftEdge() = j.at("left").get<EdgeBlend>();
        c.GetRightEdge() = j.at("right").get<EdgeBlend>();
        c.GetTopEdge() = j.at("top").get<EdgeBlend>();
        c.GetBottomEdge() = j.at("bottom").get<EdgeBlend>();
        c.SetBlackLevel(j.at("blackLevel").get<float>());
    }
}

namespace pmsdk::Serialization {

std::string SerializeBlendConfig(const Blend::BlendConfig& config) {
    nlohmann::json j = config;
    return j.dump(4);
}

void DeserializeBlendConfig(const std::string& jsonString, Blend::BlendConfig& outConfig) {
    outConfig = nlohmann::json::parse(jsonString).get<Blend::BlendConfig>();
}

} // namespace pmsdk::Serialization
