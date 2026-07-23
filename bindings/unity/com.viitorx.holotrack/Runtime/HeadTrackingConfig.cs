using UnityEngine;

namespace vxholotrack
{
    /// <summary>
    /// Inspector-editable tracker configuration (spec §13: no hardcoded values). Converts to the
    /// flat native <see cref="HtTrackerConfig"/> consumed by the SDK. Persist/edit as an asset.
    /// </summary>
    [CreateAssetMenu(menuName = "ViitorX/HoloTrack/Head Tracking Config", fileName = "HeadTrackingConfig")]
    public sealed class HeadTrackingConfig : ScriptableObject
    {
        [Header("Filter")]
        public HtFilterType filterType = HtFilterType.OneEuro;

        [Header("Exponential")]
        [Range(0f, 1f)] public float expAlpha = 0.5f;

        [Header("One Euro")]
        public float oneEuroMinCutoff = 1.0f;
        public float oneEuroBeta = 0.007f;
        public float oneEuroDCutoff = 1.0f;

        [Header("Kalman")]
        public float kalmanProcessVar = 0.01f;
        public float kalmanMeasVar = 0.1f;

        [Header("Active-viewer selection")]
        public HtSelectionMode selection = HtSelectionMode.NearestZ;
        public float hysteresisMargin = 0.15f;
        public int hysteresisFrames = 8;
        public float associationMaxDistance = 0.6f;

        [Header("Depth gate (metres)")]
        public float minDepth = 0.3f;
        public float maxDepth = 6.0f;

        [Header("State machine (seconds)")]
        public float predictionTime = 0.5f;
        public float lostTimeout = 1.0f;

        [Header("Head estimate")]
        public float cameraVFovRad = 1.20f;
        [Range(0f, 1f)] public float headHeightFraction = 0.9f;
        public float bodyHeadOffset = 0.0f;

        [Header("Safety / tuning (applied to the world eye)")]
        public float movementScale = 1.0f;
        public float deadZone = 0.0f;
        public float maxDistance = 3.0f;
        public float minDistanceClamp = 0.0f;

        [Header("OAK → world calibration")]
        public Vector3 calibTranslation = Vector3.zero;
        public Vector3 calibEuler = Vector3.zero;
        public float calibScale = 1.0f;

        /// <summary>Build the flat native configuration from these inspector values.</summary>
        public HtTrackerConfig ToNative()
        {
            Quaternion q = Quaternion.Euler(calibEuler);
            HtTrackerConfig c = new HtTrackerConfig
            {
                filterType = (int)filterType,
                expAlpha = expAlpha,
                oneEuroMinCutoff = oneEuroMinCutoff,
                oneEuroBeta = oneEuroBeta,
                oneEuroDCutoff = oneEuroDCutoff,
                kalmanProcessVar = kalmanProcessVar,
                kalmanMeasVar = kalmanMeasVar,

                selection = (int)selection,
                hysteresisMargin = hysteresisMargin,
                hysteresisFrames = hysteresisFrames,
                associationMaxDistance = associationMaxDistance,

                minDepth = minDepth,
                maxDepth = maxDepth,

                predictionTimeSeconds = predictionTime,
                lostTimeoutSeconds = lostTimeout,

                cameraVFovRad = cameraVFovRad,
                headHeightFraction = headHeightFraction,
                bodyHeadOffset = bodyHeadOffset,

                movementScale = movementScale,
                deadZone = deadZone,
                maxDistance = maxDistance,
                minDistanceClamp = minDistanceClamp,

                calibTranslation = new HtVec3(calibTranslation.x, calibTranslation.y, calibTranslation.z),
                calibRotation = new HtQuat(q.x, q.y, q.z, q.w),
                calibScale = calibScale
            };
            return c;
        }
    }
}
