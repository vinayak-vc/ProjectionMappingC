#include <gtest/gtest.h>
#include "PMSDK/Geometry/BezierCurve.h"
#include "PMSDK/Geometry/Spline.h"

using namespace pmsdk::Geometry;
using namespace pmsdk::Math;

TEST(CurveTests, BezierEvaluate) {
    BezierCurve curve;
    curve.p0 = {0.0f, 0.0f, 0.0f};
    curve.p1 = {1.0f, 0.0f, 0.0f};
    curve.p2 = {1.0f, 1.0f, 0.0f};
    curve.p3 = {0.0f, 1.0f, 0.0f};

    Vector3 start = curve.Evaluate(0.0f);
    EXPECT_FLOAT_EQ(start.x, 0.0f);
    EXPECT_FLOAT_EQ(start.y, 0.0f);

    Vector3 end = curve.Evaluate(1.0f);
    EXPECT_FLOAT_EQ(end.x, 0.0f);
    EXPECT_FLOAT_EQ(end.y, 1.0f);
}

TEST(CurveTests, SplineEvaluate) {
    Vector3 p0(0.0f, -1.0f, 0.0f);
    Vector3 p1(0.0f, 0.0f, 0.0f);
    Vector3 p2(1.0f, 0.0f, 0.0f);
    Vector3 p3(1.0f, 1.0f, 0.0f);

    Vector3 mid = Spline::EvaluateCatmullRom(p0, p1, p2, p3, 0.5f);
    // Rough sanity check that it's between p1 and p2
    EXPECT_TRUE(mid.x > 0.0f && mid.x < 1.0f);
}
