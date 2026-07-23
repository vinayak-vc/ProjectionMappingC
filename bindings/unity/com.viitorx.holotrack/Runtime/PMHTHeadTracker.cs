using System;
using UnityEngine;

namespace vxholotrack
{
    /// <summary>
    /// Owns a native HoloTrack tracker, pumps detections from an <see cref="IHeadTrackingSource"/>
    /// each frame, and exposes the smoothed viewer head pose. Allocation-free steady state: the
    /// detection buffer is reused and no managed garbage is produced per frame.
    /// </summary>
    [AddComponentMenu("ViitorX/HoloTrack/Head Tracker")]
    [DefaultExecutionOrder(-50)] // run before the camera controller's LateUpdate consumer
    public sealed class PMHTHeadTracker : MonoBehaviour
    {
        [Tooltip("Tracker configuration asset. If unset, built-in defaults are used.")]
        [SerializeField] private HeadTrackingConfig config;

        [Tooltip("A component implementing IHeadTrackingSource (e.g. PMHTSimulatedSource).")]
        [SerializeField] private MonoBehaviour sourceBehaviour;

        [Tooltip("Maximum detections accepted per frame.")]
        [SerializeField] private int maxDetections = 16;

        private IntPtr tracker = IntPtr.Zero;
        private IHeadTrackingSource source;
        private HtDetection[] buffer;
        private HtViewer viewer;

        /// <summary>The configuration asset in use (may be null).</summary>
        public HeadTrackingConfig Config => config;

        /// <summary>Native tracker handle (for advanced/interop use).</summary>
        public IntPtr NativeHandle => tracker;

        /// <summary>True when a usable head position is available this frame.</summary>
        public bool HasViewer => viewer.valid != 0;

        /// <summary>Smoothed head position in world space.</summary>
        public Vector3 HeadPositionWorld => new Vector3(viewer.headWorld.x, viewer.headWorld.y, viewer.headWorld.z);

        /// <summary>Smoothed head position in OAK camera space (pre-calibration).</summary>
        public Vector3 HeadPositionCamera => new Vector3(viewer.headCamera.x, viewer.headCamera.y, viewer.headCamera.z);

        /// <summary>Camera-space velocity estimate (m/s).</summary>
        public Vector3 Velocity => new Vector3(viewer.velocity.x, viewer.velocity.y, viewer.velocity.z);

        /// <summary>Current tracking state.</summary>
        public HtTrackingState State => (HtTrackingState)viewer.state;

        /// <summary>Stable identity of the active viewer (-1 when none).</summary>
        public int ViewerId => viewer.id;

        /// <summary>Confidence [0,1] of the backing detection.</summary>
        public float Confidence => viewer.confidence;

        /// <summary>Timestamp (seconds) of the last processed frame.</summary>
        public double LastTimestamp => viewer.timestampSeconds;

        private void Awake()
        {
            buffer = new HtDetection[Mathf.Max(1, maxDetections)];
            source = sourceBehaviour as IHeadTrackingSource;
            if (sourceBehaviour != null && source == null)
            {
                Debug.LogError($"[HoloTrack] '{sourceBehaviour.GetType().Name}' does not implement IHeadTrackingSource.", this);
            }
            CreateTracker();
        }

        private void CreateTracker()
        {
            HtTrackerConfig native = config != null ? config.ToNative() : DefaultConfig();
            tracker = HoloTrackNative.ht_tracker_create(ref native);
            if (tracker == IntPtr.Zero)
            {
                Debug.LogError($"[HoloTrack] tracker creation failed: {HoloTrackNative.GetLastError()}", this);
            }
        }

        private void Update()
        {
            if (tracker == IntPtr.Zero || source == null)
            {
                return;
            }

            if (!source.TryPoll(buffer, out int count, out double timestamp))
            {
                return; // no new frame this tick
            }

            HtStatus status = count > 0
                ? HoloTrackNative.ht_tracker_push_frame(tracker, buffer, (UIntPtr)count, timestamp)
                : HoloTrackNative.ht_tracker_push_frame(tracker, null, UIntPtr.Zero, timestamp);

            if (status != HtStatus.Success)
            {
                Debug.LogWarning($"[HoloTrack] push_frame failed: {HoloTrackNative.GetLastError()}", this);
                return;
            }

            HoloTrackNative.ht_tracker_get_viewer(tracker, out viewer);
        }

        /// <summary>Re-apply the configuration asset to the running tracker.</summary>
        public void ApplyConfig()
        {
            if (tracker == IntPtr.Zero || config == null)
            {
                return;
            }
            HtTrackerConfig native = config.ToNative();
            HoloTrackNative.ht_tracker_set_config(tracker, ref native);
        }

        /// <summary>Reset tracking (drop the viewer, clear filter/selector state).</summary>
        public void ResetTracking()
        {
            if (tracker != IntPtr.Zero)
            {
                HoloTrackNative.ht_tracker_reset(tracker);
                viewer = default;
            }
        }

