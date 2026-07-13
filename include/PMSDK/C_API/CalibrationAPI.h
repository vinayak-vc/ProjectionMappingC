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
 * @brief Adds an image to the decoder by its file path.
 * @param filepath Null-terminated C-string of the image path.
 * @return PMSDK_SUCCESS if loaded.
 */
PMSDK_API pmsdk_status_t pmsdk_decoder_add_image(pmsdk_decoder_t* handle, const char* filepath);

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
