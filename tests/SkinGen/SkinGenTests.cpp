#include <gtest/gtest.h>

#include "PMSDK/SkinGen/SkinGen.h"

#include <opencv2/imgcodecs.hpp>
#include <opencv2/imgproc.hpp>

#include <cmath>
#include <string>
#include <vector>

using namespace pmsdk;
using namespace pmsdk::SkinGen;

namespace {

constexpr int kW = 400;
constexpr int kH = 600;

// Transparent RGBA canvas (RGBA byte order).
cv::Mat TransparentCanvas() {
    return cv::Mat(kH, kW, CV_8UC4, cv::Scalar(0, 0, 0, 0));
}

ImageData ToImage(const cv::Mat& rgba) {
    ImageData image;
    image.width = rgba.cols;
    image.height = rgba.rows;
    image.channels = rgba.channels();
    image.pixels.assign(rgba.data, rgba.data + rgba.total() * static_cast<std::size_t>(rgba.channels()));
    return image;
}

const cv::Scalar kOpaque(200, 120, 40, 255);

double SignedArea(const std::vector<Math::Vector2>& pts) {
    double sum = 0.0;
    for (std::size_t i = 0; i < pts.size(); ++i) {
        const Math::Vector2& a = pts[i];
        const Math::Vector2& b = pts[(i + 1) % pts.size()];
        sum += static_cast<double>(a.x) * static_cast<double>(b.y) - static_cast<double>(b.x) * static_cast<double>(a.y);
    }
    return 0.5 * sum;
}

std::string Fixture(const std::string& name) {
    return std::string(PMSDK_TEST_DATA_DIR) + "/" + name;
}

} // namespace

TEST(SkinGenTests, AlphaCircleGivesOnePart) {
    cv::Mat img = TransparentCanvas();
    cv::circle(img, cv::Point(200, 300), 100, kOpaque, cv::FILLED);

    Result<Extraction> result = ExtractSkins(ToImage(img), Options{});
    ASSERT_TRUE(result.ok()) << result.status().message();
    const Extraction& e = result.value();
    EXPECT_EQ(e.usedMode, SegmentationMode::Alpha);
    ASSERT_EQ(e.parts.size(), 1u);

    const Part& p = e.parts[0];
    const float expectedArea = 3.14159f * 100.0f * 100.0f / (static_cast<float>(kW) * static_cast<float>(kH));
    EXPECT_NEAR(p.areaFraction, expectedArea, expectedArea * 0.03f);
    EXPECT_NEAR(p.boundsMin.x, 100.0f / kW, 0.01f);
    EXPECT_NEAR(p.boundsMax.x, 300.0f / kW, 0.01f);
    EXPECT_NEAR(p.boundsMin.y, 200.0f / kH, 0.01f);
    EXPECT_NEAR(p.boundsMax.y, 400.0f / kH, 0.01f);
    EXPECT_GE(p.polygon.size(), 3u);
    EXPECT_LE(p.polygon.size(), 128u);
    EXPECT_EQ(p.maskWidth * p.maskHeight, static_cast<int>(p.mask.size()));
}

TEST(SkinGenTests, OuterContoursPositiveAndHolesNegativeArea) {
    cv::Mat img = TransparentCanvas();
    cv::circle(img, cv::Point(200, 300), 150, kOpaque, cv::FILLED);
    cv::circle(img, cv::Point(200, 300), 60, cv::Scalar(0, 0, 0, 0), cv::FILLED);

    Result<Extraction> result = ExtractSkins(ToImage(img), Options{});
    ASSERT_TRUE(result.ok());
    ASSERT_EQ(result.value().parts.size(), 1u);
    const Part& p = result.value().parts[0];
    int holes = 0;
    int outers = 0;
    for (const Contour& c : p.contours) {
        if (c.isHole) {
            ++holes;
            EXPECT_LT(SignedArea(c.points), 0.0);
        } else {
            ++outers;
            EXPECT_GT(SignedArea(c.points), 0.0);
        }
    }
    EXPECT_EQ(outers, 1);
    EXPECT_EQ(holes, 1);
    EXPECT_GT(SignedArea(p.polygon), 0.0);
}

TEST(SkinGenTests, SmallHolesAreFilled) {
    cv::Mat img = TransparentCanvas();
    cv::circle(img, cv::Point(200, 300), 150, kOpaque, cv::FILLED);
    cv::circle(img, cv::Point(200, 300), 2, cv::Scalar(0, 0, 0, 0), cv::FILLED);

    Result<Extraction> result = ExtractSkins(ToImage(img), Options{});
    ASSERT_TRUE(result.ok());
    for (const Contour& c : result.value().parts[0].contours) {
        EXPECT_FALSE(c.isHole);
    }
}

