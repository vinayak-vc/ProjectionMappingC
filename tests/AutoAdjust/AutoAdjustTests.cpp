#include <gtest/gtest.h>

#include "PMSDK/AutoAdjust/AutoAdjust.h"
#include "PMSDK/Geometry/PerspectiveWarp.h"

#include <opencv2/calib3d.hpp>
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

#include <algorithm>
#include <cmath>
#include <string>
#include <vector>

using namespace pmsdk;
using namespace pmsdk::AutoAdjust;
using pmsdk::Math::Vector2;

namespace {

constexpr int kCamW = 640;
constexpr int kCamH = 480;

// Simulated room: projector raster [0,1]^2 (y up) -> camera pixels (y down). A homography plus an
// optional smooth bulge that no corner pin can remove (stands in for a slightly non-flat surface
// and residual lens effects).
struct World {
    cv::Mat projectorToCamera; // 3x3 CV_64F
    double bulgePx = 0.0;

    Vector2 Map(const Vector2& p) const {
        const double x = projectorToCamera.at<double>(0, 0) * p.x + projectorToCamera.at<double>(0, 1) * p.y +
                         projectorToCamera.at<double>(0, 2);
        const double y = projectorToCamera.at<double>(1, 0) * p.x + projectorToCamera.at<double>(1, 1) * p.y +
                         projectorToCamera.at<double>(1, 2);
        const double w = projectorToCamera.at<double>(2, 0) * p.x + projectorToCamera.at<double>(2, 1) * p.y +
                         projectorToCamera.at<double>(2, 2);
        double cx = x / w;
        double cy = y / w;
        if (bulgePx != 0.0) {
            // Full-period wave across the width: not absorbable by any homography.
            const double s = std::sin(2.0 * CV_PI * cx / kCamW) * std::sin(CV_PI * cy / kCamH);
            cx += bulgePx * s;
            cy += 0.6 * bulgePx * s;
        }
        return Vector2(static_cast<float>(cx), static_cast<float>(cy));
    }
};

// Projector raster corners BL, BR, TR, TL -> camera pixels (TL of the raster maps to the camera's
// upper area, so the camera sees the content upright unless rotated180).
World MakeWorld(double bulgePx, bool rotated180) {
    std::vector<cv::Point2f> raster = {cv::Point2f(0.0f, 0.0f), cv::Point2f(1.0f, 0.0f), cv::Point2f(1.0f, 1.0f),
                                       cv::Point2f(0.0f, 1.0f)};
    std::vector<cv::Point2f> camera = {cv::Point2f(70.0f, 430.0f), cv::Point2f(590.0f, 455.0f),
                                       cv::Point2f(560.0f, 40.0f), cv::Point2f(95.0f, 60.0f)};
    if (rotated180) {
        for (cv::Point2f& p : camera) {
            p = cv::Point2f(kCamW - p.x, kCamH - p.y);
        }
    }
    World world;
    cv::getPerspectiveTransform(raster, camera).convertTo(world.projectorToCamera, CV_64F);
    world.bulgePx = bulgePx;
    return world;
}

SkinWarp MakeKeystonedSkin() {
    SkinWarp warp;
    warp.corners = {Vector2(0.12f, 0.10f), Vector2(0.86f, 0.16f), Vector2(0.82f, 0.90f), Vector2(0.18f, 0.84f)};
    return warp;
}

Vector2 Observe(const World& world, const SkinWarp& warp, const Vector2& uv) {
    return world.Map(EvaluateWarp(warp, uv.x, uv.y));
}

// Analytic checker (same layout as RenderChecker) at content UV; 0 outside the content.
double CheckerValue(const CheckerSpec& spec, double u, double v) {
    if (u < 0.0 || u > 1.0 || v < 0.0 || v > 1.0) {
        return 15.0;
    }
    const double t = 1.0 - v;
    const double span = 1.0 - 2.0 * spec.margin;
    if (u < spec.margin || u > 1.0 - spec.margin || t < spec.margin || t > 1.0 - spec.margin) {
        return 225.0;
    }
    const int ix = std::clamp(static_cast<int>(std::floor((u - spec.margin) / span * spec.squaresX)), 0, spec.squaresX - 1);
    const int iy = std::clamp(static_cast<int>(std::floor((t - spec.margin) / span * spec.squaresY)), 0, spec.squaresY - 1);
    return ((ix + iy) % 2 == 0) ? 35.0 : 225.0;
}

// Renders what the camera sees: for camera pixels, solve content UV by Newton on
// uv -> world(warp(uv)) (on a coarse grid, then upsampled), then shade the checker.
cv::Mat RenderCamera(const World& world, const SkinWarp& warp, const CheckerSpec& spec) {
    std::vector<cv::Point2f> unit = {cv::Point2f(0.0f, 0.0f), cv::Point2f(1.0f, 0.0f), cv::Point2f(1.0f, 1.0f),
                                     cv::Point2f(0.0f, 1.0f)};
    std::vector<cv::Point2f> seen;
    for (const cv::Point2f& p : unit) {
        const Vector2 c = Observe(world, warp, Vector2(p.x, p.y));
        seen.emplace_back(c.x, c.y);
    }
    cv::Mat cameraToUv;
    cv::getPerspectiveTransform(seen, unit).convertTo(cameraToUv, CV_64F);

    constexpr int step = 4;
    const int gw = kCamW / step + 2;
    const int gh = kCamH / step + 2;
    cv::Mat uvMap(gh, gw, CV_32FC2);
    for (int gy = 0; gy < gh; ++gy) {
        for (int gx = 0; gx < gw; ++gx) {
            const double cx = gx * step;
            const double cy = gy * step;
            const double w = cameraToUv.at<double>(2, 0) * cx + cameraToUv.at<double>(2, 1) * cy + cameraToUv.at<double>(2, 2);
            double u = (cameraToUv.at<double>(0, 0) * cx + cameraToUv.at<double>(0, 1) * cy + cameraToUv.at<double>(0, 2)) / w;
            double v = (cameraToUv.at<double>(1, 0) * cx + cameraToUv.at<double>(1, 1) * cy + cameraToUv.at<double>(1, 2)) / w;
            for (int iter = 0; iter < 6; ++iter) {
                const Vector2 f = Observe(world, warp, Vector2(static_cast<float>(u), static_cast<float>(v)));
                const double h = 1e-3;
                const Vector2 fu = Observe(world, warp, Vector2(static_cast<float>(u + h), static_cast<float>(v)));
                const Vector2 fv = Observe(world, warp, Vector2(static_cast<float>(u), static_cast<float>(v + h)));
                const double a = (fu.x - f.x) / h;
                const double b = (fv.x - f.x) / h;
                const double c = (fu.y - f.y) / h;
                const double d = (fv.y - f.y) / h;
                const double det = a * d - b * c;
                if (std::abs(det) < 1e-9) {
                    break;
                }
                const double ex = cx - f.x;
                const double ey = cy - f.y;
                u += (d * ex - b * ey) / det;
                v += (-c * ex + a * ey) / det;
            }
            uvMap.at<cv::Vec2f>(gy, gx) = cv::Vec2f(static_cast<float>(u), static_cast<float>(v));
        }
    }
    // Pixel (x, y) covers [x-0.5, x+0.5] (OpenCV centre convention); 4x4 supersampling gives
    // camera-like anti-aliased edges.
    cv::Mat image(kCamH, kCamW, CV_8UC1);
    for (int y = 0; y < kCamH; ++y) {
        for (int x = 0; x < kCamW; ++x) {
            double sum = 0.0;
            for (int sy = 0; sy < 4; ++sy) {
                for (int sx = 0; sx < 4; ++sx) {
                    const float px = std::max(0.0f, x - 0.375f + 0.25f * sx);
                    const float py = std::max(0.0f, y - 0.375f + 0.25f * sy);
                    const float gxf = px / step;
                    const float gyf = py / step;
                    const int gx = static_cast<int>(gxf);
                    const int gy = static_cast<int>(gyf);
                    const float tx = gxf - gx;
                    const float ty = gyf - gy;
                    const cv::Vec2f p00 = uvMap.at<cv::Vec2f>(gy, gx);
                    const cv::Vec2f p10 = uvMap.at<cv::Vec2f>(gy, gx + 1);
                    const cv::Vec2f p01 = uvMap.at<cv::Vec2f>(gy + 1, gx);
                    const cv::Vec2f p11 = uvMap.at<cv::Vec2f>(gy + 1, gx + 1);
                    const cv::Vec2f uv =
                        p00 * ((1 - tx) * (1 - ty)) + p10 * (tx * (1 - ty)) + p01 * ((1 - tx) * ty) + p11 * (tx * ty);
                    sum += CheckerValue(spec, uv[0], uv[1]);
                }
            }
            image.at<std::uint8_t>(y, x) = static_cast<std::uint8_t>(std::lround(sum / 16.0));
        }
    }
    return image;
}

std::vector<Vector2> TrueCorners(const World& world, const SkinWarp& warp, const std::vector<Vector2>& uvs) {
    std::vector<Vector2> out;
    for (const Vector2& uv : uvs) {
        out.push_back(Observe(world, warp, uv));
    }
    return out;
}

double MaxError(const std::vector<Vector2>& a, const std::vector<Vector2>& b, const std::vector<std::uint8_t>& valid) {
    double worst = 0.0;
    for (std::size_t k = 0; k < a.size(); ++k) {
        if (valid[k] != 0) {
            worst = std::max(worst, static_cast<double>(std::hypot(a[k].x - b[k].x, a[k].y - b[k].y)));
        }
    }
    return worst;
}

} // namespace

