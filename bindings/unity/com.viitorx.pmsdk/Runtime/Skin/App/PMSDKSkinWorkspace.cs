using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Artist-facing Skin Editor workspace (MadMapper-style): tool bar on top, a surface
    /// list + shape library on the left, the live projector preview in the middle and an
    /// inspector (surface properties + media picker) on the right. Drives the existing
    /// <see cref="PMSDKSkinEditorController"/> / <see cref="PMSDKSkinEditorInput"/>; it adds
    /// no new editing logic, only UX. Replaces the IMGUI HUD and the two older Canvas panels
    /// (they are disabled while this component is active).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PMSDKSkinEditorController))]
    public class PMSDKSkinWorkspace : MonoBehaviour
    {
        private const float TopBarHeight = 48f;
        private const float StatusBarHeight = 28f;
        private const float LeftWidth = 290f;
        private const float RightWidth = 350f;

        [SerializeField] private bool loadSavedSkinsOnStart = true;

        private PMSDKSkinEditorController controller;
        private PMSDKSkinEditorInput input;
        private PMSDKSkinOutput output;

        private Canvas canvas;
        private RawImage previewImage;
        private Text statusLeft;
        private Text statusRight;
        private Button toolSelect;
        private Button toolRect;
        private Button toolEllipse;
        private Button toolPolygon;
        private Button toolFreeform;
        private Button snapButton;
        private Button fullscreenButton;
        private Button leftToggle;
        private Button rightToggle;
        private Button focusButton;
        private RectTransform leftPanelRect;
        private RectTransform rightPanelRect;
        private RectTransform viewportAreaRect;
        private RectTransform rasterRect;
        private RectTransform topBarRect;
        private RectTransform statusBarRect;
        private bool focusMode;
        private bool barsHidden;
        private Image topBarImage;
        private Image statusBarImage;
        private bool leftDocked = true;
        private bool rightDocked = true;
        private bool leftDrawer;
        private bool rightDrawer;
        private const string FocusPrefKey = "PMSDK.FocusMode";
        private RectTransform surfaceListRoot;
        private RectTransform setListRoot;
        private InputField newSetField;
        private PMSDKDropdown setDropdown;

        private InputField nameField;
        private Text kindLabel;
        private Slider opacitySlider;
        private Text opacityValue;
        private Slider featherSlider;
        private Text featherValue;
        private PMSDKSkinMediaPanel mediaPanel;

        private PMSDKSkin boundSkin;
        private int listSignature = int.MinValue;
        private int lastSelectedIndex = int.MinValue;
        private bool suppress;
        private string savedText = "";

        private void OnEnable()
        {
            controller = GetComponent<PMSDKSkinEditorController>();
            input = GetComponent<PMSDKSkinEditorInput>();
            if (input == null) {
                input = gameObject.AddComponent<PMSDKSkinEditorInput>();
            }
            output = GetComponent<PMSDKSkinOutput>();
            if (output == null) {
                output = gameObject.AddComponent<PMSDKSkinOutput>();
            }
            DisableLegacyUi();
            PMSDKUi.EnsureEventSystem();
            BuildUi();
            if (GetComponent<PMSDKVideoConvertFlow>() == null) {
                gameObject.AddComponent<PMSDKVideoConvertFlow>();
            }
            PMSDKVideoConvertFlow.VideoReplaced += OnVideoReplaced;
            output.FullscreenChanged += OnFullscreenChanged;
        }

        private void OnVideoReplaced(PMSDKSkinContent content)
        {
            SaveNow();
        }

        private void Start()
        {
            if (loadSavedSkinsOnStart && PMSDKSkinStore.Exists()) {
                controller.LoadDefault();
                controller.Undo.Clear();
            }
            controller.RefreshSkinList();
            RefreshSetUi();
        }

        private void OnDisable()
        {
            PMSDKVideoConvertFlow.VideoReplaced -= OnVideoReplaced;
            if (output != null) {
                output.FullscreenChanged -= OnFullscreenChanged;
            }
            if (canvas != null) {
                Destroy(canvas.gameObject);
                canvas = null;
            }
            if (input != null) {
                input.ViewportRect = null;
            }
        }

        private void OnApplicationQuit()
        {
#if !UNITY_EDITOR
            // Standalone only: in the Unity Editor, leaving Play mode also raises this, and
            // would overwrite the saved set (shared with the built EXE) with test shapes.
            if (controller != null && controller.Skins.Count > 0) {
                controller.SaveDefault();
            }
#endif
        }

        private void DisableLegacyUi()
        {
            PMSDKSkinEditorHUD hud = GetComponent<PMSDKSkinEditorHUD>();
            if (hud != null) {
                hud.enabled = false;
            }
            PMSDKSkinEditorCanvasInspector inspector = GetComponent<PMSDKSkinEditorCanvasInspector>();
            if (inspector != null) {
                inspector.enabled = false;
            }
            PMSDKSkinEditorCanvasList list = GetComponent<PMSDKSkinEditorCanvasList>();
            if (list != null) {
                list.enabled = false;
            }
        }

        // ------------------------------------------------------------------ build

        private void BuildUi()
        {
            canvas = PMSDKUi.CreateCanvas("PMSDK Workspace", 0, 100);
            RectTransform root = (RectTransform)canvas.transform;
            Image backdrop = PMSDKUi.AddImage(PMSDKUi.NewRect("Backdrop", root).gameObject, PMSDKUi.Backdrop);
            PMSDKUi.Stretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);
            // Not a raycast target: only real controls may count as "pointer over UI".
            backdrop.raycastTarget = false;

            BuildTopBar(root);
            BuildLeftPanel(root);
            BuildViewport(root);
            BuildRightPanel(root);
            BuildStatusBar(root);

            // Draw order: viewport at the back, panels over it (they overlay in Focus view),
            // then the top and status bars.
            viewportAreaRect.SetSiblingIndex(1);
            topBarRect.SetAsLastSibling();
            statusBarRect.SetAsLastSibling();

            // One screen = the projector output IS the screen, so start in Focus view.
            int saved = PlayerPrefs.GetInt(FocusPrefKey, -1);
            focusMode = saved >= 0 ? saved == 1 : PMSDKSkinOutput.ExtraDisplayCount == 0;
            ApplyLayout();
        }

        // ------------------------------------------------------------------ layout / focus view

        /// <summary>Docked: panels sit beside the viewport. Focus view: the viewport fills the
        /// window and the panels become drawers that open over it on demand.</summary>
        private void ApplyLayout()
        {
            bool leftShown = focusMode ? leftDrawer : leftDocked;
            bool rightShown = focusMode ? rightDrawer : rightDocked;
            leftPanelRect.gameObject.SetActive(leftShown);
            rightPanelRect.gameObject.SetActive(rightShown);

            float dockLeft = (!focusMode && leftShown) ? LeftWidth : 0f;
            float dockRight = (!focusMode && rightShown) ? RightWidth : 0f;
            // Focus view: the raster fills the whole window edge to edge (what you see is exactly
            // what the projector shows, so points land where they should on the wall). The bars
            // float over it, translucent, and F4 hides them completely.
            float margin = focusMode ? 0f : 12f;
            float topInset = focusMode ? 0f : TopBarHeight + 12f;
            float bottomInset = focusMode ? 0f : StatusBarHeight + 12f;
            PMSDKUi.Place(viewportAreaRect, Vector2.zero, Vector2.one,
                new Vector2(dockLeft + margin, bottomInset),
                new Vector2(-(dockRight + margin), -topInset));

            bool barsShown = !focusMode || !barsHidden;
            topBarRect.gameObject.SetActive(barsShown);
            statusBarRect.gameObject.SetActive(barsShown);
            Color barColor = PMSDKUi.PanelHeader;
            barColor.a = focusMode ? 0.62f : 1f;
            topBarImage.color = barColor;
            statusBarImage.color = barColor;

            PMSDKUi.SetButtonActive(leftToggle, leftShown);
            PMSDKUi.SetButtonActive(rightToggle, rightShown);
            PMSDKUi.SetButtonActive(focusButton, focusMode);
        }

        private void SetFocusMode(bool value)
        {
            focusMode = value;
            barsHidden = false;
            if (value) {
                leftDrawer = false;
                rightDrawer = false;
            }
            PlayerPrefs.SetInt(FocusPrefKey, value ? 1 : 0);
            ApplyLayout();
        }

        private void OnFocusClicked()
        {
            SetFocusMode(!focusMode);
        }

        private void OnLeftToggle()
        {
            if (focusMode) {
                leftDrawer = !leftDrawer;
            } else {
                leftDocked = !leftDocked;
            }
            ApplyLayout();
        }

        private void OnRightToggle()
        {
            if (focusMode) {
                rightDrawer = !rightDrawer;
            } else {
                rightDocked = !rightDocked;
            }
            ApplyLayout();
        }

        private void OnFullscreenChanged(bool fullscreen)
        {
            if (input == null) {
                return;
            }
            // In the fullscreen output view the mouse maps through the fullscreen image, so
            // points can be dragged there as well.
            input.ViewportRect = fullscreen && output.FullscreenRect != null ? output.FullscreenRect : rasterRect;
        }

        private void BuildTopBar(RectTransform root)
        {
            RectTransform bar = PMSDKUi.NewRect("Top Bar", root);
            topBarRect = bar;
            topBarImage = PMSDKUi.AddImage(bar.gameObject, PMSDKUi.PanelHeader);
            PMSDKUi.Place(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -TopBarHeight), Vector2.zero);
            HorizontalLayoutGroup hlg = PMSDKUi.AddHorizontal(bar.gameObject, 8, 6f);
            hlg.childAlignment = TextAnchor.MiddleLeft;

            Text title = PMSDKUi.AddText(bar, "SKIN EDITOR", 15, PMSDKUi.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            PMSDKUi.SetLayout(title.gameObject, 130f, 32f, 0f, -1f);

            setDropdown = new PMSDKDropdown(bar, root, "Skin set", 230f);
            setDropdown.ItemPicked += SwitchSet;
            AddBarSpacer(bar, 10f);

            toolSelect = AddBarButton(bar, "Select", 74f, delegate { controller.PickTool(PMSDKSkinEditorController.Tool.Select); });
            toolRect = AddBarButton(bar, "Rectangle", 86f, delegate { controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle); });
            toolEllipse = AddBarButton(bar, "Ellipse", 74f, delegate { controller.PickTool(PMSDKSkinEditorController.Tool.Ellipse); });
            toolPolygon = AddBarButton(bar, "Polygon", 76f, delegate { controller.PickTool(PMSDKSkinEditorController.Tool.Polygon); });
            toolFreeform = AddBarButton(bar, "Freeform", 80f, delegate { controller.PickTool(PMSDKSkinEditorController.Tool.Freeform); });

            AddBarSpacer(bar, 14f);
            AddBarButton(bar, "Undo", 60f, delegate { controller.UndoLast(); });
            AddBarButton(bar, "Redo", 60f, delegate { controller.RedoLast(); });
            snapButton = AddBarButton(bar, "Snap: off", 90f, OnSnapClicked);

            RectTransform flex = PMSDKUi.NewRect("Flex", bar);
            PMSDKUi.SetLayout(flex.gameObject, 0f, -1f, 1f, -1f);

            leftToggle = AddBarButton(bar, "Surfaces", 86f, OnLeftToggle);
            rightToggle = AddBarButton(bar, "Inspector", 86f, OnRightToggle);
            focusButton = AddBarButton(bar, "Focus view (F3)", 130f, OnFocusClicked);
            AddBarSpacer(bar, 10f);
            AddBarButton(bar, "Save", 64f, OnSaveClicked);
            fullscreenButton = AddBarButton(bar, "Output view (F11)", 140f, OnFullscreenClicked);
            Button play = AddBarButton(bar, "Play mode  >", 120f, OnPlayModeClicked);
            PMSDKUi.SetButtonActive(play, true);
        }

        private static Button AddBarButton(Transform bar, string label, float width, UnityEngine.Events.UnityAction onClick)
        {
            Button b = PMSDKUi.AddButton(bar, label, onClick);
            PMSDKUi.SetLayout(b.gameObject, width, 32f, 0f, -1f);
            return b;
        }

        private static void AddBarSpacer(Transform bar, float width)
        {
            RectTransform s = PMSDKUi.NewRect("Spacer", bar);
            PMSDKUi.SetLayout(s.gameObject, width, -1f, 0f, -1f);
        }

        private void BuildLeftPanel(RectTransform root)
        {
            RectTransform panel = PMSDKUi.NewRect("Left Panel", root);
            leftPanelRect = panel;
            PMSDKUi.AddImage(panel.gameObject, PMSDKUi.Panel);
            PMSDKUi.Place(panel, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, StatusBarHeight), new Vector2(LeftWidth, -TopBarHeight));

            RectTransform content;
            RectTransform scroll = PMSDKUi.AddScrollView(panel, out content);
            PMSDKUi.Stretch(scroll, 0f, 0f, 0f, 0f);

            PMSDKUi.AddSectionHeader(content, "Surfaces");
            RectTransform addRow = PMSDKUi.NewRect("Add Row", content);
            PMSDKUi.SetLayout(addRow.gameObject, -1f, 36f, 1f, -1f);
            PMSDKUi.AddHorizontal(addRow.gameObject, 6, 4f);
            Button add = PMSDKUi.AddButton(addRow, "+ Add shape (N)", delegate { controller.AddDefaultShape(); });
            PMSDKUi.SetLayout(add.gameObject, -1f, 30f, 1f, -1f);

            surfaceListRoot = PMSDKUi.NewRect("Surface List", content);
            PMSDKUi.AddVertical(surfaceListRoot.gameObject, 4, 3f);
            ContentSizeFitter fit = surfaceListRoot.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            PMSDKUi.AddSpacer(content, 6f);
            PMSDKUi.AddSectionHeader(content, "Shape library");
            PMSDKUi.AddText(content, "  My skins", 13, PMSDKUi.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            PMSDKUi.AddDimLabel(content, "  Your saved skin sets. Click one to open it.");
            setListRoot = PMSDKUi.NewRect("Skin Set List", content);
            PMSDKUi.AddVertical(setListRoot.gameObject, 4, 3f);
            ContentSizeFitter setFit = setListRoot.gameObject.AddComponent<ContentSizeFitter>();
            setFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform newRow = PMSDKUi.NewRect("New Set Row", content);
            PMSDKUi.SetLayout(newRow.gameObject, -1f, 32f, 1f, -1f);
            HorizontalLayoutGroup newLayout = PMSDKUi.AddHorizontal(newRow.gameObject, 4, 4f);
            newLayout.childForceExpandWidth = false;
            newSetField = PMSDKUi.AddInputField(newRow, "", "New skin name", null);
            PMSDKUi.SetLayout(newSetField.gameObject, -1f, 28f, 1f, -1f);
            Button newButton = PMSDKUi.AddButton(newRow, "New", OnNewSetClicked);
            PMSDKUi.SetLayout(newButton.gameObject, 48f, 28f, 0f, -1f);
            Button copyButton = PMSDKUi.AddButton(newRow, "Copy", OnCopySetClicked);
            PMSDKUi.SetLayout(copyButton.gameObject, 52f, 28f, 0f, -1f);
            PMSDKUi.AddDimLabel(content, "  New = empty set.  Copy = duplicate the open set.");

            PMSDKUi.AddSpacer(content, 6f);
            PMSDKUi.AddText(content, "  Preset shapes", 13, PMSDKUi.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            PMSDKUi.AddDimLabel(content, "  Adds the shapes to the canvas.");
            for (int i = 0; i < PMSDKSkinPresets.All.Length; i++) {
                PMSDKSkinPresets.Preset preset = PMSDKSkinPresets.All[i];
                RectTransform row = PMSDKUi.NewRect("Preset Row", content);
                PMSDKUi.SetLayout(row.gameObject, -1f, 30f, 1f, -1f);
                PMSDKUi.AddHorizontal(row.gameObject, 4, 0f);
                Button b = PMSDKUi.AddButton(row, preset.DisplayName, delegate { OnPresetClicked(preset); });
                Text label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                PMSDKUi.Stretch(label.rectTransform, 10f, 0f, 4f, 0f);
                PMSDKUi.SetLayout(b.gameObject, -1f, 28f, 1f, -1f);
            }
        }

        private void BuildViewport(RectTransform root)
        {
            RectTransform area = PMSDKUi.NewRect("Viewport Area", root);
            viewportAreaRect = area;
            PMSDKUi.Place(area, Vector2.zero, Vector2.one,
                new Vector2(LeftWidth + 12f, StatusBarHeight + 12f),
                new Vector2(-(RightWidth + 12f), -(TopBarHeight + 12f)));

            RectTransform frame = PMSDKUi.NewRect("Preview", area);
            Image frameImage = PMSDKUi.AddImage(frame.gameObject, Color.black);
            frameImage.raycastTarget = false;
            AspectRatioFitter fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;

            RectTransform raw = PMSDKUi.NewRect("Raster", frame);
            PMSDKUi.Stretch(raw, 0f, 0f, 0f, 0f);
            previewImage = raw.gameObject.AddComponent<RawImage>();
            previewImage.color = Color.white;
            previewImage.raycastTarget = false;
            rasterRect = raw;
            input.ViewportRect = raw;
        }

        private void BuildRightPanel(RectTransform root)
        {
            RectTransform panel = PMSDKUi.NewRect("Right Panel", root);
            rightPanelRect = panel;
            PMSDKUi.AddImage(panel.gameObject, PMSDKUi.Panel);
            PMSDKUi.Place(panel, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-RightWidth, StatusBarHeight), new Vector2(0f, -TopBarHeight));

            RectTransform content;
            RectTransform scroll = PMSDKUi.AddScrollView(panel, out content);
            PMSDKUi.Stretch(scroll, 0f, 0f, 0f, 0f);

            PMSDKUi.AddSectionHeader(content, "Surface");
            RectTransform body = PMSDKUi.NewRect("Surface Body", content);
            PMSDKUi.AddVertical(body.gameObject, 10, 6f);
            ContentSizeFitter bodyFit = body.gameObject.AddComponent<ContentSizeFitter>();
            bodyFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            nameField = PMSDKUi.AddInputField(body, "", "Surface name", OnNameEdited);
            kindLabel = PMSDKUi.AddDimLabel(body, "");
            opacitySlider = AddLabeledSlider(body, "Opacity", 0f, 1f, OnOpacityChanged, out opacityValue);
            featherSlider = AddLabeledSlider(body, "Edge feather", 0f, 0.5f, OnFeatherChanged, out featherValue);

            RectTransform orderRow = PMSDKUi.NewRect("Order Row", body);
            PMSDKUi.SetLayout(orderRow.gameObject, -1f, 30f, 1f, -1f);
            HorizontalLayoutGroup orderLayout = PMSDKUi.AddHorizontal(orderRow.gameObject, 0, 4f);
            orderLayout.childForceExpandWidth = true;
            AddRowButton(orderRow, "Bring forward", delegate { controller.MoveSelectedZOrder(+1); });
            AddRowButton(orderRow, "Send back", delegate { controller.MoveSelectedZOrder(-1); });

            RectTransform actionRow = PMSDKUi.NewRect("Action Row", body);
            PMSDKUi.SetLayout(actionRow.gameObject, -1f, 30f, 1f, -1f);
            HorizontalLayoutGroup actionLayout = PMSDKUi.AddHorizontal(actionRow.gameObject, 0, 4f);
            actionLayout.childForceExpandWidth = true;
            AddRowButton(actionRow, "Duplicate", delegate { controller.DuplicateSelected(); });
            AddRowButton(actionRow, "Solo", delegate { controller.ToggleSoloSelected(); });
            Button delete = AddRowButton(actionRow, "Delete", delegate { controller.DeleteSelected(); });
            PMSDKUi.SetButtonColor(delete, new Color(0.45f, 0.17f, 0.15f, 1f));

            PMSDKUi.AddSpacer(content, 4f);
            PMSDKUi.AddSectionHeader(content, "Media - what plays on this surface");
            RectTransform mediaBody = PMSDKUi.NewRect("Media Body", content);
            PMSDKUi.AddVertical(mediaBody.gameObject, 10, 6f);
            ContentSizeFitter mediaFit = mediaBody.gameObject.AddComponent<ContentSizeFitter>();
            mediaFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            mediaPanel = new PMSDKSkinMediaPanel(mediaBody, false);
            mediaPanel.ContentChanged += OnMediaChanged;

            PMSDKUi.AddSpacer(content, 4f);
            PMSDKUi.AddSectionHeader(content, "Output background");
            RectTransform backgroundBody = PMSDKUi.NewRect("Background Body", content);
            PMSDKUi.AddVertical(backgroundBody.gameObject, 10, 6f);
            ContentSizeFitter backgroundFit = backgroundBody.gameObject.AddComponent<ContentSizeFitter>();
            backgroundFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            new PMSDKBackgroundPicker(backgroundBody, output);

            PMSDKUi.AddSpacer(content, 4f);
            PMSDKUi.AddSectionHeader(content, "Tips");
            RectTransform tips = PMSDKUi.NewRect("Tips Body", content);
            PMSDKUi.AddVertical(tips.gameObject, 10, 2f);
            ContentSizeFitter tipsFit = tips.gameObject.AddComponent<ContentSizeFitter>();
            tipsFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            PMSDKUi.AddDimLabel(tips, "Drag a corner handle to fit a face.");
            PMSDKUi.AddDimLabel(tips, "Shift/Ctrl+click: multi-select.   Arrows: nudge.");
            PMSDKUi.AddDimLabel(tips, "Polygon: click points, Enter or click first to close.");
            PMSDKUi.AddDimLabel(tips, "Ctrl+Z / Ctrl+Y undo-redo, Ctrl+S save.");
            PMSDKUi.AddDimLabel(tips, "Save, then press Play mode to show it.");
        }

        private static Button AddRowButton(Transform row, string label, UnityEngine.Events.UnityAction onClick)
        {
            Button b = PMSDKUi.AddButton(row, label, onClick);
            PMSDKUi.SetLayout(b.gameObject, -1f, 30f, 1f, -1f);
            return b;
        }

        private static Slider AddLabeledSlider(Transform parent, string label, float min, float max,
            UnityEngine.Events.UnityAction<float> onChange, out Text valueLabel)
        {
            RectTransform row = PMSDKUi.NewRect("Slider Row " + label, parent);
            PMSDKUi.SetLayout(row.gameObject, -1f, 24f, 1f, -1f);
            HorizontalLayoutGroup hlg = PMSDKUi.AddHorizontal(row.gameObject, 0, 8f);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            Text name = PMSDKUi.AddText(row, label, 13, PMSDKUi.TextMain, TextAnchor.MiddleLeft, FontStyle.Normal);
            PMSDKUi.SetLayout(name.gameObject, 96f, 24f, 0f, -1f);
            Slider slider = PMSDKUi.AddSlider(row, min, max, min, onChange);
            PMSDKUi.SetLayout(slider.gameObject, -1f, 22f, 1f, -1f);
            valueLabel = PMSDKUi.AddText(row, "0.00", 12, PMSDKUi.TextDim, TextAnchor.MiddleRight, FontStyle.Normal);
            PMSDKUi.SetLayout(valueLabel.gameObject, 40f, 24f, 0f, -1f);
            return slider;
        }

        private void BuildStatusBar(RectTransform root)
        {
            RectTransform bar = PMSDKUi.NewRect("Status Bar", root);
            statusBarRect = bar;
            statusBarImage = PMSDKUi.AddImage(bar.gameObject, PMSDKUi.PanelHeader);
            PMSDKUi.Place(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, StatusBarHeight));
            statusLeft = PMSDKUi.AddText(bar, "", 12, PMSDKUi.TextMain, TextAnchor.MiddleLeft, FontStyle.Normal);
            PMSDKUi.Stretch(statusLeft.rectTransform, 12f, 0f, 320f, 0f);
            statusRight = PMSDKUi.AddText(bar, "", 12, PMSDKUi.TextDim, TextAnchor.MiddleRight, FontStyle.Normal);
            PMSDKUi.Stretch(statusRight.rectTransform, 320f, 0f, 12f, 0f);
        }

        // ------------------------------------------------------------------ per-frame

        private void Update()
        {
            if (canvas == null || controller == null) {
                return;
            }
            if (previewImage != null && previewImage.texture == null && output != null && output.PreviewTexture != null) {
                previewImage.texture = output.PreviewTexture;
            }
            if (canvas.enabled == output.FullscreenPreview) {
                canvas.enabled = !output.FullscreenPreview;
            }

            if (!PMSDKUi.IsTextFieldFocused()) {
                if (Input.GetKeyDown(KeyCode.F11)) {
                    output.FullscreenPreview = !output.FullscreenPreview;
                }
                if (Input.GetKeyDown(KeyCode.F4) && focusMode && !output.FullscreenPreview) {
                    barsHidden = !barsHidden;
                    ApplyLayout();
                }
                if (Input.GetKeyDown(KeyCode.F3)) {
                    if (output.FullscreenPreview) {
                        output.FullscreenPreview = false;
                        SetFocusMode(true);
                    } else {
                        SetFocusMode(!focusMode);
                    }
                }
                if (Input.GetKeyDown(KeyCode.F10)) {
                    OnPlayModeClicked();
                    return;
                }
            }

            UpdateToolButtons();
            int signature = ComputeListSignature();
            if (signature != listSignature || controller.SelectedSkinIndex != lastSelectedIndex) {
                listSignature = signature;
                lastSelectedIndex = controller.SelectedSkinIndex;
                RebuildSurfaceList();
                BindInspector();
            }
            statusLeft.text = controller.StatusLine;
            statusRight.text = BuildRightStatus();
        }

        private void UpdateToolButtons()
        {
            PMSDKSkinEditorController.Tool tool = controller.CurrentTool;
            PMSDKUi.SetButtonActive(toolSelect, tool == PMSDKSkinEditorController.Tool.Select);
            PMSDKUi.SetButtonActive(toolRect, tool == PMSDKSkinEditorController.Tool.Rectangle);
            PMSDKUi.SetButtonActive(toolEllipse, tool == PMSDKSkinEditorController.Tool.Ellipse);
            PMSDKUi.SetButtonActive(toolPolygon, tool == PMSDKSkinEditorController.Tool.Polygon);
            PMSDKUi.SetButtonActive(toolFreeform, tool == PMSDKSkinEditorController.Tool.Freeform);
            bool snapOn = controller.SnapGridStep > 0f;
            PMSDKUi.SetButtonLabel(snapButton, snapOn ? "Snap: " + controller.SnapGridStep.ToString("0.00") : "Snap: off");
            PMSDKUi.SetButtonActive(snapButton, snapOn);
        }

        private int ComputeListSignature()
        {
            int h = controller.Skins.Count;
            for (int i = 0; i < controller.Skins.Count; i++) {
                PMSDKSkin s = controller.Skins[i];
                if (s == null) {
                    h = h * 31 + 7;
                    continue;
                }
                h = h * 31 + s.GetInstanceID();
                h = h * 31 + (s.DisplayName != null ? s.DisplayName.GetHashCode() : 0);
                h = h * 31 + (controller.IsVisible(s) ? 1 : 2);
                h = h * 31 + (controller.IsMuted(s) ? 3 : 4);
                PMSDKSkinContent c = s.GetComponent<PMSDKSkinContent>();
                if (c != null) {
                    h = h * 31 + (int)c.Mode;
                    string path = PMSDKSkinMedia.CurrentPath(c);
                    h = h * 31 + (path != null ? path.GetHashCode() : 0);
                }
            }
            return h;
        }

        private string BuildRightStatus()
        {
            int extra = PMSDKSkinOutput.ExtraDisplayCount;
            string display = extra > 0 ? "Output: Display 2 active" : "F3 Focus view  |  F4 hide bars  |  F11 Output view";
            return savedText.Length > 0 ? savedText + "   |   " + display : display;
        }

        // ------------------------------------------------------------------ surface list

        private void RebuildSurfaceList()
        {
            for (int i = surfaceListRoot.childCount - 1; i >= 0; i--) {
                Destroy(surfaceListRoot.GetChild(i).gameObject);
            }
            if (controller.Skins.Count == 0) {
                PMSDKUi.AddDimLabel(surfaceListRoot, "  No surfaces yet. Pick a tool and drag on the canvas,");
                PMSDKUi.AddDimLabel(surfaceListRoot, "  or add one from the Shape library below.");
                return;
            }
            for (int i = 0; i < controller.Skins.Count; i++) {
                PMSDKSkin skin = controller.Skins[i];
                if (skin == null) {
                    continue;
                }
                AddSurfaceRow(i, skin);
            }
        }

        private void AddSurfaceRow(int index, PMSDKSkin skin)
        {
            bool selected = index == controller.SelectedSkinIndex;
            bool multi = controller.MultiSelectedIndices.Contains(index);
            RectTransform row = PMSDKUi.NewRect("Surface Row", surfaceListRoot);
            PMSDKUi.SetLayout(row.gameObject, -1f, 44f, 1f, -1f);
            HorizontalLayoutGroup hlg = PMSDKUi.AddHorizontal(row.gameObject, 0, 3f);
            hlg.childForceExpandWidth = false;

            bool visible = controller.IsVisible(skin);
            PMSDKSkin captured = skin;
            Button eye = PMSDKUi.AddButton(row, visible ? "●" : "○", delegate { controller.SetSkinVisibility(captured, !controller.IsVisible(captured)); });
            PMSDKUi.SetLayout(eye.gameObject, 34f, 44f, 0f, -1f);

            PMSDKSkinContent content = skin.GetComponent<PMSDKSkinContent>();
            string media = PMSDKSkinMedia.Describe(content);
            string title = (index + 1).ToString() + "  " + skin.DisplayName;
            int capturedIndex = index;
            Button select = PMSDKUi.AddButton(row, title + "\n" + media, delegate {
                controller.SelectedSkinIndex = capturedIndex;
                controller.SelectedCornerIndex = 0;
            });
            PMSDKUi.SetLayout(select.gameObject, -1f, 44f, 1f, -1f);
            Text label = select.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 12;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            PMSDKUi.Stretch(label.rectTransform, 10f, 0f, 4f, 0f);
            if (selected) {
                PMSDKUi.SetButtonActive(select, true);
            } else if (multi) {
                PMSDKUi.SetButtonColor(select, PMSDKUi.AccentDim);
            }
            if (!visible) {
                label.color = PMSDKUi.TextDim;
            }
        }

        // ------------------------------------------------------------------ inspector binding

        private void BindInspector()
        {
            suppress = true;
            PMSDKSkin skin = controller.Selected;
            boundSkin = skin;
            bool has = skin != null;
            nameField.interactable = has;
            opacitySlider.interactable = has;
            featherSlider.interactable = has;
            if (has) {
                if (!nameField.isFocused) {
                    nameField.text = skin.DisplayName;
                }
                int vertexCount = skin.Vertices != null ? skin.Vertices.Count : 0;
                kindLabel.text = skin.Kind + "  -  " + vertexCount + " points";
                opacitySlider.SetValueWithoutNotify(skin.Opacity);
                opacityValue.text = skin.Opacity.ToString("0.00");
                featherSlider.SetValueWithoutNotify(skin.SoftMask);
                featherValue.text = skin.SoftMask.ToString("0.00");
                mediaPanel.Bind(skin.GetComponent<PMSDKSkinContent>());
            } else {
                nameField.text = "";
                kindLabel.text = "Nothing selected";
                mediaPanel.Bind(null);
            }
            suppress = false;
        }

        private void OnNameEdited(string value)
        {
            if (suppress || boundSkin == null || string.IsNullOrWhiteSpace(value)) {
                return;
            }
            boundSkin.DisplayName = value.Trim();
        }

        private void OnOpacityChanged(float value)
        {
            if (suppress || boundSkin == null) {
                return;
            }
            boundSkin.Opacity = value;
            opacityValue.text = value.ToString("0.00");
        }

        private void OnFeatherChanged(float value)
        {
            if (suppress || boundSkin == null) {
                return;
            }
            boundSkin.SoftMask = value;
            featherValue.text = value.ToString("0.00");
        }

        private void OnMediaChanged(PMSDKSkinContent content)
        {
            SaveNow();
        }

        // ------------------------------------------------------------------ top bar actions

        private void OnSnapClicked()
        {
            if (controller.SnapGridStep <= 0f) {
                controller.SnapGridStep = 0.05f;
            } else if (controller.SnapGridStep < 0.075f) {
                controller.SnapGridStep = 0.10f;
            } else {
                controller.SnapGridStep = 0f;
            }
        }

        private void OnSaveClicked()
        {
            SaveNow();
            RefreshSetUi();
        }

        private void OnFullscreenClicked()
        {
            output.FullscreenPreview = true;
        }

        private void OnPlayModeClicked()
        {
            SaveNow();
            PMSDKSkinApp.Switch(PMSDKSkinAppMode.Player);
        }

        private void OnPresetClicked(PMSDKSkinPresets.Preset preset)
        {
            controller.ApplyPreset(preset, false);
        }

        // ------------------------------------------------------------------ skin sets

        private void RefreshSetUi()
        {
            List<string> names = PMSDKSkinStore.ListNames();
            string active = PMSDKSkinStore.ActiveName;
            if (!names.Contains(active)) {
                names.Add(active);
                names.Sort(StringComparer.OrdinalIgnoreCase);
            }
            setDropdown.SetItems(names, active);

            for (int i = setListRoot.childCount - 1; i >= 0; i--) {
                Destroy(setListRoot.GetChild(i).gameObject);
            }
            for (int i = 0; i < names.Count; i++) {
                string name = names[i];
                bool isActive = string.Equals(name, active, StringComparison.OrdinalIgnoreCase);
                Button b = PMSDKUi.AddButton(setListRoot, name + (isActive ? "   (open)" : ""), delegate { SwitchSet(name); });
                Text label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                PMSDKUi.Stretch(label.rectTransform, 10f, 0f, 4f, 0f);
                PMSDKUi.SetLayout(b.gameObject, -1f, 30f, 1f, -1f);
                PMSDKUi.SetButtonActive(b, isActive);
            }
        }

        private void SwitchSet(string name)
        {
            string clean = PMSDKSkinStore.SanitizeName(name);
            if (clean.Length == 0 || string.Equals(clean, PMSDKSkinStore.ActiveName, StringComparison.OrdinalIgnoreCase)) {
                return;
            }
            if (!PMSDKSkinStore.Exists(clean)) {
                savedText = "Skin set not found: " + clean;
                RefreshSetUi();
                return;
            }
            SaveNow();
            PMSDKSkinStore.ActiveName = clean;
            controller.LoadDefault();
            controller.Undo.Clear();
            controller.RefreshSkinList();
            savedText = "Opened '" + clean + "'";
            RefreshSetUi();
        }

        private void OnNewSetClicked()
        {
            string clean = PMSDKSkinStore.SanitizeName(newSetField.text);
            if (!ValidateNewSetName(clean)) {
                return;
            }
            SaveNow();
            if (!PMSDKSkinStore.CreateEmpty(clean)) {
                savedText = "Could not create '" + clean + "'";
                return;
            }
            PMSDKSkinStore.ActiveName = clean;
            controller.LoadDefault();
            controller.Undo.Clear();
            controller.RefreshSkinList();
            newSetField.text = "";
            savedText = "Created '" + clean + "'";
            RefreshSetUi();
        }

        private void OnCopySetClicked()
        {
            string clean = PMSDKSkinStore.SanitizeName(newSetField.text);
            if (!ValidateNewSetName(clean)) {
                return;
            }
            if (!controller.SaveTo(PMSDKSkinStore.PathFor(clean))) {
                savedText = "Could not copy to '" + clean + "'";
                return;
            }
            PMSDKSkinStore.ActiveName = clean;
            newSetField.text = "";
            savedText = "Copied to '" + clean + "' (now open)";
            RefreshSetUi();
        }

        private bool ValidateNewSetName(string clean)
        {
            if (clean.Length == 0) {
                savedText = "Type a name for the skin set first.";
                return false;
            }
            if (PMSDKSkinStore.Exists(clean)) {
                savedText = "A skin set called '" + clean + "' already exists.";
                return false;
            }
            return true;
        }

        private void SaveNow()
        {
            if (controller.SaveDefault()) {
                savedText = "Saved " + DateTime.Now.ToString("HH:mm:ss");
            } else {
                savedText = "SAVE FAILED - see log";
            }
        }
    }
}
