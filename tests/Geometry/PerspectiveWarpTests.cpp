#include <gtest/gtest.h>
#include "PMSDK/Geometry/PerspectiveWarp.h"
#include "PMSDK/Geometry/GridWarp.h"
#include <cmath>

using namespace pmsdk::Geometry;
using namespace pmsdk::Math;

TEST(PerspectiveWarpTests, IdentityMapsUnitSquare) {
    PerspectiveWarp pw;
    pw.SetCorners({0, 0}, {1, 0}, {1, 1}, {0, 1});
    // Corners
    EXPECT_NEAR(pw.Evaluate(0, 0).x, 0.0f, 1e-5f);
    EXPECT_NEAR(pw.Evaluate(0, 0).y, 0.0f, 1e-5f);
    EXPECT_NEAR(pw.Evaluate(1, 1).x, 1.0f, 1e-5f);
    EXPECT_NEAR(pw.Evaluate(1, 1).y, 1.0f, 1e-5f);
    // Center
    EXPECT_NEAR(pw.Evaluate(0.5f, 0.5f).x, 0.5f, 1e-5f);
    EXPECT_NEAR(pw.Evaluate(0.5f, 0.5f).y, 0.5f, 1e-5f);
}

TEST(PerspectiveWarpTests, MapsToExactCorners) {
    PerspectiveWarp pw;
    Vector2 bl{0.1f, 0.2f}, br{0.9f, 0.05f}, tr{0.8f, 0.95f}, tl{0.15f, 0.85f};
    pw.SetCorners(bl, br, tr, tl);
    EXPECT_NEAR(pw.Evaluate(0, 0).x, bl.x, 1e-4f);
    EXPECT_NEAR(pw.Evaluate(0, 0).y, bl.y, 1e-4f);
    EXPECT_NEAR(pw.Evaluate(1, 0).x, br.x, 1e-4f);
    EXPECT_NEAR(pw.Evaluate(1, 0).y, br.y, 1e-4f);
    EXPECT_NEAR(pw.Evaluate(1, 1).x, tr.x, 1e-4f);
    EXPECT_NEAR(pw.Evaluate(1, 1).y, tr.y, 1e-4f);
    EXPECT_NEAR(pw.Evaluate(0, 1).x, tl.x, 1e-4f);
    EXPECT_NEAR(pw.Evaluate(0, 1).y, tl.y, 1e-4f);
}

TEST(PerspectiveWarpTests, PerspectiveDiffersFromBilinearOnKeystone) {
    // A trapezoid (top edge narrower than bottom): projective and bilinear agree
    // at the 4 corners but must diverge at the midpoint of the mesh. This is the
    // shear artefact that perspective corner pin fixes.
    Vector2 bl{0, 0}, br{1, 0}, tr{0.75f, 1}, tl{0.25f, 1};

    PerspectiveWarp pw;
    pw.SetCorners(bl, br, tr, tl);
    Vector2 persp = pw.Evaluate(0.5f, 0.5f);

    // Bilinear center = average of 4 corners.
    Vector2 bilinear{(bl.x + br.x + tr.x + tl.x) / 4.0f,
                     (bl.y + br.y + tr.y + tl.y) / 4.0f};

    // Both lie on the vertical centre line (x = 0.5 by symmetry).
    EXPECT_NEAR(persp.x, 0.5f, 1e-4f);
    EXPECT_NEAR(bilinear.x, 0.5f, 1e-4f);

    // Bilinear places the texture mid-row at the naive average height (0.5).
    EXPECT_NEAR(bilinear.y, 0.5f, 1e-4f);
    // The projective mapping instead foreshortens toward the narrow (top) edge,
    // pushing the mid-row well above 0.5 (here ~0.667). The meaningful divergence
    // at the interior — with the corners identical — is exactly the shear artefact
    // a true corner pin removes.
    EXPECT_GT(persp.y, bilinear.y + 0.05f);
}

TEST(PerspectiveWarpTests, ApplyDeformationUsesVertexUV) {
    PerspectiveWarp pw;
    Vector2 bl{0, 0}, br{2, 0}, tr{2, 1}, tl{0, 1};
    pw.SetCorners(bl, br, tr, tl); // pure horizontal 2x scale

    Vertex verts[2];
    verts[0].uv = {0.5f, 0.5f};
    verts[0].position = {0, 0, 0};
    verts[1].uv = {1.0f, 1.0f};
    verts[1].position = {0, 0, 0};

    pw.ApplyDeformation(verts, 2);

    EXPECT_NEAR(verts[0].position.x, 1.0f, 1e-4f); // 0.5 * 2
    EXPECT_NEAR(verts[0].position.y, 0.5f, 1e-4f);
    EXPECT_NEAR(verts[1].position.x, 2.0f, 1e-4f);
    EXPECT_NEAR(verts[1].position.y, 1.0f, 1e-4f);
}