// ---------------- Checker layout ----------------

TEST(AutoAdjustChecker, RejectsSymmetricAndTinyBoards) {
    CheckerSpec even;
    even.squaresX = 8;
    even.squaresY = 6;
    EXPECT_FALSE(ValidateChecker(even).ok());
    CheckerSpec tiny;
    tiny.squaresX = 2;
    tiny.squaresY = 3;
    EXPECT_FALSE(ValidateChecker(tiny).ok());
    CheckerSpec margin;
    margin.margin = 0.45f;
    EXPECT_FALSE(ValidateChecker(margin).ok());
    EXPECT_TRUE(ValidateChecker(CheckerSpec{}).ok());
    EXPECT_EQ(CheckerCornerCount(even), 0);
}

TEST(AutoAdjustChecker, CornerUVsAreRowMajorFromTopLeft) {
    const CheckerSpec spec;
    Result<std::vector<Vector2>> uvs = CheckerCornerUVs(spec);
    ASSERT_TRUE(uvs.ok());
    ASSERT_EQ(uvs.value().size(), 54u);
    const float span = 1.0f - 2.0f * spec.margin;
    EXPECT_NEAR(uvs.value()[0].x, spec.margin + span / 10.0f, 1e-6f);
    EXPECT_NEAR(uvs.value()[0].y, 1.0f - (spec.margin + span / 7.0f), 1e-6f);
    EXPECT_GT(uvs.value()[1].x, uvs.value()[0].x);   // next in a row: to the right
    EXPECT_LT(uvs.value()[9].y, uvs.value()[0].y);   // next row: lower on the content
    EXPECT_NEAR(uvs.value()[9].x, uvs.value()[0].x, 1e-6f);
}

