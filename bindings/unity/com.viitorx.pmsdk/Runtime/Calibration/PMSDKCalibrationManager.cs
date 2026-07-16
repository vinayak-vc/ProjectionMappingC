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
            public Camera ProjectorCamera;
            public PMSDKCalibrationOverlay Overlay;
            public RenderTexture Thumbnail;
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
        [Tooltip("Observer camera for simulated auto-align. If null, a scene object named 'PMSDK Calibration Observer' with a Camera is used.")]
        public Camera ObserverCamera;
        [Tooltip("Capture resolution for the simulated observer camera.")]
        public int ObserverWidth = 640;
        public int ObserverHeight = 480;

        public bool CalibrationMode { get; private set; }
        public bool BlendSubmode { get; private set; }
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

            if (Input.GetKeyDown(KeyCode.Escape)) { ExitCalibration(); return; }
            if (Input.GetKeyDown(KeyCode.F1)) HelpVisible = !HelpVisible;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (Input.GetKeyDown(KeyCode.A)) { StartAutoAlign(all: shift); return; }

            if (ctrl && Input.GetKeyDown(KeyCode.S)) { SaveNow(); }
            else if (ctrl && Input.GetKeyDown(KeyCode.Z)) { UndoLast(); }
            else if (ctrl && Input.GetKeyDown(KeyCode.R)) { ResetSelectedSurface(); }
            else if (Input.GetKeyDown(KeyCode.R)) { ResetSelectedCorner(); }

            if (Input.GetKeyDown(KeyCode.PageUp)) SelectSurface(-1);
            if (Input.GetKeyDown(KeyCode.PageDown)) SelectSurface(+1);
            if (Input.GetKeyDown(KeyCode.Tab)) CycleElement(shift ? -1 : +1);
            if (Input.GetKeyDown(KeyCode.B)) BlendSubmode = !BlendSubmode;
            if (Input.GetKeyDown(KeyCode.T)) ToggleTestPattern(all: shift);
            if (Input.GetKeyDown(KeyCode.N)) AdjustBlackLevel(shift ? -0.01f : +0.01f);

            float speed = NudgePerSecond * Time.unscaledDeltaTime;
            if (shift) speed *= CoarseMultiplier;
            if (ctrl) speed *= FineMultiplier;

            float dx = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float dy = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            if (dx != 0f || dy != 0f)
            {
                if (BlendSubmode) AdjustBlend(dx * speed, dy * speed);
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

        /// <summary>Tab: cycles corner, or blend edge while in blend submode.</summary>
        public void CycleElement(int delta)
        {
            if (BlendSubmode) SelectedEdge = (SelectedEdge + delta + 4) % 4;
            else SelectedCorner = (SelectedCorner + delta + 4) % 4;
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

            Vector2 corner = GetCorner(s.CornerPin, SelectedCorner);
            Vector3 worldCorner = s.Warp.transform.TransformPoint(new Vector3(corner.x, corner.y, 0f));

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
                AutoAlignStatus = captured.Message;
                Debug.Log("[PMSDK] " + captured.Message);
            }

            Dirty = true;
            AutoAlignStatus = $"Auto-align done: {ok}/{toAlign.Count} surfaces. Fine-tune by hand, Ctrl+S to save.";
            IsAutoAligning = false;
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
            }
        }
    }
}
