#include <gtest/gtest.h>

#include "PMSDK/AutoAdjust/Planes.h"

#include <opencv2/calib3d.hpp>
#include <opencv2/core.hpp>

#include <cmath>
#include <random>
#include <vector>

using namespace pmsdk;
using namespace pmsdk::AutoAdjust;

namespace {

constexpr int kW = 320;
constexpr int kH = 240;
constexpr float kF = 280.0f;
constexpr float kCx = 160.0f;
constexpr float kCy = 120.0f;

struct TruthPlane {
    double nx;
    double ny;
    double nz;
    double d; // n . X + d = 0
};

TruthPlane Make(double nx, double ny, double nz, double px, double py, double pz) {
    const double length = std::sqrt(nx * nx + ny * ny + nz * nz);
    TruthPlane p{nx / length, ny / length, nz / length, 0.0};
    p.d = -(p.nx * px + p.ny * py + p.nz * pz);
    return p;
}

// Folded soffit above (two facets meeting at a crease in the image middle), wall below.
const TruthPlane kLeft = Make(0.35, 0.55, -1.0, 0.0, -600.0, 2200.0);
const TruthPlane kRight = Make(-0.4, 0.5, -1.0, 0.0, -600.0, 2200.0);
const TruthPlane kWall = Make(0.0, 0.0, -1.0, 0.0, 0.0, 3800.0);

CameraIntrinsics Pinhole() {
    CameraIntrinsics k;
    k.fx = kF;
    k.fy = kF;
    k.cx = kCx;
    k.cy = kCy;
    return k;
}

const TruthPlane& PlaneFor(int x, int y) {
    if (y >= kH / 2) {
        return kWall;
    }
    return x < kW / 2 ? kLeft : kRight;
}

std::vector<std::uint16_t> RenderDepth(float noiseMm) {
    std::mt19937 rng(11);
    std::normal_distribution<float> noise(0.0f, noiseMm);
    std::vector<std::uint16_t> depth(static_cast<std::size_t>(kW) * kH, 0);
    for (int y = 0; y < kH; ++y) {
        for (int x = 0; x < kW; ++x) {
            const TruthPlane& p = PlaneFor(x, y);
            const double rx = (x - kCx) / kF;
            const double ry = (y - kCy) / kF;
            const double denom = p.nx * rx + p.ny * ry + p.nz;
            const double z = -p.d / denom;
            if (z > 0.0 && z < 60000.0) {
                depth[static_cast<std::size_t>(y) * kW + static_cast<std::size_t>(x)] =
                    static_cast<std::uint16_t>(std::lround(z + noise(rng)));
            }
        }
    }
    // A band with no depth (stereo shadow) between soffit and wall.
    for (int y = kH / 2 - 3; y < kH / 2 + 3; ++y) {
        for (int x = 0; x < kW; ++x) {
            depth[static_cast<std::size_t>(y) * kW + static_cast<std::size_t>(x)] = 0;
        }
    }
    return depth;
}

double AngleDeg(const Math::Vector3& a, const TruthPlane& b) {
    const double dot = std::abs(a.x * b.nx + a.y * b.ny + a.z * b.nz);
    return std::acos(std::min(1.0, dot)) * 180.0 / 3.14159265358979;
}

} // namespace

TEST(AutoAdjustPlanes, FindsFoldedFacetsAndWall) {
    const std::vector<std::uint16_t> depth = RenderDepth(3.0f);
    Result<std::vector<Plane>> planes = DetectPlanes(depth.data(), kW, kH, kW, Pinhole());
    ASSERT_TRUE(planes.ok()) << planes.status().message();
    ASSERT_GE(planes.value().size(), 3u);

    const TruthPlane* truths[3] = {&kLeft, &kRight, &kWall};
    for (const TruthPlane* truth : truths) {
        const Plane* match = nullptr;
        double best = 1e9;
        for (const Plane& p : planes.value()) {
            const double a = AngleDeg(p.normal, *truth);
            if (a < best) {
                best = a;
                match = &p;
            }
        }
        ASSERT_NE(match, nullptr);
        EXPECT_LT(best, 2.0);
        EXPECT_LT(match->rmsMm, 6.0f);
        EXPECT_LT(match->normal.z, 0.0f) << "normals face the camera";
        EXPECT_GE(match->polygon.size(), 3u);
    }
}