TEST(AutoAdjustChecker, RenderHasDarkTopLeftSquareWhiteMarginAndRowOrder) {
    const CheckerSpec spec;
    Result<std::vector<std::uint8_t>> top = RenderChecker(spec, 200, 140, false);
    Result<std::vector<std::uint8_t>> bottom = RenderChecker(spec, 200, 140, true);
    ASSERT_TRUE(top.ok());
    ASSERT_TRUE(bottom.ok());
    const std::vector<std::uint8_t>& t = top.value();
    EXPECT_EQ(t[0], 255);                       // margin
    EXPECT_EQ(t[static_cast<std::size_t>(20 * 200 + 22)], 0);  // first square (top-left) is black
    EXPECT_EQ(t[static_cast<std::size_t>(20 * 200 + 40)], 255); // its right neighbour is white
    for (int row = 0; row < 140; ++row) {
        for (int x = 0; x < 200; x += 37) {
            EXPECT_EQ(t[static_cast<std::size_t>(row * 200 + x)], bottom.value()[static_cast<std::size_t>((139 - row) * 200 + x)]);
        }
    }
    EXPECT_FALSE(RenderChecker(spec, 4, 4, false).ok());
}

// ---------------- Warp evaluation ----------------

TEST(AutoAdjustWarp, CornerPinMatchesPerspectiveWarp) {
    const SkinWarp warp = MakeKeystonedSkin();
    Geometry::PerspectiveWarp reference;
    reference.SetCorners(warp.corners[0], warp.corners[1], warp.corners[2], warp.corners[3]);
    for (float u = 0.0f; u <= 1.0f; u += 0.25f) {
        for (float v = 0.0f; v <= 1.0f; v += 0.25f) {
            const Vector2 a = EvaluateWarp(warp, u, v);
            const Vector2 b = reference.Evaluate(u, v);
            EXPECT_NEAR(a.x, b.x, 1e-5f);
            EXPECT_NEAR(a.y, b.y, 1e-5f);
        }
    }
}

