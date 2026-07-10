#include "PMSDK/Geometry/GridWarp.h"
#include <cmath>
#include <algorithm>

namespace pmsdk::Geometry {

struct GridWarp::Impl {
    int cols{0};
    int rows{0};
    std::vector<Math::Vector3> controlPoints;

    Math::Vector3 Evaluate(float u, float v) const {
        if (cols < 2 || rows < 2) return {0.0f, 0.0f, 0.0f};

        float scaledU = u * (cols - 1);
        float scaledV = v * (rows - 1);

        int idxX = std::clamp((int)std::floor(scaledU), 0, cols - 2);
        int idxY = std::clamp((int)std::floor(scaledV), 0, rows - 2);

        float tx = scaledU - idxX;
        float ty = scaledV - idxY;

        Math::Vector3 p00 = controlPoints[idxY * cols + idxX];
        Math::Vector3 p10 = controlPoints[idxY * cols + (idxX + 1)];
        Math::Vector3 p01 = controlPoints[(idxY + 1) * cols + idxX];
        Math::Vector3 p11 = controlPoints[(idxY + 1) * cols + (idxX + 1)];

        Math::Vector3 pY0 = p00 * (1.0f - tx) + p10 * tx;
        Math::Vector3 pY1 = p01 * (1.0f - tx) + p11 * tx;

        return pY0 * (1.0f - ty) + pY1 * ty;
    }
};

GridWarp::GridWarp() : m_impl(std::make_unique<Impl>()) {}
GridWarp::~GridWarp() = default;

void GridWarp::SetControlPoints(int columns, int rows, const std::vector<Math::Vector3>& points) {
    if (points.size() != (size_t)(columns * rows)) return;
    m_impl->cols = columns;
    m_impl->rows = rows;
    m_impl->controlPoints = points;
}

std::unique_ptr<Mesh> GridWarp::GenerateMesh(int resolutionX, int resolutionY) const {
    auto mesh = std::make_unique<Mesh>();
    if (m_impl->cols < 2 || m_impl->rows < 2) return mesh;
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
