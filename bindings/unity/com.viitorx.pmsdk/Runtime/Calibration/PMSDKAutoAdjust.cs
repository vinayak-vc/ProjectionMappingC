using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>Outcome of one Auto Adjust run on one skin.</summary>
    public sealed class PMSDKAutoAdjustResult
    {
        public PMSDKSkin Skin;
        /// <summary>True when the final score meets the target (RMS and max).</summary>
        public bool Success;
        /// <summary>False when the run aborted (no camera, board not found, ...); the skin is then unchanged.</summary>
        public bool Completed;
        public string Message = "";
        /// <summary>Score of the skin as it was (best-fit definition).</summary>
        public PMSDKAutoAdjustScore Initial;
        /// <summary>Final score (against the facet quad when <see cref="SnappedToFacet"/>).</summary>
        public PMSDKAutoAdjustScore Final;
        /// <summary>Corrections applied (0 when the skin was already within the stop tolerance).</summary>
        public int Iterations;
        /// <summary>RMS (px) of every measurement in order.</summary>
        public List<float> RmsHistory = new List<float>();
        /// <summary>The corners were moved onto the edges of the facet the skin sits on (Phase 2).</summary>
        public bool SnappedToFacet;
        /// <summary>Facet quad in undistorted camera pixels (content BL, BR, TR, TL) when snapped.</summary>
        public Vector2[] FacetQuad;
        /// <summary>Millimetres per camera pixel on the facet (0 unless the skin has a facet size).</summary>
        public float MmPerPx;
        /// <summary>A warp map was switched off because the geometry changed (rebuild it afterwards).</summary>
        public bool WarpMapCleared;
    }

    /// <summary>
    /// Camera-assisted verify-and-refine of quad skins (Auto Adjust Phase 1 + 2, D-044/D-045).
    ///
    /// Projects a checkerboard inside the skin, captures it with a fixed camera, measures how far the
    /// observed corners are from a perfect perspective grid, and updates the skin's correction grid
    /// (<see cref="PMSDKSkin.SetCorrection"/>) until the error is below <see cref="StopRmsPx"/> or
    /// <see cref="MaxIterations"/> corrections were applied; the best state seen is kept.
    /// Default "keep corners, fix interior". With <see cref="SnapToFacetEdges"/> the projector first
    /// lights everything white and black, the lit area is split into facets, and the skin's corners
    /// are moved onto the facet that holds its checker before the interior is refined against that
    /// facet's quad. The caller records undo before starting; on abort the skin is restored.
    /// </summary>
    public class PMSDKAutoAdjust : MonoBehaviour
    {
        [Header("Pattern")]
        [Tooltip("Pick the square counts from the skin's shape so squares stay roughly square (long, thin facets).")]
        [SerializeField] private bool autoChecker = true;
        [SerializeField] private int shortSideSquares = 5;
        [SerializeField] private int squaresX = 10;
        [SerializeField] private int squaresY = 7;
        [SerializeField, Range(0f, 0.3f)] private float margin = 0.08f;
        [SerializeField] private int patternResolution = 1024;

        [Header("Loop")]
        [SerializeField] private int maxIterations = 4;
        [SerializeField] private float stopRmsPx = 0.5f;
        [SerializeField] private float targetRmsPx = 3f;
        [SerializeField] private float targetMaxPx = 8f;
        [SerializeField] private int settleFrames = 3;
        [SerializeField] private bool hideOtherSkins = true;
        [SerializeField] private bool snapToFacetEdges;
        [Tooltip("Also measure where a Rectangle skin's border lands, so the band outside the checker corners is corrected too.")]
        [SerializeField] private bool measureSkinEdges = true;
        [SerializeField] private float edgeMinContrast = 20f;

        [Header("Refinement")]
        [SerializeField, Range(0.1f, 1f)] private float damping = 0.8f;
        [SerializeField] private int gridColumns = 9;
        [SerializeField] private int gridRows = 9;

        /// <summary>True while a run is in progress.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Human-readable progress of the current run.</summary>
        public string Status { get; private set; } = "";

        /// <summary>Raised whenever <see cref="Status"/> changes.</summary>
        public event Action<string> StatusChanged;

        public int MaxIterations
        {
            get { return maxIterations; }
            set { maxIterations = Mathf.Clamp(value, 1, 20); }
        }

        public float StopRmsPx
        {
            get { return stopRmsPx; }
            set { stopRmsPx = Mathf.Max(0f, value); }
        }

        public float TargetRmsPx
        {
            get { return targetRmsPx; }
            set { targetRmsPx = Mathf.Max(0f, value); }
        }

        public float TargetMaxPx
        {
            get { return targetMaxPx; }
            set { targetMaxPx = Mathf.Max(0f, value); }
        }

        public int SettleFrames
        {
            get { return settleFrames; }
            set { settleFrames = Mathf.Max(0, value); }
        }

        public bool HideOtherSkins
        {
            get { return hideOtherSkins; }
            set { hideOtherSkins = value; }
        }

        /// <summary>
        /// Measure the skin's border in the camera image as well as the checker corners (Rectangle skins,
        /// D-048). Without it the band between the outermost checker corners and the border is not measured.
        /// </summary>
        public bool MeasureSkinEdges
        {
            get { return measureSkinEdges; }
            set { measureSkinEdges = value; }
        }

        /// <summary>Phase 2: move the corners onto the edges of the facet the skin sits on.</summary>
        public bool SnapToFacetEdges
        {
            get { return snapToFacetEdges; }
            set { snapToFacetEdges = value; }
        }

        public PMSDKCheckerSpec Checker
        {
            get { return new PMSDKCheckerSpec { SquaresX = squaresX, SquaresY = squaresY, Margin = margin }; }
        }

        /// <summary>When true (default) the checker's square counts follow each skin's shape.</summary>
        public bool AutoChecker
        {
            get { return autoChecker; }
            set { autoChecker = value; }
        }

        /// <summary>
        /// Checker for <paramref name="skin"/>: with <see cref="AutoChecker"/>, about
        /// <c>shortSideSquares</c> squares across the short side and proportionally more along the long
        /// side (measured in projector pixels; raster x is stretched by the skin's raster aspect), with
        /// squaresX + squaresY odd so the board orientation stays unambiguous.
        /// </summary>
        public PMSDKCheckerSpec CheckerFor(PMSDKSkin skin)
        {
            if (!autoChecker || skin == null || skin.Vertices == null || skin.Vertices.Count < 4) {
                return Checker;
            }
            Vector3 scale = skin.transform.lossyScale;
            float rasterAspect = scale.y > 1e-6f && scale.x > 1e-6f ? scale.x / scale.y : 16f / 9f;
            List<Vector2> v = skin.Vertices;
            float width = 0.5f * (Vector2.Distance(v[0], v[1]) + Vector2.Distance(v[3], v[2])) * rasterAspect;
            float height = 0.5f * (Vector2.Distance(v[0], v[3]) + Vector2.Distance(v[1], v[2]));
            if (width < 1e-5f || height < 1e-5f) {
                return Checker;
            }
            int shortSide = Mathf.Clamp(shortSideSquares, 3, 12);
            float ratio = Mathf.Max(width, height) / Mathf.Min(width, height);
            int longSide = Mathf.Clamp(Mathf.RoundToInt(shortSide * ratio), 3, 24);
            if (((shortSide + longSide) & 1) == 0) {
                longSide = longSide < 24 ? longSide + 1 : longSide - 1;
            }
            bool wide = width >= height;
            return new PMSDKCheckerSpec { SquaresX = wide ? longSide : shortSide, SquaresY = wide ? shortSide : longSide, Margin = margin };
        }

        public PMSDKRefineSettings Refinement
        {
            get {
                PMSDKRefineSettings settings = PMSDKRefineSettings.Default();
                settings.Damping = damping;
                settings.GridColumns = gridColumns;
                settings.GridRows = gridRows;
                return settings;
            }
        }

        /// <summary>
        /// Adjusts several skins one after the other with one camera session. <paramref name="onDone"/>
        /// receives one result per skin (in order).
        /// </summary>
        public IEnumerator AdjustSkins(IList<PMSDKSkin> targets, IPMSDKCalibrationCamera camera, IList<PMSDKSkin> allSkins,
            Action<List<PMSDKAutoAdjustResult>> onDone)
        {
            List<PMSDKAutoAdjustResult> results = new List<PMSDKAutoAdjustResult>();
            if (targets == null || targets.Count == 0 || camera == null) {
                PMSDKAutoAdjustResult empty = new PMSDKAutoAdjustResult { Message = targets == null || targets.Count == 0 ? "No skins to adjust." : "No calibration camera." };
                results.Add(empty);
                onDone?.Invoke(results);
                yield break;
            }
            SetStatus("Starting camera...");
            if (!camera.Begin()) {
                results.Add(new PMSDKAutoAdjustResult { Message = "The camera did not start." + CameraError(camera) });
                onDone?.Invoke(results);
                yield break;
            }
            try {
                SharedCamera shared = new SharedCamera(camera);
                for (int i = 0; i < targets.Count; i++) {
                    PMSDKAutoAdjustResult one = null;
                    SetStatus("Surface " + (i + 1) + " of " + targets.Count + "...");
                    IEnumerator run = AdjustSkin(targets[i], shared, allSkins, delegate(PMSDKAutoAdjustResult r) { one = r; });
                    while (run.MoveNext()) {
                        yield return run.Current;
                    }
                    results.Add(one ?? new PMSDKAutoAdjustResult { Skin = targets[i], Message = "No result." });
                }
            } finally {
                camera.End();
            }
            int completed = 0;
            int succeeded = 0;
            foreach (PMSDKAutoAdjustResult r in results) {
                completed += r.Completed ? 1 : 0;
                succeeded += r.Success ? 1 : 0;
            }
            SetStatus("Adjusted " + completed + " of " + results.Count + " surfaces; " + succeeded + " reached the target.");
            onDone?.Invoke(results);
        }

        /// <summary>
        /// Runs the loop on <paramref name="skin"/> (Rectangle or Ellipse). <paramref name="allSkins"/>
        /// are hidden while measuring (when <see cref="HideOtherSkins"/>) so the camera sees only this
        /// checker. <paramref name="onDone"/> always receives a result.
        /// </summary>
        public IEnumerator AdjustSkin(PMSDKSkin skin, IPMSDKCalibrationCamera camera, IList<PMSDKSkin> allSkins,
            Action<PMSDKAutoAdjustResult> onDone)
        {
            PMSDKAutoAdjustResult result = new PMSDKAutoAdjustResult { Skin = skin };
            string problem = Validate(skin, camera);
            if (problem != null) {
                result.Message = problem;
                Debug.LogWarning("[PMSDK] Auto Adjust: " + problem);
                onDone?.Invoke(result);
                yield break;
            }

            IsRunning = true;
            SkinSnapshot original = SkinSnapshot.Of(skin);
            List<PMSDKSkin> displaySkins = new List<PMSDKSkin>();
            if (allSkins != null) {
                displaySkins.AddRange(allSkins);
            }
            if (!displaySkins.Contains(skin)) {
                displaySkins.Add(skin);
            }
            PMSDKCalibrationDisplay display = new PMSDKCalibrationDisplay(displaySkins, skin.gameObject.layer);
            Texture2D checkerTexture = null;
            bool cameraStarted = false;
            try {
                do {
                    PMSDKCheckerSpec checker = CheckerFor(skin);
                    PMSDKRefineSettings refinement = Refinement;
                    string error;
                    Vector2[] uvs = PMSDKAutoAdjustNative.CheckerCornerUVs(checker, out error);
                    if (uvs == null) {
                        Abort(result, "Invalid checker: " + error);
                        break;
                    }
                    checkerTexture = PMSDKAutoAdjustNative.RenderCheckerTexture(checker, patternResolution,
                        Mathf.Max(64, Mathf.RoundToInt(patternResolution * 0.75f)), out error);
                    if (checkerTexture == null) {
                        Abort(result, "Could not render the checker: " + error);
                        break;
                    }

                    SetStatus("Starting camera...");
                    cameraStarted = camera.Begin();
                    if (!cameraStarted) {
                        Abort(result, "The camera did not start." + CameraError(camera));
                        break;
                    }
                    PMSDKCameraIntrinsics intrinsics = null;
                    IPMSDKCameraIntrinsicsSource intrinsicsSource = camera as IPMSDKCameraIntrinsicsSource;
                    if (intrinsicsSource != null) {
                        intrinsicsSource.TryGetIntrinsics(out intrinsics);
                    }

                    // A warp map is built for one geometry; this run changes it, so it is switched off.
                    if (!string.IsNullOrEmpty(skin.WarpMapFile)) {
                        skin.WarpMapFile = null;
                        result.WarpMapCleared = true;
                    }

                    // Phase 2: white and black floods for facet segmentation.
                    bool snap = snapToFacetEdges;
                    byte[] white = null;
                    byte[] black = null;
                    if (snap) {
                        SetStatus("Measuring the lit surfaces...");
                        display.HideAllExcept(null);
                        display.SetBackground(Color.black);
                        for (int f = 0; f < settleFrames; f++) {
                            yield return null;
                        }
                        black = camera.CaptureLuminance();
                        display.SetBackground(Color.white);
                        for (int f = 0; f < settleFrames; f++) {
                            yield return null;
                        }
                        white = camera.CaptureLuminance();
                        display.RestoreBackground();
                        if (white == null || black == null) {
                            Abort(result, "The camera returned no frame while measuring the lit surfaces." + CameraError(camera));
                            break;
                        }
                    }

                    if (hideOtherSkins) {
                        display.HideAllExcept(skin);
                    }
                    display.ShowPattern(skin, checkerTexture);

                    Vector2[] previousRaw = null;
                    float bestRms = float.PositiveInfinity;
                    PMSDKAutoAdjustScore bestScore = new PMSDKAutoAdjustScore();
                    SkinSnapshot best = SkinSnapshot.Of(skin);
                    bool aborted = false;
                    bool snapped = false;
                    Vector2[] contentQuad = null;

                    while (true) {
                        for (int f = 0; f < settleFrames; f++) {
                            yield return null;
                        }
                        SetStatus(result.RmsHistory.Count == 0 ? "Measuring..." : "Measuring after correction " + result.Iterations + "...");
                        Observation observation;
                        if (!TryObserve(camera, checker, previousRaw, intrinsics, out observation, out error)) {
                            if (result.RmsHistory.Count == 0) {
                                Abort(result, error);
                                aborted = true;
                            } else {
                                result.Message = "Stopped early: " + error;
                            }
                            break;
                        }
                        contentQuad = ContentQuad(uvs, observation) ?? contentQuad;

                        if (snap && !snapped) {
                            PMSDKAutoAdjustScore before;
                            if (PMSDKAutoAdjustNative.TryMeasure(uvs, observation.Undistorted, observation.Valid, refinement, out before, out error)) {
                                result.Initial = before;
                                result.RmsHistory.Add(before.RmsPx);
                            }
                            Vector2[] target;
                            if (!TrySnap(skin, uvs, observation, white, black, camera, intrinsics, out target, out error)) {
                                result.Message = "Facet snap skipped: " + error;
                                snap = false;
                                continue;
                            }
                            refinement.UseTargetQuad = true;
                            refinement.TargetQuad = target;
                            refinement.PinCorners = false;
                            result.SnappedToFacet = true;
                            result.FacetQuad = target;
                            snapped = true;
                            previousRaw = null;
                            // Scores are now against the facet; the pre-snap state is not a candidate.
                            bestRms = float.PositiveInfinity;
                            best = SkinSnapshot.Of(skin);
                            continue;
                        }

                        bool lastMeasurement = result.Iterations >= maxIterations;
                        PMSDKAutoAdjustScore score;
                        Vector2[] newOffsets = null;
                        bool scored;
                        Vector2[] pointUvs = uvs;
                        Vector2[] pointCamera = observation.Undistorted;
                        byte[] pointValid = observation.Valid;
                        PMSDKRefineSettings stepSettings = refinement;
                        int borderPoints = 0;
                        if (measureSkinEdges && skin.Kind == PMSDKSkinShapeKind.Rectangle) {
                            borderPoints = AddSkinBorder(camera, checker, uvs, observation, intrinsics, ref stepSettings, out pointUvs,
                                out pointCamera, out pointValid);
                        }
                        if (lastMeasurement) {
                            scored = PMSDKAutoAdjustNative.TryMeasure(pointUvs, pointCamera, pointValid, stepSettings, out score, out error);
                        } else {
                            scored = PMSDKAutoAdjustNative.TryRefine(skin.Vertices, skin.CorrectionColumns, skin.CorrectionRows,
                                skin.GetCorrectionOffsets(), pointUvs, pointCamera, pointValid, stepSettings,
                                out score, out newOffsets, out error);
                        }
                        if (!scored) {
                            if (result.RmsHistory.Count == 0) {
                                Abort(result, "Measurement failed: " + error);
                                aborted = true;
                            } else {
                                result.Message = "Stopped early: " + error;
                            }
                            break;
                        }

                        if (result.RmsHistory.Count == 0) {
                            result.Initial = score;
                        }
                        result.RmsHistory.Add(score.RmsPx);
                        if (score.RmsPx < bestRms) {
                            bestRms = score.RmsPx;
                            bestScore = score;
                            best = SkinSnapshot.Of(skin);
                        }
                        SetStatus("RMS " + score.RmsPx.ToString("0.00") + " px, max " + score.MaxPx.ToString("0.00") + " px (" +
                                  (score.ValidCount - borderPoints) + " corners, " + borderPoints + " border points)");

                        if (score.RmsPx <= stopRmsPx || lastMeasurement) {
                            break;
                        }
                        skin.SetCorrection(refinement.GridColumns, refinement.GridRows, newOffsets);
                        result.Iterations++;
                        previousRaw = observation.Raw;
                    }

                    if (aborted) {
                        original.ApplyTo(skin);
                        if (result.WarpMapCleared) {
                            skin.WarpMapFile = original.WarpMapFile;
                            result.WarpMapCleared = false;
                        }
                        break;
                    }
                    if (float.IsPositiveInfinity(bestRms)) {
                        // Nothing could be scored after the snap: go back to how the skin was.
                        original.ApplyTo(skin);
                        result.Message = "No measurement succeeded. " + result.Message;
                        SetStatus(result.Message);
                        break;
                    }
                    best.ApplyTo(skin);
                    result.Final = bestScore;
                    result.Completed = true;
                    result.Success = bestScore.RmsPx <= targetRmsPx && bestScore.MaxPx <= targetMaxPx;
                    Vector2[] scaleQuad = result.FacetQuad ?? contentQuad;
                    if (skin.HasFacetSize && scaleQuad != null) {
                        result.MmPerPx = PMSDKAutoAdjustNative.MetricScale(scaleQuad, skin.FacetWidthMm, skin.FacetHeightMm);
                    }
                    result.Message = Summary(result) + (string.IsNullOrEmpty(result.Message) ? "" : " " + result.Message);
                    SetStatus(result.Message);
                } while (false);
            } finally {
                display.Dispose();
                if (cameraStarted) {
                    camera.End();
                }
                if (checkerTexture != null) {
                    DestroyTexture(checkerTexture);
                }
                IsRunning = false;
            }
            onDone?.Invoke(result);
        }

        /// <summary>
        /// Scores <paramref name="skin"/> exactly as it is displayed (warp map included) without changing
        /// it. <paramref name="onDone"/> receives (success, score, message).
        /// </summary>
        public IEnumerator MeasureSkin(PMSDKSkin skin, IPMSDKCalibrationCamera camera, IList<PMSDKSkin> allSkins,
            Action<bool, PMSDKAutoAdjustScore, string> onDone)
        {
            string problem = Validate(skin, camera);
            if (problem != null) {
                onDone?.Invoke(false, new PMSDKAutoAdjustScore(), problem);
                yield break;
            }
            IsRunning = true;
            List<PMSDKSkin> displaySkins = new List<PMSDKSkin>();
            if (allSkins != null) {
                displaySkins.AddRange(allSkins);
            }
            PMSDKCalibrationDisplay display = new PMSDKCalibrationDisplay(displaySkins, skin.gameObject.layer);
            Texture2D checkerTexture = null;
            bool cameraStarted = false;
            bool ok = false;
            PMSDKAutoAdjustScore score = new PMSDKAutoAdjustScore();
            string message = "";
            try {
                do {
                    PMSDKCheckerSpec checker = CheckerFor(skin);
                    string error;
                    Vector2[] uvs = PMSDKAutoAdjustNative.CheckerCornerUVs(checker, out error);
                    checkerTexture = uvs != null
                        ? PMSDKAutoAdjustNative.RenderCheckerTexture(checker, patternResolution, Mathf.Max(64, Mathf.RoundToInt(patternResolution * 0.75f)), out error)
                        : null;
                    if (checkerTexture == null) {
                        message = "Could not prepare the checker: " + error;
                        break;
                    }
                    cameraStarted = camera.Begin();
                    if (!cameraStarted) {
                        message = "The camera did not start." + CameraError(camera);
                        break;
                    }
                    PMSDKCameraIntrinsics intrinsics = null;
                    IPMSDKCameraIntrinsicsSource source = camera as IPMSDKCameraIntrinsicsSource;
                    if (source != null) {
                        source.TryGetIntrinsics(out intrinsics);
                    }
                    if (hideOtherSkins) {
                        display.HideAllExcept(skin);
                    }
                    display.ShowPattern(skin, checkerTexture, true);
                    for (int f = 0; f < settleFrames; f++) {
                        yield return null;
                    }
                    Observation observation;
                    if (!TryObserve(camera, checker, null, intrinsics, out observation, out error)) {
                        message = error;
                        break;
                    }
                    if (!PMSDKAutoAdjustNative.TryMeasure(uvs, observation.Undistorted, observation.Valid, Refinement, out score, out error)) {
                        message = "Measurement failed: " + error;
                        break;
                    }
                    ok = true;
                    message = "RMS " + score.RmsPx.ToString("0.00") + " px, max " + score.MaxPx.ToString("0.00") + " px (" + score.ValidCount + " corners).";
                } while (false);
            } finally {
                display.Dispose();
                if (cameraStarted) {
                    camera.End();
                }
                if (checkerTexture != null) {
                    DestroyTexture(checkerTexture);
                }
                IsRunning = false;
            }
            SetStatus(message);
            onDone?.Invoke(ok, score, message);
        }

        private static string Summary(PMSDKAutoAdjustResult result)
        {
            string text = "RMS " + result.Initial.RmsPx.ToString("0.00") + " -> " + result.Final.RmsPx.ToString("0.00") +
                          " px (max " + result.Final.MaxPx.ToString("0.00") + " px)";
            if (result.MmPerPx > 0f) {
                text += " = " + (result.Final.RmsPx * result.MmPerPx).ToString("0.0") + " mm RMS";
            }
            text += " after " + result.Iterations + " correction(s)";
            if (result.SnappedToFacet) {
                text += ", corners snapped to the facet";
            }
            text += result.Success ? "." : "; target not reached.";
            if (result.WarpMapCleared) {
                text += " The warp map was switched off; rebuild it if needed.";
            }
            return text;
        }

        private bool TrySnap(PMSDKSkin skin, Vector2[] uvs, Observation observation, byte[] white, byte[] black,
            IPMSDKCalibrationCamera camera, PMSDKCameraIntrinsics intrinsics, out Vector2[] target, out string error)
        {
            target = null;
            List<PMSDKFacet> facets;
            int containing;
            if (!PMSDKAutoAdjustNative.TrySegmentFacets(white, black, camera.Width, camera.Height, observation.Raw, observation.Valid,
                    out facets, out containing, out error)) {
                return false;
            }
            if (containing < 0) {
                error = "no lit facet contains the checker (" + facets.Count + " facets found).";
                return false;
            }
            Vector2[] quad;
            if (!PMSDKAutoAdjustNative.TryUndistort(facets[containing].Quad, intrinsics, out quad, out error)) {
                return false;
            }
            Vector2[] corners;
            if (!PMSDKAutoAdjustNative.TrySolveCorners(skin.Vertices, skin.CorrectionColumns, skin.CorrectionRows, skin.GetCorrectionOffsets(),
                    uvs, observation.Undistorted, observation.Valid, quad, out corners, out error)) {
                return false;
            }
            skin.Vertices = new List<Vector2>(corners);
            skin.ClearCorrection();
            target = quad;
            SetStatus("Corners snapped to facet " + (containing + 1) + " of " + facets.Count + ".");
            return true;
        }

        // Appends the skin-border samples (PMSDKSkinEdgeProbe) to the checker corners and fixes the target
        // ("correct") as an explicit quad so the border points cannot pull the fit (D-048): the facet quad
        // after a snap; with pinned corners the skin's observed corners (where the measured border lines
        // meet), so the edges become straight lines between corners that do not move; otherwise the best fit
        // to the checker corners. Returns the number of border points added.
        private int AddSkinBorder(IPMSDKCalibrationCamera camera, PMSDKCheckerSpec checker, Vector2[] uvs, Observation observation,
            PMSDKCameraIntrinsics intrinsics, ref PMSDKRefineSettings settings, out Vector2[] pointUvs, out Vector2[] pointCamera,
            out byte[] pointValid)
        {
            pointUvs = uvs;
            pointCamera = observation.Undistorted;
            pointValid = observation.Valid;
            List<Vector2> borderUvs = new List<Vector2>();
            List<Vector2> borderPoints = new List<Vector2>();
            PMSDKSkinEdgeProbe.FindBorder(observation.Frame, camera.Width, camera.Height, checker, uvs, observation.Raw, observation.Valid,
                intrinsics, edgeMinContrast, borderUvs, borderPoints);
            if (borderUvs.Count == 0) {
                return 0;
            }
            Vector2[] quad = null;
            if (settings.UseTargetQuad && settings.TargetQuad != null && settings.TargetQuad.Length == 4) {
                quad = settings.TargetQuad;
            } else if (!settings.PinCorners || !PMSDKSkinEdgeProbe.TryCornerQuad(borderUvs, borderPoints, out quad)) {
                quad = ContentQuad(uvs, observation);
            }
            if (quad == null) {
                return 0;
            }
            PMSDKHomography target = PMSDKHomography.FitNormalized(
                new Vector2[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) }, quad);
            if (!target.Valid) {
                return 0;
            }
            int n = uvs.Length;
            pointUvs = new Vector2[n + borderUvs.Count];
            pointCamera = new Vector2[n + borderUvs.Count];
            pointValid = new byte[n + borderUvs.Count];
            System.Array.Copy(uvs, pointUvs, n);
            System.Array.Copy(observation.Undistorted, pointCamera, n);
            System.Array.Copy(observation.Valid, pointValid, n);
            for (int i = 0; i < borderUvs.Count; i++) {
                pointUvs[n + i] = borderUvs[i];
                pointCamera[n + i] = PMSDKSkinEdgeProbe.AcrossOnly(target, borderUvs[i], borderPoints[i]);
                pointValid[n + i] = 1;
            }
            settings.UseTargetQuad = true;
            settings.TargetQuad = (Vector2[])quad.Clone();
            return borderUvs.Count;
        }

        // Content corners in the camera from the best-fit homography of the valid corners (for the mm scale).
        private static Vector2[] ContentQuad(Vector2[] uvs, Observation observation)
        {
            List<Vector2> src = new List<Vector2>();
            List<Vector2> dst = new List<Vector2>();
            for (int i = 0; i < uvs.Length; i++) {
                if (observation.Valid[i] != 0) {
                    src.Add(uvs[i]);
                    dst.Add(observation.Undistorted[i]);
                }
            }
            if (src.Count < 4) {
                return null;
            }
            PMSDKHomography h = PMSDKHomography.FitNormalized(src, dst);
            if (!h.Valid) {
                return null;
            }
            return new Vector2[] {
                h.Apply(new Vector2(0f, 0f)), h.Apply(new Vector2(1f, 0f)), h.Apply(new Vector2(1f, 1f)), h.Apply(new Vector2(0f, 1f)),
            };
        }

        private string Validate(PMSDKSkin skin, IPMSDKCalibrationCamera camera)
        {
            if (IsRunning) {
                return "Auto Adjust is already running.";
            }
            if (skin == null) {
                return "No skin selected.";
            }
            if (skin.Kind != PMSDKSkinShapeKind.Rectangle && skin.Kind != PMSDKSkinShapeKind.Ellipse) {
                return "Auto Adjust supports Rectangle and Ellipse skins (this one is " + skin.Kind + ").";
            }
            if (skin.Vertices == null || skin.Vertices.Count != 4) {
                return "The skin needs exactly 4 corners.";
            }
            if (camera == null) {
                return "No calibration camera.";
            }
            if (!PMSDKAutoAdjustNative.IsAvailable()) {
                return "ProjectionMappingSDK.dll has no Auto Adjust support (update the native plugin).";
            }
            return null;
        }

        private void Abort(PMSDKAutoAdjustResult result, string message)
        {
            result.Completed = false;
            result.Success = false;
            result.Message = message;
            SetStatus(message);
            Debug.LogWarning("[PMSDK] Auto Adjust: " + message);
        }

        private void SetStatus(string text)
        {
            Status = text;
            StatusChanged?.Invoke(text);
        }

        internal static string CameraError(IPMSDKCalibrationCamera camera)
        {
            SharedCamera shared = camera as SharedCamera;
            IPMSDKCalibrationCamera inner = shared != null ? shared.Inner : camera;
            PMSDKOakCalibrationCamera oak = inner as PMSDKOakCalibrationCamera;
            return oak != null && !string.IsNullOrEmpty(oak.LastError) ? " " + oak.LastError : "";
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (Application.isPlaying) {
                Destroy(texture);
            } else {
                DestroyImmediate(texture);
            }
        }

        private struct Observation
        {
            public byte[] Frame;
            public Vector2[] Raw;
            public Vector2[] Undistorted;
            public byte[] Valid;
        }

        private static bool TryObserve(IPMSDKCalibrationCamera camera, PMSDKCheckerSpec checker, Vector2[] predicted,
            PMSDKCameraIntrinsics intrinsics, out Observation observation, out string error)
        {
            observation = new Observation();
            byte[] frame = camera.CaptureLuminance();
            if (frame == null || frame.Length < camera.Width * camera.Height) {
                error = "The camera returned no frame." + CameraError(camera);
                return false;
            }
            Vector2[] raw;
            byte[] valid;
            int validCount;
            bool wholeBoard;
            if (!PMSDKAutoAdjustNative.TryDetect(frame, camera.Width, camera.Height, checker, predicted, out raw, out valid,
                    out validCount, out wholeBoard, out error)) {
                error = "The checkerboard was not found in the camera image (" + error +
                        "). Check that the camera sees the whole skin, the room is dim and the camera is in focus.";
                return false;
            }
            Vector2[] undistorted;
            if (!PMSDKAutoAdjustNative.TryUndistort(raw, intrinsics, out undistorted, out error)) {
                error = "Lens undistortion failed: " + error;
                return false;
            }
            observation.Frame = frame;
            observation.Raw = raw;
            observation.Undistorted = undistorted;
            observation.Valid = valid;
            error = "";
            return true;
        }

        /// <summary>A skin's corners and correction grid at one moment.</summary>
        private sealed class SkinSnapshot
        {
            private List<Vector2> vertices;
            private int columns;
            private int rows;
            private List<Vector2> offsets;
            public string WarpMapFile;

            public static SkinSnapshot Of(PMSDKSkin skin)
            {
                return new SkinSnapshot {
                    vertices = new List<Vector2>(skin.Vertices),
                    columns = skin.CorrectionColumns,
                    rows = skin.CorrectionRows,
                    offsets = skin.GetCorrectionOffsets(),
                    WarpMapFile = skin.WarpMapFile,
                };
            }

            public void ApplyTo(PMSDKSkin skin)
            {
                skin.Vertices = new List<Vector2>(vertices);
                if (columns >= 2 && rows >= 2) {
                    skin.SetCorrection(columns, rows, offsets);
                } else {
                    skin.ClearCorrection();
                }
            }
        }

        /// <summary>Camera already begun by the caller: Begin/End do nothing.</summary>
        internal sealed class SharedCamera : IPMSDKCalibrationCamera, IPMSDKCameraIntrinsicsSource, IPMSDKDepthSource
        {
            public readonly IPMSDKCalibrationCamera Inner;

            public SharedCamera(IPMSDKCalibrationCamera inner)
            {
                Inner = inner;
            }

            public int Width
            {
                get { return Inner.Width; }
            }

            public int Height
            {
                get { return Inner.Height; }
            }

            public bool HasDepth
            {
                get {
                    IPMSDKDepthSource depth = Inner as IPMSDKDepthSource;
                    return depth != null && depth.HasDepth;
                }
            }

            public bool Begin()
            {
                return true;
            }

            public byte[] CaptureLuminance()
            {
                return Inner.CaptureLuminance();
            }

            public void End()
            {
            }

            public bool TryGetIntrinsics(out PMSDKCameraIntrinsics intrinsics)
            {
                IPMSDKCameraIntrinsicsSource source = Inner as IPMSDKCameraIntrinsicsSource;
                if (source == null) {
                    intrinsics = null;
                    return false;
                }
                return source.TryGetIntrinsics(out intrinsics);
            }

            public ushort[] CaptureDepth()
            {
                IPMSDKDepthSource depth = Inner as IPMSDKDepthSource;
                return depth != null ? depth.CaptureDepth() : null;
            }
        }
    }
}