TEST(AutoAdjustPlanes, WallSizeAndOutlineMatchTheScene) {
    const std::vector<std::uint16_t> depth = RenderDepth(2.0f);
    Result<std::vector<Plane>> planes = DetectPlanes(depth.data(), kW, kH, kW, Pinhole());
    ASSERT_TRUE(planes.ok());
    const Plane* wall = nullptr;
    for (const Plane& p : planes.value()) {
        if (AngleDeg(p.normal, kWall) < 2.0) {
            wall = &p;
        }
    }
    ASSERT_NE(wall, nullptr);
    // Wall at 3.8 m fills the lower image half: 320 px wide -> 3.8 m * 320 / 280 = 4343 mm.
    EXPECT_NEAR(wall->widthMm, 4343.0f, 0.06f * 4343.0f);
    EXPECT_NEAR(wall->heightMm, 3800.0f * 117.0f / 280.0f, 0.1f * 1588.0f);
    EXPECT_LT(wall->viewAngleDeg, 2.0f);
    // Its quad spans the lower half of the image.
    EXPECT_GT(wall->quad[0].y, 200.0f);
    EXPECT_LT(wall->quad[3].y, 140.0f);
    EXPECT_GT(wall->quad[3].y, 110.0f);
}

TEST(AutoAdjustPlanes, RejectsBadInputAndEmptyDepth) {
    const std::vector<std::uint16_t> empty(static_cast<std::size_t>(kW) * kH, 0);
    EXPECT_EQ(DetectPlanes(empty.data(), kW, kH, kW, Pinhole()).status().code(), ErrorCode::NotFound);
    EXPECT_EQ(DetectPlanes(nullptr, kW, kH, kW, Pinhole()).status().code(), ErrorCode::InvalidArgument);
    EXPECT_EQ(DetectPlanes(empty.data(), kW, kH, kW, CameraIntrinsics{}).status().code(), ErrorCode::InvalidArgument);
    PlaneOptions bad;
    bad.step = 0;
    EXPECT_EQ(DetectPlanes(empty.data(), kW, kH, kW, Pinhole(), bad).status().code(), ErrorCode::InvalidArgument);
}

TEST(AutoAdjustPlanes, LensDistortionIsRemovedFromRays) {
    // A wide lens (strong barrel distortion) looking at one tilted wall: raw pixels must be undistorted
    // before back-projection, otherwise the wall is bent.
    CameraIntrinsics lens = Pinhole();
    lens.distortion = {-0.32f, 0.11f, 0.0f, 0.0f, -0.015f};
    const TruthPlane wall = Make(0.25, 0.1, -1.0, 0.0, 0.0, 2500.0);
    std::vector<cv::Point2f> raw;
    for (int y = 0; y < kH; ++y) {
        for (int x = 0; x < kW; ++x) {
            raw.emplace_back(static_cast<float>(x), static_cast<float>(y));
        }
    }
    const cv::Matx33d k(kF, 0, kCx, 0, kF, kCy, 0, 0, 1);
    std::vector<cv::Point2f> normalized;
    cv::undistortPoints(raw, normalized, k, std::vector<double>(lens.distortion.begin(), lens.distortion.end()));
    std::vector<std::uint16_t> depth(raw.size(), 0);
    for (std::size_t i = 0; i < raw.size(); ++i) {
        const double denom = wall.nx * normalized[i].x + wall.ny * normalized[i].y + wall.nz;
        depth[i] = static_cast<std::uint16_t>(std::lround(-wall.d / denom));
    }
    Result<std::vector<Plane>> corrected = DetectPlanes(depth.data(), kW, kH, kW, lens);
    ASSERT_TRUE(corrected.ok()) << corrected.status().message();
    ASSERT_EQ(corrected.value().size(), 1u);
    EXPECT_LT(corrected.value()[0].rmsMm, 2.0f);
    EXPECT_LT(AngleDeg(corrected.value()[0].normal, wall), 1.0);

    Result<std::vector<Plane>> naive = DetectPlanes(depth.data(), kW, kH, kW, Pinhole());
    ASSERT_TRUE(naive.ok());
    const bool bent = naive.value().size() > 1 || naive.value()[0].rmsMm > 3.0f * corrected.value()[0].rmsMm;
    EXPECT_TRUE(bent) << "ignoring the lens should visibly bend the wall";
}