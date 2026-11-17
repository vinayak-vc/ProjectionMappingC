#include <gtest/gtest.h>

#include "PMSDK/AutoAdjust/Facets.h"

#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

#include <array>
#include <cmath>
#include <vector>

using namespace pmsdk;
using namespace pmsdk::AutoAdjust;
using pmsdk::Math::Vector2;

namespace {

constexpr int kW = 640;
constexpr int kH = 480;

// Three adjacent facets of a folded surface (camera pixels, shared edges), each lit at its own
// brightness, on an unlit background.
const std::array<std::array<cv::Point, 4>, 3> kFacets = {{
    {cv::Point(100, 310), cv::Point(250, 330), cv::Point(260, 120), cv::Point(100, 100)},
    {cv::Point(250, 330), cv::Point(430, 300), cv::Point(420, 90), cv::Point(260, 120)},
    {cv::Point(430, 300), cv::Point(550, 320), cv::Point(560, 140), cv::Point(420, 90)},
}};
const std::array<int, 3> kBrightness = {215, 150, 95};

void Render(cv::Mat& white, cv::Mat& black) {
    black = cv::Mat(kH, kW, CV_8UC1, cv::Scalar(12));
    white = cv::Mat(kH, kW, CV_8UC1, cv::Scalar(22));
    for (std::size_t f = 0; f < kFacets.size(); ++f) {
        std::vector<cv::Point> poly(kFacets[f].begin(), kFacets[f].end());
        cv::fillConvexPoly(white, poly, cv::Scalar(kBrightness[f]), cv::LINE_AA);
    }
    cv::GaussianBlur(white, white, cv::Size(3, 3), 0.8);
    cv::RNG rng(7);
    cv::Mat noise(kH, kW, CV_16SC1);
    rng.fill(noise, cv::RNG::NORMAL, 0.0, 3.0);
    cv::Mat noisy;
    white.convertTo(noisy, CV_16SC1);
    noisy += noise;
    noisy.convertTo(white, CV_8UC1);
}

std::array<Vector2, 4> TruthQuad(std::size_t f) {
    std::array<Vector2, 4> q{};
    for (std::size_t i = 0; i < 4; ++i) {
        q[i] = Vector2(static_cast<float>(kFacets[f][i].x), static_cast<float>(kFacets[f][i].y));
    }
    return OrderQuad(q);
}

Vector2 Centroid(const std::array<Vector2, 4>& q) {
    return (q[0] + q[1] + q[2] + q[3]) * 0.25f;
}

} // namespace

TEST(AutoAdjustFacets, OrderQuadSortsImageCorners) {
    const std::array<Vector2, 4> shuffled = {Vector2(300, 50), Vector2(20, 400), Vector2(10, 40), Vector2(310, 390)};
    const std::array<Vector2, 4> ordered = OrderQuad(shuffled);
    EXPECT_EQ(ordered[0], Vector2(20, 400));  // bottom-left
    EXPECT_EQ(ordered[1], Vector2(310, 390)); // bottom-right
    EXPECT_EQ(ordered[2], Vector2(300, 50));  // top-right
    EXPECT_EQ(ordered[3], Vector2(10, 40));   // top-left
}

TEST(AutoAdjustFacets, SegmentsFoldedSurfaceIntoItsFacets) {
    cv::Mat white;
    cv::Mat black;
    Render(white, black);
    Result<std::vector<Facet>> facets = SegmentFacets(white.data, black.data, kW, kH, static_cast<int>(white.step));
    ASSERT_TRUE(facets.ok()) << facets.status().message();
    ASSERT_EQ(facets.value().size(), 3u);

    for (std::size_t f = 0; f < kFacets.size(); ++f) {
        const std::array<Vector2, 4> truth = TruthQuad(f);
        const Vector2 c = Centroid(truth);
        const Facet* match = nullptr;
        float best = 1e9f;
        for (const Facet& candidate : facets.value()) {
            const Vector2 cc = Centroid(candidate.quad);
            const float d = std::hypot(cc.x - c.x, cc.y - c.y);
            if (d < best) {
                best = d;
                match = &candidate;
            }
        }
        ASSERT_NE(match, nullptr);
        EXPECT_TRUE(match->quadFromOutline) << "facet " << f;
        for (std::size_t i = 0; i < 4; ++i) {
            EXPECT_LT(std::hypot(match->quad[i].x - truth[i].x, match->quad[i].y - truth[i].y), 5.0f)
                << "facet " << f << " corner " << i;
        }
        EXPECT_GT(match->meanContrast, 50.0f);
    }
}

