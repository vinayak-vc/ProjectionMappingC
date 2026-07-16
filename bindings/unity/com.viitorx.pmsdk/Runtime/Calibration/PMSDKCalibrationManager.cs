using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Runtime on-site calibration controller (design: docs/calibration-ux-design.md).
    ///
    /// Everything works in a standalone build with only a keyboard — no Inspector:
    /// - Boot: loads pmsdk_calibration.json and applies it; if the file is missing
    ///   (first run) or "-calibrate" was passed, enters calibration mode automatically.
    /// - F2 toggles calibration mode any time. Esc exits. Exit auto-saves.
    /// - PgUp/PgDn select projector surface, Tab cycles corner, arrows nudge
    ///   (Shift = coarse x5, Ctrl = fine x0.1), T test pattern (Shift+T = all),
    ///   B blend submode (Tab edge, arrows width/gamma, N black level),
    ///   R reset corner, Ctrl+R reset surface, Ctrl+S save, Ctrl+Z undo, F1 help.
    /// - Mouse (P2): drag a corner handle directly (Alt = fine); click a projector
    ///   thumbnail on the operator console to select that surface. Uses
    ///   Display.RelativeMouseAt in multi-display players, falls back to the raw
    ///   mouse position in the Editor (drag on whichever Game view has focus).
    ///
    /// Core actions are public methods so they can be driven programmatically
    /// (tests, MCP, future StreamDeck/OSC remote) — the keyboard layer just calls them.
    /// </summary>
    public class PMSDKCalibrationManager : MonoBehaviour
    {
        public class Surface
        {
            public PMSDKMeshWarp Warp;
            public PMSDKCornerPin CornerPin;
            public PMSDKEdgeBlend Blend;
            public PMSDKTestPattern TestPattern;
            public PMSDKGridWarp Grid;
            public Camera ProjectorCamera;
            public PMSDKCalibrationOverlay Overlay;
            public RenderTexture Thumbnail;
            // Target rectangle in the observing camera's view (UI-normalized,
            // bottom-left origin, order TL,TR,BR,BL) where this projector should
            // land. null = auto-align uses the full observed projection.
            public Vector2[] TargetCorners;
            public bool HasTarget;
            public string Id => Warp != null ? Warp.gameObject.name : "<missing>";
        }

        [Header("Behaviour")]
        [Tooltip("Enter calibration mode automatically when no calibration file exists (first run).")]
        public bool AutoEnterWhenUncalibrated = true;
        [Tooltip("Optional explicit calibration file path. Empty = persistentDataPath (or -calibfile CLI arg).")]
        public string FileOverridePath = "";

        [Header("Tuning")]
        [Tooltip("Corner nudge per second of held arrow key, in normalized raster units.")]
        public float NudgePerSecond = 0.12f;
        public float CoarseMultiplier = 5f;
        public float FineMultiplier = 0.1f;
        [Tooltip("Grab radius around a handle for mouse dragging, in viewport (0..1) units.")]
        public float HandleGrabRadius = 0.04f;
        [Tooltip("Loupe magnification: fraction of the projector view shown in the loupe.")]
        public float LoupeZoom = 0.15f;

        [Header("Auto-Align (P3)")]
        [Tooltip("Use a physical webcam (via the native OpenCV decoder) as the auto-align observer. When off, the simulated ObserverCamera below is used.")]
        public bool UseNativeWebcam;
        [Tooltip("OS index of the webcam when UseNativeWebcam is on.")]
        public int WebcamIndex;
        [Tooltip("Frames grabbed and discarded before each webcam capture (VideoCapture buffers frames; without flushing, captures show the previous pattern).")]
        public int WebcamFlushFrames = 2;
        [Tooltip("After aligning ALL projectors (Shift+A), auto-compute edge-blend widths from the camera-detected overlap between them.")]
        public bool AutoBlendAfterAlignAll = true;
        [Tooltip("Observer camera for simulated auto-align. If null, a scene object named 'PMSDK Calibration Observer' with a Camera is used.")]
        public Camera ObserverCamera;
        [Tooltip("Capture resolution for the simulated observer camera.")]
        public int ObserverWidth = 640;
        public int ObserverHeight = 480;

        public bool CalibrationMode { get; private set; }
        public bool BlendSubmode { get; private set; }
        public bool GridEditMode { get; private set; }
        public int SelectedGridPoint { get; private set; }
        public bool TargetMarkMode { get; private set; }
        public int SelectedTargetCorner { get; private set; }
        public Texture2D TargetPreview { get; private set; }
        public bool HelpVisible { get; private set; }
        public bool Dirty { get; private set; }
        public bool IsDragging => dragging;
        public int SelectedSurfaceIndex { get; private set; }
        /// <summary>0=TL 1=TR 2=BR 3=BL (Tab cycle order).</summary>
        public int SelectedCorner { get; private set; }
        /// <summary>0=Left 1=Right 2=Top 3=Bottom.</summary>
        public int SelectedEdge { get; private set; }
        public int UndoDepth => undoStack.Count;
        public string FilePath { get; private set; }
        public RenderTexture LoupeRT { get; private set; }
        public bool IsAutoAligning { get; private set; }
        public string AutoAlignStatus { get; private set; }
        public IReadOnlyList<Surface> Surfaces => surfaces;
        public Surface Selected => surfaces.Count > 0 ? surfaces[Mathf.Clamp(SelectedSurfaceIndex, 0, surfaces.Count - 1)] : null;

        public static readonly string[] CornerNames = { "TL", "TR", "BR", "BL" };
        public static readonly string[] EdgeNames = { "LEFT", "RIGHT", "TOP", "BOTTOM" };

        private readonly List<Surface> surfaces = new List<Surface>();
        private PMSDKCalibrationHUD hud;
        private PMSDKAutoAlign autoAlign;
        private IPMSDKCalibrationCamera previewCam;

        // Undo (P2): coalesced snapshots of the selected surface.
        private struct UndoEntry
        {
            public int SurfaceIndex;
            public Vector2 Tl, Tr, Bl, Br;
            public float BlendL, BlendR, BlendT, BlendB, Gamma, Black;
        }
        private readonly List<UndoEntry> undoStack = new List<UndoEntry>();
        private const int MaxUndoDepth = 100;
        private const float UndoCoalesceSeconds = 0.7f;
        private float lastUndoPushTime = -999f;
        private int lastUndoPushSurface = -1;

        // Mouse drag (P2)
        private bool dragging;
        private bool hadFinePosition;
        private Vector2 lastFineViewport;

        // Loupe + thumbnails (P2)
        private Camera serviceCamera; // reused for loupe and thumbnail rendering
        private float nextThumbnailTime;
        private const float ThumbnailInterval = 0.33f;
        private const int ThumbnailWidth = 320;
        private const int ThumbnailHeight = 180;

        private void Start()
        {
            FilePath = PMSDKCalibrationIO.ResolvePath(FileOverridePath);
            DiscoverSurfaces();

            // This manager supersedes the old self-contained corner-pin UI.
            foreach (var legacy in FindObjectsByType<PMSDKCornerPinUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                legacy.enabled = false;
            }

            var file = PMSDKCalibrationIO.Load(FilePath);
            if (file != null)
            {
                ApplyFile(file);
                Debug.Log($"[PMSDK] Calibration loaded from '{FilePath}' ({file.surfaces.Length} surfaces, saved {file.savedAtUtc}).");
            }

            bool shouldCalibrate = PMSDKCalibrationIO.ForceCalibrateRequested()
                                   || (AutoEnterWhenUncalibrated && file == null);
            if (shouldCalibrate && surfaces.Count > 0)
            {
                EnterCalibration();
            }
        }

        private void OnDestroy()
        {
            if (LoupeRT != null) LoupeRT.Release();
            foreach (var s in surfaces)
            {
                if (s.Thumbnail != null) s.Thumbnail.Release();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                if (CalibrationMode) ExitCalibration();
                else EnterCalibration();
            }

            if (!CalibrationMode)
            {
                return;
            }

            // While an auto-align sweep is displaying patterns, ignore all other input
            // so a stray key can't corrupt the measurement.
            if (IsAutoAligning)
            {
                return;
            }

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            // --- Target-mark submode: pick where the projection should land in the
            // camera view, then A aligns to it. Owns Esc/A while active. ---
            if (TargetMarkMode)
            {
                UpdateTargetPreview();
                if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape)) { ExitTargetMarkMode(); return; }
                if (Input.GetKeyDown(KeyCode.F1)) HelpVisible = !HelpVisible;
                if (Input.GetKeyDown(KeyCode.Tab)) SelectedTargetCorner = (SelectedTargetCorner + (shift ? 3 : 1)) % 4;
                if (Input.GetKeyDown(KeyCode.R)) ResetTarget();
                if (Input.GetKeyDown(KeyCode.A)) { RunAlignWithMarkedTarget(); return; }

                float tgtSpeed = NudgePerSecond * Time.unscaledDeltaTime;
                if (shift) tgtSpeed *= CoarseMultiplier;
                if (ctrl) tgtSpeed *= FineMultiplier;
                float tdx = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
                float tdy = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
                if (tdx != 0f || tdy != 0f) NudgeTargetCorner(new Vector2(tdx, tdy) * tgtSpeed);
                return; // target-mark mode consumes input
            }

            if (Input.GetKeyDown(KeyCode.Escape)) { ExitCalibration(); return; }
            if (Input.GetKeyDown(KeyCode.F1)) HelpVisible = !HelpVisible;

            if (Input.GetKeyDown(KeyCode.M)) { EnterTargetMarkMode(); return; }
            if (Input.GetKeyDown(KeyCode.A)) { StartAutoAlign(all: shift); return; }

            if (ctrl && Input.GetKeyDown(KeyCode.S)) { SaveNow(); }
            else if (ctrl && Input.GetKeyDown(KeyCode.Z)) { UndoLast(); }
            else if (ctrl && Input.GetKeyDown(KeyCode.R)) { if (GridEditMode) ResetGrid(); else ResetSelectedSurface(); }
            else if (Input.GetKeyDown(KeyCode.R)) { if (!GridEditMode) ResetSelectedCorner(); }

            if (Input.GetKeyDown(KeyCode.PageUp)) SelectSurface(-1);
            if (Input.GetKeyDown(KeyCode.PageDown)) SelectSurface(+1);
            if (Input.GetKeyDown(KeyCode.Tab)) CycleElement(shift ? -1 : +1);
            if (Input.GetKeyDown(KeyCode.B) && !GridEditMode) BlendSubmode = !BlendSubmode;
            if (Input.GetKeyDown(KeyCode.G)) ToggleGridMode();
            if (Input.GetKeyDown(KeyCode.T)) ToggleTestPattern(all: shift);
            if (Input.GetKeyDown(KeyCode.N) && !GridEditMode) AdjustBlackLevel(shift ? -0.01f : +0.01f);

            // Grid subdivision: [ ] change columns, - = change rows.
            if (GridEditMode)
            {
                if (Input.GetKeyDown(KeyCode.LeftBracket)) ResizeGrid(-1, 0);
                if (Input.GetKeyDown(KeyCode.RightBracket)) ResizeGrid(+1, 0);
                if (Input.GetKeyDown(KeyCode.Minus)) ResizeGrid(0, -1);
                if (Input.GetKeyDown(KeyCode.Equals)) ResizeGrid(0, +1);
            }

            float speed = NudgePerSecond * Time.unscaledDeltaTime;
            if (shift) speed *= CoarseMultiplier;
            if (ctrl) speed *= FineMultiplier;

            float dx = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float dy = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            if (dx != 0f || dy != 0f)
            {
                if (GridEditMode) NudgeSelectedGridPoint(new Vector2(dx, dy) * speed);
                else if (BlendSubmode) AdjustBlend(dx * speed, dy * speed);
                else NudgeSelectedCorner(new Vector2(dx, dy) * speed);
            }

            HandleMouse(alt);
            UpdateLoupe();
            UpdateThumbnails();
        }

        // ---------------- Core actions (keyboard-independent, publicly drivable) ----

        public void EnterCalibration()
        {
            DiscoverSurfaces();
            if (surfaces.Count == 0)
            {
                Debug.LogWarning("[PMSDK] No PMSDKMeshWarp surfaces found — nothing to calibrate.");
                return;
            }
            CalibrationMode = true;
            SelectedSurfaceIndex = Mathf.Clamp(SelectedSurfaceIndex, 0, surfaces.Count - 1);
            EnsureEventSystem();
            EnsureHud();
            SetOverlaysVisible(true);
        }

        public void ExitCalibration()
        {
            if (TargetMarkMode) ExitTargetMarkMode();
            if (Dirty)
            {
                SaveNow();
            }
            CalibrationMode = false;
            BlendSubmode = false;
            HelpVisible = false;
            dragging = false;
            SetOverlaysVisible(false);
        }

        public void SelectSurface(int delta)
        {
            if (surfaces.Count == 0) return;
            SelectedSurfaceIndex = (SelectedSurfaceIndex + delta + surfaces.Count) % surfaces.Count;
        }

        public void SetSelectedSurface(int index)
        {
            if (surfaces.Count == 0) return;
            SelectedSurfaceIndex = Mathf.Clamp(index, 0, surfaces.Count - 1);
        }

        /// <summary>Tab: cycles the active element — grid point, blend edge, or corner.</summary>
        public void CycleElement(int delta)
        {
            if (GridEditMode)
            {
                var g = Selected?.Grid;
                if (g != null && g.PointCount > 0)
                    SelectedGridPoint = (SelectedGridPoint + delta + g.PointCount) % g.PointCount;
            }
            else if (BlendSubmode) SelectedEdge = (SelectedEdge + delta + 4) % 4;
            else SelectedCorner = (SelectedCorner + delta + 4) % 4;
        }

        // ---------------- Grid warp (N x M) ---------------------------------------

        /// <summary>Toggle N x M grid-warp editing on the selected surface.</summary>
        public void ToggleGridMode()
        {
            var s = Selected;
            if (s == null || s.Warp == null) return;

            if (!GridEditMode)
            {
                if (s.Grid == null)
                {
                    s.Grid = s.Warp.GetComponent<PMSDKGridWarp>();
                    if (s.Grid == null) s.Grid = s.Warp.gameObject.AddComponent<PMSDKGridWarp>();
                }
                s.Grid.enabled = true; // takes over from the corner pin
                BlendSubmode = false;
                GridEditMode = true;
                SelectedGridPoint = Mathf.Clamp(SelectedGridPoint, 0, s.Grid.PointCount - 1);
            }
            else
            {
                // Leaving grid mode hands the surface back to the corner pin.
                if (s.Grid != null) s.Grid.enabled = false;
                GridEditMode = false;
            }
            Dirty = true;
        }

        public void NudgeSelectedGridPoint(Vector2 delta)
        {
            var g = Selected?.Grid;
            if (g == null || SelectedGridPoint >= g.PointCount) return;
            PushUndoIfNeeded();
            g.SetPointByIndex(SelectedGridPoint, g.GetPointByIndex(SelectedGridPoint) + delta);
            Dirty = true;
        }

        public void ResetGrid()
        {
            var g = Selected?.Grid;
            if (g == null) return;
            PushUndoIfNeeded();
            g.ResetGrid();
            Dirty = true;
        }

        public void ResizeGrid(int dCols, int dRows)
        {
            var g = Selected?.Grid;
            if (g == null) return;
            g.Resize(g.Columns + dCols, g.Rows + dRows);
            SelectedGridPoint = Mathf.Clamp(SelectedGridPoint, 0, g.PointCount - 1);
            Dirty = true;
        }

        // ---------------- Target-rectangle marking --------------------------------

        /// <summary>
        /// Enter target-mark mode for the selected surface: opens a live camera
        /// preview on the operator console with four draggable corners the operator
        /// places on the physical screen edges. A then aligns the projection to that
        /// rectangle instead of the full observed area.
        /// </summary>
        public void EnterTargetMarkMode()
        {
            var s = Selected;
            if (s == null || s.Warp == null) return;

            previewCam = UseNativeWebcam
                ? new PMSDKNativeWebcamCamera(WebcamIndex, WebcamFlushFrames)
                : (IPMSDKCalibrationCamera)MakeSimObserver();
            if (previewCam == null || !previewCam.Begin())
            {
                previewCam = null;
                AutoAlignStatus = "Target marking needs a camera (enable UseNativeWebcam or assign an observer camera).";
                Debug.LogWarning("[PMSDK] " + AutoAlignStatus);
                return;
            }

            EnsureTarget(s);
            TargetMarkMode = true;
            GridEditMode = false;
            BlendSubmode = false;
            SelectedTargetCorner = 0;
        }

        public void ExitTargetMarkMode()
        {
            StopPreview();
            TargetMarkMode = false;
        }

        public void SelectTargetCorner(int i) => SelectedTargetCorner = Mathf.Clamp(i, 0, 3);

        /// <summary>Set a target corner (UI-normalized, bottom-left origin).</summary>
        public void SetTargetCorner(int i, Vector2 uiNormalized)
        {
            var s = Selected;
            if (s == null) return;
            EnsureTarget(s);
            uiNormalized.x = Mathf.Clamp01(uiNormalized.x);
            uiNormalized.y = Mathf.Clamp01(uiNormalized.y);
            s.TargetCorners[Mathf.Clamp(i, 0, 3)] = uiNormalized;
            s.HasTarget = true;
            Dirty = true;
        }

        public Vector2 GetTargetCorner(int i)
        {
            var s = Selected;
            EnsureTarget(s);
            return s.TargetCorners[Mathf.Clamp(i, 0, 3)];
        }

        public void NudgeTargetCorner(Vector2 delta)
        {
            SetTargetCorner(SelectedTargetCorner, GetTargetCorner(SelectedTargetCorner) + delta);
        }

        public void ResetTarget()
        {
            var s = Selected;
            if (s == null) return;
            s.TargetCorners = FullFrameTarget();
            s.HasTarget = false;
            Dirty = true;
        }

        /// <summary>Run auto-align on the selected surface using the marked target rectangle.</summary>
        private void RunAlignWithMarkedTarget()
        {
            var s = Selected;
            if (s == null) return;
            EnsureTarget(s);
            // Convert UI-normalized (bottom-left) TL,TR,BR,BL to the camera-normalized
            // (top-left origin) space AlignSelectedWithTarget expects.
            var t = s.TargetCorners;
            var cameraTarget = new[]
            {
                new Vector2(t[0].x, 1f - t[0].y), // TL
                new Vector2(t[1].x, 1f - t[1].y), // TR
                new Vector2(t[2].x, 1f - t[2].y), // BR
                new Vector2(t[3].x, 1f - t[3].y)  // BL
            };
            StopPreview();
            TargetMarkMode = false;
            AlignSelectedWithTarget(cameraTarget);
        }

        private void EnsureTarget(Surface s)
        {
            if (s != null && (s.TargetCorners == null || s.TargetCorners.Length != 4))
            {
                s.TargetCorners = FullFrameTarget();
            }
        }

        // Full-frame target in UI-normalized bottom-left order TL,TR,BR,BL.
        private static Vector2[] FullFrameTarget()
        {
            return new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
        }

        private PMSDKSimulatedCamera MakeSimObserver()
        {
            var observer = ResolveObserverCamera();
            if (observer == null) return null;
            return new PMSDKSimulatedCamera(observer, Mathf.Max(64, ObserverWidth), Mathf.Max(64, ObserverHeight));
        }

        private void UpdateTargetPreview()
        {
            if (previewCam == null) return;
            byte[] lum = previewCam.CaptureLuminance();
            int w = previewCam.Width, h = previewCam.Height;
            if (lum == null || lum.Length != w * h) return;
            if (TargetPreview == null || TargetPreview.width != w || TargetPreview.height != h)
            {
                TargetPreview = new Texture2D(w, h, TextureFormat.R8, false) { name = "PMSDK_TargetPreview" };
            }
            TargetPreview.SetPixelData(lum, 0);
            TargetPreview.Apply(false);
        }

        private void StopPreview()
        {
            if (previewCam != null)
            {
                previewCam.End();
                previewCam = null;
            }
        }

        public void NudgeSelectedCorner(Vector2 normalizedDelta)
        {
            var s = Selected;
            if (s == null || s.CornerPin == null) return;
            PushUndoIfNeeded();
            Vector2 v = GetCorner(s.CornerPin, SelectedCorner) + normalizedDelta;
            v.x = Mathf.Clamp01(v.x);
            v.y = Mathf.Clamp01(v.y);
            SetCorner(s.CornerPin, SelectedCorner, v);
            Dirty = true;
        }

        public void SetSelectedCornerPosition(Vector2 normalizedPosition)
        {
            var s = Selected;
            if (s == null || s.CornerPin == null) return;
            normalizedPosition.x = Mathf.Clamp01(normalizedPosition.x);
            normalizedPosition.y = Mathf.Clamp01(normalizedPosition.y);
            SetCorner(s.CornerPin, SelectedCorner, normalizedPosition);
            Dirty = true;
        }

        /// <summary>Blend submode arrows: horizontal = edge width, vertical = gamma.</summary>
        public void AdjustBlend(float widthDelta, float gammaDelta)
        {
            var s = Selected;
            if (s == null || s.Blend == null) return;
            PushUndoIfNeeded();
            if (widthDelta != 0f)
            {
                float w = Mathf.Clamp(GetEdge(s.Blend, SelectedEdge) + widthDelta, 0f, 0.45f);
                SetEdge(s.Blend, SelectedEdge, w);
            }
            if (gammaDelta != 0f)
            {
                s.Blend.Gamma = Mathf.Clamp(s.Blend.Gamma + gammaDelta * 5f, 0.5f, 4f);
            }
            Dirty = true;
        }

        public void AdjustBlackLevel(float delta)
        {
            var s = Selected;
            if (s == null || s.Blend == null) return;
            PushUndoIfNeeded();
            s.Blend.BlackLevel = Mathf.Clamp(s.Blend.BlackLevel + delta, 0f, 0.5f);
            Dirty = true;
        }

        public void ToggleTestPattern(bool all)
        {
            if (all)
            {
                bool anyOn = false;
                foreach (var s in surfaces) anyOn |= s.TestPattern != null && s.TestPattern.enabled;
                foreach (var s in surfaces) if (s.TestPattern != null) s.TestPattern.enabled = !anyOn;
            }
            else if (Selected?.TestPattern != null)
            {
                Selected.TestPattern.enabled = !Selected.TestPattern.enabled;
            }
        }

        public void ResetSelectedCorner()
        {
            var s = Selected;
            if (s == null || s.CornerPin == null) return;
            PushUndoIfNeeded();
            SetCorner(s.CornerPin, SelectedCorner, IdentityCorner(SelectedCorner));
            Dirty = true;
        }

        public void ResetSelectedSurface()
        {
            var s = Selected;
            if (s == null || s.CornerPin == null) return;
            PushUndoIfNeeded();
            for (int i = 0; i < 4; i++) SetCorner(s.CornerPin, i, IdentityCorner(i));
            Dirty = true;
        }

        public void UndoLast()
        {
            if (undoStack.Count == 0) return;
            var e = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);
            if (e.SurfaceIndex >= surfaces.Count) return;
            var s = surfaces[e.SurfaceIndex];
            if (s.CornerPin != null)
            {
                s.CornerPin.TopLeft = e.Tl;
                s.CornerPin.TopRight = e.Tr;
                s.CornerPin.BottomLeft = e.Bl;
                s.CornerPin.BottomRight = e.Br;
            }
            if (s.Blend != null)
            {
                s.Blend.LeftEdge = e.BlendL;
                s.Blend.RightEdge = e.BlendR;
                s.Blend.TopEdge = e.BlendT;
                s.Blend.BottomEdge = e.BlendB;
                s.Blend.Gamma = e.Gamma;
                s.Blend.BlackLevel = e.Black;
            }
            SelectedSurfaceIndex = e.SurfaceIndex;
            Dirty = true;
            // Force the next edit to snapshot fresh instead of coalescing into a
            // group that no longer exists.
            lastUndoPushTime = -999f;
        }

        public void SaveNow()
        {
            var file = new PMSDKCalibrationFile { surfaces = new PMSDKSurfaceCalibration[surfaces.Count] };
            for (int i = 0; i < surfaces.Count; i++)
            {
                var s = surfaces[i];
                file.surfaces[i] = new PMSDKSurfaceCalibration
                {
                    id = s.Id,
                    targetDisplay = s.ProjectorCamera != null ? s.ProjectorCamera.targetDisplay : 0,
                    tl = s.CornerPin.TopLeft,
                    tr = s.CornerPin.TopRight,
                    bl = s.CornerPin.BottomLeft,
                    br = s.CornerPin.BottomRight,
                    blendLeft = s.Blend != null ? s.Blend.LeftEdge : 0f,
                    blendRight = s.Blend != null ? s.Blend.RightEdge : 0f,
                    blendTop = s.Blend != null ? s.Blend.TopEdge : 0f,
                    blendBottom = s.Blend != null ? s.Blend.BottomEdge : 0f,
                    gamma = s.Blend != null ? s.Blend.Gamma : 2.2f,
                    blackLevel = s.Blend != null ? s.Blend.BlackLevel : 0f
                };

                if (s.Grid != null)
                {
                    var entry = file.surfaces[i];
                    entry.gridEnabled = s.Grid.enabled;
                    entry.gridColumns = s.Grid.Columns;
                    entry.gridRows = s.Grid.Rows;
                    entry.gridPoints = new Vector2[s.Grid.PointCount];
                    for (int p = 0; p < s.Grid.PointCount; p++) entry.gridPoints[p] = s.Grid.GetPointByIndex(p);
                }

                if (s.HasTarget && s.TargetCorners != null && s.TargetCorners.Length == 4)
                {
                    file.surfaces[i].hasTarget = true;
                    file.surfaces[i].targetCorners = (Vector2[])s.TargetCorners.Clone();
                }
            }
            if (PMSDKCalibrationIO.Save(FilePath, file))
            {
                Dirty = false;
                Debug.Log($"[PMSDK] Calibration saved to '{FilePath}'.");
            }
        }

        // ---------------- Mouse drag (P2) ------------------------------------------

        private void HandleMouse(bool fine)
        {
            if (BlendSubmode)
            {
                dragging = false;
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                TryBeginDrag();
            }
            else if (dragging && Input.GetMouseButton(0))
            {
                ContinueDrag(fine);
            }
            if (Input.GetMouseButtonUp(0))
            {
                dragging = false;
            }
        }

        private void TryBeginDrag()
        {
            if (GridEditMode)
            {
                // Pick the nearest grid control point on the selected surface.
                var s = Selected;
                if (s?.Grid == null || !TryGetPointerViewport(s, out Vector2 gvp)) return;
                float best = HandleGrabRadius;
                int bestPoint = -1;
                for (int i = 0; i < s.Grid.PointCount; i++)
                {
                    float d = Vector2.Distance(gvp, s.Grid.GetPointByIndex(i));
                    if (d < best) { best = d; bestPoint = i; }
                }
                if (bestPoint >= 0)
                {
                    SelectedGridPoint = bestPoint;
                    PushUndoIfNeeded();
                    dragging = true;
                    hadFinePosition = false;
                }
                return;
            }

            float bestDist = HandleGrabRadius;
            int bestSurface = -1, bestCorner = -1;
            for (int i = 0; i < surfaces.Count; i++)
            {
                if (!TryGetPointerViewport(surfaces[i], out Vector2 vp)) continue;
                for (int c = 0; c < 4; c++)
                {
                    float d = Vector2.Distance(vp, GetCorner(surfaces[i].CornerPin, c));
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestSurface = i;
                        bestCorner = c;
                    }
                }
            }
            if (bestSurface >= 0)
            {
                SelectedSurfaceIndex = bestSurface;
                SelectedCorner = bestCorner;
                PushUndoIfNeeded();
                dragging = true;
                hadFinePosition = false;
            }
        }

        private void ContinueDrag(bool fine)
        {
            var s = Selected;
            if (s == null || !TryGetPointerViewport(s, out Vector2 vp))
            {
                return;
            }

            if (GridEditMode)
            {
                if (s.Grid == null || SelectedGridPoint >= s.Grid.PointCount) return;
                if (fine)
                {
                    if (hadFinePosition)
                        s.Grid.SetPointByIndex(SelectedGridPoint, s.Grid.GetPointByIndex(SelectedGridPoint) + (vp - lastFineViewport) * 0.1f);
                    lastFineViewport = vp;
                    hadFinePosition = true;
                }
                else
                {
                    s.Grid.SetPointByIndex(SelectedGridPoint, vp);
                }
                Dirty = true;
                return;
            }

            if (fine)
            {
                // Fine drag: apply a tenth of the pointer's movement each frame.
                if (hadFinePosition)
                {
                    Vector2 current = GetCorner(s.CornerPin, SelectedCorner);
                    SetSelectedCornerPosition(current + (vp - lastFineViewport) * 0.1f);
                }
                lastFineViewport = vp;
                hadFinePosition = true;
            }
            else
            {
                SetSelectedCornerPosition(vp);
                hadFinePosition = false;
            }
        }

        /// <summary>
        /// Pointer position in a surface's projector-camera viewport space (0..1).
        /// Multi-display players: Display.RelativeMouseAt gives per-display coords;
        /// Editor (unsupported there): raw mouse position against every camera —
        /// the focused Game view is the one whose numbers make sense, and handle
        /// grabbing is distance-gated so wrong-view coordinates just miss.
        /// </summary>
        private bool TryGetPointerViewport(Surface s, out Vector2 viewport)
        {
            viewport = default;
            if (s.ProjectorCamera == null) return false;

            Vector3 mouse = Input.mousePosition;
            Vector3 rel = Display.RelativeMouseAt(mouse);
            if (rel != Vector3.zero)
            {
                if ((int)rel.z != s.ProjectorCamera.targetDisplay) return false;
                mouse = new Vector3(rel.x, rel.y, 0f);
            }
            Vector3 vp = s.ProjectorCamera.ScreenToViewportPoint(mouse);
            viewport = new Vector2(vp.x, vp.y);
            return viewport.x >= -0.1f && viewport.x <= 1.1f && viewport.y >= -0.1f && viewport.y <= 1.1f;
        }

        // ---------------- Loupe + thumbnails (P2) ----------------------------------

        private void UpdateLoupe()
        {
            var s = Selected;
            if (s == null || s.ProjectorCamera == null || BlendSubmode) return;

            if (LoupeRT == null)
            {
                LoupeRT = new RenderTexture(256, 256, 16) { name = "PMSDK_LoupeRT" };
            }
            EnsureServiceCamera();

            // Center on the element currently being edited.
            Vector2 focus = (GridEditMode && s.Grid != null && SelectedGridPoint < s.Grid.PointCount)
                ? s.Grid.GetPointByIndex(SelectedGridPoint)
                : GetCorner(s.CornerPin, SelectedCorner);
            Vector3 worldCorner = s.Warp.transform.TransformPoint(new Vector3(focus.x, focus.y, 0f));

            var cam = serviceCamera;
            var proj = s.ProjectorCamera;
            cam.orthographicSize = proj.orthographicSize * LoupeZoom;
            cam.cullingMask = proj.cullingMask;
            cam.transform.position = worldCorner - s.Warp.transform.forward * 5f;
            cam.transform.rotation = proj.transform.rotation;
            cam.targetTexture = LoupeRT;
            cam.aspect = 1f;
            cam.Render();
            cam.targetTexture = null;
        }

        private void UpdateThumbnails()
        {
            if (Time.unscaledTime < nextThumbnailTime) return;
            nextThumbnailTime = Time.unscaledTime + ThumbnailInterval;
            EnsureServiceCamera();

            foreach (var s in surfaces)
            {
                if (s.ProjectorCamera == null) continue;
                if (s.Thumbnail == null)
                {
                    s.Thumbnail = new RenderTexture(ThumbnailWidth, ThumbnailHeight, 16) { name = "PMSDK_Thumb_" + s.Id };
                }
                var cam = serviceCamera;
                var proj = s.ProjectorCamera;
                cam.orthographicSize = proj.orthographicSize;
                cam.cullingMask = proj.cullingMask;
                cam.transform.position = proj.transform.position;
                cam.transform.rotation = proj.transform.rotation;
                cam.targetTexture = s.Thumbnail;
                cam.aspect = (float)ThumbnailWidth / ThumbnailHeight;
                cam.Render();
                cam.targetTexture = null;
            }
        }

        private void EnsureServiceCamera()
        {
            if (serviceCamera != null) return;
            var go = new GameObject("PMSDK Calibration Service Camera");
            go.transform.SetParent(transform, false);
            serviceCamera = go.AddComponent<Camera>();
            serviceCamera.orthographic = true;
            serviceCamera.nearClipPlane = 0.1f;
            serviceCamera.farClipPlane = 50f;
            serviceCamera.clearFlags = CameraClearFlags.SolidColor;
            serviceCamera.backgroundColor = Color.black;
            serviceCamera.enabled = false; // manual Render() only
        }

        // ---------------- Internals ------------------------------------------------

        private void PushUndoIfNeeded()
        {
            var s = Selected;
            if (s == null || s.CornerPin == null) return;
            bool coalesce = SelectedSurfaceIndex == lastUndoPushSurface
                            && Time.unscaledTime - lastUndoPushTime < UndoCoalesceSeconds;
            lastUndoPushTime = Time.unscaledTime;
            if (coalesce) return;

            lastUndoPushSurface = SelectedSurfaceIndex;
            var e = new UndoEntry
            {
                SurfaceIndex = SelectedSurfaceIndex,
                Tl = s.CornerPin.TopLeft,
                Tr = s.CornerPin.TopRight,
                Bl = s.CornerPin.BottomLeft,
                Br = s.CornerPin.BottomRight,
                BlendL = s.Blend != null ? s.Blend.LeftEdge : 0f,
                BlendR = s.Blend != null ? s.Blend.RightEdge : 0f,
                BlendT = s.Blend != null ? s.Blend.TopEdge : 0f,
                BlendB = s.Blend != null ? s.Blend.BottomEdge : 0f,
                Gamma = s.Blend != null ? s.Blend.Gamma : 2.2f,
                Black = s.Blend != null ? s.Blend.BlackLevel : 0f
            };
            undoStack.Add(e);
            if (undoStack.Count > MaxUndoDepth)
            {
                undoStack.RemoveAt(0);
            }
        }

        private void DiscoverSurfaces()
        {
            surfaces.Clear();
            var warps = FindObjectsByType<PMSDKMeshWarp>(FindObjectsSortMode.None);
            System.Array.Sort(warps, (a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));
            foreach (var warp in warps)
            {
                var s = new Surface
                {
                    Warp = warp,
                    CornerPin = warp.GetComponent<PMSDKCornerPin>(),
                    Blend = warp.GetComponent<PMSDKEdgeBlend>(),
                    TestPattern = warp.GetComponent<PMSDKTestPattern>(),
                    Grid = warp.GetComponent<PMSDKGridWarp>(),
                    ProjectorCamera = warp.Projector != null ? warp.Projector.GetComponent<Camera>() : null
                };
                if (s.CornerPin == null)
                {
                    Debug.LogWarning($"[PMSDK] Surface '{warp.gameObject.name}' has no PMSDKCornerPin — skipped for calibration.");
                    continue;
                }
                s.Overlay = PMSDKCalibrationOverlay.GetOrCreate(this, s);
                surfaces.Add(s);
            }
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<StandaloneInputModule>();
            }
        }

        private void EnsureHud()
        {
            if (hud == null)
            {
                hud = PMSDKCalibrationHUD.Create(this);
            }
        }

        // ---------------- Auto-align (P3) ------------------------------------------

        /// <summary>
        /// Run camera-assisted auto-align on the selected surface (all = every surface).
        /// Uses the default full-quad target, which recovers the current projection —
        /// callers wanting a real re-map supply target corners via AlignSurfaceWithTarget.
        /// </summary>
        public void StartAutoAlign(bool all)
        {
            if (IsAutoAligning) return;
            Camera observer = null;
            if (!UseNativeWebcam)
            {
                observer = ResolveObserverCamera();
                if (observer == null)
                {
                    AutoAlignStatus = "Auto-align needs an observer camera (assign ObserverCamera, add a 'PMSDK Calibration Observer' Camera, or enable UseNativeWebcam).";
                    Debug.LogWarning("[PMSDK] " + AutoAlignStatus);
                    return;
                }
            }
            EnsureAutoAlign();
            StartCoroutine(AutoAlignRoutine(all, observer, null));
        }

        /// <summary>
        /// Align the selected surface so its output lands on an explicit target: four
        /// camera-space points (normalized 0..1, TL,TR,BR,BL) marking where the
        /// projection should go (e.g. the physical screen bounds in the camera view).
        /// </summary>
        public void AlignSelectedWithTarget(Vector2[] targetCornersCameraNorm)
        {
            if (IsAutoAligning) return;
            Camera observer = null;
            if (!UseNativeWebcam)
            {
                observer = ResolveObserverCamera();
                if (observer == null) return;
            }
            EnsureAutoAlign();
            StartCoroutine(AutoAlignRoutine(false, observer, targetCornersCameraNorm));
        }

        private IEnumerator AutoAlignRoutine(bool all, Camera observer, Vector2[] target)
        {
            IsAutoAligning = true;
            PushUndoIfNeeded();
            lastUndoPushTime = -999f; // don't coalesce further edits into this snapshot

            var toAlign = new List<Surface>();
            if (all) toAlign.AddRange(surfaces);
            else if (Selected != null) toAlign.Add(Selected);

            int ok = 0;
            var results = new List<PMSDKAutoAlign.Result>();
            foreach (var s in toAlign)
            {
                AutoAlignStatus = $"Auto-aligning {s.Id}…";
                IPMSDKCalibrationCamera cam = UseNativeWebcam
                    ? new PMSDKNativeWebcamCamera(WebcamIndex, WebcamFlushFrames)
                    : new PMSDKSimulatedCamera(observer, Mathf.Max(64, ObserverWidth), Mathf.Max(64, ObserverHeight));
                PMSDKAutoAlign.Result captured = default;
                bool done = false;
                yield return autoAlign.AlignSurface(s, cam, target, r => { captured = r; done = true; });
                while (!done) yield return null;
                if (captured.Success) ok++;
                results.Add(captured);
                AutoAlignStatus = captured.Message;
                Debug.Log("[PMSDK] " + captured.Message);
            }

            string blendMsg = "";
            if (all && AutoBlendAfterAlignAll && ok >= 2)
            {
                blendMsg = ApplyAutoBlend(toAlign, results);
            }

            Dirty = true;
            AutoAlignStatus = $"Auto-align done: {ok}/{toAlign.Count} surfaces.{blendMsg} Fine-tune by hand, Ctrl+S to save.";
            IsAutoAligning = false;
        }

        /// <summary>
        /// Compute edge-blend widths from the overlap between the just-aligned
        /// projectors (all seen by the same camera) and apply them. Returns a status
        /// fragment. Requires every correspondence to share the camera resolution.
        /// </summary>
        private string ApplyAutoBlend(List<Surface> aligned, List<PMSDKAutoAlign.Result> results)
        {
            var corr = new List<PMSDKGrayCodeDecode.Correspondence[]>();
            var blendSurfaces = new List<Surface>();
            int camW = -1, camH = -1, projW = 0, projH = 0;
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                if (!r.Success || r.Correspondence == null) continue;
                if (camW < 0) { camW = r.CamW; camH = r.CamH; projW = r.ProjW; projH = r.ProjH; }
                // Auto-blend overlap detection only makes sense when all projectors
                // are observed by the one camera (same frame).
                if (r.CamW != camW || r.CamH != camH) continue;
                corr.Add(r.Correspondence);
                blendSurfaces.Add(aligned[i]);
            }
            if (corr.Count < 2) return " (auto-blend skipped: need 2+ projectors in one camera view).";

            var widths = PMSDKAutoBlend.Compute(corr, camW, camH, projW, projH);
            int applied = 0;
            for (int i = 0; i < blendSurfaces.Count; i++)
            {
                var blend = blendSurfaces[i].Blend;
                if (blend == null) continue;
                blend.LeftEdge = widths[i].Left;
                blend.RightEdge = widths[i].Right;
                blend.TopEdge = widths[i].Top;
                blend.BottomEdge = widths[i].Bottom;
                if (blend.Gamma <= 0f) blend.Gamma = 2.2f;
                applied++;
            }
            return $" Auto-blend set on {applied} projectors.";
        }

        private void EnsureAutoAlign()
        {
            if (autoAlign == null)
            {
                autoAlign = gameObject.GetComponent<PMSDKAutoAlign>();
                if (autoAlign == null) autoAlign = gameObject.AddComponent<PMSDKAutoAlign>();
            }
        }

        private Camera ResolveObserverCamera()
        {
            if (ObserverCamera != null) return ObserverCamera;
            var go = GameObject.Find("PMSDK Calibration Observer");
            if (go != null)
            {
                ObserverCamera = go.GetComponent<Camera>();
            }
            return ObserverCamera;
        }

        private void SetOverlaysVisible(bool visible)
        {
            foreach (var s in surfaces)
            {
                if (s.Overlay != null) s.Overlay.SetVisible(visible);
            }
            if (hud != null) hud.SetVisible(visible);
        }

        public static Vector2 IdentityCorner(int corner)
        {
            switch (corner)
            {
                case 0: return new Vector2(0, 1); // TL
                case 1: return new Vector2(1, 1); // TR
                case 2: return new Vector2(1, 0); // BR
                default: return new Vector2(0, 0); // BL
            }
        }

        public static Vector2 GetCorner(PMSDKCornerPin pin, int corner)
        {
            switch (corner)
            {
                case 0: return pin.TopLeft;
                case 1: return pin.TopRight;
                case 2: return pin.BottomRight;
                default: return pin.BottomLeft;
            }
        }

        private static void SetCorner(PMSDKCornerPin pin, int corner, Vector2 v)
        {
            switch (corner)
            {
                case 0: pin.TopLeft = v; break;
                case 1: pin.TopRight = v; break;
                case 2: pin.BottomRight = v; break;
                default: pin.BottomLeft = v; break;
            }
        }

        private static float GetEdge(PMSDKEdgeBlend b, int edge)
        {
            switch (edge)
            {
                case 0: return b.LeftEdge;
                case 1: return b.RightEdge;
                case 2: return b.TopEdge;
                default: return b.BottomEdge;
            }
        }

        private static void SetEdge(PMSDKEdgeBlend b, int edge, float v)
        {
            switch (edge)
            {
                case 0: b.LeftEdge = v; break;
                case 1: b.RightEdge = v; break;
                case 2: b.TopEdge = v; break;
                default: b.BottomEdge = v; break;
            }
        }

        private void ApplyFile(PMSDKCalibrationFile file)
        {
            foreach (var saved in file.surfaces)
            {
                Surface match = null;
                foreach (var s in surfaces)
                {
                    if (s.Id == saved.id) { match = s; break; }
                }
                if (match == null)
                {
                    Debug.LogWarning($"[PMSDK] Calibration entry '{saved.id}' has no matching surface in this scene — skipped.");
                    continue;
                }
                match.CornerPin.TopLeft = saved.tl;
                match.CornerPin.TopRight = saved.tr;
                match.CornerPin.BottomLeft = saved.bl;
                match.CornerPin.BottomRight = saved.br;
                if (match.Blend != null)
                {
                    match.Blend.LeftEdge = saved.blendLeft;
                    match.Blend.RightEdge = saved.blendRight;
                    match.Blend.TopEdge = saved.blendTop;
                    match.Blend.BottomEdge = saved.blendBottom;
                    match.Blend.Gamma = saved.gamma;
                    match.Blend.BlackLevel = saved.blackLevel;
                }

                // Restore a saved grid warp (and let it take over from the corner pin).
                if (saved.gridEnabled && saved.gridPoints != null && saved.gridPoints.Length == saved.gridColumns * saved.gridRows)
                {
                    if (match.Grid == null)
                    {
                        match.Grid = match.Warp.GetComponent<PMSDKGridWarp>();
                        if (match.Grid == null) match.Grid = match.Warp.gameObject.AddComponent<PMSDKGridWarp>();
                    }
                    match.Grid.Resize(saved.gridColumns, saved.gridRows);
                    for (int p = 0; p < saved.gridPoints.Length; p++) match.Grid.SetPointByIndex(p, saved.gridPoints[p]);
                    match.Grid.enabled = true;
                }
                else if (match.Grid != null)
                {
                    match.Grid.enabled = false;
                }

                if (saved.hasTarget && saved.targetCorners != null && saved.targetCorners.Length == 4)
                {
                    match.TargetCorners = (Vector2[])saved.targetCorners.Clone();
                    match.HasTarget = true;
                }
            }
        }
    }
}