TEST(SkinGenTests, TwoSeparatePiecesGiveTwoParts) {
    cv::Mat img = TransparentCanvas();
    cv::rectangle(img, cv::Rect(20, 20, 150, 150), kOpaque, cv::FILLED);
    cv::rectangle(img, cv::Rect(220, 300, 160, 240), kOpaque, cv::FILLED);

    Options options;
    options.splitParts = false;
    Result<Extraction> result = ExtractSkins(ToImage(img), options);
    ASSERT_TRUE(result.ok());
    ASSERT_EQ(result.value().parts.size(), 2u);
    EXPECT_GE(result.value().parts[0].areaFraction, result.value().parts[1].areaFraction);
}

TEST(SkinGenTests, SpecksAreDropped) {
    cv::Mat img = TransparentCanvas();
    cv::rectangle(img, cv::Rect(50, 50, 300, 400), kOpaque, cv::FILLED);
    cv::circle(img, cv::Point(20, 560), 2, kOpaque, cv::FILLED);

    Options options;
    options.splitParts = false;
    Result<Extraction> result = ExtractSkins(ToImage(img), options);
    ASSERT_TRUE(result.ok());
    EXPECT_EQ(result.value().parts.size(), 1u);
}

TEST(SkinGenTests, ThinAppendageBecomesItsOwnPart) {
    // A thick body with a thin pole rising from it, topped by a small blob: body + pole/blob.
    cv::Mat img = TransparentCanvas();
    cv::ellipse(img, cv::Point(200, 420), cv::Size(170, 150), 0, 0, 360, kOpaque, cv::FILLED);
    cv::rectangle(img, cv::Rect(192, 60, 16, 280), kOpaque, cv::FILLED);
    cv::circle(img, cv::Point(200, 60), 18, kOpaque, cv::FILLED);

    Options split;
    split.splitParts = true;
    Result<Extraction> two = ExtractSkins(ToImage(img), split);
    ASSERT_TRUE(two.ok());
    EXPECT_EQ(two.value().parts.size(), 2u);
    EXPECT_GT(two.value().parts[0].areaFraction, two.value().parts[1].areaFraction * 4.0f);

    Options whole;
    whole.splitParts = false;
    Result<Extraction> one = ExtractSkins(ToImage(img), whole);
    ASSERT_TRUE(one.ok());
    EXPECT_EQ(one.value().parts.size(), 1u);
}

TEST(SkinGenTests, PartsDoNotOverlapAndCoverTheObject) {
    cv::Mat img = TransparentCanvas();
    cv::ellipse(img, cv::Point(200, 420), cv::Size(170, 150), 0, 0, 360, kOpaque, cv::FILLED);
    cv::rectangle(img, cv::Rect(192, 60, 16, 280), kOpaque, cv::FILLED);

    Result<Extraction> result = ExtractSkins(ToImage(img), Options{});
    ASSERT_TRUE(result.ok());
    cv::Mat coverage(kH, kW, CV_8UC1, cv::Scalar(0));
    for (const Part& p : result.value().parts) {
        for (int y = 0; y < p.maskHeight; ++y) {
            for (int x = 0; x < p.maskWidth; ++x) {
                if (p.mask[static_cast<std::size_t>(y * p.maskWidth + x)] != 0) {
                    std::uint8_t& c = coverage.at<std::uint8_t>(p.maskY + y, p.maskX + x);
                    EXPECT_EQ(c, 0) << "pixel assigned to two parts";
                    c = 255;
                }
            }
        }
    }
    cv::Mat alpha;
    cv::extractChannel(img, alpha, 3);
    cv::Mat expected = alpha > 8;
    EXPECT_NEAR(cv::countNonZero(coverage), cv::countNonZero(expected), cv::countNonZero(expected) * 0.01);
}

TEST(SkinGenTests, MaxPolygonPointsIsRespected) {
    cv::Mat img = TransparentCanvas();
    cv::circle(img, cv::Point(200, 300), 150, kOpaque, cv::FILLED);

    Options options;
    options.maxPolygonPoints = 8;
    Result<Extraction> result = ExtractSkins(ToImage(img), options);
    ASSERT_TRUE(result.ok());
    const Part& p = result.value().parts[0];
    EXPECT_LE(p.polygon.size(), 8u);
    EXPECT_GE(p.polygon.size(), 3u);
    for (const Contour& c : p.contours) {
        EXPECT_LE(c.points.size(), 8u);
    }
}

