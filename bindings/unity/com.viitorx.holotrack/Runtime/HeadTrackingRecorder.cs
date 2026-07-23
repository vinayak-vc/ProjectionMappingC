using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace vxholotrack
{
    /// <summary>
    /// Records the tracked head pose to a CSV file (spec §12): timestamp, head position, state,
    /// confidence, latency. Start/stop from the inspector context menu or by script. Rows are
    /// buffered and flushed on stop.
    /// </summary>
    [AddComponentMenu("ViitorX/HoloTrack/Recorder")]
    public sealed class HeadTrackingRecorder : MonoBehaviour
    {
        [SerializeField] private PMHTHeadTracker tracker;

        [Tooltip("Output file name written under Application.persistentDataPath.")]
        [SerializeField] private string fileName = "holotrack_recording.csv";

        [Tooltip("Begin recording automatically on Start.")]
        [SerializeField] private bool recordOnStart = false;

        private readonly StringBuilder builder = new StringBuilder();
        private bool recording;

        /// <summary>True while rows are being captured.</summary>
        public bool IsRecording => recording;

        /// <summary>Absolute path of the CSV that will be written.</summary>
        public string OutputPath => Path.Combine(Application.persistentDataPath, fileName);

        private void Start()
        {
            if (recordOnStart)
            {
                StartRecording();
            }
        }

        /// <summary>Begin a new recording (discards any un-saved buffer).</summary>
        [ContextMenu("Start Recording")]
        public void StartRecording()
        {
            builder.Clear();
            builder.AppendLine("timestamp,headX,headY,headZ,state,confidence,latencyMs,trackingId");
            recording = true;
        }

        /// <summary>Stop recording and flush the buffered rows to <see cref="OutputPath"/>.</summary>
        [ContextMenu("Stop Recording")]
        public void StopRecording()
        {
            if (!recording)
            {
                return;
            }
            recording = false;
            File.WriteAllText(OutputPath, builder.ToString());
            Debug.Log($"[HoloTrack] recording saved: {OutputPath}", this);
        }

        private void LateUpdate()
        {
            if (!recording || tracker == null || !tracker.HasViewer)
            {
                return;
            }

            Vector3 p = tracker.HeadPositionWorld;
            float latencyMs = Mathf.Max(0f, (float)(Time.timeAsDouble - tracker.LastTimestamp) * 1000f);
            CultureInfo ci = CultureInfo.InvariantCulture;
            builder.Append(tracker.LastTimestamp.ToString("F4", ci)).Append(',')
                   .Append(p.x.ToString("F4", ci)).Append(',')
                   .Append(p.y.ToString("F4", ci)).Append(',')
                   .Append(p.z.ToString("F4", ci)).Append(',')
                   .Append(tracker.State.ToString()).Append(',')
                   .Append(tracker.Confidence.ToString("F3", ci)).Append(',')
                   .Append(latencyMs.ToString("F2", ci)).Append(',')
                   .Append(tracker.ViewerId.ToString(ci))
                   .Append('\n');
        }

        private void OnDisable()
        {
            if (recording)
            {
                StopRecording();
            }
        }
    }
}
