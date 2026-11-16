#include <gtest/gtest.h>

#include "PMSDK/AutoAdjust/WarpMap.h"

#include <cmath>
#include <random>
#include <vector>

using namespace pmsdk;
using namespace pmsdk::AutoAdjust;
using pmsdk::Math::Vector2;

namespace {

const WarpMapRect kRect = [] {
    WarpMapRect r;
    r.x = 0.2f;
    r.y = 0.1f;
    r.width = 0.6f;
    r.height = 0.7f;
    return r;
}();

// A smooth, non-projective raster -> UV relation (what a curved surface needs).
Vector2 TrueUv(const Vector2& raster) {
    const float u = (raster.x - kRect.x) / kRect.width;
    const float v = (raster.y - kRect.y) / kRect.height;
    return Vector2(u + 0.03f * std::sin(6.0f * v), v + 0.02f * std::cos(5.0f * u));
}

// Raster position of map pixel (x, y) centre.
Vector2 PixelRaster(int x, int y, int w, int h) {
    return Vector2(kRect.x + (x + 0.5f) / w * kRect.width, kRect.y + (y + 0.5f) / h * kRect.height);
}

} // namespace

TEST(AutoAdjustWarpMap, ReconstructsSmoothMapAndFillsSmallHoles) {
    std::mt19937 rng(3);
    std::uniform_real_distribution<float> ux(kRect.x, kRect.x + kRect.width);
    std::uniform_real_distribution<float> uy(kRect.y, kRect.y + kRect.height);
    const Vector2 hole(0.5f, 0.45f);
    std::vector<WarpMapSample> samples;
    for (int i = 0; i < 60000; ++i) {
        const Vector2 r(ux(rng), uy(rng));
        if (std::hypot(r.x - hole.x, r.y - hole.y) < 0.02f) {
            continue; // a few-pixel hole the filler must close
        }
        samples.push_back(WarpMapSample{r, TrueUv(r)});
    }
    WarpMapOptions options;
    options.width = 128;
    options.height = 128;
    Result<WarpMap> map = BuildWarpMap(samples, kRect, options);
    ASSERT_TRUE(map.ok()) << map.status().message();
    EXPECT_GT(map.value().coverage, 0.97f);

    float worst = 0.0f;
    double sum = 0.0;
    int n = 0;
    for (int y = 2; y < options.height - 2; ++y) {
        for (int x = 2; x < options.width - 2; ++x) {
            const std::size_t i = (static_cast<std::size_t>(y) * options.width + static_cast<std::size_t>(x)) * 4;
            ASSERT_EQ(map.value().rgba[i + 3], 1.0f) << x << "," << y;
            const Vector2 truth = TrueUv(PixelRaster(x, y, options.width, options.height));
            const float error = std::hypot(map.value().rgba[i] - truth.x, map.value().rgba[i + 1] - truth.y);
            worst = std::max(worst, error);
            sum += error;
            ++n;
        }
    }
    // One map pixel is 1/128 = 0.0078 in UV; the worst case includes the filled hole.
    EXPECT_LT(worst, 0.01f);
    EXPECT_LT(sum / n, 0.002);

    // The hole centre is filled close to the truth.
    const int hx = static_cast<int>((hole.x - kRect.x) / kRect.width * options.width);
    const int hy = static_cast<int>((hole.y - kRect.y) / kRect.height * options.height);
    const std::size_t hi = (static_cast<std::size_t>(hy) * options.width + static_cast<std::size_t>(hx)) * 4;
    const Vector2 holeTruth = TrueUv(PixelRaster(hx, hy, options.width, options.height));
    EXPECT_EQ(map.value().rgba[hi + 3], 1.0f);
    EXPECT_LT(std::hypot(map.value().rgba[hi] - holeTruth.x, map.value().rgba[hi + 1] - holeTruth.y), 0.01f);
}

TEST(AutoAdjustWarpMap, AreasFarFromSamplesStayInvalid) {
    std::vector<WarpMapSample> samples;
    // Samples only in the left half of the rect.
    for (int j = 0; j < 200; ++j) {
        for (int i = 0; i < 100; ++i) {
            const Vector2 r(kRect.x + (i + 0.5f) / 200.0f * kRect.width, kRect.y + (j + 0.5f) / 200.0f * kRect.height);
            samples.push_back(WarpMapSample{r, TrueUv(r)});
        }
    }
    WarpMapOptions options;
    options.width = 64;
    options.height = 64;
    options.maxFillDistancePx = 4.0f;
    Result<WarpMap> map = BuildWarpMap(samples, kRect, options);
    ASSERT_TRUE(map.ok());
    EXPECT_NEAR(map.value().coverage, 0.5f + 4.0f / 64.0f, 0.04f);
    const std::size_t rightEdge = (static_cast<std::size_t>(32) * 64 + 63) * 4;
    EXPECT_EQ(map.value().rgba[rightEdge + 3], 0.0f);
    const std::size_t leftSide = (static_cast<std::size_t>(32) * 64 + 10) * 4;
    EXPECT_EQ(map.value().rgba[leftSide + 3], 1.0f);
}

TEST(AutoAdjustWarpMap, RejectsBadInput) {
    std::vector<WarpMapSample> outside = {WarpMapSample{Vector2(5.0f, 5.0f), Vector2(0.5f, 0.5f)}};
    EXPECT_EQ(BuildWarpMap(outside, kRect).status().code(), ErrorCode::NotFound);
    WarpMapOptions tiny;
    tiny.width = 1;
    EXPECT_EQ(BuildWarpMap(outside, kRect, tiny).status().code(), ErrorCode::InvalidArgument);
    WarpMapRect empty;
    empty.width = 0.0f;
    EXPECT_EQ(BuildWarpMap(outside, empty).status().code(), ErrorCode::InvalidArgument);
}
