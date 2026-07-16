#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/C_API/Types.h"
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

// -----------------------------------------------------------------------------
// GrayCode
// -----------------------------------------------------------------------------

/**
 * @brief Creates a new GrayCode generator for the given resolution.
 * @param width The projector width.
 * @param height The projector height.
 * @return A handle to the GrayCode instance, or null on failure.
 */
PMSDK_API pmsdk_graycode_t* pmsdk_graycode_create(int width, int height);

/**
 * @brief Destroys a GrayCode instance.
 * @param handle Handle to the GrayCode instance.
 */
PMSDK_API void pmsdk_graycode_destroy(pmsdk_graycode_t* handle);

/**
 * @brief Gets the total number of patterns needed to encode the resolution.
 * @param handle Handle to the GrayCode instance.
 * @return The number of patterns, or 0 on failure.
 */
PMSDK_API size_t pmsdk_graycode_get_pattern_count(const pmsdk_graycode_t* handle);

/**
 * @brief Generates an 8-bit grayscale pattern.
 * @param handle Handle to the GrayCode instance.
 * @param index The pattern index (0 to GetPatternCount() - 1).
 * @param outPixels Pointer to a pre-allocated buffer of size (width * height) bytes.
 * @return PMSDK_SUCCESS on success.
 */
PMSDK_API pmsdk_status_t pmsdk_graycode_generate_pattern(const pmsdk_graycode_t* handle, size_t index, uint8_t* outPixels);

/**
 * @brief Number of patterns in the ROBUST sequence: all-white + all-black
 * references, then every Gray-code pattern followed by its inverse
 * (2 + 2 * pattern_count). Required input for pmsdk_decoder_decode_robust.
 */
PMSDK_API size_t pmsdk_graycode_get_robust_pattern_count(const pmsdk_graycode_t* handle);

/**
 * @brief Generates a pattern of the robust sequence.
 * Layout: 0 = white ref, 1 = black ref, 2+2k = pattern k, 3+2k = inverse of pattern k.
 * @param outPixels Pre-allocated buffer of (width * height) bytes.
 */
PMSDK_API pmsdk_status_t pmsdk_graycode_generate_robust_pattern(const pmsdk_graycode_t* handle, size_t index, uint8_t* outPixels);

// -----------------------------------------------------------------------------
// Calibrator
// -----------------------------------------------------------------------------

/**
 * @brief Creates a new Calibrator instance.
 * @return A handle to the Calibrator instance, or null on failure.
 */
PMSDK_API pmsdk_calibrator_t* pmsdk_calibrator_create(void);

/**
 * @brief Destroys a Calibrator instance.
 * @param handle Handle to the Calibrator instance.
 */
PMSDK_API void pmsdk_calibrator_destroy(pmsdk_calibrator_t* handle);

/**
 * @brief Adds an observation of object points (e.g. 3D checkerboard corners) and image points (2D camera pixels).
 * @param handle Handle to the Calibrator.
 * @param objectPoints Array of 3D points.
 * @param imagePoints Array of corresponding 2D points.
 * @param pointCount The number of points in the arrays.
 * @return PMSDK_SUCCESS on success.
 */
PMSDK_API pmsdk_status_t pmsdk_calibrator_add_observation(pmsdk_calibrator_t* handle, 
                                                          const pmsdk_vec3_t* objectPoints, 
                                                          const pmsdk_vec2_t* imagePoints, 
                                                          size_t pointCount);

/**
 * @brief Calibrates the camera using all added observations.
 * @param handle Handle to the Calibrator.
 * @param imageWidth Width of the camera image.
 * @param imageHeight Height of the camera image.
 * @param outIntrinsics Pointer to receive the camera intrinsics (fx, fy, cx, cy).
 * @param outDistortion Pointer to receive the distortion coefficients (k1, k2, p1, p2, k3). Array must be size 5.
 * @param outRmsError Pointer to receive the RMS reprojection error.
 * @return PMSDK_SUCCESS on success.
 */
PMSDK_API pmsdk_status_t pmsdk_calibrator_calibrate(pmsdk_calibrator_t* handle, 
                                                     int imageWidth, 
                                                     int imageHeight,
                                                     float* outIntrinsics, // float[4]
                                                     float* outDistortion, // float[5]
                                                     double* outRmsError);

// -----------------------------------------------------------------------------
// Decoder
// -----------------------------------------------------------------------------

/**
 * @brief Creates a new GrayCodeDecoder instance.
 * @param projectorWidth Width of the projector.
 * @param projectorHeight Height of the projector.
 * @return A handle to the decoder, or null on failure.
 */
PMSDK_API pmsdk_decoder_t* pmsdk_decoder_create(int projectorWidth, int projectorHeight);

/**
 * @brief Destroys a Decoder instance.
 */
PMSDK_API void pmsdk_decoder_destroy(pmsdk_decoder_t* handle);

/**
 * @brief Opens a physical camera for direct SDK capture.
 * @param handle Handle to the decoder.
 * @param cameraIndex OS index of the camera.
 * @return PMSDK_SUCCESS if opened.
 */