TEST(SkinGenTests, YUpFlipsVerticalCoordinates) {
    cv::Mat img = TransparentCanvas();
    cv::rectangle(img, cv::Rect(100, 50, 200, 100), kOpaque, cv::FILLED); // near the TOP of the image

    Options down;
    down.splitParts = false;
    Result<Extraction> a = ExtractSkins(ToImage(img), down);
    ASSERT_TRUE(a.ok());
    EXPECT_LT(a.value().parts[0].boundsMax.y, 0.5f);

    Options up = down;
    up.yUp = true;
    Result<Extraction> b = ExtractSkins(ToImage(img), up);
    ASSERT_TRUE(b.ok());
    const Part& p = b.value().parts[0];
    EXPECT_GT(p.boundsMin.y, 0.5f);
    EXPECT_LE(p.boundsMin.y, p.boundsMax.y);
    // Quad order: bottom-left, bottom-right, top-right, top-left.
    EXPECT_LT(p.quad[0].y, p.quad[3].y);
    EXPECT_LT(p.quad[0].x, p.quad[1].x);
    EXPECT_GT(SignedArea(p.polygon), 0.0);
}

TEST(SkinGenTests, ColorKeyOnUniformBackground) {
    cv::Mat img(kH, kW, CV_8UC3, cv::Scalar(120, 120, 120));
    cv::rectangle(img, cv::Rect(100, 150, 200, 300), cv::Scalar(220, 30, 30), cv::FILLED);

    Result<Extraction> result = ExtractSkins(ToImage(img), Options{});
    ASSERT_TRUE(result.ok()) << result.status().message();
    EXPECT_EQ(result.value().usedMode, SegmentationMode::ColorKey);
    ASSERT_EQ(result.value().parts.size(), 1u);
    EXPECT_NEAR(result.value().parts[0].areaFraction, 200.0f * 300.0f / (kW * kH), 0.01f);
}

TEST(SkinGenTests, GrabCutOnBusyBackground) {
    // Left-to-right gradient background (not a uniform border) with a bright disc.
    cv::Mat img(kH, kW, CV_8UC3);
    for (int y = 0; y < kH; ++y) {
        for (int x = 0; x < kW; ++x) {
            img.at<cv::Vec3b>(y, x) = cv::Vec3b(static_cast<std::uint8_t>(20 + x / 4), 40, static_cast<std::uint8_t>(90 - x / 8));
        }
    }
    cv::circle(img, cv::Point(200, 300), 110, cv::Scalar(250, 240, 60), cv::FILLED);

    Result<Extraction> result = ExtractSkins(ToImage(img), Options{});
    ASSERT_TRUE(result.ok()) << result.status().message();
    EXPECT_EQ(result.value().usedMode, SegmentationMode::GrabCut);
    ASSERT_GE(result.value().parts.size(), 1u);
    const float expected = 3.14159f * 110.0f * 110.0f / (kW * kH);
    EXPECT_NEAR(result.value().parts[0].areaFraction, expected, expected * 0.35f);
}

TEST(SkinGenTests, ProvidedMaskIsUsed) {
    cv::Mat img(kH, kW, CV_8UC3, cv::Scalar(10, 10, 10));
    cv::Mat mask(kH, kW, CV_8UC1, cv::Scalar(0));
    cv::rectangle(mask, cv::Rect(50, 100, 120, 80), cv::Scalar(255), cv::FILLED);

    Options options;
    options.mode = SegmentationMode::ProvidedMask;
    options.splitParts = false;
    Result<Extraction> result = ExtractSkins(ToImage(img), options, mask.data);
    ASSERT_TRUE(result.ok()) << result.status().message();
    ASSERT_EQ(result.value().parts.size(), 1u);
    EXPECT_EQ(result.value().parts[0].maskX, 50);
    EXPECT_EQ(result.value().parts[0].maskY, 100);
    EXPECT_EQ(result.value().parts[0].maskWidth, 120);
    EXPECT_EQ(result.value().parts[0].maskHeight, 80);

    Result<Extraction> missing = ExtractSkins(ToImage(img), options, nullptr);
    EXPECT_FALSE(missing.ok());
    EXPECT_EQ(missing.status().code(), ErrorCode::InvalidArgument);
}

