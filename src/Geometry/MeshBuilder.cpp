#include "PMSDK/Geometry/MeshBuilder.h"
#include <vector>
#include <cmath>

namespace pmsdk::Geometry {

std::unique_ptr<Mesh> MeshBuilder::CreatePlane(float width, float height) {
    auto mesh = std::make_unique<Mesh>();
    float hw = width * 0.5f;
    float hh = height * 0.5f;

    std::vector<Vertex> vertices = {
        {{-hw, -hh, 0.0f}, {0.0f, 0.0f, 1.0f}, {0.0f, 0.0f}, {1.0f, 1.0f, 1.0f, 1.0f}},
        {{ hw, -hh, 0.0f}, {0.0f, 0.0f, 1.0f}, {1.0f, 0.0f}, {1.0f, 1.0f, 1.0f, 1.0f}},
        {{ hw,  hh, 0.0f}, {0.0f, 0.0f, 1.0f}, {1.0f, 1.0f}, {1.0f, 1.0f, 1.0f, 1.0f}},
        {{-hw,  hh, 0.0f}, {0.0f, 0.0f, 1.0f}, {0.0f, 1.0f}, {1.0f, 1.0f, 1.0f, 1.0f}}
    };

    std::vector<uint32_t> indices = {
        0, 1, 2,
        0, 2, 3
    };

    mesh->SetVertices(vertices);
    mesh->SetIndices(indices);
    return mesh;
}

std::unique_ptr<Mesh> MeshBuilder::CreateGrid(float width, float height, int columns, int rows) {
    if (columns < 1) columns = 1;
    if (rows < 1) rows = 1;

    auto mesh = std::make_unique<Mesh>();
    std::vector<Vertex> vertices;
    std::vector<uint32_t> indices;

    float dx = width / columns;
    float dy = height / rows;
    float hw = width * 0.5f;
    float hh = height * 0.5f;

    for (int y = 0; y <= rows; ++y) {
        for (int x = 0; x <= columns; ++x) {
            Vertex v;
            v.position = {x * dx - hw, y * dy - hh, 0.0f};
            v.normal = {0.0f, 0.0f, 1.0f};
            v.uv = {(float)x / columns, (float)y / rows};
            v.color = {1.0f, 1.0f, 1.0f, 1.0f};
            vertices.push_back(v);
        }
    }

    for (int y = 0; y < rows; ++y) {
        for (int x = 0; x < columns; ++x) {
            uint32_t idx0 = y * (columns + 1) + x;
            uint32_t idx1 = idx0 + 1;
            uint32_t idx2 = (y + 1) * (columns + 1) + x;
            uint32_t idx3 = idx2 + 1;

            indices.push_back(idx0);
            indices.push_back(idx1);
            indices.push_back(idx2);

            indices.push_back(idx2);
            indices.push_back(idx1);
            indices.push_back(idx3);
        }
    }

    mesh->SetVertices(vertices);
    mesh->SetIndices(indices);
    return mesh;
}

std::unique_ptr<Mesh> MeshBuilder::CreateCylinder(float radius, float height, int segments) {
    if (segments < 3) segments = 3;

    auto mesh = std::make_unique<Mesh>();
    std::vector<Vertex> vertices;
    std::vector<uint32_t> indices;

    float hh = height * 0.5f;
    float angleStep = (2.0f * 3.14159265f) / segments;

    // Side vertices
    for (int i = 0; i <= segments; ++i) {
        float angle = i * angleStep;
        float c = std::cos(angle);
        float s = std::sin(angle);
        float u = (float)i / segments;

        Vertex vTop;
        vTop.position = {c * radius, hh, s * radius};
        vTop.normal = {c, 0.0f, s};
        vTop.uv = {u, 1.0f};
        vTop.color = {1.0f, 1.0f, 1.0f, 1.0f};
        vertices.push_back(vTop);

        Vertex vBot;
        vBot.position = {c * radius, -hh, s * radius};
        vBot.normal = {c, 0.0f, s};
        vBot.uv = {u, 0.0f};
        vBot.color = {1.0f, 1.0f, 1.0f, 1.0f};
        vertices.push_back(vBot);
    }

    // Side indices
    for (int i = 0; i < segments; ++i) {
        uint32_t idx0 = static_cast<uint32_t>(i * 2);
        uint32_t idx1 = static_cast<uint32_t>(i * 2 + 1);
        uint32_t idx2 = static_cast<uint32_t>((i + 1) * 2);
        uint32_t idx3 = idx2 + 1;

        indices.push_back(idx0);
        indices.push_back(idx1);
        indices.push_back(idx2);

        indices.push_back(idx2);
        indices.push_back(idx1);
        indices.push_back(idx3);
    }

    // Skipping caps for brevity in projection mapping, typically surfaces are open.

    mesh->SetVertices(vertices);
    mesh->SetIndices(indices);
    return mesh;
}

} // namespace pmsdk::Geometry
