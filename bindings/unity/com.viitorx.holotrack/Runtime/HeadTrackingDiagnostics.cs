using UnityEngine;

namespace vxholotrack
{
    /// <summary>
    /// Runtime diagnostics overlay + scene gizmos (spec §11): tracking FPS, viewer position,
    /// confidence, state, distance, latency, active filter, and tracking id; draws the head
    /// point, the display frustum, and the camera-space depth point in the Scene view.
    /// </summary>
    [AddComponentMenu("ViitorX/HoloTrack/Diagnostics")]
    public sealed class HeadTrackingDiagnostics : MonoBehaviour
    {
        [SerializeField] private PMHTHeadTracker tracker;
        [SerializeField] private HeadTrackingDisplaySurface surface;

        [Header("Overlay")]
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private Vector2 overlayOrigin = new Vector2(12f, 12f);

        [Header("Gizmos")]
        [SerializeField] private bool drawHeadPoint = true;
        [SerializeField] private bool drawFrustum = true;
        [SerializeField] private float headGizmoRadius = 0.05f;

        private float smoothedFps;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
            {
                float instant = 1.0f / dt;
                smoothedFps = smoothedFps <= 0f ? instant : Mathf.Lerp(smoothedFps, instant, 0.1f);
            }
        }

        private void OnGUI()
        {
            if (!showOverlay || tracker == null)
            {
                return;
            }

            float latencyMs = tracker.HasViewer
                ? Mathf.Max(0f, (float)(Time.timeAsDouble - tracker.LastTimestamp) * 1000f)
                : 0f;

            float distance = 0f;
            if (surface != null && tracker.HasViewer)
            {
                surface.GetCorners(out Vector3 bl, out Vector3 br, out Vector3 tl);
                Vector3 normal = Vector3.Cross(br - bl, tl - bl).normalized;
                distance = Mathf.Abs(Vector3.Dot(tracker.HeadPositionWorld - bl, normal));
            }

            string filterName = tracker.Config != null ? tracker.Config.filterType.ToString() : "Default";
            Vector3 pos = tracker.HeadPositionWorld;

            GUILayout.BeginArea(new Rect(overlayOrigin.x, overlayOrigin.y, 320f, 220f), GUI.skin.box);
            GUILayout.Label("HoloTrack Diagnostics");
            GUILayout.Label($"Tracking FPS : {smoothedFps:F1}");
            GUILayout.Label($"State        : {tracker.State}");
            GUILayout.Label($"Tracking ID  : {tracker.ViewerId}");
            GUILayout.Label($"Viewer (world): {pos.x:F2}, {pos.y:F2}, {pos.z:F2}");
            GUILayout.Label($"Distance     : {distance:F2} m");
            GUILayout.Label($"Confidence   : {tracker.Confidence:F2}");
            GUILayout.Label($"Latency      : {latencyMs:F1} ms");
            GUILayout.Label($"Filter       : {filterName}");
            GUILayout.EndArea();
        }

        private void OnDrawGizmos()
        {
            if (tracker == null || !Application.isPlaying || !tracker.HasViewer)
            {
                return;
            }

            Vector3 head = tracker.HeadPositionWorld;
            if (drawHeadPoint)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(head, headGizmoRadius);
            }

            if (drawFrustum && surface != null)
            {
                surface.GetCorners(out Vector3 bl, out Vector3 br, out Vector3 tl);
                Vector3 tr = br + (tl - bl);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(head, bl);
                Gizmos.DrawLine(head, br);
                Gizmos.DrawLine(head, tl);
                Gizmos.DrawLine(head, tr);
            }
        }
    }
}
