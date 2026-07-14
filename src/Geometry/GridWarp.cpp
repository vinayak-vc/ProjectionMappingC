#include "PMSDK/Geometry/GridWarp.h"
#include <cmath>
#include <algorithm>
#include <stdexcept>
#include <execution>

namespace pmsdk::Geometry {

struct GridWarp::Impl {
    int cols{0};
    int rows{0};
    std::vector<Math::Vector3> controlPoints;

    Math::Vector3 Evaluate(float u, float v) const {
        if (cols < 2 || rows < 2) return {0.0f, 0.0f, 0.0f};

        float clampedU = std::clamp(u, 0.0f, 1.0f);
        float clampedV = std::clamp(v, 0.0f, 1.0f);
        
        float scaledU = clampedU * (cols - 1);
        float scaledV = clampedV * (rows - 1);

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
    if (columns < 2 || rows < 2) {
        throw std::invalid_argument("GridWarp requires at least 2x2 control points.");
    }
    if (points.size() != (size_t)(columns * rows)) {
        throw std::invalid_argument("GridWarp point count must match columns * rows.");
    }
    m_impl->cols = columns;
    m_impl->rows = rows;
    m_impl->controlPoints = points;
}

Math::Vector3 GridWarp::Evaluate(float u, float v) const {
    return m_impl->Evaluate(u, v);
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

    mesh->SetVertices(vertices.data(), vertices.size());
    mesh->SetIndices(indices.data(), indices.size());
    mesh->RecalculateNormals();
    return mesh;
}

void GridWarp::ApplyDeformation(Vertex* vertices, size_t count) const {
    if (m_impl->cols < 2 || m_impl->rows < 2 || count == 0 || !vertices) return;

    std::for_each(std::execution::par_unseq, vertices, vertices + count, [this](auto& v) {
        float clampedU = std::clamp(v.uv.x, 0.0f, 1.0f);
        float clampedV = std::clamp(v.uv.y, 0.0f, 1.0f);
        
        float scaledU = clampedU * (m_impl->cols - 1);
        float scaledV = clampedV * (m_impl->rows - 1);

        int idxX = std::clamp((int)std::floor(scaledU), 0, m_impl->cols - 2);
        int idxY = std::clamp((int)std::floor(scaledV), 0, m_impl->rows - 2);

        float tx = scaledU - idxX;
        float ty = scaledV - idxY;

        Math::Vector3 p00 = m_impl->controlPoints[idxY * m_impl->cols + idxX];
        Math::Vector3 p10 = m_impl->controlPoints[idxY * m_impl->cols + (idxX + 1)];
        Math::Vector3 p01 = m_impl->controlPoints[(idxY + 1) * m_impl->cols + idxX];
        Math::Vector3 p11 = m_impl->controlPoints[(idxY + 1) * m_impl->cols + (idxX + 1)];

        Math::Vector3 pY0 = p00 * (1.0f - tx) + p10 * tx;
        Math::Vector3 pY1 = p01 * (1.0f - tx) + p11 * tx;

        v.position = pY0 * (1.0f - ty) + pY1 * ty;
    });
}

} // namespace pmsdk::Geometry
