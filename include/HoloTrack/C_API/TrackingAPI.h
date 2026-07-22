/**
 * @file TrackingAPI.h
 * @brief HoloTrackSDK C-API — create a tracker, push detections, read the head pose, and build
 *        the off-axis projection. This is the surface Unity/Unreal P-Invoke against.
 *
 * All functions validate their arguments and never throw across the boundary; failures return a
 * non-zero @ref ht_status_t and set a per-thread message retrievable via @ref ht_get_last_error.
 */
#pragma once

#include "HoloTrack/C_API/Types.h"

#ifdef __cplusplus
extern "C" {
#endif

/** @brief SDK version (semantic). Any pointer may be null. */
HOLOTRACK_API void ht_get_version(int* major, int* minor, int* patch);

/** @brief Human-readable message for the most recent failing call on this thread (never null). */
HOLOTRACK_API const char* ht_get_last_error(void);

/**
 * @brief Create a tracker.
 * @param config Initial configuration (must not be null).
 * @return Handle, or null on failure (see @ref ht_get_last_error).
 */
HOLOTRACK_API ht_tracker_t* ht_tracker_create(const ht_tracker_config_t* config);

/** @brief Destroy a tracker (null is ignored). */
HOLOTRACK_API void ht_tracker_destroy(ht_tracker_t* tracker);

/** @brief Replace the configuration. */
HOLOTRACK_API ht_status_t ht_tracker_set_config(ht_tracker_t* tracker, const ht_tracker_config_t* config);

/** @brief Read the current configuration into @p out. */
HOLOTRACK_API ht_status_t ht_tracker_get_config(ht_tracker_t* tracker, ht_tracker_config_t* out);

/**
 * @brief Process one detection frame.
 * @param detections Array of @p count detections; may be null when @p count is 0.
 * @param count Number of detections.
 * @param timestampSeconds Monotonic frame timestamp.
 */
HOLOTRACK_API ht_status_t ht_tracker_push_frame(ht_tracker_t* tracker,
                                                const ht_detection_t* detections,
                                                size_t count, double timestampSeconds);

/** @brief Read the current viewer state into @p out. */
HOLOTRACK_API ht_status_t ht_tracker_get_viewer(ht_tracker_t* tracker, ht_viewer_t* out);

/**
 * @brief Compute the off-axis view+projection for the current viewer and a display surface.
 * @param pa Screen bottom-left (world). @param pb bottom-right. @param pc top-left.
 * @param nearPlane Near clip (>0). @param farPlane Far clip (> near).
 * @param out Receives the matrices; @c out->valid is 0 for a degenerate configuration.
 */
HOLOTRACK_API ht_status_t ht_tracker_compute_offaxis(ht_tracker_t* tracker,
                                                     const ht_vec3_t* pa, const ht_vec3_t* pb,
                                                     const ht_vec3_t* pc, float nearPlane,
                                                     float farPlane, ht_offaxis_t* out);

/**
 * @brief Stateless off-axis view+projection for an ARBITRARY eye and display surface.
 *
 * Unlike @ref ht_tracker_compute_offaxis (which uses the tracker's internal head), this takes
 * an explicit @p eye, so a caller can drive a stereo pair by solving twice — one eye at
 * head - right*(IPD/2), one at head + right*(IPD/2) — against the same display quad. No tracker
 * handle required.
 * @param pa Screen bottom-left (world). @param pb bottom-right. @param pc top-left.
 * @param eye Eye position (world).
 * @param nearPlane Near clip (>0). @param farPlane Far clip (> near).
 * @param out Receives the matrices; @c out->valid is 0 for a degenerate configuration.
 */
HOLOTRACK_API ht_status_t ht_compute_offaxis_eye(const ht_vec3_t* pa, const ht_vec3_t* pb,
                                                 const ht_vec3_t* pc, const ht_vec3_t* eye,
                                                 float nearPlane, float farPlane, ht_offaxis_t* out);

/** @brief Reset tracking (drop the viewer, clear filter/selector state). */
HOLOTRACK_API ht_status_t ht_tracker_reset(ht_tracker_t* tracker);

#ifdef __cplusplus
}
#endif
