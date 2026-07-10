#pragma once

#include <nlohmann/json.hpp>
#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Math/Transform.h"

namespace pmsdk::Math {
    inline void to_json(nlohmann::json& j, const Vector3& v) {
        j = nlohmann::json{"x", v.x, "y", v.y, "z", v.z};
        // wait, nlohmann json array might be better, or an object. Let's use an object.
        j = nlohmann::json{{"x", v.x}, {"y", v.y}, {"z", v.z}};
    }
    
    inline void from_json(const nlohmann::json& j, Vector3& v) {
        j.at("x").get_to(v.x);
        j.at("y").get_to(v.y);
        j.at("z").get_to(v.z);
    }

    inline void to_json(nlohmann::json& j, const Quaternion& q) {
        j = nlohmann::json{{"x", q.x}, {"y", q.y}, {"z", q.z}, {"w", q.w}};
    }

    inline void from_json(const nlohmann::json& j, Quaternion& q) {
        j.at("x").get_to(q.x);
        j.at("y").get_to(q.y);
        j.at("z").get_to(q.z);
        j.at("w").get_to(q.w);
    }

    inline void to_json(nlohmann::json& j, const Transform& t) {
        j = nlohmann::json{
            {"position", t.position},
            {"rotation", t.rotation},
            {"scale", t.scale}
        };
    }

    inline void from_json(const nlohmann::json& j, Transform& t) {
        j.at("position").get_to(t.position);
        j.at("rotation").get_to(t.rotation);
        j.at("scale").get_to(t.scale);
    }
} // namespace pmsdk::Math