TEST(AutoAdjustWarp, CorrectionGridIsBilinearAndResamples) {
    CorrectionGrid grid;
    grid.columns = 2;
    grid.rows = 2;
    grid.offsets = {Vector2(0.0f, 0.0f), Vector2(0.1f, 0.0f), Vector2(0.0f, 0.2f), Vector2(0.1f, 0.2f)};
    const Vector2 mid = EvaluateCorrection(grid, 0.5f, 0.5f);
    EXPECT_NEAR(mid.x, 0.05f, 1e-6f);
    EXPECT_NEAR(mid.y, 0.10f, 1e-6f);
    const CorrectionGrid fine = ResampleCorrection(grid, 5, 4);
    ASSERT_EQ(fine.offsets.size(), 20u);
    const Vector2 sample = EvaluateCorrection(fine, 0.3f, 0.7f);
    EXPECT_NEAR(sample.x, 0.03f, 1e-5f);
    EXPECT_NEAR(sample.y, 0.14f, 1e-5f);
    EXPECT_EQ(EvaluateCorrection(CorrectionGrid{}, 0.5f, 0.5f), Vector2(0.0f, 0.0f));
}

// ---------------- Detection ----------------

TEST(AutoAdjustDetect, FindsWholeBoardInOrder) {
    const CheckerSpec spec;
    const World world = MakeWorld(0.0, false);
    const SkinWarp warp = MakeKeystonedSkin();
    const cv::Mat image = RenderCamera(world, warp, spec);
    Result<Detection> detection = DetectChecker(image.data, image.cols, image.rows, static_cast<int>(image.step), spec);
    ASSERT_TRUE(detection.ok()) << detection.status().message();
    EXPECT_TRUE(detection.value().wholeBoard);
    EXPECT_EQ(detection.value().validCount, 54);
    const std::vector<Vector2> truth = TrueCorners(world, warp, CheckerCornerUVs(spec).value());
    EXPECT_LT(MaxError(detection.value().corners, truth, detection.value().valid), 0.35);
}

