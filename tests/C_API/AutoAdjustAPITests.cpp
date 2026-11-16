#include <gtest/gtest.h>

#include "PMSDK/C_API/AutoAdjustAPI.h"

#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

#include <cmath>
#include <cstring>
#include <string>
#include <vector>

namespace {

pmsdk_autoadjust_checker_t DefaultChecker() {
    pmsdk_autoadjust_checker_t checker;
    pmsdk_autoadjust_checker_init(&checker);
    return checker;
}

} // namespace

TEST(AutoAdjustAPI, DefaultsMatchDocumentation) {
    const pmsdk_autoadjust_checker_t checker = DefaultChecker();
    EXPECT_EQ(checker.squares_x, 10);
    EXPECT_EQ(checker.squares_y, 7);
    EXPECT_FLOAT_EQ(checker.margin, 0.08f);
    pmsdk_autoadjust_options_t options;
    pmsdk_autoadjust_options_init(&options);
    EXPECT_FLOAT_EQ(options.damping, 0.8f);
    EXPECT_EQ(options.grid_columns, 9);
    EXPECT_EQ(options.grid_rows, 9);
    EXPECT_EQ(options.pin_corners, 1);
    EXPECT_EQ(options.use_target_quad, 0);
}

TEST(AutoAdjustAPI, CheckerCornersFollowCopyRule) {
    const pmsdk_autoadjust_checker_t checker = DefaultChecker();
    EXPECT_EQ(pmsdk_autoadjust_checker_corners(&checker, nullptr, 0), 54u);
    std::vector<pmsdk_vec2_t> two(2);
    EXPECT_EQ(pmsdk_autoadjust_checker_corners(&checker, two.data(), two.size()), 54u);
    EXPECT_GT(two[1].x, two[0].x);
    pmsdk_autoadjust_checker_t even = checker;
    even.squares_y = 6;
    EXPECT_EQ(pmsdk_autoadjust_checker_corners(&even, nullptr, 0), 0u);
    EXPECT_NE(std::string(pmsdk_autoadjust_get_last_error()), "");
}

TEST(AutoAdjustAPI, RenderRejectsSmallBuffer) {
    const pmsdk_autoadjust_checker_t checker = DefaultChecker();
    std::vector<uint8_t> small(10);
    EXPECT_EQ(pmsdk_autoadjust_render_checker(&checker, 64, 64, 0, small.data(), small.size()), PMSDK_ERROR_INVALID_ARGUMENT);
    std::vector<uint8_t> image(64 * 64);
    EXPECT_EQ(pmsdk_autoadjust_render_checker(&checker, 64, 64, 1, image.data(), image.size()), PMSDK_SUCCESS);
}

