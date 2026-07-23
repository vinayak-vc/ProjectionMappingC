/**
 * @file Types.h
 * @brief Flat C types for the HoloTrackSDK C-API — the only stable ABI (mirrors PMSDK D-006).
 *
 * No C++ types, STL, or exceptions cross this boundary. Structs are plain POD so C#/P-Invoke can
 * blit them directly. Enumerations use fixed int values matching the C++ enums.
 */
#pragma once

#include "HoloTrack/Core/Export.h"
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/** @brief Status codes returned by all C-API functions. */
typedef enum {
    HT_SUCCESS = 0,                /**< Operation succeeded. */
    HT_ERROR_INVALID_ARGUMENT = 1, /**< A null or out-of-range argument was supplied. */
    HT_ERROR_INVALID_HANDLE = 2,   /**< The tracker handle was null/invalid. */
    HT_ERROR_UNKNOWN = 3           /**< An unexpected internal error occurred. */
} ht_status_t;

/** @brief Opaque tracker handle. */
typedef struct ht_tracker_t ht_tracker_t;

/** @brief Filter type; matches holotrack::FilterType. */
typedef enum {
    HT_FILTER_NONE = 0,
    HT_FILTER_EXPONENTIAL = 1,
    HT_FILTER_ONE_EURO = 2,
    HT_FILTER_KALMAN = 3
} ht_filter_type_t;

/** @brief Active-viewer selection mode; matches holotrack::SelectionMode. */
typedef enum {
    HT_SELECT_NEAREST_Z = 0,
    HT_SELECT_LARGEST_BOX = 1
} ht_selection_mode_t;

/** @brief Tracking state; matches holotrack::TrackingState. */
typedef enum {
    HT_STATE_SEARCHING = 0,
    HT_STATE_TRACKING = 1,
    HT_STATE_PREDICTION = 2,
    HT_STATE_LOST = 3
} ht_tracking_state_t;

/** @brief 3-component vector. */
typedef struct { float x, y, z; } ht_vec3_t;

/** @brief Quaternion (x,y,z,w). */
typedef struct { float x, y, z, w; } ht_quat_t;

/** @brief Flat mirror of holotrack::TrackerConfig (all tunables, no hardcoded values). */
typedef struct {
    int filterType;               /**< @ref ht_filter_type_t */
    float expAlpha;
    float oneEuroMinCutoff;
    float oneEuroBeta;
    float oneEuroDCutoff;
    float kalmanProcessVar;
    float kalmanMeasVar;

    int selection;                /**< @ref ht_selection_mode_t */
    float hysteresisMargin;
    int hysteresisFrames;
    float associationMaxDistance;

    float minDepth;
    float maxDepth;

    float predictionTimeSeconds;
    float lostTimeoutSeconds;

    float cameraVFovRad;
    float headHeightFraction;
    float bodyHeadOffset;

    float movementScale;
    float deadZone;
    float maxDistance;
    float minDistanceClamp;

    ht_vec3_t calibTranslation;
    ht_quat_t calibRotation;
    float calibScale;
} ht_tracker_config_t;

/** @brief Flat mirror of holotrack::Detection (with optional pose keypoints inlined). */
typedef struct {
    float bboxX, bboxY, bboxW, bboxH;
    ht_vec3_t spatial;
    float confidence;

    int poseValid;
    int hasNose, hasLeftEye, hasRightEye, hasNeck;
    ht_vec3_t nose, leftEye, rightEye, neck;
} ht_detection_t;

/** @brief Flat mirror of holotrack::TrackedViewer. */
typedef struct {
    int id;
    int valid;                    /**< 0/1 */
    ht_vec3_t headCamera;
    ht_vec3_t headWorld;
    ht_vec3_t velocity;
    float confidence;
    int state;                    /**< @ref ht_tracking_state_t */
    double timestampSeconds;
} ht_viewer_t;

/** @brief Off-axis projection result: column-major 4x4 view and projection matrices. */
typedef struct {
    float view[16];
    float projection[16];
    float eyeToScreen;
    int valid;                    /**< 0/1 */
} ht_offaxis_t;

#ifdef __cplusplus
}
#endif
