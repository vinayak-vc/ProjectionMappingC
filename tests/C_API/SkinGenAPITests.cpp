#include <gtest/gtest.h>

#include "PMSDK/C_API/SkinGenAPI.h"

#include <opencv2/imgproc.hpp>

#include <string>
#include <vector>

namespace {

std::string Fixture(const std::string& name) {
    return std::string(PMSDK_TEST_DATA_DIR) + "/" + name;
}

} // namespace

TEST(SkinGenAPITests, OptionsInitSetsDocumentedDefaults) {
    pmsdk_skingen_options_t o;
    pmsdk_skingen_options_init(&o);
    EXPECT_EQ(o.mode, PMSDK_SKINGEN_MODE_AUTO);
    EXPECT_EQ(o.alpha_threshold, 8);
    EXPECT_EQ(o.split_parts, 1);
    EXPECT_EQ(o.max_polygon_points, 128);
    EXPECT_EQ(o.y_up, 0);
    pmsdk_skingen_options_init(nullptr); // must not crash
}

TEST(SkinGenAPITests, ExtractFromPixelsAndReadBack) {
    const int w = 200;
    const int h = 100;
    cv::Mat img(h, w, CV_8UC4, cv::Scalar(0, 0, 0, 0));
    cv::rectangle(img, cv::Rect(20, 10, 100, 60), cv::Scalar(255, 0, 0, 255), cv::FILLED);

    pmsdk_skingen_options_t o;
    pmsdk_skingen_options_init(&o);
    o.y_up = 1;
    pmsdk_skingen_result_t* r = pmsdk_skingen_extract_from_pixels(img.data, w, h, 4, nullptr, &o);
    ASSERT_NE(r, nullptr) << pmsdk_skingen_get_last_error();

    int32_t iw = 0;
    int32_t ih = 0;
    ASSERT_EQ(pmsdk_skingen_get_image_size(r, &iw, &ih), PMSDK_SUCCESS);
    EXPECT_EQ(iw, w);
    EXPECT_EQ(ih, h);
    EXPECT_EQ(pmsdk_skingen_get_used_mode(r), PMSDK_SKINGEN_MODE_ALPHA);
    ASSERT_EQ(pmsdk_skingen_get_part_count(r), 1u);
    EXPECT_NEAR(pmsdk_skingen_get_part_area_fraction(r, 0), 100.0f * 60.0f / (w * h), 0.01f);

    float bounds[4] = {};
    ASSERT_EQ(pmsdk_skingen_get_part_bounds(r, 0, bounds), PMSDK_SUCCESS);
    EXPECT_NEAR(bounds[0], 20.0f / w, 0.01f);
    EXPECT_NEAR(bounds[2], 120.0f / w, 0.01f);

    pmsdk_vec2_t quad[4];
    ASSERT_EQ(pmsdk_skingen_get_part_quad(r, 0, quad), PMSDK_SUCCESS);
    EXPECT_LT(quad[0].y, quad[3].y); // y up: bottom-left is below top-left

    const size_t polyCount = pmsdk_skingen_copy_polygon(r, 0, nullptr, 0);
    ASSERT_GE(polyCount, 3u);
    std::vector<pmsdk_vec2_t> poly(polyCount);
    EXPECT_EQ(pmsdk_skingen_copy_polygon(r, 0, poly.data(), poly.size()), polyCount);

    EXPECT_GE(pmsdk_skingen_get_contour_count(r, 0), 1u);
    EXPECT_EQ(pmsdk_skingen_get_contour_is_hole(r, 0, 0), 0);
    EXPECT_EQ(pmsdk_skingen_get_contour_is_hole(r, 0, 99), -1);

    int32_t rect[4] = {};
    ASSERT_EQ(pmsdk_skingen_get_mask_rect(r, 0, rect), PMSDK_SUCCESS);
    EXPECT_EQ(rect[0], 20);
    EXPECT_EQ(rect[2], 100);
    const size_t maskBytes = pmsdk_skingen_copy_mask(r, 0, nullptr, 0);
    EXPECT_EQ(maskBytes, static_cast<size_t>(rect[2]) * static_cast<size_t>(rect[3]));
    std::vector<uint8_t> mask(maskBytes);
    EXPECT_EQ(pmsdk_skingen_copy_mask(r, 0, mask.data(), mask.size()), maskBytes);
    EXPECT_EQ(mask[0], 255);

    // Bad indices / pointers fail safely.
    EXPECT_EQ(pmsdk_skingen_get_part_bounds(r, 5, bounds), PMSDK_ERROR_INVALID_ARGUMENT);
    EXPECT_EQ(pmsdk_skingen_get_part_bounds(nullptr, 0, bounds), PMSDK_ERROR_INVALID_ARGUMENT);
    EXPECT_EQ(pmsdk_skingen_copy_polygon(r, 5, nullptr, 0), 0u);

    pmsdk_skingen_result_destroy(r);
    pmsdk_skingen_result_destroy(nullptr);
}

TEST(SkinGenAPITests, FailuresReturnNullWithMessage) {
    pmsdk_skingen_result_t* r = pmsdk_skingen_extract_from_file("definitely_missing_file.png", nullptr);
    EXPECT_EQ(r, nullptr);
    EXPECT_STRNE(pmsdk_skingen_get_last_error(), "");

    EXPECT_EQ(pmsdk_skingen_extract_from_file(nullptr, nullptr), nullptr);
    EXPECT_EQ(pmsdk_skingen_extract_from_pixels(nullptr, 10, 10, 4, nullptr, nullptr), nullptr);
    EXPECT_EQ(pmsdk_skingen_extract_from_memory(nullptr, 0, nullptr), nullptr);
}

TEST(SkinGenAPITests, ExtractStatueFromFile) {
    pmsdk_skingen_options_t o;
    pmsdk_skingen_options_init(&o);
    o.y_up = 1;
    pmsdk_skingen_result_t* r = pmsdk_skingen_extract_from_file(Fixture("skingen_bronze_statue.png").c_str(), &o);
    ASSERT_NE(r, nullptr) << pmsdk_skingen_get_last_error();
    EXPECT_EQ(pmsdk_skingen_get_part_count(r), 2u);
    pmsdk_skingen_result_destroy(r);
}
