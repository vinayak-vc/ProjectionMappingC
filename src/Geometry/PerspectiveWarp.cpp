#include "PMSDK/Geometry/PerspectiveWarp.h"
#include <execution>
#include <cmath>

namespace pmsdk::Geometry {

struct PerspectiveWarp::Impl {
    // Homography mapping unit square -> quad (Heckbert 1989):
    //   X = a*u + b*v + c,  Y = d*u + e*v + f,  W = g*u + h*v + 1
    //   x = X/W, y = Y/W
    float a = 1, b = 0, c = 0;
    float d = 0, e = 1, f = 0;
    float g = 0, h = 0;

    void Compute(float x0, float y0,   // (0,0) bottom-left
                 float x1, float y1,   // (1,0) bottom-right
                 float x2, float y2,   // (1,1) top-right
                 float x3, float y3) { // (0,1) top-left
        float dx1 = x1 - x2;
        float dx2 = x3 - x2;
        float dx3 = x0 - x1 + x2 - x3;
        float dy1 = y1 - y2;
        float dy2 = y3 - y2;
        float dy3 = y0 - y1 + y2 - y3;

        if (std::abs(dx3) < 1e-9f && std::abs(dy3) < 1e-9f) {
            // Affine (parallelogram) — no projective term.
            a = x1 - x0; b = x3 - x0; c = x0;
            d = y1 - y0; e = y3 - y0; f = y0;
            g = 0; h = 0;
        } else {
            float denom = dx1 * dy2 - dx2 * dy1;
            if (std::abs(denom) < 1e-12f) denom = 1e-12f; // degenerate quad guard
            g = (dx3 * dy2 - dx2 * dy3) / denom;
            h = (dx1 * dy3 - dx3 * dy1) / denom;
            a = x1 - x0 + g * x1;
            b = x3 - x0 + h * x3;
            c = x0;
            d = y1 - y0 + g * y1;
            e = y3 - y0 + h * y3;
            f = y0;
        }
    }

    Math::Vector2 Map(float u, float v) const {
        float X = a * u + b * v + c;
        float Y = d * u + e * v + f;
        float W = g * u + h * v + 1.0f;
        if (std::abs(W) < 1e-12f) W = 1e-12f;
        return {X / W, Y / W};
    }
};

PerspectiveWarp::PerspectiveWarp() : m_impl(std::make_unique<Impl>()) {}
PerspectiveWarp::~PerspectiveWarp() = default;

void PerspectiveWarp::SetCorners(const Math::Vector2& bottomLeft, const Math::Vector2& bottomRight,
                                 const Math::Vector2& topRight, const Math::Vector2& topLeft) {
    m_impl->Compute(bottomLeft.x, bottomLeft.y,
                    bottomRight.x, bottomRight.y,
                    topRight.x, topRight.y,
                    topLeft.x, topLeft.y);
}

Math::Vector2 PerspectiveWarp::Evaluate(float u, float v) const {
    return m_impl->Map(u, v);
}

void PerspectiveWarp::ApplyDeformation(Vertex* vertices, size_t count) const {
    if (count == 0 || !vertices) return;

    const Impl impl = *m_impl; // copy for capture-by-value across threads
    std::for_each(std::execution::par_unseq, vertices, vertices + count, [&impl](auto& vert) {
        float u = std::clamp(vert.uv.x, 0.0f, 1.0f);
        float v = std::clamp(vert.uv.y, 0.0f, 1.0f);
        Math::Vector2 p = impl.Map(u, v);
        vert.position.x = p.x;
        vert.position.y = p.y;
        // Flatten to the raster plane. The source mesh (e.g. a Unity Plane) carries
        // its own z across its extent; a corner pin maps into z=0 raster space, so
        // zero it for consistency with GridWarp and correct depth under a
        // perspective projector camera.
        vert.position.z = 0.0f;
    });
}

} // namespace pmsdk::Geometry
