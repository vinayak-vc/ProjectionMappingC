#include <gtest/gtest.h>
#include <functional>
#include "PMSDK/Calibration/GrayCode.h"
#include "PMSDK/Calibration/GrayCodeDecoder.h"

using namespace pmsdk::Calibration;

namespace {

// Feed a robust pattern sequence into a decoder as if a camera observed the
// projector 1:1, optionally transforming each pixel (albedo/ambient simulation).
void FeedRobustSequence(GrayCodeDecoder& decoder, const GrayCode& gc, int w, int h,
                        const std::function<uint8_t(uint8_t, int, int)>& transform) {
    for (size_t i = 0; i < gc.GetRobustPatternCount(); ++i) {
        std::vector<uint8_t> img = gc.GenerateRobustPattern(i);
        for (int y = 0; y < h; ++y) {
            for (int x = 0; x < w; ++x) {
                img[y * w + x] = transform(img[y * w + x], x, y);
            }
        }
        ASSERT_TRUE(decoder.AddImageFromMemory(img.data(), w, h));
    }
}

} // namespace

TEST(GrayCodeRobustTests, RobustPatternCountAndReferences) {
    GrayCode gc(32, 32); // 5 + 5 bit patterns
    EXPECT_EQ(gc.GetPatternCount(), 10u);
    EXPECT_EQ(gc.GetRobustPatternCount(), 22u); // 2 refs + 10 patterns + 10 inverses

    auto white = gc.GenerateRobustPattern(0);
    auto black = gc.GenerateRobustPattern(1);
    for (uint8_t v : white) EXPECT_EQ(v, 255);
    for (uint8_t v : black) EXPECT_EQ(v, 0);

    // Pattern k and its inverse must be exact complements.
    auto p0 = gc.GenerateRobustPattern(2);
    auto p0inv = gc.GenerateRobustPattern(3);
    ASSERT_EQ(p0.size(), p0inv.size());
    for (size_t i = 0; i < p0.size(); ++i) {
        EXPECT_EQ(static_cast<int>(p0[i]) + static_cast<int>(p0inv[i]), 255);
    }

    // Robust pattern 2+2k must equal the legacy pattern k.
    auto legacy0 = gc.GeneratePattern(0);
    EXPECT_EQ(p0, legacy0);
}

TEST(GrayCodeRobustTests, DecodeRobustIdentityRoundtrip) {
    const int w = 32, h = 32;
    GrayCode gc(w, h);
    GrayCodeDecoder decoder(w, h);

    FeedRobustSequence(decoder, gc, w, h, [](uint8_t v, int, int) { return v; });

    auto matches = decoder.DecodeRobust(30);
    ASSERT_EQ(matches.size(), static_cast<size_t>(w * h));

    for (const auto& m : matches) {
        EXPECT_FLOAT_EQ(m.cameraPoint.x, m.projectorPoint.x);
        EXPECT_FLOAT_EQ(m.cameraPoint.y, m.projectorPoint.y);
    }
}

TEST(GrayCodeRobustTests, DecodeRobustSurvivesAlbedoAndAmbient) {
    // Simulate a real surface: per-pixel albedo 25%..100% plus ambient offset.
    // A global-threshold decode misclassifies dark-albedo lit pixels; the
    // pattern-vs-inverse comparison must not.
    const int w = 32, h = 32;
    GrayCode gc(w, h);
    GrayCodeDecoder decoder(w, h);

    auto surface = [](uint8_t v, int x, int y) -> uint8_t {
        float albedo = 0.25f + 0.75f * (static_cast<float>(x) / 31.0f);
        float ambient = 20.0f + 10.0f * (static_cast<float>(y) / 31.0f);
        float lit = ambient + albedo * (static_cast<float>(v) / 255.0f) * (200.0f - ambient);
        return static_cast<uint8_t>(lit);
    };

    FeedRobustSequence(decoder, gc, w, h, surface);

    auto matches = decoder.DecodeRobust(20);
    ASSERT_EQ(matches.size(), static_cast<size_t>(w * h));
    for (const auto& m : matches) {
        EXPECT_FLOAT_EQ(m.cameraPoint.x, m.projectorPoint.x);
        EXPECT_FLOAT_EQ(m.cameraPoint.y, m.projectorPoint.y);
    }
}

TEST(GrayCodeRobustTests, ShadowMaskRejectsUnlitPixels) {
    // Left half of the camera image never receives projector light (contrast 0).
    // Those pixels must be excluded instead of decoding to garbage.
    const int w = 32, h = 32;
    GrayCode gc(w, h);
    GrayCodeDecoder decoder(w, h);

    auto halfLit = [w](uint8_t v, int x, int) -> uint8_t {
        return x < w / 2 ? static_cast<uint8_t>(15) : v; // flat dim value, no contrast
    };

    FeedRobustSequence(decoder, gc, w, h, halfLit);

    auto matches = decoder.DecodeRobust(30);
    ASSERT_EQ(matches.size(), static_cast<size_t>((w / 2) * h));
    for (const auto& m : matches) {
        EXPECT_GE(m.cameraPoint.x, static_cast<float>(w / 2));
        EXPECT_FLOAT_EQ(m.cameraPoint.x, m.projectorPoint.x);
        EXPECT_FLOAT_EQ(m.cameraPoint.y, m.projectorPoint.y);
    }
}

TEST(GrayCodeRobustTests, ClearAndCountAndLastFrame) {
    const int w = 8, h = 8;
    GrayCodeDecoder decoder(w, h);
    EXPECT_EQ(decoder.GetImageCount(), 0u);

    std::vector<uint8_t> img(w * h, 42);
    ASSERT_TRUE(decoder.AddImageFromMemory(img.data(), w, h));
    EXPECT_EQ(decoder.GetImageCount(), 1u);

    std::vector<uint8_t> readback;
    int rw = 0, rh = 0;
    ASSERT_TRUE(decoder.GetLastFrame(readback, rw, rh));
    EXPECT_EQ(rw, w);
    EXPECT_EQ(rh, h);
    ASSERT_EQ(readback.size(), static_cast<size_t>(w * h));
    EXPECT_EQ(readback[0], 42);

    decoder.ClearImages();
    EXPECT_EQ(decoder.GetImageCount(), 0u);
    EXPECT_FALSE(decoder.GetLastFrame(readback, rw, rh));
}

TEST(GrayCodeRobustTests, DecodeRobustRequiresExactSequenceLength) {
    const int w = 8, h = 8;
    GrayCodeDecoder decoder(w, h);
    std::vector<uint8_t> img(w * h, 128);
    decoder.AddImageFromMemory(img.data(), w, h); // 1 image != required 2 + 2*6
    EXPECT_TRUE(decoder.DecodeRobust(30).empty());
}