TEST(AutoAdjustAPI, DetectsRenderedCheckerThroughHomography) {
    const pmsdk_autoadjust_checker_t checker = DefaultChecker();
    const int contentW = 500;
    const int contentH = 350;
    std::vector<uint8_t> content(static_cast<size_t>(contentW * contentH));
    ASSERT_EQ(pmsdk_autoadjust_render_checker(&checker, contentW, contentH, 0, content.data(), content.size()), PMSDK_SUCCESS);
    const cv::Mat contentMat(contentH, contentW, CV_8UC1, content.data());

    // Content image corners (top-left origin) -> camera.
    const std::vector<cv::Point2f> src = {cv::Point2f(0, 0), cv::Point2f(static_cast<float>(contentW), 0),
                                          cv::Point2f(static_cast<float>(contentW), static_cast<float>(contentH)),
                                          cv::Point2f(0, static_cast<float>(contentH))};
    const std::vector<cv::Point2f> dst = {cv::Point2f(90, 70), cv::Point2f(560, 50), cv::Point2f(585, 420), cv::Point2f(60, 400)};
    const cv::Mat h = cv::getPerspectiveTransform(src, dst);
    cv::Mat camera(480, 640, CV_8UC1, cv::Scalar(20));
    cv::warpPerspective(contentMat, camera, h, camera.size(), cv::INTER_LINEAR, cv::BORDER_TRANSPARENT);

    std::vector<pmsdk_vec2_t> corners(54);
    std::vector<uint8_t> valid(54);
    int32_t validCount = 0;
    int32_t wholeBoard = 0;
    ASSERT_EQ(pmsdk_autoadjust_detect(camera.data, camera.cols, camera.rows, static_cast<int32_t>(camera.step), &checker, nullptr,
                                      0.0f, corners.data(), valid.data(), corners.size(), &validCount, &wholeBoard),
              PMSDK_SUCCESS)
        << pmsdk_autoadjust_get_last_error();
    EXPECT_EQ(validCount, 54);
    EXPECT_EQ(wholeBoard, 1);

    std::vector<pmsdk_vec2_t> uvs(54);
    pmsdk_autoadjust_checker_corners(&checker, uvs.data(), uvs.size());
    for (size_t k = 0; k < uvs.size(); ++k) {
        // content UV (y up) -> content pixel coordinates (y down; OpenCV puts pixel centres on
        // integers, so subtract half a pixel) -> camera
        const std::vector<cv::Point2f> in = {
            cv::Point2f(uvs[k].x * contentW - 0.5f, (1.0f - uvs[k].y) * contentH - 0.5f)};
        std::vector<cv::Point2f> out;
        cv::perspectiveTransform(in, out, h);
        EXPECT_NEAR(corners[k].x, out[0].x, 0.6f) << "corner " << k;
        EXPECT_NEAR(corners[k].y, out[0].y, 0.6f) << "corner " << k;
    }

    pmsdk_autoadjust_options_t options;
    pmsdk_autoadjust_options_init(&options);
    pmsdk_autoadjust_score_t score{};
    ASSERT_EQ(pmsdk_autoadjust_measure(uvs.data(), corners.data(), valid.data(), uvs.size(), &options, &score), PMSDK_SUCCESS);
    EXPECT_EQ(score.valid_count, 54);
    EXPECT_LT(score.rms_px, 0.3f);
}

TEST(AutoAdjustAPI, DetectFailsCleanlyOnBlankImage) {
    const pmsdk_autoadjust_checker_t checker = DefaultChecker();
    std::vector<uint8_t> blank(320 * 240, 30);
    std::vector<pmsdk_vec2_t> corners(54);
    std::vector<uint8_t> valid(54, 1);
    int32_t validCount = -1;
    EXPECT_NE(pmsdk_autoadjust_detect(blank.data(), 320, 240, 320, &checker, nullptr, 0.0f, corners.data(), valid.data(),
                                      corners.size(), &validCount, nullptr),
              PMSDK_SUCCESS);
    EXPECT_EQ(validCount, 0);
    EXPECT_EQ(valid[0], 0);
    EXPECT_NE(std::string(pmsdk_autoadjust_get_last_error()), "");
}

