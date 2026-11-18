using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Workspace inspector section for Auto Adjust (D-044..D-047), using the OAK-D:
    ///   - "Auto adjust" / "Adjust all": checker verify-and-refine of the selected / every quad skin,
    ///     optionally snapping the corners to the facet edges (Phase 1 + 2);
    ///   - facet size (mm) of the selected skin for millimetre scores (SURFACE-METRIC);
    ///   - "Build warp map": dense Gray-code map for curved surfaces (Phase 3);
    ///   - "Find surfaces": depth-based plane detection that creates one skin per surface (Phase 4);
    ///   - "Camera photo" / "Map marked surface": the user (or an AI cutout) marks a surface on the camera photo
    ///     and it becomes one masked skin; its warp map later uses the same camera region (D-049/D-050);
    ///   - exposure: structured light needs a locked camera exposure (auto exposure breaks decoding).
    ///   - "Physical spacing (depth)": the warp map lays the content out in equal millimetres along the measured,
    ///     unfolded surface instead of as a perspective picture from the camera; "Measure on surface (depth)"
    ///     reports the grid errors in mm per facet; "Show calibration grid" puts a labelled grid on the skin (D-051).
    ///   - "Calibrate projector": projector-camera self-calibration + triangulation; its depth replaces the OAK stereo
    ///     for physical spacing and measurements, and refreshes marked skins' camera regions (D-052).
    ///   - "Clear": removes the correction grid and the warp map.
    /// Every action records one undo step and saves the set when it changes something.
    /// </summary>
    public class PMSDKAutoAdjustPanel
    {
        private readonly MonoBehaviour host;
        private readonly PMSDKSkinEditorController controller;
        private readonly Action save;
        private readonly PMSDKAutoAdjust autoAdjust;
        private readonly PMSDKWarpMapBuilder warpMapBuilder;
        private readonly PMSDKSurfaceFinder surfaceFinder;
        private readonly Button runButton;
        private readonly Button allButton;
        private readonly Button warpButton;
        private readonly Button clearButton;
        private readonly Button findButton;
        private readonly Button photoButton;
        private readonly Button markedButton;
        private readonly InputField exposureField;
        private readonly Toggle physicalToggle;
        private readonly InputField aspectField;
        private readonly Toggle gridToggle;
        private readonly Button measureButton;
        private readonly Button calibrateButton;
        private readonly Button calibrateAllButton;
        private readonly Button calibrateSelectedButton;
        private bool flowRunning;
        private readonly Text calibrationLabel;
        private Texture2D gridTexture;
        private PMSDKSkin gridSkin;
        private readonly Toggle snapToggle;
        private readonly InputField widthField;
        private readonly InputField heightField;
        private readonly Text statusLabel;
        private readonly Text correctionLabel;
        private PMSDKSkin boundSkin;
        private bool suppress;
        private bool photoRunning;
        private float exposureMs = DefaultExposureMs;

        /// <summary>Exposure the OAK is locked to for every run (hardware run 1: 3 ms at ISO 400 on a lit soffit).</summary>
        public const float DefaultExposureMs = 3f;
        private const uint ExposureIso = 400;

        /// <summary>Creates the camera for a run (colour only). Defaults to the OAK-D.</summary>
        public Func<IPMSDKCalibrationCamera> CameraFactory;

        /// <summary>Creates the camera for "Find surfaces" (colour + depth). Defaults to the OAK-D with depth.</summary>
        public Func<IPMSDKCalibrationCamera> DepthCameraFactory;

        public PMSDKAutoAdjustPanel(Transform parent, MonoBehaviour coroutineHost, PMSDKSkinEditorController editorController,
            Action saveSet)
        {
            host = coroutineHost;
            controller = editorController;
            save = saveSet;
            CameraFactory = delegate { return CreateOakCamera(false); };
            DepthCameraFactory = delegate { return CreateOakCamera(true); };
            autoAdjust = GetOrAdd<PMSDKAutoAdjust>();
            warpMapBuilder = GetOrAdd<PMSDKWarpMapBuilder>();
            surfaceFinder = GetOrAdd<PMSDKSurfaceFinder>();
            autoAdjust.StatusChanged += OnStatusChanged;
            warpMapBuilder.StatusChanged += OnStatusChanged;
            surfaceFinder.StatusChanged += OnStatusChanged;
            warpMapBuilder.ReportFolder = Path.Combine(Application.persistentDataPath, "SurfaceReports");
            PMSDKProjectorCalibration savedCalibration;
            string calibrationError;
            if (PMSDKProjectorCalibration.TryLoad(PMSDKProjectorCalibration.Folder, out savedCalibration, out calibrationError)) {
                warpMapBuilder.ProjectorCalibration = savedCalibration;
            } else if (!string.IsNullOrEmpty(calibrationError)) {
                Debug.LogWarning("[PMSDK] " + calibrationError);
            }

            RectTransform root = PMSDKUi.NewRect("Auto Adjust", parent);
            PMSDKUi.AddVertical(root.gameObject, 0, 4f);
            ContentSizeFitter fit = root.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            PMSDKUi.AddDimLabel(root, "Measures the projection with the OAK-D and corrects it.");
            PMSDKUi.AddDimLabel(root, "Fix the camera, dim the room, close OAK Viewer.");

            RectTransform exposureRow = NewRow(root, "Exposure Row");
            PMSDKUi.AddDimLabel(exposureRow, "Exposure ms (0 = auto)");
            exposureField = PMSDKUi.AddInputField(exposureRow, exposureMs.ToString("0.##", CultureInfo.InvariantCulture), "ms", OnExposureEdited);

            PMSDKUi.AddDimLabel(root, "Calibrate: draw your skins, then let the camera make the content equal-sized on the surface.");
            RectTransform flowRow = NewRow(root, "Calibrate Skins Row");
            calibrateAllButton = AddRowButton(flowRow, "Calibrate all skins", OnCalibrateAllClicked);
            calibrateSelectedButton = AddRowButton(flowRow, "Calibrate selected skin", OnCalibrateSelectedClicked);
            PMSDKUi.AddDimLabel(root, "Advanced:");

            RectTransform rowA = NewRow(root, "Auto Adjust Row");
            runButton = AddRowButton(rowA, "Auto adjust", OnRunClicked);
            allButton = AddRowButton(rowA, "Adjust all", OnAllClicked);
            snapToggle = PMSDKUi.AddToggle(root, "Snap corners to facet edges", false, delegate(bool on) { autoAdjust.SnapToFacetEdges = on; });

            RectTransform sizeRow = NewRow(root, "Facet Size Row");
            PMSDKUi.AddDimLabel(sizeRow, "Size mm");
            widthField = PMSDKUi.AddInputField(sizeRow, "", "width", OnSizeEdited);
            heightField = PMSDKUi.AddInputField(sizeRow, "", "height", OnSizeEdited);

            RectTransform rowB = NewRow(root, "Warp Map Row");
            warpButton = AddRowButton(rowB, "Build warp map", OnWarpClicked);
            clearButton = AddRowButton(rowB, "Clear", OnClearClicked);
            physicalToggle = PMSDKUi.AddToggle(root, "Physical spacing (depth): equal mm on every facet", warpMapBuilder.Target == PMSDKWarpMapTarget.PhysicalSpacing,
                delegate(bool on) { warpMapBuilder.Target = on ? PMSDKWarpMapTarget.PhysicalSpacing : PMSDKWarpMapTarget.CameraPerspective; });
            RectTransform aspectRow = NewRow(root, "Content Aspect Row");
            PMSDKUi.AddDimLabel(aspectRow, "Content aspect (0 = fill)");
            aspectField = PMSDKUi.AddInputField(aspectRow, warpMapBuilder.ContentAspect.ToString("0.###", CultureInfo.InvariantCulture), "w / h", OnAspectEdited);
            gridToggle = PMSDKUi.AddToggle(root, "Show calibration grid", false, OnGridToggled);

            RectTransform rowC = NewRow(root, "Find Surfaces Row");
            findButton = AddRowButton(rowC, "Find surfaces (depth)", OnFindClicked);
            measureButton = AddRowButton(rowC, "Measure on surface (depth)", OnMeasureClicked);

            RectTransform rowE = NewRow(root, "Projector Calibration Row");
            calibrateButton = AddRowButton(rowE, "Calibrate projector", OnCalibrateClicked);
            calibrationLabel = PMSDKUi.AddDimLabel(root, CalibrationText());

            PMSDKUi.AddDimLabel(root, "Marked surface: take a photo, outline the surface in red (or save an AI mask)");
            PMSDKUi.AddDimLabel(root, "as " + PMSDKCameraMark.MarkedFileName + " next to it, then map it.");
            RectTransform rowD = NewRow(root, "Marked Surface Row");
            photoButton = AddRowButton(rowD, "Camera photo", OnPhotoClicked);
            markedButton = AddRowButton(rowD, "Map marked surface", OnMarkedClicked);

            correctionLabel = PMSDKUi.AddDimLabel(root, "");
            statusLabel = PMSDKUi.AddDimLabel(root, "");
            Bind(null);
        }

        /// <summary>Shows the state of <paramref name="skin"/> (null = nothing selected).</summary>
        public void Bind(PMSDKSkin skin)
        {
            suppress = true;
            if (gridToggle != null && gridToggle.isOn && skin != gridSkin) {
                MoveGrid(skin);
            }
            boundSkin = skin;
            bool busy = Busy;
            bool quad = skin != null && (skin.Kind == PMSDKSkinShapeKind.Rectangle || skin.Kind == PMSDKSkinShapeKind.Ellipse);
            bool polygon = skin != null && (skin.Kind == PMSDKSkinShapeKind.Polygon || skin.Kind == PMSDKSkinShapeKind.Freeform);
            runButton.interactable = quad && !busy;
            warpButton.interactable = (quad || polygon) && !busy;
            measureButton.interactable = (quad || polygon) && !busy;
            calibrateButton.interactable = !busy;
            calibrateAllButton.interactable = !busy && controller.Skins.Count > 0;
            calibrateSelectedButton.interactable = !busy && skin != null;
            allButton.interactable = !busy && QuadSkins().Count > 0;
            findButton.interactable = !busy;
            photoButton.interactable = !busy;
            markedButton.interactable = !busy;
            clearButton.interactable = skin != null && !busy && (skin.HasCorrection || !string.IsNullOrEmpty(skin.WarpMapFile));
            snapToggle.SetIsOnWithoutNotify(autoAdjust.SnapToFacetEdges);
            widthField.interactable = skin != null && !busy;
            heightField.interactable = skin != null && !busy;
            widthField.text = skin != null && skin.FacetWidthMm > 0f ? skin.FacetWidthMm.ToString("0", CultureInfo.InvariantCulture) : "";
            heightField.text = skin != null && skin.FacetHeightMm > 0f ? skin.FacetHeightMm.ToString("0", CultureInfo.InvariantCulture) : "";
            if (skin == null) {
                correctionLabel.text = "";
            } else if (polygon) {
                correctionLabel.text = string.IsNullOrEmpty(skin.WarpMapFile) ? "Polygon: no warp map. Use Calibrate selected skin (Auto adjust is Rectangle/Ellipse only)." : "Polygon: warp map on.";
            } else if (!quad) {
                correctionLabel.text = "Use Calibrate selected skin on Rectangle, Ellipse or Polygon surfaces.";
            } else {
                string text = skin.HasCorrection ? "Correction " + skin.CorrectionColumns + " x " + skin.CorrectionRows : "No correction";
                text += string.IsNullOrEmpty(skin.WarpMapFile) ? ", no warp map." : ", warp map on.";
                correctionLabel.text = text;
            }
            suppress = false;
        }

        private bool Busy
        {
            get { return autoAdjust.IsRunning || warpMapBuilder.IsRunning || surfaceFinder.IsRunning || photoRunning || flowRunning; }
        }

        private void OnRunClicked()
        {
            IPMSDKCalibrationCamera camera = StartAction();
            if (camera == null || boundSkin == null) {
                return;
            }
            controller.Undo.Record(controller, "auto-adjust");
            host.StartCoroutine(autoAdjust.AdjustSkin(boundSkin, camera, new List<PMSDKSkin>(controller.Skins), delegate(PMSDKAutoAdjustResult result) {
                Finish(result.Message, result.Completed);
            }));
        }

        private void OnAllClicked()
        {
            List<PMSDKSkin> targets = QuadSkins();
            IPMSDKCalibrationCamera camera = targets.Count > 0 ? StartAction() : null;
            if (camera == null) {
                return;
            }
            controller.Undo.Record(controller, "auto-adjust-all");
            host.StartCoroutine(autoAdjust.AdjustSkins(targets, camera, new List<PMSDKSkin>(controller.Skins), delegate(List<PMSDKAutoAdjustResult> results) {
                bool any = false;
                List<string> lines = new List<string>();
                foreach (PMSDKAutoAdjustResult r in results) {
                    any |= r.Completed;
                    string name = r.Skin != null ? r.Skin.DisplayName : "?";
                    lines.Add(name + ": " + (r.Completed ? r.Final.RmsPx.ToString("0.00") + " px" + (r.Success ? "" : " (above target)") : "failed"));
                }
                Finish(autoAdjust.Status + "\n" + string.Join("\n", lines), any);
            }));
        }

        private void OnWarpClicked()
        {
            if (boundSkin == null) {
                return;
            }
            IPMSDKCalibrationCamera camera = StartAction(warpMapBuilder.Target == PMSDKWarpMapTarget.PhysicalSpacing);
            if (camera == null) {
                return;
            }
            ApplyCameraRegion(boundSkin);
            controller.Undo.Record(controller, "warp-map");
            host.StartCoroutine(warpMapBuilder.BuildWarpMap(boundSkin, camera, new List<PMSDKSkin>(controller.Skins), delegate(PMSDKWarpMapResult result) {
                if (result.Surface != null && result.Surface.Valid) {
                    Debug.Log("[PMSDK] " + result.Surface.ToText());
                }
                Finish(result.Message, result.Success);
            }));
        }

        private void OnCalibrateAllClicked()
        {
            StartCalibrationFlow(new List<PMSDKSkin>(controller.Skins));
        }

        private void OnCalibrateSelectedClicked()
        {
            if (boundSkin != null) {
                StartCalibrationFlow(new List<PMSDKSkin> { boundSkin });
            }
        }

        private void StartCalibrationFlow(List<PMSDKSkin> skins)
        {
            if (Busy || skins.Count == 0 || DepthCameraFactory == null) {
                return;
            }
            SetButtons(false);
            flowRunning = true;
            controller.Undo.Record(controller, "calibrate-skins");
            host.StartCoroutine(CalibrationFlow(skins));
        }

        /// <summary>
        /// One-click calibration (D-055): calibrate the projector when there is no calibration (or the rig moved), then
        /// build a physical-spacing warp map for every skin. Skins are never created or moved; the map spreads each
        /// skin's content evenly over the real surface inside the skin's own outline.
        /// </summary>
        private IEnumerator CalibrationFlow(List<PMSDKSkin> skins)
        {
            PMSDKWarpMapTarget previousTarget = warpMapBuilder.Target;
            warpMapBuilder.Target = PMSDKWarpMapTarget.PhysicalSpacing;
            List<string> lines = new List<string>();
            bool recalibrated = false;
            bool changed = false;
            try {
                if (warpMapBuilder.ProjectorCalibration == null) {
                    IEnumerator calibrate = CalibrateProjectorStep(lines);
                    while (calibrate.MoveNext()) {
                        yield return calibrate.Current;
                    }
                    recalibrated = true;
                    if (warpMapBuilder.ProjectorCalibration == null) {
                        yield break;
                    }
                }
                for (int i = 0; i < skins.Count; i++) {
                    PMSDKSkin skin = skins[i];
                    if (skin == null) {
                        continue;
                    }
                    PMSDKWarpMapResult result = null;
                    for (int attempt = 0; attempt < 2; attempt++) {
                        statusLabel.text = "Skin " + (i + 1) + " / " + skins.Count + ": " + skin.DisplayName + "...";
                        ApplyCameraRegion(skin);
                        IEnumerator build = warpMapBuilder.BuildWarpMap(skin, DepthCameraFactory(), new List<PMSDKSkin>(controller.Skins),
                            delegate(PMSDKWarpMapResult r) { result = r; });
                        while (build.MoveNext()) {
                            yield return build.Current;
                        }
                        if (result == null || !result.CalibrationStale || recalibrated) {
                            break;
                        }
                        // The rig moved since the last calibration: calibrate once more and retry this skin.
                        IEnumerator calibrate = CalibrateProjectorStep(lines);
                        while (calibrate.MoveNext()) {
                            yield return calibrate.Current;
                        }
                        recalibrated = true;
                    }
                    if (result != null && result.Success) {
                        changed = true;
                        PMSDKSurfaceReport surface = result.Surface;
                        lines.Add(skin.DisplayName + ": " + (surface != null && surface.Valid
                            ? surface.RmsMm.ToString("0.0") + " mm RMS, cell " + surface.CellWidthMm.ToString("0") + " x " + surface.CellHeightMm.ToString("0") + " mm"
                            : "map built"));
                    } else {
                        lines.Add(skin.DisplayName + ": " + (result != null ? result.Message : "failed"));
                    }
                }
            } finally {
                warpMapBuilder.Target = previousTarget;
                flowRunning = false;
            }
            calibrationLabel.text = CalibrationText();
            Finish("Calibration done.\n" + string.Join("\n", lines), changed);
        }

        private IEnumerator CalibrateProjectorStep(List<string> lines)
        {
            statusLabel.text = "Calibrating the projector...";
            bool ok = false;
            string message = "";
            IEnumerator calibrate = surfaceFinder.CalibrateProjector(controller, DepthCameraFactory(), 1024, PMSDKProjectorCalibration.Folder,
                delegate(bool success, string text, PMSDKProjectorCalibration calibration) {
                    ok = success;
                    message = text;
                    if (success) {
                        warpMapBuilder.ProjectorCalibration = calibration;
                    }
                });
            while (calibrate.MoveNext()) {
                yield return calibrate.Current;
            }
            lines.Add(ok ? "Projector calibrated." : "Projector calibration failed: " + message);
        }

        private void OnCalibrateClicked()
        {
            IPMSDKCalibrationCamera camera = StartAction(true);
            if (camera == null) {
                return;
            }
            host.StartCoroutine(surfaceFinder.CalibrateProjector(controller, camera, 1024, PMSDKProjectorCalibration.Folder,
                delegate(bool ok, string message, PMSDKProjectorCalibration calibration) {
                    if (ok) {
                        warpMapBuilder.ProjectorCalibration = calibration;
                    }
                    calibrationLabel.text = CalibrationText();
                    Finish(message, false);
                }));
        }

        private string CalibrationText()
        {
            PMSDKProjectorCalibration calibration = warpMapBuilder.ProjectorCalibration;
            return calibration == null
                ? "Projector not calibrated (physical spacing uses OAK stereo depth)."
                : "Projector calibrated " + calibration.CreatedUtc + ": " + calibration.Describe() + ". Recalibrate after moving camera or projector.";
        }

        private void OnMeasureClicked()
        {
            if (boundSkin == null) {
                return;
            }
            IPMSDKCalibrationCamera camera = StartAction(true);
            if (camera == null) {
                return;
            }
            ApplyCameraRegion(boundSkin);
            host.StartCoroutine(warpMapBuilder.MeasureSurface(boundSkin, camera, new List<PMSDKSkin>(controller.Skins), delegate(PMSDKSurfaceReport report) {
                if (report.Valid) {
                    Debug.Log("[PMSDK] " + report.ToText());
                }
                Finish((report.Valid ? report.Summary() : report.Message) + " Report: " + warpMapBuilder.ReportFolder, false);
            }));
        }

        /// <summary>Limits warp maps / measurements to the camera region the skin was marked with, when it has one.</summary>
        private void ApplyCameraRegion(PMSDKSkin skin)
        {
            byte[] region;
            int regionWidth;
            int regionHeight;
            string regionError;
            if (PMSDKCameraMark.TryLoadRegion(skin, out region, out regionWidth, out regionHeight, out regionError)) {
                warpMapBuilder.SetCameraRegion(region, regionWidth, regionHeight);
            } else {
                warpMapBuilder.SetCameraRegion(null, 0, 0);
                if (!string.IsNullOrEmpty(regionError)) {
                    Debug.LogWarning("[PMSDK] Camera region of '" + skin.DisplayName + "' ignored: " + regionError);
                }
            }
        }

        private void OnAspectEdited(string value)
        {
            if (suppress) {
                return;
            }
            float aspect;
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out aspect) && aspect >= 0f) {
                warpMapBuilder.ContentAspect = aspect;
            }
            aspectField.SetTextWithoutNotify(warpMapBuilder.ContentAspect.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private void OnGridToggled(bool on)
        {
            MoveGrid(on ? boundSkin : null);
        }

        /// <summary>Shows the calibration grid on <paramref name="skin"/> only (null removes it).</summary>
        private void MoveGrid(PMSDKSkin skin)
        {
            if (gridSkin != null && gridSkin.TestPatternOverride == gridTexture) {
                gridSkin.TestPatternOverride = null;
            }
            gridSkin = null;
            if (skin == null) {
                return;
            }
            if (gridTexture == null) {
                gridTexture = PMSDKCalibrationGrid.Create(16, 16, 64);
            }
            skin.TestPatternOverride = gridTexture;
            gridSkin = skin;
        }

        private void OnFindClicked()
        {
            if (Busy) {
                return;
            }
            IPMSDKCalibrationCamera camera = DepthCameraFactory != null ? DepthCameraFactory() : null;
            if (camera == null) {
                statusLabel.text = "No depth camera available.";
                return;
            }
            SetButtons(false);
            controller.Undo.Record(controller, "find-surfaces");
            host.StartCoroutine(surfaceFinder.FindSurfaces(controller, camera, delegate(PMSDKSurfaceFinderResult result) {
                Finish(result.Message, result.Created.Count > 0);
            }));
        }

        private void OnPhotoClicked()
        {
            IPMSDKCalibrationCamera camera = StartAction();
            if (camera == null) {
                return;
            }
            photoRunning = true;
            host.StartCoroutine(TakePhoto(camera));
        }

        private IEnumerator TakePhoto(IPMSDKCalibrationCamera camera)
        {
            statusLabel.text = "Starting camera...";
            yield return null;
            string message;
            bool cameraStarted = false;
            try {
                cameraStarted = camera.Begin();
                byte[] frame = cameraStarted ? camera.CaptureLuminance() : null;
                if (frame == null) {
                    message = "No camera photo." + PMSDKAutoAdjust.CameraError(camera);
                } else {
                    Directory.CreateDirectory(PMSDKCameraMark.Folder);
                    PMSDKCameraMark.SavePhoto(PMSDKCameraMark.PhotoPath, frame, camera.Width, camera.Height);
                    message = "Saved " + PMSDKCameraMark.PhotoPath + " (" + camera.Width + " x " + camera.Height + "). Outline the surface in pure red " +
                              "and save it as " + PMSDKCameraMark.MarkedFileName + " in the same folder. Do not move the camera.";
                    Application.OpenURL(new Uri(PMSDKCameraMark.Folder).AbsoluteUri);
                }
            } catch (Exception ex) {
                message = "Could not save the camera photo: " + ex.Message;
            } finally {
                if (cameraStarted) {
                    camera.End();
                }
                photoRunning = false;
            }
            Finish(message, false);
        }

        private void OnMarkedClicked()
        {
            if (Busy) {
                return;
            }
            int width;
            int height;
            if (!PMSDKCameraMark.TryGetImageSize(PMSDKCameraMark.PhotoPath, out width, out height)) {
                statusLabel.text = "Take a camera photo first (" + PMSDKCameraMark.PhotoPath + ").";
                return;
            }
            byte[] mask;
            string error;
            if (!PMSDKCameraMark.TryLoadMask(PMSDKCameraMark.MarkedPath, width, height, out mask, out error)) {
                statusLabel.text = error;
                return;
            }
            IPMSDKCalibrationCamera camera = StartAction();
            if (camera == null) {
                return;
            }
            controller.Undo.Record(controller, "map-marked-surface");
            host.StartCoroutine(surfaceFinder.MapMarkedSurface(controller, camera, mask, width, height, null, delegate(PMSDKSurfaceFinderResult result) {
                string message = result.Message;
                if (result.Success) {
                    PMSDKSkin skin = result.Created[0];
                    try {
                        PMSDKCameraMark.SaveRegion(skin, mask, width, height);
                    } catch (Exception ex) {
                        message += " The camera region was not saved (" + ex.Message + "); Build warp map will use every lit pixel.";
                    }
                    controller.RefreshSkinList();
                    controller.SelectedSkinIndex = controller.Skins.IndexOf(skin);
                    message += " Next: Build warp map for straight content across folds.";
                }
                Finish(message, result.Success);
            }));
        }

        private void OnClearClicked()
        {
            if (boundSkin == null || Busy) {
                return;
            }
            controller.Undo.Record(controller, "clear-correction");
            boundSkin.ClearCorrection();
            boundSkin.WarpMapFile = null;
            statusLabel.text = "Correction and warp map cleared.";
            save?.Invoke();
            Bind(boundSkin);
        }

        private void OnSizeEdited(string value)
        {
            if (suppress || boundSkin == null) {
                return;
            }
            float width;
            float height;
            bool okWidth = float.TryParse(widthField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out width);
            bool okHeight = float.TryParse(heightField.text, NumberStyles.Float, CultureInfo.InvariantCulture, out height);
            controller.Undo.Record(controller, "facet-size");
            boundSkin.FacetWidthMm = okWidth ? width : 0f;
            boundSkin.FacetHeightMm = okHeight ? height : 0f;
        }

        private void OnExposureEdited(string value)
        {
            if (suppress) {
                return;
            }
            float ms;
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out ms) && ms >= 0f) {
                exposureMs = Mathf.Min(ms, 33f);
            }
            exposureField.SetTextWithoutNotify(exposureMs.ToString("0.##", CultureInfo.InvariantCulture));
        }

        private PMSDKOakCalibrationCamera CreateOakCamera(bool depth)
        {
            PMSDKOakCalibrationCamera camera = depth ? new PMSDKOakCalibrationCamera(1920, 15f, true) : new PMSDKOakCalibrationCamera();
            uint exposureUs = (uint)Mathf.RoundToInt(exposureMs * 1000f);
            if (exposureUs > 0) {
                camera.ManualExposureUs = exposureUs;
                camera.ManualIso = ExposureIso;
            }
            return camera;
        }

        private IPMSDKCalibrationCamera StartAction()
        {
            return StartAction(false);
        }

        private IPMSDKCalibrationCamera StartAction(bool needsDepth)
        {
            if (Busy) {
                return null;
            }
            Func<IPMSDKCalibrationCamera> factory = needsDepth ? DepthCameraFactory : CameraFactory;
            IPMSDKCalibrationCamera camera = factory != null ? factory() : null;
            if (camera == null) {
                statusLabel.text = needsDepth ? "No depth camera available." : "No calibration camera available.";
                return null;
            }
            SetButtons(false);
            return camera;
        }

        private void Finish(string message, bool changed)
        {
            statusLabel.text = message;
            if (changed) {
                save?.Invoke();
            }
            controller.RefreshSkinList();
            Bind(boundSkin);
        }

        private void SetButtons(bool enabled)
        {
            runButton.interactable = enabled;
            allButton.interactable = enabled;
            warpButton.interactable = enabled;
            clearButton.interactable = enabled;
            findButton.interactable = enabled;
            photoButton.interactable = enabled;
            markedButton.interactable = enabled;
            measureButton.interactable = enabled;
            calibrateButton.interactable = enabled;
            calibrateAllButton.interactable = enabled;
            calibrateSelectedButton.interactable = enabled;
        }

        private List<PMSDKSkin> QuadSkins()
        {
            List<PMSDKSkin> list = new List<PMSDKSkin>();
            foreach (PMSDKSkin skin in controller.Skins) {
                if (skin != null && (skin.Kind == PMSDKSkinShapeKind.Rectangle || skin.Kind == PMSDKSkinShapeKind.Ellipse)) {
                    list.Add(skin);
                }
            }
            return list;
        }

        private T GetOrAdd<T>() where T : Component
        {
            T component = host.GetComponent<T>();
            if (component == null) {
                component = host.gameObject.AddComponent<T>();
            }
            return component;
        }

        private static RectTransform NewRow(Transform parent, string name)
        {
            RectTransform row = PMSDKUi.NewRect(name, parent);
            PMSDKUi.SetLayout(row.gameObject, -1f, 30f, 1f, -1f);
            HorizontalLayoutGroup layout = PMSDKUi.AddHorizontal(row.gameObject, 0, 4f);
            layout.childForceExpandWidth = true;
            return row;
        }

        private static Button AddRowButton(Transform row, string label, UnityEngine.Events.UnityAction onClick)
        {
            Button button = PMSDKUi.AddButton(row, label, onClick);
            PMSDKUi.SetLayout(button.gameObject, -1f, 30f, 1f, -1f);
            return button;
        }

        private void OnStatusChanged(string text)
        {
            if (statusLabel != null) {
                statusLabel.text = text;
            }
        }
    }
}