TEST(SkinGenTests, RejectsBadInput) {
    ImageData bad;
    EXPECT_EQ(ExtractSkins(bad, Options{}).status().code(), ErrorCode::InvalidArgument);

    ImageData mismatch;
    mismatch.width = 10;
    mismatch.height = 10;
    mismatch.channels = 4;
    mismatch.pixels.assign(5, 0);
    EXPECT_EQ(ExtractSkins(mismatch, Options{}).status().code(), ErrorCode::InvalidArgument);

    ImageData rgb;
    rgb.width = 8;
    rgb.height = 8;
    rgb.channels = 3;
    rgb.pixels.assign(8 * 8 * 3, 0);
    Options alpha;
    alpha.mode = SegmentationMode::Alpha;
    EXPECT_EQ(ExtractSkins(rgb, alpha).status().code(), ErrorCode::InvalidArgument);

    Options tooFew;
    tooFew.maxPolygonPoints = 2;
    EXPECT_EQ(ExtractSkins(rgb, tooFew).status().code(), ErrorCode::InvalidArgument);
}

TEST(SkinGenTests, FullyTransparentImageFails) {
    cv::Mat img = TransparentCanvas();
    Options options;
    options.mode = SegmentationMode::Alpha;
    Result<Extraction> result = ExtractSkins(ToImage(img), options);
    ASSERT_FALSE(result.ok());
    EXPECT_EQ(result.status().code(), ErrorCode::OperationFailed);
}

TEST(SkinGenTests, DecodeImageRoundTripKeepsAlpha) {
    cv::Mat img = TransparentCanvas();
    cv::circle(img, cv::Point(200, 300), 80, kOpaque, cv::FILLED);
    cv::Mat bgra;
    cv::cvtColor(img, bgra, cv::COLOR_RGBA2BGRA);
    std::vector<std::uint8_t> bytes;
    ASSERT_TRUE(cv::imencode(".png", bgra, bytes));

    Result<ImageData> decoded = DecodeImage(bytes.data(), bytes.size());
    ASSERT_TRUE(decoded.ok()) << decoded.status().message();
    EXPECT_EQ(decoded.value().width, kW);
    EXPECT_EQ(decoded.value().height, kH);
    EXPECT_EQ(decoded.value().channels, 4);

    Result<Extraction> result = ExtractSkins(decoded.value(), Options{});
    ASSERT_TRUE(result.ok());
    EXPECT_EQ(result.value().usedMode, SegmentationMode::Alpha);

    EXPECT_FALSE(DecodeImage(nullptr, 0).ok());
    const std::uint8_t junk[8] = {1, 2, 3, 4, 5, 6, 7, 8};
    EXPECT_FALSE(DecodeImage(junk, sizeof(junk)).ok());
}

TEST(SkinGenTests, LoadImageMissingFile) {
    Result<ImageData> result = LoadImage(Fixture("does_not_exist.png"));
    ASSERT_FALSE(result.ok());
    EXPECT_EQ(result.status().code(), ErrorCode::NotFound);
}

// --- Real fixtures: a statue with a trident (cut-out PNGs with alpha) -------------------

class StatueFixtureTests : public ::testing::TestWithParam<const char*> {};

TEST_P(StatueFixtureTests, StatueAndTridentAreSeparateParts) {
    Options options;
    options.yUp = true;
    options.maxPolygonPoints = 64;
    Result<Extraction> result = ExtractSkinsFromFile(Fixture(GetParam()), options);
    ASSERT_TRUE(result.ok()) << result.status().message();
    const Extraction& e = result.value();
    EXPECT_EQ(e.usedMode, SegmentationMode::Alpha);
    ASSERT_EQ(e.parts.size(), 2u) << "expected statue body + trident";

    const Part& body = e.parts[0];
    const Part& trident = e.parts[1];
    EXPECT_GT(body.areaFraction, 0.2f);
    EXPECT_LT(trident.areaFraction, 0.06f);
    EXPECT_GT(trident.areaFraction, 0.005f);

    // The trident is the thin, tall part on the left and rises above the body.
    EXPECT_LT(trident.boundsMax.x - trident.boundsMin.x, body.boundsMax.x - body.boundsMin.x);
    EXPECT_GT(trident.boundsMax.y, body.boundsMax.y);

    // The arm gaps are holes in the body outline.
    int holes = 0;
    for (const Contour& c : body.contours) {
        if (c.isHole) {
            ++holes;
        }
    }
    EXPECT_GE(holes, 1);
    EXPECT_LE(body.polygon.size(), 64u);
    EXPECT_GT(SignedArea(body.polygon), 0.0);
}

INSTANTIATE_TEST_SUITE_P(Statues, StatueFixtureTests,
                         ::testing::Values("skingen_bronze_statue.png", "skingen_checker_statue.png"));
