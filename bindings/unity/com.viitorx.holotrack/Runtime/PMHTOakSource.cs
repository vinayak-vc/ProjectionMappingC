using System;
using UnityEngine;

namespace vxholotrack
{
    /// <summary>
    /// Real OAK-D detection source: on-device spatial person detection via DepthAI, exposed to
    /// <see cref="PMHTHeadTracker"/> through the same <see cref="IHeadTrackingSource"/> path as the
    /// simulated source. Requires the native DLL to be built with DepthAI (otherwise
    /// <see cref="HoloTrackNative.ht_oak_is_supported"/> is 0 and this logs a warning and produces
    /// no frames). A compiled spatial-detection blob (e.g. MobileNet-SSD 300x300) is required.
    /// </summary>
    [AddComponentMenu("ViitorX/HoloTrack/OAK-D Source")]
    public sealed class PMHTOakSource : MonoBehaviour, IHeadTrackingSource
    {
        [Tooltip("Which detector produces the head position. FaceThenPerson prefers the face and falls back to the person box.")]
        [SerializeField] private HtDetectionMode detectionMode = HtDetectionMode.FaceThenPerson;

        [Tooltip("Path to the PERSON spatial-detection blob (absolute, or under StreamingAssets). Used by Person/FaceThenPerson.")]
        [SerializeField] private string blobPath = string.Empty;

        [Tooltip("Path to the FACE spatial-detection blob (absolute, or under StreamingAssets). Used by Face/FaceThenPerson.")]
        [SerializeField] private string faceBlobPath = string.Empty;

        [Tooltip("FaceThenPerson: consecutive faceless frames before falling back to the person box.")]
        [SerializeField] private int faceFallbackFrames = 15;

        [Tooltip("Detector class id treated as 'person' (MobileNet-SSD VOC: 15).")]
        [SerializeField] private int personLabel = 15;

        [Range(0f, 1f)]
        [SerializeField] private float confidenceThreshold = 0.5f;

        [SerializeField] private float depthLowerThresholdMm = 300f;
        [SerializeField] private float depthUpperThresholdMm = 8000f;

        [Tooltip("Mirror detections horizontally (negate X). Enable when the camera faces the viewer (selfie view) so the tracked head moves the correct way.")]
        [SerializeField] private bool mirrorX = false;

        private IntPtr source = IntPtr.Zero;
        private bool started;

        /// <summary>True if the native DLL includes DepthAI support.</summary>
        public bool IsSupported => HoloTrackNative.ht_oak_is_supported() != 0;

        /// <summary>True while the device is streaming.</summary>
        public bool IsRunning => source != IntPtr.Zero && HoloTrackNative.ht_oak_is_running(source) != 0;

        private void OnEnable()
        {
            if (!IsSupported)
            {
                Debug.LogWarning("[HoloTrack] DLL built without DepthAI — OAK source is inactive. " +
                                 "Rebuild HoloTrackSDK with HOLOTRACK_WITH_DEPTHAI.", this);
                return;
            }

            HtOakOptions opts = new HtOakOptions
            {
                detectionMode = (int)detectionMode,
                blobPath = ResolveBlobPath(blobPath),
                faceBlobPath = ResolveBlobPath(faceBlobPath),
                faceFallbackFrames = faceFallbackFrames,
                personLabel = personLabel,
                confidenceThreshold = confidenceThreshold,
                depthLowerThresholdMm = depthLowerThresholdMm,
                depthUpperThresholdMm = depthUpperThresholdMm
            };
            source = HoloTrackNative.ht_oak_create(ref opts);
            if (source == IntPtr.Zero)
            {
                Debug.LogError("[HoloTrack] ht_oak_create failed.", this);
                return;
            }

            started = HoloTrackNative.ht_oak_start(source) == HtStatus.Success;
            if (!started)
            {
                Debug.LogError($"[HoloTrack] OAK start failed: {HoloTrackNative.GetOakLastError(source)}", this);
            }
        }

        /// <inheritdoc />
        public bool TryPoll(HtDetection[] buffer, out int count, out double timestampSeconds)
        {
            count = 0;
            timestampSeconds = Time.timeAsDouble;
            if (!started || source == IntPtr.Zero || buffer == null || buffer.Length == 0)
            {
                return false;
            }

            HtStatus status = HoloTrackNative.ht_oak_poll(source, buffer, (UIntPtr)buffer.Length,
                                                          out UIntPtr nativeCount, out int hasFrame,
                                                          out double _);
            if (status != HtStatus.Success || hasFrame == 0)
            {
                return false;
            }
            count = (int)nativeCount;

            if (mirrorX)
            {
                // Camera-facing-viewer selfie mirror: negate X on the spatial position and every
                // pose keypoint so the tracked head moves the same direction the viewer does.
                for (int i = 0; i < count && i < buffer.Length; i++)
                {
                    buffer[i].spatial.x = -buffer[i].spatial.x;
                    buffer[i].nose.x = -buffer[i].nose.x;
                    buffer[i].leftEye.x = -buffer[i].leftEye.x;
                    buffer[i].rightEye.x = -buffer[i].rightEye.x;
                    buffer[i].neck.x = -buffer[i].neck.x;
                }
            }
            return true; // a new frame (possibly zero detections) — let the tracker advance
        }

        private void OnDisable()
        {
            if (source != IntPtr.Zero)
            {
                HoloTrackNative.ht_oak_stop(source);
                HoloTrackNative.ht_oak_destroy(source);
                source = IntPtr.Zero;
            }
            started = false;
        }

        private static string ResolveBlobPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }
            if (System.IO.Path.IsPathRooted(path))
            {
                return path;
            }
            return System.IO.Path.Combine(Application.streamingAssetsPath, path);
        }
    }
}
