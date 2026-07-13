#include "PMSDK/Geometry/BVH.h"
#include "PMSDK/Math/BoundingBox.h"
#include <vector>
#include <algorithm>

namespace pmsdk::Geometry {

struct BVHNode {
    Math::BoundingBox bounds;
    uint32_t leftChild{0};
    uint32_t rightChild{0};
    uint32_t firstTriangle{0};
    uint32_t triangleCount{0};

    bool IsLeaf() const { return triangleCount > 0; }
};

struct BVHTriangle {
    Math::Vector3 v0, v1, v2;
    Math::Vector3 center;
    uint32_t originalIndex;
};

struct BVH::Impl {
    std::vector<BVHNode> nodes;
    std::vector<BVHTriangle> triangles;
    uint32_t rootNodeIdx{0};

    void UpdateNodeBounds(uint32_t nodeIdx) {
        BVHNode& node = nodes[nodeIdx];
        node.bounds.min = Math::Vector3(1e30f, 1e30f, 1e30f);
        node.bounds.max = Math::Vector3(-1e30f, -1e30f, -1e30f);
        for (uint32_t i = 0; i < node.triangleCount; ++i) {
            const BVHTriangle& leafTri = triangles[node.firstTriangle + i];
            node.bounds.Expand(leafTri.v0);
            node.bounds.Expand(leafTri.v1);
            node.bounds.Expand(leafTri.v2);
        }
    }

    void Subdivide(uint32_t nodeIdx) {
        BVHNode& node = nodes[nodeIdx];
        if (node.triangleCount <= 2) return;

        Math::Vector3 extent = node.bounds.max - node.bounds.min;
        int axis = 0;
        if (extent.y > extent.x) axis = 1;
        if (extent.z > (axis == 0 ? extent.x : extent.y)) axis = 2;

        float splitPos = node.bounds.min.x + extent.x * 0.5f;
        if (axis == 1) splitPos = node.bounds.min.y + extent.y * 0.5f;
        if (axis == 2) splitPos = node.bounds.min.z + extent.z * 0.5f;

        uint32_t i = node.firstTriangle;
        uint32_t j = i + node.triangleCount - 1;

        while (i <= j) {
            float val = (axis == 0) ? triangles[i].center.x : (axis == 1 ? triangles[i].center.y : triangles[i].center.z);
            if (val < splitPos) {
                i++;
            } else {
                std::swap(triangles[i], triangles[j]);
                if (j == 0) break;
                j--;
            }
        }

        uint32_t leftCount = i - node.firstTriangle;
        if (leftCount == 0 || leftCount == node.triangleCount) return;

        uint32_t leftChildIdx = (uint32_t)nodes.size();
        nodes.push_back(BVHNode());
        uint32_t rightChildIdx = (uint32_t)nodes.size();
        nodes.push_back(BVHNode());

        // Re-acquire node reference since vector might have reallocated
        nodes[nodeIdx].leftChild = leftChildIdx;
        nodes[nodeIdx].rightChild = rightChildIdx;

        nodes[leftChildIdx].firstTriangle = nodes[nodeIdx].firstTriangle;
        nodes[leftChildIdx].triangleCount = leftCount;
        UpdateNodeBounds(leftChildIdx);
        Subdivide(leftChildIdx);

        nodes[rightChildIdx].firstTriangle = i;
        nodes[rightChildIdx].triangleCount = nodes[nodeIdx].triangleCount - leftCount;
        UpdateNodeBounds(rightChildIdx);
        Subdivide(rightChildIdx);

        nodes[nodeIdx].triangleCount = 0;
    }
};

BVH::BVH() : m_impl(std::make_unique<Impl>()) {}
BVH::~BVH() = default;

void BVH::Build(const Mesh& mesh) {
    size_t v_count = 0;
    auto verts = mesh.GetVertices(&v_count);
    size_t i_count = 0;
    auto idx = mesh.GetIndices(&i_count);
    if (i_count == 0 || v_count == 0) return;

    m_impl->triangles.clear();
    m_impl->nodes.clear();

    for (size_t i = 0; i < i_count; i += 3) {
        if (i + 2 >= i_count) break;
        BVHTriangle tri;
        tri.v0 = verts[idx[i]].position;
        tri.v1 = verts[idx[i+1]].position;
        tri.v2 = verts[idx[i+2]].position;
        tri.center = (tri.v0 + tri.v1 + tri.v2) * (1.0f / 3.0f);
        tri.originalIndex = (uint32_t)(i / 3);
        m_impl->triangles.push_back(tri);
    }

    BVHNode root;
    root.leftChild = root.rightChild = 0;
    root.firstTriangle = 0;
    root.triangleCount = (uint32_t)m_impl->triangles.size();

    m_impl->nodes.push_back(root);
    m_impl->rootNodeIdx = 0;
    
    m_impl->UpdateNodeBounds(0);
    m_impl->Subdivide(0);
}

bool BVH::Intersect(const Math::Ray& ray, RayTriangleIntersectionResult& outResult) const {
    if (m_impl->nodes.empty()) return false;

    outResult.hit = false;
    outResult.distance = 1e30f;

    std::vector<uint32_t> stack;
    stack.push_back(m_impl->rootNodeIdx);

    while (!stack.empty()) {
        uint32_t nodeIdx = stack.back();
        stack.pop_back();

        const BVHNode& node = m_impl->nodes[nodeIdx];
        if (!Intersection::RayBoundingBox(ray, node.bounds)) continue;

        if (node.IsLeaf()) {
            for (uint32_t i = 0; i < node.triangleCount; ++i) {
                const BVHTriangle& tri = m_impl->triangles[node.firstTriangle + i];
                auto res = Intersection::RayTriangle(ray, tri.v0, tri.v1, tri.v2);
                if (res.hit && res.distance < outResult.distance) {
                    outResult = res;
                }
            }
        } else {
            stack.push_back(node.leftChild);
            stack.push_back(node.rightChild);
        }
    }

    return outResult.hit;
}

} // namespace pmsdk::Geometry