TEST(AutoAdjustAPI, RefineValidatesInputsAndWritesGrid) {
    pmsdk_autoadjust_options_t options;
    pmsdk_autoadjust_options_init(&options);
    pmsdk_autoadjust_warp_t warp{};
    warp.corners[0] = {0.1f, 0.1f};
    warp.corners[1] = {0.9f, 0.1f};
    warp.corners[2] = {0.9f, 0.9f};
    warp.corners[3] = {0.1f, 0.9f};

    // A planar view: camera = 500 * raster with y flipped.
    std::vector<pmsdk_vec2_t> uvs;
    std::vector<pmsdk_vec2_t> camera;
    for (int j = 1; j < 6; ++j) {
        for (int i = 1; i < 6; ++i) {
            const float u = i / 6.0f;
            const float v = j / 6.0f;
            pmsdk_vec2_t p{};
            ASSERT_EQ(pmsdk_autoadjust_evaluate_warp(&warp, u, v, &p), PMSDK_SUCCESS);
            uvs.push_back({u, v});
            camera.push_back({p.x * 500.0f, (1.0f - p.y) * 500.0f + 0.4f * std::sin(7.0f * u)});
        }
    }
    std::vector<uint8_t> valid(uvs.size(), 1);
    std::vector<pmsdk_vec2_t> grid(81);
    pmsdk_autoadjust_score_t before{};
    ASSERT_EQ(pmsdk_autoadjust_refine(&warp, uvs.data(), camera.data(), valid.data(), uvs.size(), &options, &before, grid.data(),
                                      grid.size()),
              PMSDK_SUCCESS)
        << pmsdk_autoadjust_get_last_error();
    EXPECT_EQ(before.valid_count, 25);
    EXPECT_GT(before.rms_px, 0.0f);
    EXPECT_NEAR(grid[0].x, 0.0f, 1e-3f); // pinned corner node

    std::vector<pmsdk_vec2_t> tooSmall(10);
    EXPECT_EQ(pmsdk_autoadjust_refine(&warp, uvs.data(), camera.data(), valid.data(), uvs.size(), &options, &before,
                                      tooSmall.data(), tooSmall.size()),
              PMSDK_ERROR_INVALID_ARGUMENT);

    pmsdk_autoadjust_warp_t badWarp = warp;
    badWarp.grid_columns = 3;
    badWarp.grid_rows = 3;
    badWarp.grid_offsets = nullptr;
    EXPECT_EQ(pmsdk_autoadjust_refine(&badWarp, uvs.data(), camera.data(), valid.data(), uvs.size(), &options, &before, grid.data(),
                                      grid.size()),
              PMSDK_ERROR_INVALID_ARGUMENT);

    std::vector<uint8_t> fewValid(uvs.size(), 0);
    fewValid[0] = 1;
    fewValid[1] = 1;
    EXPECT_NE(pmsdk_autoadjust_refine(&warp, uvs.data(), camera.data(), fewValid.data(), uvs.size(), &options, &before, grid.data(),
                                      grid.size()),
              PMSDK_SUCCESS);
    EXPECT_NE(std::string(pmsdk_autoadjust_get_last_error()), "");
}

TEST(AutoAdjustAPI, UndistortRejectsBadCoefficientCount) {
    pmsdk_autoadjust_intrinsics_t intrinsics{};
    intrinsics.fx = 500.0f;
    intrinsics.fy = 500.0f;
    intrinsics.distortion_count = 15;
    pmsdk_vec2_t p{100.0f, 100.0f};
    EXPECT_EQ(pmsdk_autoadjust_undistort(&p, &p, 1, &intrinsics), PMSDK_ERROR_INVALID_ARGUMENT);
    intrinsics.distortion_count = 0;
    EXPECT_EQ(pmsdk_autoadjust_undistort(&p, &p, 1, &intrinsics), PMSDK_SUCCESS);
    EXPECT_FLOAT_EQ(p.x, 100.0f);
}

TEST(AutoAdjustAPI, FacetsHandleRoundTrip) {
    cv::Mat black(240, 320, CV_8UC1, cv::Scalar(10));
    cv::Mat white(240, 320, CV_8UC1, cv::Scalar(18));
    const std::vector<cv::Point> left = {cv::Point(40, 200), cv::Point(160, 210), cv::Point(160, 40), cv::Point(50, 50)};
    const std::vector<cv::Point> right = {cv::Point(160, 210), cv::Point(290, 190), cv::Point(280, 60), cv::Point(160, 40)};
    cv::fillConvexPoly(white, left, cv::Scalar(210));
    cv::fillConvexPoly(white, right, cv::Scalar(120));
    pmsdk_autoadjust_facet_options_t options;
    pmsdk_autoadjust_facet_options_init(&options);
    EXPECT_EQ(options.max_facets, 32);
    pmsdk_autoadjust_facets_t* facets = pmsdk_autoadjust_segment_facets(white.data, black.data, 320, 240, 320, &options);
    ASSERT_NE(facets, nullptr) << pmsdk_autoadjust_get_last_error();
    EXPECT_EQ(pmsdk_autoadjust_facets_count(facets), 2u);
    pmsdk_vec2_t quad[4];
    int32_t fromOutline = 0;
    float area = 0.0f;
    float contrast = 0.0f;
    ASSERT_EQ(pmsdk_autoadjust_facets_get(facets, 0, quad, &fromOutline, &area, &contrast), PMSDK_SUCCESS);
    EXPECT_GT(area, 1000.0f);
    EXPECT_GT(pmsdk_autoadjust_facets_copy_polygon(facets, 0, nullptr, 0), 3u);
    const pmsdk_vec2_t inside[2] = {{220.0f, 120.0f}, {230.0f, 130.0f}};
    const int32_t index = pmsdk_autoadjust_facets_find_containing(facets, inside, nullptr, 2);
    ASSERT_GE(index, 0);
    pmsdk_vec2_t found[4];
    pmsdk_autoadjust_facets_get(facets, static_cast<size_t>(index), found, nullptr, nullptr, nullptr);
    EXPECT_GT(found[1].x, 250.0f); // the right-hand facet
    EXPECT_EQ(pmsdk_autoadjust_facets_get(facets, 9, quad, nullptr, nullptr, nullptr), PMSDK_ERROR_INVALID_ARGUMENT);
    pmsdk_autoadjust_facets_destroy(facets);
    pmsdk_autoadjust_facets_destroy(nullptr);
    EXPECT_EQ(pmsdk_autoadjust_segment_facets(nullptr, black.data, 320, 240, 320, &options), nullptr);
    EXPECT_NE(std::string(pmsdk_autoadjust_get_last_error()), "");
    const pmsdk_vec2_t square[4] = {{0, 100}, {100, 100}, {100, 0}, {0, 0}};
    EXPECT_NEAR(pmsdk_autoadjust_metric_scale(square, 500.0f, 500.0f), 5.0f, 1e-5f);
}

