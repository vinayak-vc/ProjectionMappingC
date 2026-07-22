using UnityEngine;

namespace vxholotrack
{
    /// <summary>
    /// Head-tracked STEREO (SBS-3D) driver: the binocular analog of
    /// <see cref="HeadTrackedCameraController"/>. Each frame it takes the tracked head center,
    /// offsets it by ±IPD/2 along the display's right axis, and applies a true off-axis frustum
    /// to each of two eye cameras against the SAME physical display surface — combining motion
    /// parallax (the head moves the whole rig) with binocular disparity (the ±IPD offset). Feed
    /// the two eye cameras' render targets into your SBS composer/packer downstream.
    ///
    /// Generic and engine-only: it references a tracker, a display surface, and two Unity
    /// cameras — no dependency on any specific stereo rig. Place it on the content camera so the
    /// rest-eye can be captured from that transform.
    /// </summary>
    [AddComponentMenu("ViitorX/HoloTrack/Head Tracked Stereo Controller")]
    public sealed class HeadTrackedStereoController : MonoBehaviour
    {
        [Tooltip("The head tracker driving both eyes.")]
        [SerializeField] private PMHTHeadTracker tracker;

        [Tooltip("The physical display surface (fixed off-axis window).")]
        [SerializeField] private HeadTrackingDisplaySurface surface;

        [Header("Eye cameras")]
        [Tooltip("Left-eye camera (its render target feeds the SBS composer's left eye).")]
        [SerializeField] private Camera leftCamera;
        [Tooltip("Right-eye camera.")]
        [SerializeField] private Camera rightCamera;

        [Tooltip("Interocular distance in world units (metres). ~0.06 for an adult.")]
        [SerializeField] private float ipd = 0.06f;

        [Tooltip("Swap left/right eyes if depth reads inverted on the hardware.")]
        [SerializeField] private bool swapEyes = false;

        [Header("Clip planes")]
        [SerializeField] private float nearPlane = 0.05f;
        [SerializeField] private float farPlane = 1000.0f;

        [Header("Rest eye")]
        [Tooltip("If true, this component's start position is captured as the rest eye; else restEyeWorld is used.")]
        [SerializeField] private bool useStartAsRestEye = true;
        [SerializeField] private Vector3 restEyeWorld = new Vector3(0f, 0f, -2f);

        private void Awake()
        {
            if (useStartAsRestEye)
            {
                restEyeWorld = transform.position;
            }
        }

        private void LateUpdate()
        {
            if (tracker == null || surface == null || leftCamera == null || rightCamera == null || !tracker.HasViewer)
            {
                return;
            }

            Vector3 head = ApplySafety(tracker.HeadPositionWorld);
            surface.GetCorners(out Vector3 bl, out Vector3 br, out Vector3 tl);
            Vector3 right = (br - bl).normalized;
            float half = ipd * 0.5f * (swapEyes ? -1.0f : 1.0f);

            Vector3 eyeL = head - right * half;
            Vector3 eyeR = head + right * half;

            if (tracker.TryComputeOffAxis(bl, br, tl, eyeL, nearPlane, farPlane, out Matrix4x4 viewL, out Matrix4x4 projL))
            {
                leftCamera.transform.position = eyeL;
                leftCamera.worldToCameraMatrix = viewL;
                leftCamera.projectionMatrix = projL;
            }
            if (tracker.TryComputeOffAxis(bl, br, tl, eyeR, nearPlane, farPlane, out Matrix4x4 viewR, out Matrix4x4 projR))
            {
                rightCamera.transform.position = eyeR;
                rightCamera.worldToCameraMatrix = viewR;
                rightCamera.projectionMatrix = projR;
            }
        }

        /// <summary>
        /// Assign the two eye cameras at runtime. A stereo rig that creates its eye pair on demand
        /// (e.g. PMSDKStereoContentRig via EnsureEyeCameras) binds them here so this controller can
        /// drive their off-axis matrices each LateUpdate.
        /// </summary>
        public void SetEyeCameras(Camera left, Camera right)
        {
            leftCamera = left;
            rightCamera = right;
        }

        /// <summary>Restore both eye cameras to Unity's automatic view/projection.</summary>
        public void RestoreDefaultProjection()
        {
            if (leftCamera != null)
            {
                leftCamera.ResetWorldToCameraMatrix();
                leftCamera.ResetProjectionMatrix();
            }
            if (rightCamera != null)
            {
                rightCamera.ResetWorldToCameraMatrix();
                rightCamera.ResetProjectionMatrix();
            }
        }

        private void OnDisable()
        {
            RestoreDefaultProjection();
        }

        // Movement scale + dead zone + max-travel clamp about the rest eye (spec §7), read from
        // the tracker's config so the stereo path shares the mono controller's tuning.
        private Vector3 ApplySafety(Vector3 headWorld)
        {
            float scale = 1.0f;
            float dead = 0.0f;
            float maxTravel = Mathf.Infinity;

            HeadTrackingConfig cfg = tracker.Config;
            if (cfg != null)
            {
                scale = cfg.movementScale;
                dead = cfg.deadZone;
                maxTravel = cfg.maxDistance > 0f ? cfg.maxDistance : Mathf.Infinity;
            }

            Vector3 delta = headWorld - restEyeWorld;
            if (delta.magnitude < dead)
            {
                return restEyeWorld;
            }
            delta *= scale;
            if (delta.magnitude > maxTravel)
            {
                delta = delta.normalized * maxTravel;
            }
            return restEyeWorld + delta;
        }
    }
}
