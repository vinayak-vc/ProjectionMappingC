using System;
using System.Collections.Generic;
using System.Globalization;
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
        private readonly Toggle snapToggle;
        private readonly InputField widthField;
        private readonly InputField heightField;
        private readonly Text statusLabel;
        private readonly Text correctionLabel;
        private PMSDKSkin boundSkin;
        private bool suppress;

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
            CameraFactory = delegate { return new PMSDKOakCalibrationCamera(); };
            DepthCameraFactory = delegate { return new PMSDKOakCalibrationCamera(1920, 15f, true); };
            autoAdjust = GetOrAdd<PMSDKAutoAdjust>();
            warpMapBuilder = GetOrAdd<PMSDKWarpMapBuilder>();
            surfaceFinder = GetOrAdd<PMSDKSurfaceFinder>();
            autoAdjust.StatusChanged += OnStatusChanged;
            warpMapBuilder.StatusChanged += OnStatusChanged;
            surfaceFinder.StatusChanged += OnStatusChanged;

            RectTransform root = PMSDKUi.NewRect("Auto Adjust", parent);
            PMSDKUi.AddVertical(root.gameObject, 0, 4f);
            ContentSizeFitter fit = root.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            PMSDKUi.AddDimLabel(root, "Measures the projection with the OAK-D and corrects it.");
            PMSDKUi.AddDimLabel(root, "Fix the camera, dim the room, close OAK Viewer.");

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

            RectTransform rowC = NewRow(root, "Find Surfaces Row");
            findButton = AddRowButton(rowC, "Find surfaces (depth)", OnFindClicked);

            correctionLabel = PMSDKUi.AddDimLabel(root, "");
            statusLabel = PMSDKUi.AddDimLabel(root, "");
            Bind(null);
        }

        /// <summary>Shows the state of <paramref name="skin"/> (null = nothing selected).</summary>
        public void Bind(PMSDKSkin skin)
        {
            suppress = true;
            boundSkin = skin;
            bool busy = Busy;
            bool quad = skin != null && (skin.Kind == PMSDKSkinShapeKind.Rectangle || skin.Kind == PMSDKSkinShapeKind.Ellipse);
            runButton.interactable = quad && !busy;
            warpButton.interactable = quad && !busy;
            allButton.interactable = !busy && QuadSkins().Count > 0;
            findButton.interactable = !busy;
            clearButton.interactable = skin != null && !busy && (skin.HasCorrection || !string.IsNullOrEmpty(skin.WarpMapFile));
            snapToggle.SetIsOnWithoutNotify(autoAdjust.SnapToFacetEdges);
            widthField.interactable = skin != null && !busy;
            heightField.interactable = skin != null && !busy;
            widthField.text = skin != null && skin.FacetWidthMm > 0f ? skin.FacetWidthMm.ToString("0", CultureInfo.InvariantCulture) : "";
            heightField.text = skin != null && skin.FacetHeightMm > 0f ? skin.FacetHeightMm.ToString("0", CultureInfo.InvariantCulture) : "";
            if (skin == null) {
                correctionLabel.text = "";
            } else if (!quad) {
                correctionLabel.text = "Auto adjust works on Rectangle and Ellipse surfaces.";
            } else {
                string text = skin.HasCorrection ? "Correction " + skin.CorrectionColumns + " x " + skin.CorrectionRows : "No correction";
                text += string.IsNullOrEmpty(skin.WarpMapFile) ? ", no warp map." : ", warp map on.";
                correctionLabel.text = text;
            }
            suppress = false;
        }

        private bool Busy
        {
            get { return autoAdjust.IsRunning || warpMapBuilder.IsRunning || surfaceFinder.IsRunning; }
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
            IPMSDKCalibrationCamera camera = StartAction();
            if (camera == null || boundSkin == null) {
                return;
            }
            controller.Undo.Record(controller, "warp-map");
            host.StartCoroutine(warpMapBuilder.BuildWarpMap(boundSkin, camera, new List<PMSDKSkin>(controller.Skins), delegate(PMSDKWarpMapResult result) {
                Finish(result.Message, result.Success);
            }));
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

        private IPMSDKCalibrationCamera StartAction()
        {
            if (Busy) {
                return null;
            }
            IPMSDKCalibrationCamera camera = CameraFactory != null ? CameraFactory() : null;
            if (camera == null) {
                statusLabel.text = "No calibration camera available.";
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