TEST(AutoAdjustAPI, WarpMapFromSamples) {
    std::vector<pmsdk_vec2_t> raster;
    std::vector<pmsdk_vec2_t> uv;
    for (int j = 0; j < 64; ++j) {
        for (int i = 0; i < 64; ++i) {
            const float u = (i + 0.5f) / 64.0f;
            const float v = (j + 0.5f) / 64.0f;
            raster.push_back({0.1f + 0.5f * u, 0.2f + 0.4f * v});
            uv.push_back({u, v});
        }
    }
    pmsdk_autoadjust_warpmap_options_t options;
    pmsdk_autoadjust_warpmap_options_init(&options);
    options.width = 32;
    options.height = 32;
    std::vector<float> rgba(32 * 32 * 4);
    float coverage = 0.0f;
    ASSERT_EQ(pmsdk_autoadjust_build_warp_map(raster.data(), uv.data(), raster.size(), 0.1f, 0.2f, 0.5f, 0.4f, &options, rgba.data(),
                                              rgba.size(), &coverage),
              PMSDK_SUCCESS)
        << pmsdk_autoadjust_get_last_error();
    EXPECT_GT(coverage, 0.99f);
    const size_t centre = (16 * 32 + 16) * 4;
    EXPECT_NEAR(rgba[centre], 16.5f / 32.0f, 0.01f);
    EXPECT_NEAR(rgba[centre + 1], 16.5f / 32.0f, 0.01f);
    EXPECT_EQ(rgba[centre + 3], 1.0f);
    std::vector<float> small(10);
    EXPECT_EQ(pmsdk_autoadjust_build_warp_map(raster.data(), uv.data(), raster.size(), 0.1f, 0.2f, 0.5f, 0.4f, &options, small.data(),
                                              small.size(), nullptr),
              PMSDK_ERROR_INVALID_ARGUMENT);
}

TEST(AutoAdjustAPI, PlanesFromFlatWall) {
    std::vector<uint16_t> depth(160 * 120, 2500);
    pmsdk_autoadjust_plane_options_t options;
    pmsdk_autoadjust_plane_options_init(&options);
    EXPECT_EQ(options.step, 4);
    pmsdk_autoadjust_intrinsics_t lens{};
    lens.fx = 150.0f;
    lens.fy = 150.0f;
    lens.cx = 80.0f;
    lens.cy = 60.0f;
    pmsdk_autoadjust_planes_t* planes = pmsdk_autoadjust_detect_planes(depth.data(), 160, 120, 160, &lens, &options);
    ASSERT_NE(planes, nullptr) << pmsdk_autoadjust_get_last_error();
    ASSERT_EQ(pmsdk_autoadjust_planes_count(planes), 1u);
    pmsdk_autoadjust_plane_info_t info{};
    ASSERT_EQ(pmsdk_autoadjust_planes_get(planes, 0, &info), PMSDK_SUCCESS);
    EXPECT_NEAR(info.normal.z, -1.0f, 1e-3f);
    EXPECT_NEAR(info.d, 2500.0f, 1.0f);
    EXPECT_NEAR(info.width_mm, 2500.0f * 160.0f / 150.0f, 200.0f);
    EXPECT_GT(pmsdk_autoadjust_planes_copy_polygon(planes, 0, nullptr, 0), 3u);
    EXPECT_EQ(pmsdk_autoadjust_planes_get(planes, 5, &info), PMSDK_ERROR_INVALID_ARGUMENT);
    pmsdk_autoadjust_planes_destroy(planes);
    std::vector<uint16_t> none(160 * 120, 0);
    EXPECT_EQ(pmsdk_autoadjust_detect_planes(none.data(), 160, 120, 160, &lens, &options), nullptr);
}