TEST(AutoAdjustDetect, UpsideDownCameraKeepsContentOrder) {
    const CheckerSpec spec;
    const World world = MakeWorld(0.0, true);
    const SkinWarp warp = MakeKeystonedSkin();
    const cv::Mat image = RenderCamera(world, warp, spec);
    Result<Detection> detection = DetectChecker(image.data, image.cols, image.rows, static_cast<int>(image.step), spec);
    ASSERT_TRUE(detection.ok()) << detection.status().message();
    const std::vector<Vector2> truth = TrueCorners(world, warp, CheckerCornerUVs(spec).value());
    EXPECT_LT(MaxError(detection.value().corners, truth, detection.value().valid), 0.35);
}

TEST(AutoAdjustDetect, ToleratesSensorNoise) {
    const CheckerSpec spec;
    const World world = MakeWorld(0.0, false);
    const SkinWarp warp = MakeKeystonedSkin();
    cv::Mat image = RenderCamera(world, warp, spec);
    cv::Mat noise(image.size(), CV_16SC1);
    cv::RNG rng(1234);
    rng.fill(noise, cv::RNG::NORMAL, 0.0, 8.0);
    cv::Mat noisy;
    image.convertTo(noisy, CV_16SC1);
    noisy += noise;
    noisy.convertTo(image, CV_8UC1);
    Result<Detection> detection = DetectChecker(image.data, image.cols, image.rows, static_cast<int>(image.step), spec);
    ASSERT_TRUE(detection.ok()) << detection.status().message();
    const std::vector<Vector2> truth = TrueCorners(world, warp, CheckerCornerUVs(spec).value());
    // Noise (sigma 8 grey levels) spreads single corners; the average stays sub-pixel.
    double sumSq = 0.0;
    for (std::size_t k = 0; k < truth.size(); ++k) {
        const Vector2 d = detection.value().corners[k] - truth[k];
        sumSq += static_cast<double>(d.x) * d.x + static_cast<double>(d.y) * d.y;
    }
    EXPECT_LT(std::sqrt(sumSq / static_cast<double>(truth.size())), 0.35);
    EXPECT_LT(MaxError(detection.value().corners, truth, detection.value().valid), 1.0);
}

TEST(AutoAdjustDetect, PartialOcclusionUsesPredictionsAndRejectsHiddenCorners) {
    const CheckerSpec spec;
    const World world = MakeWorld(0.0, false);
    const SkinWarp warp = MakeKeystonedSkin();
    cv::Mat image = RenderCamera(world, warp, spec);
    const cv::Rect blocker(300, 150, 110, 120); // a person / object in front of part of the board
    image(blocker).setTo(cv::Scalar(15));

    Result<Detection> withoutPrediction =
        DetectChecker(image.data, image.cols, image.rows, static_cast<int>(image.step), spec);
    EXPECT_FALSE(withoutPrediction.ok());

    const std::vector<Vector2> truth = TrueCorners(world, warp, CheckerCornerUVs(spec).value());
    std::vector<Vector2> predicted;
    for (const Vector2& p : truth) {
        predicted.push_back(p + Vector2(2.0f, -1.5f));
    }
    Result<Detection> detection =
        DetectChecker(image.data, image.cols, image.rows, static_cast<int>(image.step), spec, &predicted);
    ASSERT_TRUE(detection.ok()) << detection.status().message();
    EXPECT_FALSE(detection.value().wholeBoard);
    int hiddenAccepted = 0;
    int visible = 0;
    for (std::size_t k = 0; k < truth.size(); ++k) {
        const cv::Rect grown(blocker.x - 6, blocker.y - 6, blocker.width + 12, blocker.height + 12);
        const bool hidden = grown.contains(cv::Point(static_cast<int>(truth[k].x), static_cast<int>(truth[k].y)));
        if (hidden && detection.value().valid[k] != 0) {
            ++hiddenAccepted;
        }
        if (!hidden) {
            ++visible;
        }
    }
    EXPECT_EQ(hiddenAccepted, 0);
    EXPECT_GE(detection.value().validCount, visible - 4);
    EXPECT_LT(MaxError(detection.value().corners, truth, detection.value().valid), 0.5);
}

