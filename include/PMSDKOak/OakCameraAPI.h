/**
 * @file OakCameraAPI.h
 * @brief C API of PMSDKOakCamera.dll: OAK-D colour camera (CAM_A) frames for calibration.
 *
 * The OAK-D is not a UVC webcam (it enumerates as a DepthAI/XLink device), so OpenCV
 * cv::VideoCapture cannot open it (D-034). This DLL runs a minimal DepthAI pipeline
 * (ColorCamera -> NV12 video stream, plus a CameraControl input) on a background thread and keeps
 * the newest frame's luma (Y) plane, which is all the calibration detectors need.
 *
 * Usage: create -> start -> poll pmoak_latest_sequence() until it advances past the frame you
 * need -> pmoak_copy_latest_gray() -> stop/destroy. Only one process may own the device: close OAK
 * Viewer and do not run HoloTrack's OAK source at the same time.
 *
 * Status codes: 0 = success, negative = error (see pmoak_last_error), 1 = "no frame yet".
 */
#pragma once

#include <stddef.h>
#include <stdint.h>

#include "PMSDKOak/Export.h"

#ifdef __cplusplus
extern "C" {
#endif

/** @brief Opaque camera handle. */
typedef struct pmoak_camera_t pmoak_camera_t;

/** @brief Status codes returned by the API. */
typedef enum {
    PMOAK_OK = 0,
    PMOAK_NO_FRAME = 1,
    PMOAK_ERROR_INVALID_ARGUMENT = -1,
    PMOAK_ERROR_NOT_RUNNING = -2,
    PMOAK_ERROR_DEVICE = -3,
    PMOAK_ERROR_BUFFER_TOO_SMALL = -4
} pmoak_status_t;

/** @brief Lens model of the factory calibration; matches dai::CameraModel. */
typedef enum {
    PMOAK_MODEL_PERSPECTIVE = 0,
    PMOAK_MODEL_FISHEYE = 1,
    PMOAK_MODEL_EQUIRECTANGULAR = 2,
    PMOAK_MODEL_RADIAL_DIVISION = 3
} pmoak_camera_model_t;

/** @brief Start options. Field order must match the C# struct. */
typedef struct {
    int maxWidth;        /**< Largest output width; the sensor image is downscaled to fit (default 1920). */
    float fps;           /**< Requested frame rate (default 15). */
    const char* mxId;    /**< Device MXID to open, or null/empty for the first available device. */
    int enableDepth;     /**< 1 = also stream stereo depth aligned to the colour camera (default 0). */
} pmoak_options_t;

/** @brief Factory intrinsics of the colour camera, scaled to the output frame size. */
typedef struct {
    int width;               /**< Output frame width the intrinsics apply to. */
    int height;              /**< Output frame height the intrinsics apply to. */
    int model;               /**< @ref pmoak_camera_model_t. */
    float fx;
    float fy;
    float cx;
    float cy;
    int distortionCount;     /**< Valid entries in @ref distortion (OpenCV order k1,k2,p1,p2,k3,k4,k5,k6,s1..s4,tx,ty). */
    float distortion[14];
} pmoak_intrinsics_t;

/** @brief Fill @p options with defaults. */
PMOAK_API void pmoak_options_default(pmoak_options_t* options);

/** @brief Create a camera handle (does not touch the device). Null on allocation failure. */
PMOAK_API pmoak_camera_t* pmoak_create(const pmoak_options_t* options);

/** @brief Stop and destroy. Null is ignored. */
PMOAK_API void pmoak_destroy(pmoak_camera_t* camera);

/** @brief Open the device and start streaming. Blocks while the device boots (a few seconds). */
PMOAK_API int pmoak_start(pmoak_camera_t* camera);

/** @brief Stop streaming and release the device. */
PMOAK_API int pmoak_stop(pmoak_camera_t* camera);

/** @brief 1 while streaming, else 0. */
PMOAK_API int pmoak_is_running(pmoak_camera_t* camera);

/** @brief Output frame size (valid after a successful start). */
PMOAK_API int pmoak_get_frame_size(pmoak_camera_t* camera, int* outWidth, int* outHeight);

/** @brief Number of frames received since start (0 = none yet). Increases by one per frame. */
PMOAK_API int64_t pmoak_latest_sequence(pmoak_camera_t* camera);

/**
 * @brief Copy the newest frame's luma plane (8-bit, tightly packed, width*height bytes).
 * @return PMOAK_OK, PMOAK_NO_FRAME before the first frame, or an error.
 */
PMOAK_API int pmoak_copy_latest_gray(pmoak_camera_t* camera, uint8_t* buffer, size_t capacity,
                                     int* outWidth, int* outHeight, int64_t* outSequence);

/** @brief Number of depth frames received since start (0 = none, or depth not enabled). */
PMOAK_API int64_t pmoak_latest_depth_sequence(pmoak_camera_t* camera);

/**
 * @brief Copy the newest depth frame: millimetres per pixel (0 = no data), aligned to and the same
 *        size as the colour frame (so the colour intrinsics apply), tightly packed, rows top first.
 * @param capacity buffer size in elements (>= width * height).
 * @return PMOAK_OK, PMOAK_NO_FRAME before the first depth frame, or an error (also when depth is off).
 */
PMOAK_API int pmoak_copy_latest_depth(pmoak_camera_t* camera, uint16_t* buffer, size_t capacity, int* outWidth, int* outHeight,
                                      int64_t* outSequence);

/** @brief Lock exposure: time in microseconds (1..33000) and ISO sensitivity (100..1600). */
PMOAK_API int pmoak_set_manual_exposure(pmoak_camera_t* camera, uint32_t exposureUs, uint32_t iso);

/** @brief Return to automatic exposure. */
PMOAK_API int pmoak_set_auto_exposure(pmoak_camera_t* camera);

/** @brief Lock focus at a lens position 0..255 (ignored by fixed-focus modules). */
PMOAK_API int pmoak_set_manual_focus(pmoak_camera_t* camera, int lensPosition);

/** @brief Factory intrinsics for the output frame size (valid after a successful start). */
PMOAK_API int pmoak_get_intrinsics(pmoak_camera_t* camera, pmoak_intrinsics_t* outIntrinsics);

/** @brief Device MXID and sensor name ("<mxid> <sensor>"); empty before start. Never null. */
PMOAK_API const char* pmoak_device_description(pmoak_camera_t* camera);

/** @brief Last error message for this handle. Never null. */
PMOAK_API const char* pmoak_last_error(pmoak_camera_t* camera);

#ifdef __cplusplus
}
#endif