TEST(AutoAdjustFacets, FindsTheFacetHoldingTheCheckerCorners) {
    cv::Mat white;
    cv::Mat black;
    Render(white, black);
    Result<std::vector<Facet>> facets = SegmentFacets(white.data, black.data, kW, kH, static_cast<int>(white.step));
    ASSERT_TRUE(facets.ok());
    // Corners inside the middle facet, plus one outlier on the left facet and one invalid.
    const std::vector<Vector2> points = {Vector2(320, 200), Vector2(340, 220), Vector2(360, 180), Vector2(150, 200), Vector2(5, 5)};
    const std::vector<std::uint8_t> valid = {1, 1, 1, 1, 0};
    const int index = FindFacetContaining(facets.value(), points, valid);
    ASSERT_GE(index, 0);
    const Vector2 c = Centroid(facets.value()[static_cast<std::size_t>(index)].quad);
    const Vector2 middle = Centroid(TruthQuad(1));
    EXPECT_LT(std::hypot(c.x - middle.x, c.y - middle.y), 10.0f);
    EXPECT_EQ(FindFacetContaining(facets.value(), {Vector2(5, 5)}, {1}), -1);
}

TEST(AutoAdjustFacets, UnlitImageHasNoFacetsAndBadInputFails) {
    const cv::Mat flat(kH, kW, CV_8UC1, cv::Scalar(30));
    Result<std::vector<Facet>> none = SegmentFacets(flat.data, flat.data, kW, kH, kW);
    ASSERT_TRUE(none.ok());
    EXPECT_TRUE(none.value().empty());
    EXPECT_FALSE(SegmentFacets(nullptr, flat.data, kW, kH, kW).ok());
    FacetOptions bad;
    bad.maxFacets = 0;
    EXPECT_FALSE(SegmentFacets(flat.data, flat.data, kW, kH, kW, bad).ok());
}

TEST(AutoAdjustFacets, SolveCornersPutsContentOnTheTargetQuad) {
    // Projector raster -> camera homography.
    const std::vector<cv::Point2f> raster = {cv::Point2f(0, 0), cv::Point2f(1, 0), cv::Point2f(1, 1), cv::Point2f(0, 1)};
    const std::vector<cv::Point2f> image = {cv::Point2f(60, 440), cv::Point2f(600, 450), cv::Point2f(580, 30), cv::Point2f(80, 40)};
    cv::Mat world;
    cv::getPerspectiveTransform(raster, image).convertTo(world, CV_64F);
    const auto toCamera = [&](const Vector2& p) {
        std::vector<cv::Point2f> in = {cv::Point2f(p.x, p.y)};
        std::vector<cv::Point2f> out;
        cv::perspectiveTransform(in, out, world);
        return Vector2(out[0].x, out[0].y);
    };

    SkinWarp current;
    current.corners = {Vector2(0.2f, 0.2f), Vector2(0.7f, 0.25f), Vector2(0.75f, 0.8f), Vector2(0.25f, 0.7f)};
    std::vector<Vector2> uvs;
    std::vector<Vector2> camera;
    for (int j = 1; j < 5; ++j) {
        for (int i = 1; i < 5; ++i) {
            const Vector2 uv(i / 5.0f, j / 5.0f);
            uvs.push_back(uv);
            camera.push_back(toCamera(EvaluateWarp(current, uv.x, uv.y)));
        }
    }
    const std::vector<std::uint8_t> valid(uvs.size(), 1);
    const std::array<Vector2, 4> wanted = {Vector2(0.15f, 0.12f), Vector2(0.8f, 0.18f), Vector2(0.85f, 0.88f), Vector2(0.1f, 0.8f)};
    std::array<Vector2, 4> target{};
    for (std::size_t i = 0; i < 4; ++i) {
        target[i] = toCamera(wanted[i]);
    }
    Result<std::array<Vector2, 4>> corners = SolveCornersForTarget(current, uvs, camera, valid, target);
    ASSERT_TRUE(corners.ok()) << corners.status().message();
    for (std::size_t i = 0; i < 4; ++i) {
        EXPECT_NEAR(corners.value()[i].x, wanted[i].x, 1e-3f);
        EXPECT_NEAR(corners.value()[i].y, wanted[i].y, 1e-3f);
    }
    const std::vector<std::uint8_t> few = {1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0};
    EXPECT_EQ(SolveCornersForTarget(current, uvs, camera, few, target).status().code(), ErrorCode::NotFound);
}

TEST(AutoAdjustFacets, MetricScaleFromKnownSize) {
    const std::array<Vector2, 4> quad = {Vector2(100, 300), Vector2(300, 300), Vector2(300, 200), Vector2(100, 200)};
    EXPECT_NEAR(MetricScaleMmPerPx(quad, 400.0f, 200.0f), 2.0f, 1e-5f);
    EXPECT_EQ(MetricScaleMmPerPx(quad, 0.0f, 200.0f), 0.0f);
    const std::array<Vector2, 4> degenerate = {Vector2(1, 1), Vector2(1, 1), Vector2(1, 1), Vector2(1, 1)};
    EXPECT_EQ(MetricScaleMmPerPx(degenerate, 400.0f, 200.0f), 0.0f);
}
