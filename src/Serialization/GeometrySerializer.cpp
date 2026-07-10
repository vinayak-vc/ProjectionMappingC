#include "PMSDK/Serialization/GeometrySerializer.h"
#include "JsonHelpers.h"

namespace pmsdk::Geometry {
    // We only serialize position and uv for basic meshes. Normals can be recomputed.
    void to_json(nlohmann::json& j, const Vertex& v) {
        j = nlohmann::json{
            {"position", v.position},
            {"uv", {{"x", v.uv.x}, {"y", v.uv.y}}}
        };
    }

    void from_json(const nlohmann::json& j, Vertex& v) {
        j.at("position").get_to(v.position);
        v.uv.x = j.at("uv").at("x").get<float>();
        v.uv.y = j.at("uv").at("y").get<float>();
    }
}

namespace pmsdk::Serialization {

std::string SerializeMesh(const Geometry::Mesh& mesh) {
    auto verts = mesh.GetVertices();
    auto idx = mesh.GetIndices();
    
    nlohmann::json j;
    j["vertices"] = std::vector<Geometry::Vertex>(verts.begin(), verts.end());
    j["indices"] = std::vector<uint32_t>(idx.begin(), idx.end());

    return j.dump(4);
}

void DeserializeMesh(const std::string& jsonString, Geometry::Mesh& outMesh) {
    auto j = nlohmann::json::parse(jsonString);
    auto verts = j.at("vertices").get<std::vector<Geometry::Vertex>>();
    auto idx = j.at("indices").get<std::vector<uint32_t>>();
    
    outMesh.SetVertices(verts);
    outMesh.SetIndices(idx);
    outMesh.RecalculateNormals();
}

std::string SerializeDynamicMesh(const Geometry::DynamicMesh& mesh) {
    auto staticMesh = mesh.ToMesh();
    return SerializeMesh(*staticMesh);
}

void DeserializeDynamicMesh(const std::string& jsonString, Geometry::DynamicMesh& outMesh) {
    Geometry::Mesh m;
    DeserializeMesh(jsonString, m);
    // Since DynamicMesh takes a static Mesh via conversion, we don't have a fromMesh constructor
    // yet. But we can build it manually.
    outMesh.Clear();
    auto verts = m.GetVertices();
    auto idx = m.GetIndices();
    for (const auto& v : verts) outMesh.AddVertex(v);
    for (size_t i = 0; i < idx.size(); i += 3) {
        outMesh.AddFace(idx[i], idx[i+1], idx[i+2]);
    }
}

std::string SerializeBezierPatch(const Geometry::BezierPatch& /*patch*/) {
    return "{}"; // minimal implementation for now
}
void DeserializeBezierPatch(const std::string& /*jsonString*/, Geometry::BezierPatch& /*outPatch*/) {}
std::string SerializeGridWarp(const Geometry::GridWarp& /*grid*/) {
    return "{}"; // minimal implementation for now
}
void DeserializeGridWarp(const std::string& /*jsonString*/, Geometry::GridWarp& /*outGrid*/) {}

} // namespace pmsdk::Serialization
