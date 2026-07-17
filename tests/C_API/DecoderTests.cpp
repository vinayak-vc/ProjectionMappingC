#include <gtest/gtest.h>
#include "PMSDK/C_API/CalibrationAPI.h"
#include <opencv2/opencv.hpp>
#include <vector>
#include <string>
#include <fstream>
#include <filesystem>

// Simple tests for Decoder API

class DecoderTests : public ::testing::Test {
protected:
    void SetUp() override {
        // Create a temporary directory for test images
        std::filesystem::create_directory("decoder_test_images");
        
        // Generate mock graycode patterns for a 4x4 projector
        // 4 cols -> 2 bits. 4 rows -> 2 bits. Total 4 images.
        // X sequence:
        // bit 0 (MSB): x0=0, x1=0, x2=1, x3=1 -> gray 0,0,1,1
        // bit 1:       x0=0, x1=1, x2=1, x3=0 -> gray 0,1,1,0
        // Y sequence: same for y
        
        // We will create images of 4x4 camera pixels that exactly observe the projector 1:1.
        
        int projW = 4, projH = 4;
        
        pmsdk_graycode_t* generator = pmsdk_graycode_create(projW, projH);
        size_t count = pmsdk_graycode_get_pattern_count(generator);
        
        std::vector<uint8_t> buffer(projW * projH);
        
        for (size_t i = 0; i < count; ++i) {
            pmsdk_graycode_generate_pattern(generator, i, buffer.data());
            
            // Save as an image using OpenCV
            cv::Mat img(projH, projW, CV_8UC1, buffer.data());
            std::string path = "decoder_test_images/pattern_" + std::to_string(i) + ".png";
            cv::imwrite(path, img);
        }
        
        pmsdk_graycode_destroy(generator);
    }

    void TearDown() override {
        std::filesystem::remove_all("decoder_test_images");
    }
};

TEST_F(DecoderTests, CreateAndDestroy) {
    pmsdk_decoder_t* decoder = pmsdk_decoder_create(1920, 1080);
    EXPECT_NE(decoder, nullptr);
    pmsdk_decoder_destroy(decoder);
}

TEST_F(DecoderTests, DecodeAndTriangulate) {
    pmsdk_decoder_t* decoder = pmsdk_decoder_create(4, 4);
    ASSERT_NE(decoder, nullptr);
    
    // Add images
    for (int i = 0; i < 4; ++i) {
        std::string path = "decoder_test_images/pattern_" + std::to_string(i) + ".png";
        pmsdk_status_t status = pmsdk_decoder_add_image(decoder, path.c_str());
        EXPECT_EQ(status, PMSDK_SUCCESS);
    }
    
    // Geometry note: the decoded correspondence is the identity map (camera pixel
    // (x,y) sees projector pixel (x,y)). With identical intrinsics and a pure
    // translation baseline, identical pixels mean PARALLEL rays -> triangulation
    // at infinity (w ~ 0), which the SDK correctly filters out. To make the test
    // geometry convergent and deterministic, shift the projector's principal
    // point: cam ray x = X.x/X.z + 2, proj ray x = (X.x+10)/X.z + 1; equal pixels
    // force 10/X.z = 1, i.e. every triangulated point lies at depth z = 10.
    float camIntr[4] = {1.0f, 1.0f, 2.0f, 2.0f}; // fx, fy, cx, cy
    float camExtr[6] = {0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f}; // rvec, tvec (camera at origin)

    float projIntr[4] = {1.0f, 1.0f, 1.0f, 2.0f}; // cx = 1 (see note above)
    float projExtr[6] = {0.0f, 0.0f, 0.0f, 10.0f, 0.0f, 0.0f}; // projector translated by 10 on X

    std::vector<pmsdk_vec3_t> points(100);
    size_t count = 0;

    pmsdk_status_t status = pmsdk_decoder_decode_and_triangulate(
        decoder, 127,
        camIntr, camExtr,
        projIntr, projExtr,
        points.data(), &count, points.size()
    );

    EXPECT_EQ(status, PMSDK_SUCCESS);
    EXPECT_EQ(count, 16); // 4x4 image, all should match and triangulate
    for (size_t i = 0; i < count; ++i) {
        EXPECT_NEAR(points[i].z, 10.0f, 1e-3f); // convergent geometry puts every point at z=10
    }

    pmsdk_decoder_destroy(decoder);
}
