/**
 * @file DeviceAPI.h
 * @brief C-API for the OAK-D detection source. Always present in the ABI; inert (Start fails,
 *        @ref ht_oak_is_supported returns 0) when the SDK was built without DepthAI (D-032).
 *
 * Usage: create → start → poll each frame for detections, then feed them to a tracker via
 * @ref ht_tracker_push_frame. Keeps a single tracker-input path (push) whether detections come
 * from the OAK device or a simulated/host source.
 */
#pragma once

#include "HoloTrack/C_API/Types.h"

#ifdef __cplusplus
extern "C" {
#endif

/** @brief Opaque OAK detection-source handle. */
typedef struct ht_oak_source_t ht_oak_source_t;

/** @brief OAK device options. Mirrors holotrack::OakOptions. */
typedef struct {
    const char* blobPath;          /**< Spatial-detection network blob path (required to start). */
    int personLabel;               /**< VOC class id for "person" (MobileNet-SSD: 15). */
    float confidenceThreshold;     /**< Minimum detection confidence [0,1]. */
    float depthLowerThresholdMm;   /**< Stereo depth lower clamp (mm). */
    float depthUpperThresholdMm;   /**< Stereo depth upper clamp (mm). */
} ht_oak_options_t;

/** @brief 1 if this build includes DepthAI support, else 0. */
HOLOTRACK_API int ht_oak_is_supported(void);

/** @brief Create an OAK source. @param options may be null for defaults. Null on allocation failure. */
HOLOTRACK_API ht_oak_source_t* ht_oak_create(const ht_oak_options_t* options);

/** @brief Destroy an OAK source (stops it first). Null is ignored. */
HOLOTRACK_API void ht_oak_destroy(ht_oak_source_t* source);

/** @brief Open the device and start streaming. Fails (non-success) without DepthAI or a device. */
HOLOTRACK_API ht_status_t ht_oak_start(ht_oak_source_t* source);

/** @brief Stop streaming and release the device. */
HOLOTRACK_API ht_status_t ht_oak_stop(ht_oak_source_t* source);

/** @brief 1 while running, else 0. */
HOLOTRACK_API int ht_oak_is_running(ht_oak_source_t* source);

/**
 * @brief Copy the latest frame's detections into @p buffer.
 * @param buffer Caller array of capacity @p capacity.
 * @param outCount Number of detections written (<= capacity).
 * @param outHasFrame Set to 1 if a new frame was available this call, else 0.
 * @param outTimestampSeconds Frame timestamp (monotonic seconds).
 */
HOLOTRACK_API ht_status_t ht_oak_poll(ht_oak_source_t* source, ht_detection_t* buffer,
                                      size_t capacity, size_t* outCount, int* outHasFrame,
                                      double* outTimestampSeconds);

/** @brief Last error message (never null). */
HOLOTRACK_API const char* ht_oak_last_error(ht_oak_source_t* source);

#ifdef __cplusplus
}
#endif
