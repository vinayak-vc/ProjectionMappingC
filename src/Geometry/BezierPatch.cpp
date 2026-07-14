#include "PMSDK/Geometry/BezierPatch.h"
#include "PMSDK/Geometry/BezierCurve.h"
#include <algorithm>
#include <execution>
namespace pmsdk::Geometry {

struct BezierPatch::Impl {
    Math::Vector3 controlPoints[16];

    Math::Vector3 Evaluate(float u, float v) const {
        // Compute Bernstein basis polynomials for u
        float u1 = 1.0f - u;
        float u1_2 = u1 * u1;
        float u1_3 = u1_2 * u1;
        float u2 = u * u;
        float u3 = u2 * u;

        float bu[4] = {
            u1_3,
            3.0f * u * u1_2,
            3.0f * u2 * u1,
            u3
        };

        // Compute Bernstein basis polynomials for v
        float v1 = 1.0f - v;
        float v1_2 = v1 * v1;
        float v1_3 = v1_2 * v1;
        float v2 = v * v;
        float v3 = v2 * v;

        float bv[4] = {
            v1_3,
            3.0f * v * v1_2,
            3.0f * v2 * v1,
            v3
        };

        Math::Vector3 result(0.0f, 0.0f, 0.0f);
        
        // Sum the 16 control points weighted by the basis functions
        for (int i = 0; i < 4; ++i) {
            for (int j = 0; j < 4; ++j) {
                // i maps to u (columns), j maps to v (rows)
                result += controlPoints[i * 4 + j] * (bu[i] * bv[j]);
            }
        }
        
        return result;
    }
};

BezierPatch::BezierPatch() : m_impl(std::make_unique<Impl>()) {
    for (int i = 0; i < 16; ++i) m_impl->controlPoints[i] = {0.0f, 0.0f, 0.0f};
}

BezierPatch::~BezierPatch() = default;

void BezierPatch::SetControlPoints(const std::vector<Math::Vector3>& points) {
    if (points.size() != 16) return;
    for (int i = 0; i < 16; ++i) {
        m_impl->controlPoints[i] = points[i];
    }
}

Math::Vector3 BezierPatch::Evaluate(float u, float v) const {
    return m_impl->Evaluate(u, v);
}

std::unique_ptr<Mesh> BezierPatch::GenerateMesh(int resolutionX, int resolutionY) const {
    auto mesh = std::make_unique<Mesh>();
    if (resolutionX < 1) resolutionX = 1;
    if (resolutionY < 1) resolutionY = 1;

    std::vector<Vertex> vertices;
    std::vector<uint32_t> indices;

    for (int y = 0; y <= resolutionY; ++y) {
        float v = (float)y / resolutionY;
        for (int x = 0; x <= resolutionX; ++x) {
            float u = (float)x / resolutionX;
            Vertex vert;
            vert.position = m_impl->Evaluate(u, v);
            vert.uv = {u, v};
            vert.color = {1.0f, 1.0f, 1.0f, 1.0f};
            vertices.push_back(vert);
        }
    }

    for (int y = 0; y < resolutionY; ++y) {
        for (int x = 0; x < resolutionX; ++x) {
            uint32_t idx0 = y * (resolutionX + 1) + x;
            uint32_t idx1 = idx0 + 1;
            uint32_t idx2 = (y + 1) * (resolutionX + 1) + x;
            uint32_t idx3 = idx2 + 1;

            indices.push_back(idx0);
            indices.push_back(idx2);
            indices.push_back(idx1);

            indices.push_back(idx1);
            indices.push_back(idx2);
            indices.push_back(idx3);
        }
    }

    mesh->SetVertices(vertices.data(), vertices.size());
    mesh->SetIndices(indices.data(), indices.size());
    mesh->RecalculateNormals();
    return mesh;
}

void BezierPatch::ApplyDeformation(Vertex* vertices, size_t count) const {
    if (count == 0 || !vertices) return;

    std::for_each(std::execution::par_unseq, vertices, vertices + count, [this](auto& v) {
        float u = v.uv.x;
        float v_c = v.uv.y;

        float u1 = 1.0f - u;
        float u1_2 = u1 * u1;
        float u1_3 = u1_2 * u1;
        float u2 = u * u;
        float u3 = u2 * u;

        float bu[4] = {
            u1_3,
            3.0f * u * u1_2,
            3.0f * u2 * u1,
            u3
        };

        float v1 = 1.0f - v_c;
        float v1_2 = v1 * v1;
        float v1_3 = v1_2 * v1;
        float v2 = v_c * v_c;
        float v3 = v2 * v_c;

        float bv[4] = {
            v1_3,
            3.0f * v_c * v1_2,
            3.0f * v2 * v1,
            v3
        };

        Math::Vector3 result(0.0f, 0.0f, 0.0f);
        
        for (int i = 0; i < 4; ++i) {
            for (int j = 0; j < 4; ++j) {
                result += m_impl->controlPoints[i * 4 + j] * (bu[i] * bv[j]);
            }
        }
        
        v.position = result;
    });
}

} // namespace pmsdk::Geometry
