#include "PMSDK/Serialization/WarpSerializer.h"
#include "JsonHelpers.h"

namespace pmsdk::Warp {
    void to_json(nlohmann::json& j, const Projector& p) {
        float sx, sy;
        p.GetLensShift(sx, sy);
        j = nlohmann::json{
            {"transform", p.GetTransform()},
            {"throwRatio", p.GetThrowRatio()},
            {"aspectRatio", p.GetAspectRatio()},
            {"lensShift", {{"x", sx}, {"y", sy}}}
        };
    }

    void from_json(const nlohmann::json& j, Projector& p) {
        p.SetTransform(j.at("transform").get<Math::Transform>());
        p.SetThrowRatio(j.at("throwRatio").get<float>());
        p.SetAspectRatio(j.at("aspectRatio").get<float>());
        p.SetLensShift(j.at("lensShift").at("x").get<float>(), j.at("lensShift").at("y").get<float>());
    }
}

namespace pmsdk::Serialization {

std::string SerializeWarpNode(const Warp::WarpNode& node) {
    // We'd traverse the hierarchy recursively here. Let's just serialize the local transform and name.
    nlohmann::json j;
    j["name"] = node.GetName();
    j["localTransform"] = node.GetLocalTransform();
    return j.dump(4);
}
void DeserializeWarpNode(const std::string& jsonString, Warp::WarpNode& outNode) {
    auto j = nlohmann::json::parse(jsonString);
    outNode.SetName(j.at("name").get<std::string>());
    outNode.SetLocalTransform(j.at("localTransform").get<Math::Transform>());
}

std::string SerializeDeformationField(const Warp::DeformationField& /*field*/) {
    return "{}";
}
void DeserializeDeformationField(const std::string& /*jsonString*/, Warp::DeformationField& /*outField*/) {}

std::string SerializeProjector(const Warp::Projector& projector) {
    nlohmann::json j = projector;
    return j.dump(4);
}
void DeserializeProjector(const std::string& jsonString, Warp::Projector& outProjector) {
    outProjector = nlohmann::json::parse(jsonString).get<Warp::Projector>();
}

} // namespace pmsdk::Serialization
