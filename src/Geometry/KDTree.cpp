#include "PMSDK/Geometry/KDTree.h"
#include <algorithm>
#include <numeric>

namespace pmsdk::Geometry {

struct KDNode {
    Math::Vector3 point;
    int originalIndex;
    int left{-1};
    int right{-1};
    int axis; // 0=x, 1=y, 2=z
};

struct KDTree::Impl {
    std::vector<KDNode> nodes;
    int root{-1};

    int BuildRecursive(std::vector<std::pair<Math::Vector3, int>>& pts, int start, int end, int depth) {
        if (start >= end) return -1;
        
        int axis = depth % 3;
        int mid = start + (end - start) / 2;

        std::nth_element(pts.begin() + start, pts.begin() + mid, pts.begin() + end, 
            [axis](const auto& a, const auto& b) {
                if (axis == 0) return a.first.x < b.first.x;
                if (axis == 1) return a.first.y < b.first.y;
                return a.first.z < b.first.z;
            });

        int nodeIdx = (int)nodes.size();
        KDNode node;
        node.point = pts[mid].first;
        node.originalIndex = pts[mid].second;
        node.axis = axis;
        nodes.push_back(node);

        int leftChild = BuildRecursive(pts, start, mid, depth + 1);
        int rightChild = BuildRecursive(pts, mid + 1, end, depth + 1);

        nodes[nodeIdx].left = leftChild;
        nodes[nodeIdx].right = rightChild;

        return nodeIdx;
    }

    void SearchRecursive(int nodeIdx, const Math::Vector3& target, int& bestIdx, float& bestDistSq) const {
        if (nodeIdx == -1) return;

        const KDNode& node = nodes[nodeIdx];
        float distSq = (node.point - target).LengthSquared();

        if (distSq < bestDistSq) {
            bestDistSq = distSq;
            bestIdx = node.originalIndex;
        }

        float diff = 0.0f;
        if (node.axis == 0) diff = target.x - node.point.x;
        else if (node.axis == 1) diff = target.y - node.point.y;
        else diff = target.z - node.point.z;

        int firstChild = (diff < 0) ? node.left : node.right;
        int secondChild = (diff < 0) ? node.right : node.left;

        SearchRecursive(firstChild, target, bestIdx, bestDistSq);

        if (diff * diff < bestDistSq) {
            SearchRecursive(secondChild, target, bestIdx, bestDistSq);
        }
    }
};

KDTree::KDTree() : m_impl(std::make_unique<Impl>()) {}
KDTree::~KDTree() = default;

void KDTree::Build(const std::vector<Math::Vector3>& points) {
    m_impl->nodes.clear();
    m_impl->root = -1;

    if (points.empty()) return;

    std::vector<std::pair<Math::Vector3, int>> pts;
    pts.reserve(points.size());
    for (int i = 0; i < (int)points.size(); ++i) {
        pts.emplace_back(points[i], i);
    }

    m_impl->root = m_impl->BuildRecursive(pts, 0, (int)pts.size(), 0);
}

int KDTree::FindNearest(const Math::Vector3& target, float& outDistanceSquared) const {
    if (m_impl->root == -1) return -1;

    int bestIdx = -1;
    outDistanceSquared = 1e30f;
    m_impl->SearchRecursive(m_impl->root, target, bestIdx, outDistanceSquared);
    return bestIdx;
}

} // namespace pmsdk::Geometry