        /// <summary>
        /// Compute the off-axis view + projection for a display surface and the current viewer.
        /// </summary>
        /// <returns>True on a valid (non-degenerate) result.</returns>
        public bool TryComputeOffAxis(Vector3 bottomLeft, Vector3 bottomRight, Vector3 topLeft,
                                      float nearPlane, float farPlane,
                                      out Matrix4x4 view, out Matrix4x4 projection)
        {
            view = Matrix4x4.identity;
            projection = Matrix4x4.identity;
            if (tracker == IntPtr.Zero)
            {
                return false;
            }

            HtVec3 pa = new HtVec3(bottomLeft.x, bottomLeft.y, bottomLeft.z);
            HtVec3 pb = new HtVec3(bottomRight.x, bottomRight.y, bottomRight.z);
            HtVec3 pc = new HtVec3(topLeft.x, topLeft.y, topLeft.z);
            HtStatus status = HoloTrackNative.ht_tracker_compute_offaxis(
                tracker, ref pa, ref pb, ref pc, nearPlane, farPlane, out HtOffAxis result);
            if (status != HtStatus.Success || result.valid == 0 || result.view == null || result.projection == null)
            {
                return false;
            }

            view = ToMatrix(result.view);
            projection = ToMatrix(result.projection);
            return true;
        }

        /// <summary>
        /// Off-axis view + projection for an EXPLICIT eye position (stateless native solve). Used to
        /// drive a stereo pair — left/right eye at head ± IPD/2 — against the same display surface.
        /// </summary>
        /// <returns>True on a valid (non-degenerate) result.</returns>
        public bool TryComputeOffAxis(Vector3 bottomLeft, Vector3 bottomRight, Vector3 topLeft, Vector3 eye,
                                      float nearPlane, float farPlane,
                                      out Matrix4x4 view, out Matrix4x4 projection)
        {
            view = Matrix4x4.identity;
            projection = Matrix4x4.identity;

            HtVec3 pa = new HtVec3(bottomLeft.x, bottomLeft.y, bottomLeft.z);
            HtVec3 pb = new HtVec3(bottomRight.x, bottomRight.y, bottomRight.z);
            HtVec3 pc = new HtVec3(topLeft.x, topLeft.y, topLeft.z);
            HtVec3 e = new HtVec3(eye.x, eye.y, eye.z);
            HtStatus status = HoloTrackNative.ht_compute_offaxis_eye(ref pa, ref pb, ref pc, ref e,
                                                                     nearPlane, farPlane, out HtOffAxis result);
            if (status != HtStatus.Success || result.valid == 0 || result.view == null || result.projection == null)
            {
                return false;
            }
            view = ToMatrix(result.view);
            projection = ToMatrix(result.projection);
            return true;
        }

        private void OnDestroy()
        {
            if (tracker != IntPtr.Zero)
            {
                HoloTrackNative.ht_tracker_destroy(tracker);
                tracker = IntPtr.Zero;
            }
        }

        // Column-major float[16] (index = col*4 + row) → Unity Matrix4x4.
        private static Matrix4x4 ToMatrix(float[] m)
        {
            Matrix4x4 r = new Matrix4x4();
            r.SetColumn(0, new Vector4(m[0], m[1], m[2], m[3]));
            r.SetColumn(1, new Vector4(m[4], m[5], m[6], m[7]));
            r.SetColumn(2, new Vector4(m[8], m[9], m[10], m[11]));
            r.SetColumn(3, new Vector4(m[12], m[13], m[14], m[15]));
            return r;
        }

        private static HtTrackerConfig DefaultConfig()
        {
            return new HtTrackerConfig
            {
                filterType = (int)HtFilterType.OneEuro,
                expAlpha = 0.5f,
                oneEuroMinCutoff = 1.0f,
                oneEuroBeta = 0.007f,
                oneEuroDCutoff = 1.0f,
                kalmanProcessVar = 0.01f,
                kalmanMeasVar = 0.1f,
                selection = (int)HtSelectionMode.NearestZ,
                hysteresisMargin = 0.15f,
                hysteresisFrames = 8,
                associationMaxDistance = 0.6f,
                minDepth = 0.3f,
                maxDepth = 6.0f,
                predictionTimeSeconds = 0.5f,
                lostTimeoutSeconds = 1.0f,
                cameraVFovRad = 1.20f,
                headHeightFraction = 0.9f,
                bodyHeadOffset = 0.0f,
                movementScale = 1.0f,
                deadZone = 0.0f,
                maxDistance = 3.0f,
                minDistanceClamp = 0.0f,
                calibTranslation = new HtVec3(0f, 0f, 0f),
                calibRotation = new HtQuat(0f, 0f, 0f, 1f),
                calibScale = 1.0f
            };
        }
    }
}