TEST(AutoAdjustAPI, SolveCornersWithIdentityCamera) {
    pmsdk_autoadjust_warp_t warp{};
    warp.corners[0] = {0.2f, 0.2f};
    warp.corners[1] = {0.8f, 0.2f};
    warp.corners[2] = {0.8f, 0.8f};
    warp.corners[3] = {0.2f, 0.8f};
    std::vector<pmsdk_vec2_t> uvs;
    std::vector<pmsdk_vec2_t> camera;
    for (int j = 1; j < 4; ++j) {
        for (int i = 1; i < 4; ++i) {
            pmsdk_vec2_t p{};
            pmsdk_autoadjust_evaluate_warp(&warp, i / 4.0f, j / 4.0f, &p);
            uvs.push_back({i / 4.0f, j / 4.0f});
            camera.push_back({p.x * 1000.0f, p.y * 1000.0f});
        }
    }
    std::vector<uint8_t> valid(uvs.size(), 1);
    const pmsdk_vec2_t target[4] = {{100.0f, 100.0f}, {900.0f, 100.0f}, {900.0f, 900.0f}, {100.0f, 900.0f}};
    pmsdk_vec2_t corners[4];
    ASSERT_EQ(pmsdk_autoadjust_solve_corners(&warp, uvs.data(), camera.data(), valid.data(), uvs.size(), target, corners), PMSDK_SUCCESS)
        << pmsdk_autoadjust_get_last_error();
    EXPECT_NEAR(corners[0].x, 0.1f, 1e-4f);
    EXPECT_NEAR(corners[2].y, 0.9f, 1e-4f);
    EXPECT_EQ(pmsdk_autoadjust_solve_corners(&warp, uvs.data(), camera.data(), valid.data(), uvs.size(), nullptr, corners),
              PMSDK_ERROR_INVALID_ARGUMENT);
}
TEST(AutoAdjustAPI, EvaluateWarpManyMatchesSingle) {
    pmsdk_autoadjust_warp_t warp{};
    warp.corners[0] = {0.1f, 0.15f};
    warp.corners[1] = {0.85f, 0.1f};
    warp.corners[2] = {0.9f, 0.9f};
    warp.corners[3] = {0.12f, 0.8f};
    std::vector<pmsdk_vec2_t> uvs = {{0.0f, 0.0f}, {0.3f, 0.7f}, {1.0f, 1.0f}};
    std::vector<pmsdk_vec2_t> many(uvs.size());
    ASSERT_EQ(pmsdk_autoadjust_evaluate_warp_many(&warp, uvs.data(), uvs.size(), many.data()), PMSDK_SUCCESS);
    for (size_t i = 0; i < uvs.size(); ++i) {
        pmsdk_vec2_t single{};
        pmsdk_autoadjust_evaluate_warp(&warp, uvs[i].x, uvs[i].y, &single);
        EXPECT_FLOAT_EQ(many[i].x, single.x);
        EXPECT_FLOAT_EQ(many[i].y, single.y);
    }
    EXPECT_FLOAT_EQ(many[0].x, 0.1f);
    EXPECT_EQ(pmsdk_autoadjust_evaluate_warp_many(&warp, nullptr, 1, many.data()), PMSDK_ERROR_INVALID_ARGUMENT);
}