TEST(AutoAdjustDetect, ReportsNotFoundOnBlankImage) {
    const cv::Mat blank(kCamH, kCamW, CV_8UC1, cv::Scalar(40));
    Result<Detection> detection = DetectChecker(blank.data, blank.cols, blank.rows, static_cast<int>(blank.step), CheckerSpec{});
    ASSERT_FALSE(detection.ok());
    EXPECT_EQ(detection.status().code(), ErrorCode::NotFound);
}

// ---------------- Undistortion ----------------

TEST(AutoAdjustUndistort, InvertsPerspectiveDistortion) {
    CameraIntrinsics k;
    k.fx = 600.0f;
    k.fy = 600.0f;
    k.cx = 320.0f;
    k.cy = 240.0f;
    k.distortion = {-0.25f, 0.07f, 0.001f, -0.0008f, -0.01f};
    std::vector<cv::Point3f> rays;
    std::vector<Vector2> ideal;
    for (float x = -0.45f; x <= 0.45f; x += 0.15f) {
        for (float y = -0.35f; y <= 0.35f; y += 0.14f) {
            rays.emplace_back(x, y, 1.0f);
            ideal.emplace_back(k.fx * x + k.cx, k.fy * y + k.cy);
        }
    }
    const cv::Matx33d kMat(k.fx, 0, k.cx, 0, k.fy, k.cy, 0, 0, 1);
    std::vector<cv::Point2f> distorted;
    cv::projectPoints(rays, cv::Vec3d(0, 0, 0), cv::Vec3d(0, 0, 0), kMat,
                      std::vector<double>(k.distortion.begin(), k.distortion.end()), distorted);
    std::vector<Vector2> raw;
    for (const cv::Point2f& p : distorted) {
        raw.emplace_back(p.x, p.y);
    }
    Result<std::vector<Vector2>> undistorted = UndistortPoints(raw, k);
    ASSERT_TRUE(undistorted.ok());
    std::vector<std::uint8_t> all(raw.size(), 1);
    EXPECT_GT(MaxError(raw, ideal, all), 5.0);  // the distortion is real
    EXPECT_LT(MaxError(undistorted.value(), ideal, all), 0.05);
}

TEST(AutoAdjustUndistort, InvertsFisheyeDistortion) {
    CameraIntrinsics k;
    k.model = LensModel::Fisheye;
    k.fx = 420.0f;
    k.fy = 420.0f;
    k.cx = 320.0f;
    k.cy = 240.0f;
    k.distortion = {0.05f, -0.02f, 0.004f, -0.001f};
    std::vector<cv::Point2f> normalized;
    std::vector<Vector2> ideal;
    for (float x = -0.6f; x <= 0.6f; x += 0.2f) {
        for (float y = -0.45f; y <= 0.45f; y += 0.15f) {
            normalized.emplace_back(x, y);
            ideal.emplace_back(k.fx * x + k.cx, k.fy * y + k.cy);
        }
    }
    const cv::Matx33d kMat(k.fx, 0, k.cx, 0, k.fy, k.cy, 0, 0, 1);
    std::vector<cv::Point2f> distorted;
    cv::fisheye::distortPoints(normalized, distorted, kMat, std::vector<double>(k.distortion.begin(), k.distortion.end()));
    std::vector<Vector2> raw;
    for (const cv::Point2f& p : distorted) {
        raw.emplace_back(p.x, p.y);
    }
    Result<std::vector<Vector2>> undistorted = UndistortPoints(raw, k);
    ASSERT_TRUE(undistorted.ok());
    std::vector<std::uint8_t> all(raw.size(), 1);
    EXPECT_LT(MaxError(undistorted.value(), ideal, all), 0.05);
}

