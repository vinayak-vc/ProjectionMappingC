#include "PMSDK/Geometry/BezierPatch.h"
#include "PMSDK/Geometry/BezierCurve.h"

namespace pmsdk::Geometry {

struct BezierPatch::Impl {
    Math::Vector3 controlPoints[16];

    Math::Vector3 Evaluate(float u, float v) const {
        // Evaluate 4 bezier curves along the V direction (columns)
        BezierCurve curves[4];
        for (int i = 0; i < 4; ++i) {
            curves[i].p0 = controlPoints[i * 4 + 0];
            curves[i].p1 = controlPoints[i * 4 + 1];
            curves[i].p2 = controlPoints[i * 4 + 2];
            curves[i].p3 = controlPoints[i * 4 + 3];
        }

        Math::Vector3 p0 = curves[0].Evaluate(u);
        Math::Vector3 p1 = curves[1].Evaluate(u);
        Math::Vector3 p2 = curves[2].Evaluate(u);
        Math::Vector3 p3 = curves[3].Evaluate(u);

        // Evaluate the resulting curve along the U direction
        BezierCurve finalCurve;
        finalCurve.p0 = p0;
        finalCurve.p1 = p1;
        finalCurve.p2 = p2;
        finalCurve.p3 = p3;

        return finalCurve.Evaluate(v);
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

    mesh->SetVertices(vertices);
    mesh->SetIndices(indices);
    mesh->RecalculateNormals();
    return mesh;
}

} // namespace pmsdk::Geometry
