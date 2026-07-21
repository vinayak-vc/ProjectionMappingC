using UnityEngine;

namespace vxholotrack
{
    /// <summary>
    /// Drives a Unity <see cref="Camera"/> as a perspective-correct holographic view (spec §7, §8).
    /// Each frame it takes the tracked head position, applies the safety/tuning layer
    /// (movement scale, dead zone, max travel), moves the camera to that eye, and sets the
    /// off-axis view + projection matrices for the configured display surface — producing true
    /// motion parallax rather than a naive camera translation (D-030).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("ViitorX/HoloTrack/Head Tracked Camera Controller")]
    public sealed class HeadTrackedCameraController : MonoBehaviour
    {
        [Tooltip("The head tracker driving this camera.")]
        [SerializeField] private PMHTHeadTracker tracker;

        [Tooltip("The physical display surface (fixed off-axis window).")]
        [SerializeField] private HeadTrackingDisplaySurface surface;

        [Header("Clip planes")]
        [SerializeField] private float nearPlane = 0.05f;
        [SerializeField] private float farPlane = 1000.0f;

        [Header("Rest eye")]
        [Tooltip("If true, the camera's start position is captured as the rest eye; otherwise restEyeWorld is used.")]
        [SerializeField] private bool useStartAsRestEye = true;
        [SerializeField] private Vector3 restEyeWorld = new Vector3(0f, 0f, -2f);

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (useStartAsRestEye)
            {
                restEyeWorld = transform.position;
            }
        }

        private void LateUpdate()
        {
            if (tracker == null || surface == null || cam == null || !tracker.HasViewer)
            {
                return;
            }

            Vector3 eye = ApplySafety(tracker.HeadPositionWorld);
            surface.GetCorners(out Vector3 bl, out Vector3 br, out Vector3 tl);

            // Move the transform to the eye so the near/far planes and gizmos stay meaningful;
            // the explicit matrices below override Unity's usual view/projection derivation.
            transform.position = eye;

            if (tracker.TryComputeOffAxis(bl, br, tl, nearPlane, farPlane,
                                          out Matrix4x4 view, out Matrix4x4 projection))
            {
                cam.worldToCameraMatrix = view;
                cam.projectionMatrix = projection;
            }
        }

        /// <summary>Reset to Unity's automatic view/projection (e.g. when disabling head tracking).</summary>
        public void RestoreDefaultProjection()
        {
            if (cam != null)
            {
                cam.ResetWorldToCameraMatrix();
                cam.ResetProjectionMatrix();
            }
        }

        private void OnDisable()
        {
            RestoreDefaultProjection();
        }

        // Movement scale + dead zone + max-travel clamp, relative to the rest eye (spec §7).
        private Vector3 ApplySafety(Vector3 head)
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

            Vector3 delta = head - restEyeWorld;
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