TEST(AutoAdjustUndistort, NoDistortionIsIdentityAndBadIntrinsicsFail) {
    CameraIntrinsics k;
    k.fx = 500.0f;
    k.fy = 500.0f;
    const std::vector<Vector2> points = {Vector2(10.0f, 20.0f), Vector2(300.0f, 200.0f)};
    Result<std::vector<Vector2>> same = UndistortPoints(points, k);
    ASSERT_TRUE(same.ok());
    EXPECT_EQ(same.value()[1], points[1]);
    CameraIntrinsics bad;
    EXPECT_FALSE(UndistortPoints(points, bad).ok());
}

// ---------------- Measure / Refine (closed loop without images) ----------------

TEST(AutoAdjustRefine, PlanarSurfaceScoresZero) {
    const CheckerSpec spec;
    const World world = MakeWorld(0.0, false);
    const SkinWarp warp = MakeKeystonedSkin();
    const std::vector<Vector2> uvs = CheckerCornerUVs(spec).value();
    const std::vector<std::uint8_t> valid(uvs.size(), 1);
    Result<Score> score = Measure(uvs, TrueCorners(world, warp, uvs), valid);
    ASSERT_TRUE(score.ok());
    EXPECT_EQ(score.value().validCount, 54);
    EXPECT_LT(score.value().rmsPx, 0.01f);
}

TEST(AutoAdjustRefine, ConvergesOnBulgedSurfaceAndKeepsCorners) {
    const CheckerSpec spec;
    const World world = MakeWorld(30.0, false);
    SkinWarp warp = MakeKeystonedSkin();
    const std::vector<Vector2> uvs = CheckerCornerUVs(spec).value();
    const std::vector<std::uint8_t> valid(uvs.size(), 1);

    const Vector2 cornerBefore = Observe(world, warp, Vector2(0.0f, 1.0f));
    float firstRms = 0.0f;
    for (int iteration = 0; iteration < 4; ++iteration) {
        Result<RefineResult> step = Refine(warp, uvs, TrueCorners(world, warp, uvs), valid);
        ASSERT_TRUE(step.ok()) << step.status().message();
        if (iteration == 0) {
            firstRms = step.value().before.rmsPx;
        }
        warp.correction = step.value().correction;
    }
    Result<Score> after = Measure(uvs, TrueCorners(world, warp, uvs), valid);
    ASSERT_TRUE(after.ok());
    RecordProperty("first_rms_px", std::to_string(firstRms));
    RecordProperty("final_rms_px", std::to_string(after.value().rmsPx));
    EXPECT_GT(firstRms, 1.5f);
    EXPECT_LT(after.value().rmsPx, 0.25f);
    EXPECT_LT(after.value().rmsPx, firstRms * 0.15f);

    // Pinned corners: the skin's outline corners did not move.
    const Vector2 cornerAfter = Observe(world, warp, Vector2(0.0f, 1.0f));
    EXPECT_LT(std::hypot(cornerAfter.x - cornerBefore.x, cornerAfter.y - cornerBefore.y), 0.05);
    EXPECT_EQ(warp.correction.columns, 9);
    EXPECT_EQ(warp.correction.rows, 9);
}

