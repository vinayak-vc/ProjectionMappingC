#include <gtest/gtest.h>
#include "PMSDK/Geometry/KDTree.h"

using namespace pmsdk::Geometry;
using namespace pmsdk::Math;

TEST(KDTreeTests, BuildAndFindNearest) {
    std::vector<Vector3> points = {
        {0.0f, 0.0f, 0.0f},
        {10.0f, 0.0f, 0.0f},
        {0.0f, 10.0f, 0.0f},
        {10.0f, 10.0f, 0.0f},
        {5.0f, 5.0f, 5.0f}
    };

    KDTree tree;
    tree.Build(points);

    float distSq = 0.0f;
    int idx = tree.FindNearest(Vector3(9.0f, 1.0f, 0.0f), distSq);
    
    EXPECT_EQ(idx, 1); // Should find {10.0f, 0.0f, 0.0f}
    EXPECT_FLOAT_EQ(distSq, 2.0f); // 1^2 + 1^2
}
