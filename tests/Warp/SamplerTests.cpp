#include <gtest/gtest.h>
#include "PMSDK/Warp/Sampler.h"
#include "PMSDK/Geometry/MeshBuilder.h"

using namespace pmsdk;
using namespace pmsdk::Warp;

TEST(SamplerTests, SampleUV) {
    // Create a 1x1 plane with 2 triangles
    auto mesh = Geometry::MeshBuilder::CreatePlane(1, 1);
    
    // UV maps to bottom-left (0,0) to top-right (1,1)
    // Vertices of Plane from MeshBuilder:
    // v0: (-0.5, -0.5) uv(0,0)
    // v1: ( 0.5, -0.5) uv(1,0)
    // v2: (-0.5,  0.5) uv(0,1)
    // Wait, mesh builder generates them as it pleases, let's just test the center
    // We expect the point at (0, 0, 0) to evaluate to uv(0.5, 0.5) on either triangle that covers it.
    
    Math::Vector2 uv = Sampler::SampleUVAtPoint(*mesh, 0, {0.0f, 0.0f, 0.0f});
    
    // Since it's a quad centered at origin, (0,0,0) is exactly in the middle.
    // However, it might be exactly on an edge. One of the triangles will give (0.5, 0.5).
    // The other might yield out-of-bounds UVs if the point is outside the triangle, but for (0,0,0) both should yield (0.5,0.5).
    EXPECT_NEAR(uv.x, 0.5f, 0.001f);
    EXPECT_NEAR(uv.y, 0.5f, 0.001f);
}