TEST(AutoAdjustRefine, TargetQuadMovesContentToRequestedPlace) {
    const CheckerSpec spec;
    const World world = MakeWorld(0.0, false);
    SkinWarp warp = MakeKeystonedSkin();
    const std::vector<Vector2> uvs = CheckerCornerUVs(spec).value();
    const std::vector<std::uint8_t> valid(uvs.size(), 1);
    RefineOptions options;
    options.useTargetQuad = true;
    options.pinCorners = false;
    // Ask for the content a few pixels to the right of where it is.
    for (int i = 0; i < 4; ++i) {
        const Vector2 uv = (i == 0) ? Vector2(0, 0) : (i == 1) ? Vector2(1, 0) : (i == 2) ? Vector2(1, 1) : Vector2(0, 1);
        options.targetQuad[static_cast<std::size_t>(i)] = Observe(world, warp, uv) + Vector2(6.0f, 0.0f);
    }
    for (int iteration = 0; iteration < 5; ++iteration) {
        Result<RefineResult> step = Refine(warp, uvs, TrueCorners(world, warp, uvs), valid, options);
        ASSERT_TRUE(step.ok()) << step.status().message();
        warp.correction = step.value().correction;
    }
    Result<Score> after = Measure(uvs, TrueCorners(world, warp, uvs), valid, options);
    ASSERT_TRUE(after.ok());
    EXPECT_LT(after.value().rmsPx, 0.3f);
}

TEST(AutoAdjustRefine, RejectsTooFewCornersAndBadOptions) {
    const std::vector<Vector2> uvs = {Vector2(0.1f, 0.1f), Vector2(0.9f, 0.1f), Vector2(0.9f, 0.9f), Vector2(0.1f, 0.9f)};
    const std::vector<std::uint8_t> valid(4, 1);
    Result<RefineResult> few = Refine(SkinWarp{}, uvs, uvs, valid);
    ASSERT_FALSE(few.ok());
    EXPECT_EQ(few.status().code(), ErrorCode::NotFound);
    RefineOptions bad;
    bad.damping = 0.0f;
    EXPECT_EQ(Refine(SkinWarp{}, uvs, uvs, valid, bad).status().code(), ErrorCode::InvalidArgument);
    const std::vector<std::uint8_t> shortValid(3, 1);
    EXPECT_EQ(Measure(uvs, uvs, shortValid).status().code(), ErrorCode::InvalidArgument);
}

// ---------------- End to end: render -> detect -> refine loop ----------------

TEST(AutoAdjustLoop, ImagesConvergeWithinFourIterations) {
    const CheckerSpec spec;
    const World world = MakeWorld(30.0, false);
    SkinWarp warp = MakeKeystonedSkin();
    const std::vector<Vector2> uvs = CheckerCornerUVs(spec).value();

    std::vector<Vector2> previous;
    float firstRms = -1.0f;
    for (int iteration = 0; iteration < 4; ++iteration) {
        const cv::Mat image = RenderCamera(world, warp, spec);
        Result<Detection> detection = DetectChecker(image.data, image.cols, image.rows, static_cast<int>(image.step), spec,
                                                    previous.empty() ? nullptr : &previous);
        ASSERT_TRUE(detection.ok()) << "iteration " << iteration << ": " << detection.status().message();
        Result<RefineResult> step = Refine(warp, uvs, detection.value().corners, detection.value().valid);
        ASSERT_TRUE(step.ok()) << step.status().message();
        if (iteration == 0) {
            firstRms = step.value().before.rmsPx;
        }
        warp.correction = step.value().correction;
        previous = detection.value().corners;
    }
    const cv::Mat finalImage = RenderCamera(world, warp, spec);
    Result<Detection> finalDetection =
        DetectChecker(finalImage.data, finalImage.cols, finalImage.rows, static_cast<int>(finalImage.step), spec, &previous);
    ASSERT_TRUE(finalDetection.ok());
    Result<Score> finalScore = Measure(uvs, finalDetection.value().corners, finalDetection.value().valid);
    ASSERT_TRUE(finalScore.ok());
    RecordProperty("first_rms_px", std::to_string(firstRms));
    RecordProperty("final_rms_px", std::to_string(finalScore.value().rmsPx));
    RecordProperty("final_max_px", std::to_string(finalScore.value().maxPx));
    EXPECT_GT(firstRms, 1.5f);
    EXPECT_LT(finalScore.value().rmsPx, 0.5f);
}
