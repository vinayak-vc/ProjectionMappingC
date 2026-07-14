#pragma once

#include "PMSDK/Math/Ray.h"
#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Math/Vector2.h"
#include "PMSDK/Math/Plane.h"
#include "PMSDK/Math/BoundingBox.h"

namespace pmsdk::Geometry {

struct RayIntersectionResult {
    bool hit{false};
    float distance{0.0f};
    Math::Vector3 point{0.0f, 0.0f, 0.0f};
};

struct RayTriangleIntersectionResult : public RayIntersectionResult {
    Math::Vector2 barycentric{0.0f, 0.0f};
};

class Intersection {
public:
    static RayTriangleIntersectionResult RayTriangle(
        const Math::Ray& ray,
        const Math::Vector3& v0,
        const Math::Vector3& v1,
        const Math::Vector3& v2) 
    {
        RayTriangleIntersectionResult result;
        const float EPSILON = 0.0000001f;

        Math::Vector3 edge1 = v1 - v0;
        Math::Vector3 edge2 = v2 - v0;
        Math::Vector3 h = ray.direction.Cross(edge2);
        float a = edge1.Dot(h);

        if (a > -EPSILON && a < EPSILON) return result; // Ray is parallel to triangle

        float f = 1.0f / a;
        Math::Vector3 s = ray.origin - v0;
        float u = f * s.Dot(h);

        if (u < 0.0f || u > 1.0f) return result;

        Math::Vector3 q = s.Cross(edge1);
        float v = f * ray.direction.Dot(q);

        if (v < 0.0f || u + v > 1.0f) return result;

        float t = f * edge2.Dot(q);

        if (t > EPSILON) {
            result.hit = true;
            result.distance = t;
            result.point = ray.PointAt(t);
            result.barycentric = {u, v};
        }
        return result;
    }

    static RayIntersectionResult RayPlane(const Math::Ray& ray, const Math::Plane& plane) {
        RayIntersectionResult result;
        float denom = plane.normal.Dot(ray.direction);
        if (std::abs(denom) > 0.000001f) {
            float t = -(plane.normal.Dot(ray.origin) + plane.distance) / denom;
            if (t >= 0.0f) {
                result.hit = true;
                result.distance = t;
                result.point = ray.PointAt(t);
            }
        }
        return result;
    }

    static bool RayBoundingBox(const Math::Ray& ray, const Math::BoundingBox& box) {
        float tmin = (box.min.x - ray.origin.x) / ray.direction.x;
        float tmax = (box.max.x - ray.origin.x) / ray.direction.x;

        if (tmin > tmax) std::swap(tmin, tmax);

        float tymin = (box.min.y - ray.origin.y) / ray.direction.y;
        float tymax = (box.max.y - ray.origin.y) / ray.direction.y;

        if (tymin > tymax) std::swap(tymin, tymax);

        if ((tmin > tymax) || (tymin > tmax)) return false;

        if (tymin > tmin) tmin = tymin;
        if (tymax < tmax) tmax = tymax;

        float tzmin = (box.min.z - ray.origin.z) / ray.direction.z;
        float tzmax = (box.max.z - ray.origin.z) / ray.direction.z;

        if (tzmin > tzmax) std::swap(tzmin, tzmax);

        if ((tmin > tzmax) || (tzmin > tmax)) return false;
        
        return true;
    }
};

} // namespace pmsdk::Geometry