PMSDK_API pmsdk_status_t pmsdk_decoder_open_camera(pmsdk_decoder_t* handle, int cameraIndex);

/**
 * @brief Captures a frame from the opened camera and adds it to the decoder.
 * @param handle Handle to the decoder.
 * @return PMSDK_SUCCESS if captured.
 */
PMSDK_API pmsdk_status_t pmsdk_decoder_capture_frame(pmsdk_decoder_t* handle);

/**
 * @brief Closes the camera.
 * @param handle Handle to the decoder.
 */
PMSDK_API void pmsdk_decoder_close_camera(pmsdk_decoder_t* handle);

/**
 * @brief Captures a frame after grabbing and discarding buffered frames first.
 * cv::VideoCapture buffers internally; without flushing, the capture after a
 * pattern change often shows the PREVIOUS pattern. Use 2-3 flush frames.
 */
PMSDK_API pmsdk_status_t pmsdk_decoder_capture_frame_flushed(pmsdk_decoder_t* handle, int flushFrames);

/**
 * @brief Adds an image to the decoder by its file path.
 * @param filepath Null-terminated C-string of the image path.
 * @return PMSDK_SUCCESS if loaded.
 */
PMSDK_API pmsdk_status_t pmsdk_decoder_add_image(pmsdk_decoder_t* handle, const char* filepath);

/**
 * @brief Adds an 8-bit grayscale image supplied by the host (row-major, top-left origin).
 */
PMSDK_API pmsdk_status_t pmsdk_decoder_add_image_memory(pmsdk_decoder_t* handle, const uint8_t* pixels, int width, int height);

/**
 * @brief Reads back the most recently captured/added frame as 8-bit grayscale.
 *
 * Call once with outPixels == NULL to receive the dimensions in inOutWidth /
 * inOutHeight, then again with a buffer of exactly (width * height) bytes and
 * the same dimensions.
 *
 * @return PMSDK_SUCCESS, or PMSDK_ERROR_INVALID_ARGUMENT if no frame exists or
 * the supplied dimensions do not match the frame.
 */
PMSDK_API pmsdk_status_t pmsdk_decoder_get_last_frame(const pmsdk_decoder_t* handle, uint8_t* outPixels, int* inOutWidth, int* inOutHeight);

/** @brief Number of images currently accumulated in the decoder. */
PMSDK_API size_t pmsdk_decoder_get_image_count(const pmsdk_decoder_t* handle);

/** @brief Discards all accumulated images (camera stays open). */
PMSDK_API void pmsdk_decoder_clear_images(pmsdk_decoder_t* handle);

/**
 * @brief Robust decode over a robust capture sequence (see
 * pmsdk_graycode_generate_robust_pattern): images must be
 * [white, black, p0, p0inv, p1, p1inv, ...].
 *
 * Bits are decided per pixel by pattern-vs-inverse comparison and pixels whose
 * white-black contrast is below minContrast are rejected (shadow mask), which
 * makes the decode robust to ambient light and surface albedo — unlike
 * pmsdk_decoder_decode_and_triangulate's single global threshold.
 *
 * Returns raw camera->projector correspondences (no triangulation), suitable
 * for homography-based auto-align (docs/calibration-ux-design.md §9).
 *
 * @param minContrast Minimum white-black difference (0-255) for a lit pixel.
 * @param outCameraPoints Array of maxPoints camera pixels (x, y).
 * @param outProjectorPoints Array of maxPoints projector pixels (x, y).
 * @param outCount Receives the TOTAL number of matches found (may exceed maxPoints; only maxPoints are written).
 */
PMSDK_API pmsdk_status_t pmsdk_decoder_decode_robust(
    const pmsdk_decoder_t* handle,
    int minContrast,
    pmsdk_vec2_t* outCameraPoints,
    pmsdk_vec2_t* outProjectorPoints,
    size_t* outCount,
    size_t maxPoints);

/**
 * @brief Decodes the accumulated images and triangulates into 3D points.
 * @param handle Handle to the decoder.
 * @param threshold Grayscale threshold (0-255).
 * @param camIntrinsics Float array [4] (fx, fy, cx, cy) for the camera.
 * @param camExtrinsics Float array [6] (rvec x, y, z, tvec x, y, z) for the camera.
 * @param projIntrinsics Float array [4] for the projector.
 * @param projExtrinsics Float array [6] for the projector.
 * @param outPoints Pointer to an array of pmsdk_vec3_t to receive points.
 * @param outCount Pointer to size_t to receive the number of points written.
 * @param maxPoints Maximum number of points that can be written to outPoints.
 * @return PMSDK_SUCCESS on success.
 */
PMSDK_API pmsdk_status_t pmsdk_decoder_decode_and_triangulate(
    const pmsdk_decoder_t* handle,
    int threshold,
    const float* camIntrinsics, const float* camExtrinsics,
    const float* projIntrinsics, const float* projExtrinsics,
    pmsdk_vec3_t* outPoints, size_t* outCount, size_t maxPoints);

#ifdef __cplusplus
}
#endif
