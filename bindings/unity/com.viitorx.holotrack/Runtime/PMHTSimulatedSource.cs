using UnityEngine;

namespace vxholotrack
{
    /// <summary>
    /// Simulated detection source for development without an OAK-D. Emits a single detection whose
    /// spatial position is a proxy transform's position expressed in this component's local space
    /// (treated as OAK camera space, metres). Drag the proxy around in the scene to exercise the
    /// full tracking + off-axis pipeline and see motion parallax with no hardware (D-032).
    /// </summary>
    [AddComponentMenu("ViitorX/HoloTrack/Simulated Source")]
    public sealed class PMHTSimulatedSource : MonoBehaviour, IHeadTrackingSource
    {
        [Tooltip("Transform whose position (in this source's local space) is the simulated head.")]
        [SerializeField] private Transform headProxy;

        [Tooltip("Reported detection confidence [0,1].")]
        [Range(0f, 1f)]
        [SerializeField] private float confidence = 0.95f;

        [Tooltip("Emit pose keypoints (eyes) so the estimator uses the exact head position.")]
        [SerializeField] private bool emitPoseKeypoints = true;

        /// <inheritdoc />
        public bool TryPoll(HtDetection[] buffer, out int count, out double timestampSeconds)
        {
            timestampSeconds = Time.timeAsDouble;
            if (headProxy == null || buffer == null || buffer.Length == 0)
            {
                count = 0;
                return true; // still a valid (empty) frame
            }

            Vector3 p = transform.InverseTransformPoint(headProxy.position);
            HtVec3 spatial = new HtVec3(p.x, p.y, p.z);

            HtDetection d = default;
            d.spatial = spatial;
            d.bboxW = 0.2f;
            d.bboxH = 0.5f;
            d.confidence = confidence;
            if (emitPoseKeypoints)
            {
                d.poseValid = 1;
                d.hasLeftEye = 1;
                d.hasRightEye = 1;
                d.leftEye = spatial;
                d.rightEye = spatial;
            }

            buffer[0] = d;
            count = 1;
            return true;
        }
    }
}
