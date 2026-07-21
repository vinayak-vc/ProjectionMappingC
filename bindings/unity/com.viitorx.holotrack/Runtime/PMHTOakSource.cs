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
        [Tooltip("Path to the spatial-detection network blob (absolute, or under StreamingAssets).")]
        [SerializeField] private string blobPath = string.Empty;

        [Tooltip("Detector class id treated as 'person' (MobileNet-SSD VOC: 15).")]
        [SerializeField] private int personLabel = 15;

        [Range(0f, 1f)]
        [SerializeField] private float confidenceThreshold = 0.5f;

        [SerializeField] private float depthLowerThresholdMm = 300f;
        [SerializeField] private float depthUpperThresholdMm = 8000f;

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

            string resolvedBlob = ResolveBlobPath(blobPath);
            HtOakOptions opts = new HtOakOptions
            {
                blobPath = resolvedBlob,
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
