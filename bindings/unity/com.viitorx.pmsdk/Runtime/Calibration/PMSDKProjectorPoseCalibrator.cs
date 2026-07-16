using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// On-site projector-pose calibration for OBJECT MAPPING. Workflow:
    ///   1. Place anchor transforms on recognizable features of the virtual twin
    ///      (corners, edges) — assign them to <see cref="ModelAnchors"/>.
    ///   2. Enter calibration (F3). For each anchor a marker appears in the projector
    ///      output; move it (arrows / mouse drag) onto where that feature sits on the
    ///      REAL object, then Enter to capture and advance.
    ///   3. After >= 4 captures, Space solves the projector camera's pose + FOV so the
    ///      twin overlays the real object (PMSDKPoseSolver), and applies it.
    ///
    /// The solve is pure Unity-camera reprojection (no native / OpenCV conversion).
    /// Correspondences persist in the calibration JSON alongside warp/blend.
    /// </summary>
    public class PMSDKProjectorPoseCalibrator : MonoBehaviour
    {
        [Tooltip("Projector camera to calibrate. Defaults to this GameObject's Camera.")]
        public Camera ProjectorCamera;
        [Tooltip("3D feature points on the virtual twin, in world space.")]
        public List<Transform> ModelAnchors = new List<Transform>();
        [Tooltip("Also solve field of view (recommended unless the projector lens is known exactly).")]
        public bool SolveFieldOfView = true;
        [Tooltip("Marker move speed (viewport units/sec) when nudging with arrows.")]
        public float MarkerSpeed = 0.25f;

        public bool Active { get; private set; }
        public int CurrentAnchor { get; private set; }
        public float LastRmsPixels { get; private set; } = -1f;
        public string Status { get; private set; } = "";

        private readonly List<PMSDKPoseSolver.Correspondence> captured = new List<PMSDKPoseSolver.Correspondence>();
        private Vector2 marker = new Vector2(0.5f, 0.5f);

        private void Awake()
        {
            if (ProjectorCamera == null) ProjectorCamera = GetComponent<Camera>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) { if (Active) Exit(); else Enter(); }
            if (!Active) return;

            if (Input.GetKeyDown(KeyCode.Escape)) { Exit(); return; }

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float step = MarkerSpeed * Time.unscaledDeltaTime * (shift ? 4f : 1f);
            float dx = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float dy = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            marker.x = Mathf.Clamp01(marker.x + dx * step);
            marker.y = Mathf.Clamp01(marker.y + dy * step);

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) CaptureCurrent();
            if (Input.GetKeyDown(KeyCode.Backspace)) UndoCapture();
            if (Input.GetKeyDown(KeyCode.Space)) SolveAndApply();

            RefreshStatus();
        }

        public void Enter()
        {
            if (ProjectorCamera == null) ProjectorCamera = GetComponent<Camera>();
            captured.Clear();
            CurrentAnchor = 0;
            marker = new Vector2(0.5f, 0.5f);
            Active = true;
            RefreshStatus();
        }

        public void Exit() { Active = false; }

        /// <summary>Record the current anchor's 3D point against the current marker position.</summary>
        public void CaptureCurrent()
        {
            if (CurrentAnchor >= ModelAnchors.Count) return;
            var t = ModelAnchors[CurrentAnchor];
            if (t == null) { CurrentAnchor++; return; }
            captured.Add(new PMSDKPoseSolver.Correspondence { World = t.position, Viewport = marker });
            CurrentAnchor++;
        }

        public void UndoCapture()
        {
            if (captured.Count == 0) return;
            captured.RemoveAt(captured.Count - 1);
            CurrentAnchor = Mathf.Max(0, CurrentAnchor - 1);
        }

        /// <summary>Add a correspondence directly (for scripted / tested use).</summary>
        public void AddCorrespondence(Vector3 world, Vector2 viewport)
        {
            captured.Add(new PMSDKPoseSolver.Correspondence { World = world, Viewport = viewport });
        }

        public void Clear() { captured.Clear(); CurrentAnchor = 0; }

        public float SolveAndApply()
        {
            if (ProjectorCamera == null || captured.Count < 4)
            {
                Status = $"Need >= 4 captures (have {captured.Count}).";
                return -1f;
            }
            float rms = PMSDKPoseSolver.Solve(ProjectorCamera, captured, 80, SolveFieldOfView);
            // rms is in viewport units; report in output pixels.
            LastRmsPixels = rms * ProjectorCamera.pixelHeight;
            Status = $"Solved: {captured.Count} pts, reproj {LastRmsPixels:F1}px. Fine-tune with warp if needed.";
            Debug.Log("[PMSDK] Projector pose " + Status);
            return LastRmsPixels;
        }

        public IReadOnlyList<PMSDKPoseSolver.Correspondence> Captured => captured;
        public Vector2 Marker => marker;
        public void SetMarker(Vector2 vp) { marker = new Vector2(Mathf.Clamp01(vp.x), Mathf.Clamp01(vp.y)); }

        private void RefreshStatus()
        {
            if (CurrentAnchor < ModelAnchors.Count)
                Status = $"Mark anchor {CurrentAnchor + 1}/{ModelAnchors.Count} — move onto the feature, Enter to capture. ({captured.Count} done, Space=solve)";
            else
                Status = $"All {ModelAnchors.Count} anchors captured. Space to solve.";
        }
    }
}
