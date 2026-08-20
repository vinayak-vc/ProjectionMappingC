using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// UGUI-based skin inspector panel (M-Sk4 v1.4 polish). Replaces the IMGUI
    /// inspector section of <see cref="PMSDKSkinEditorHUD"/>: proper Text labels,
    /// InputField for name, Sliders for opacity / feather / per-edge, Button actions.
    /// The rest of the HUD (toolbar, skin list, status/help) stays IMGUI — this
    /// component only overrides the RIGHT PANEL. When present, it auto-flips the sibling
    /// IMGUI HUD's <see cref="PMSDKSkinEditorHUD.DrawInspector"/> off (and restores on disable).
    ///
    /// Builds the entire Canvas hierarchy in code so the package needs no prefab
    /// assets. Uses legacy <see cref="Text"/> + <see cref="InputField"/> (not TMP)
    /// so there's no dependency on the TextMeshPro package.
    /// </summary>
    [DisallowMultipleComponent]
    public class PMSDKSkinEditorCanvasInspector : MonoBehaviour
    {
        public PMSDKSkinEditorController Controller;
        public int TargetDisplay = 0; // Display 1 = 0
        public int PanelWidth = 320;
        public int PanelPadding = 12;
        public float LabelWidth = 90f;

        // ---- runtime references (created on OnEnable) ----
        private Canvas canvas;
        private RectTransform panel;
        private InputField nameField;
        private Text kindLabel;
        private Text vertsLabel;
        private Slider opacitySlider;   private Text opacityValueLabel;
        private Slider featherSlider;   private Text featherValueLabel;
        private Toggle perEdgeToggle;
        private GameObject perEdgeGroup;
        private Slider ftSlider, frSlider, fbSlider, flSlider;
        private Text ftLabel, frLabel, fbLabel, flLabel;
        private Text zLabel;
        private Text emptyLabel;
        private PMSDKSkinEditorHUD imguiHud;
        private bool restoreImguiInspectorOnDisable;

        // Track selection to avoid rebinding fields every frame.
        private PMSDKSkin lastSelected;

        // Suppress the value-changed callbacks while we're rebinding the fields from
        // the newly-selected skin's state (otherwise we'd immediately overwrite it).
        private bool suppressCallbacks;

        private void OnEnable()
        {
            if (Controller == null) Controller = GetComponent<PMSDKSkinEditorController>();
            EnsureEventSystem();
            BuildCanvas();
            RebindImguiHud(hide: true);
        }

        private void OnDisable()
        {
            RebindImguiHud(hide: false);
            if (canvas != null)
            {
                if (Application.isEditor && !Application.isPlaying) DestroyImmediate(canvas.gameObject);
                else Destroy(canvas.gameObject);
                canvas = null;
            }
        }

        private void Update()
        {
            if (canvas == null || Controller == null) return;
            var sel = Controller.Selected;
            if (sel != lastSelected)
            {
                lastSelected = sel;
                RebindFieldsFromSkin(sel);
            }
            SyncDynamicLabels(sel);
        }

        // ---- IMGUI-HUD interop ----

        private void RebindImguiHud(bool hide)
        {
            if (imguiHud == null) imguiHud = GetComponent<PMSDKSkinEditorHUD>();
            if (imguiHud == null) return;
            if (hide)
            {
                restoreImguiInspectorOnDisable = imguiHud.DrawInspector;
                imguiHud.DrawInspector = false;
            }
            else if (restoreImguiInspectorOnDisable)
            {
                imguiHud.DrawInspector = true;
            }
        }

        // ---- canvas construction ----

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("PMSDK EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("PMSDK Canvas Inspector");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = TargetDisplay;
            canvas.sortingOrder = 100; // above stray HUD elements
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Right-side panel.
            panel = CreateChild(canvasGO.transform, "Inspector Panel",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f))
                .GetComponent<RectTransform>();
            panel.sizeDelta = new Vector2(PanelWidth, 640);
            panel.anchoredPosition = new Vector2(-PanelPadding, 0f);

            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);

            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 6;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fit = panel.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddHeader(panel, "Skin Inspector");

            emptyLabel = AddLabel(panel, "(no skin selected)");
            emptyLabel.color = new Color(0.75f, 0.75f, 0.75f, 1f);

            AddSpacer(panel, 4);
            nameField = AddInputField(panel, "Name", "", OnNameChanged);
            kindLabel = AddLabel(panel, "Kind: —");
            vertsLabel = AddLabel(panel, "Verts: —");

            AddSpacer(panel, 4);
            (opacitySlider, opacityValueLabel) = AddLabeledSlider(panel, "Opacity", 0f, 1f, 1f, OnOpacityChanged);
            (featherSlider, featherValueLabel) = AddLabeledSlider(panel, "Feather", 0f, 0.5f, 0f, OnFeatherChanged);

            perEdgeToggle = AddToggle(panel, "Per-edge feather (Ellipse)", false, OnPerEdgeToggled);
            perEdgeGroup = new GameObject("PerEdgeGroup");
            perEdgeGroup.transform.SetParent(panel, worldPositionStays: false);
            var pRT = perEdgeGroup.AddComponent<RectTransform>();
            pRT.sizeDelta = new Vector2(0, 0);
            var pVLG = perEdgeGroup.AddComponent<VerticalLayoutGroup>();
            pVLG.spacing = 4;
            pVLG.padding = new RectOffset(16, 0, 4, 4);
            pVLG.childControlWidth = true;
            pVLG.childForceExpandWidth = true;
            pVLG.childForceExpandHeight = false;
            var pFit = perEdgeGroup.AddComponent<ContentSizeFitter>();
            pFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var pTr = (RectTransform)perEdgeGroup.transform;
            (ftSlider, ftLabel) = AddLabeledSlider(pTr, "T", 0f, 0.5f, 0f, v => OnPerEdgeChanged(0, v));
            (frSlider, frLabel) = AddLabeledSlider(pTr, "R", 0f, 0.5f, 0f, v => OnPerEdgeChanged(1, v));
            (fbSlider, fbLabel) = AddLabeledSlider(pTr, "B", 0f, 0.5f, 0f, v => OnPerEdgeChanged(2, v));
            (flSlider, flLabel) = AddLabeledSlider(pTr, "L", 0f, 0.5f, 0f, v => OnPerEdgeChanged(3, v));
            perEdgeGroup.SetActive(false);

            AddSpacer(panel, 4);
            zLabel = AddLabel(panel, "Z: 0");
            var zRow = AddRow(panel, "z-row");
            AddButton(zRow, "Back  [", () => Controller.MoveSelectedZOrder(-1));
            AddButton(zRow, "Fwd  ]", () => Controller.MoveSelectedZOrder(+1));

            var actionsRow = AddRow(panel, "actions1");
            AddButton(actionsRow, "Duplicate", () => Controller.DuplicateSelected());
            AddButton(actionsRow, "Solo [X]", () => Controller.ToggleSoloSelected());

            var actions2 = AddRow(panel, "actions2");
            AddButton(actions2, "Pattern >", () => Controller.CycleTestPatternSelected(+1));
            AddButton(actions2, "Delete", () => Controller.DeleteSelected());

            RebindFieldsFromSkin(Controller != null ? Controller.Selected : null);
        }

        // ---- rebinding from skin state ----

        private void RebindFieldsFromSkin(PMSDKSkin s)
        {
            suppressCallbacks = true;
            try
            {
                bool has = s != null;
                emptyLabel.gameObject.SetActive(!has);
                nameField.gameObject.SetActive(has);
                kindLabel.gameObject.SetActive(has);
                vertsLabel.gameObject.SetActive(has);
                opacitySlider.transform.parent.gameObject.SetActive(has);
                featherSlider.transform.parent.gameObject.SetActive(has);
                perEdgeToggle.gameObject.SetActive(has && s != null && s.Kind == PMSDKSkinShapeKind.Ellipse);
                perEdgeGroup.SetActive(has && s != null && s.Kind == PMSDKSkinShapeKind.Ellipse && s.PerEdgeFeatherEnabled);
                zLabel.gameObject.SetActive(has);

                if (!has) return;

                nameField.text = s.DisplayName ?? "";
                opacitySlider.value = Mathf.Clamp01(s.Opacity);
                featherSlider.value = Mathf.Clamp(s.SoftMask, 0f, 0.5f);
                perEdgeToggle.isOn = s.PerEdgeFeatherEnabled;
                ftSlider.value = Mathf.Clamp(s.EdgeFeather.x, 0f, 0.5f);
                frSlider.value = Mathf.Clamp(s.EdgeFeather.y, 0f, 0.5f);
                fbSlider.value = Mathf.Clamp(s.EdgeFeather.z, 0f, 0.5f);
                flSlider.value = Mathf.Clamp(s.EdgeFeather.w, 0f, 0.5f);
            }
            finally { suppressCallbacks = false; }
        }

        private void SyncDynamicLabels(PMSDKSkin s)
        {
            if (s == null) return;
            kindLabel.text  = $"Kind: {s.Kind}";
            vertsLabel.text = $"Verts: {s.Vertices?.Count ?? 0}";
            opacityValueLabel.text = s.Opacity.ToString("F2");
            featherValueLabel.text = s.SoftMask.ToString("F2");
            zLabel.text = $"Z: {s.ZOrder}";
            if (s.Kind == PMSDKSkinShapeKind.Ellipse && s.PerEdgeFeatherEnabled)
            {
                ftLabel.text = "T " + s.EdgeFeather.x.ToString("F2");
                frLabel.text = "R " + s.EdgeFeather.y.ToString("F2");
                fbLabel.text = "B " + s.EdgeFeather.z.ToString("F2");
                flLabel.text = "L " + s.EdgeFeather.w.ToString("F2");
            }
        }

        // ---- input handlers ----

        private void OnNameChanged(string v)
        {
            if (suppressCallbacks) return;
            var s = Controller?.Selected;
            if (s != null) s.DisplayName = v;
        }
        private void OnOpacityChanged(float v)
        {
            if (suppressCallbacks) return;
            var s = Controller?.Selected;
            if (s != null) s.Opacity = v;
        }
        private void OnFeatherChanged(float v)
        {
            if (suppressCallbacks) return;
            var s = Controller?.Selected;
            if (s != null) s.SoftMask = v;
        }
        private void OnPerEdgeToggled(bool on)
        {
            if (suppressCallbacks) return;
            var s = Controller?.Selected;
            if (s != null) { s.PerEdgeFeatherEnabled = on; perEdgeGroup.SetActive(on); }
        }
        private void OnPerEdgeChanged(int idx, float v)
        {
            if (suppressCallbacks) return;
            var s = Controller?.Selected;
            if (s == null) return;
            var f = s.EdgeFeather;
            switch (idx) { case 0: f.x = v; break; case 1: f.y = v; break; case 2: f.z = v; break; case 3: f.w = v; break; }
            s.EdgeFeather = f;
        }

        // ---- UI factory helpers ----

        public bool CanvasBuilt => canvas != null;

        /// <summary>Rebuild the canvas + rebind IMGUI HUD from scratch. Used by tests to
        /// force a deterministic build after Controller is assigned (in EditMode the
        /// enable/disable timing around AddComponent can race with field assignment).</summary>
        public void RebuildForTests()
        {
            OnDisable();
            OnEnable();
        }
        public InputField NameField => nameField;
        public Slider OpacitySlider => opacitySlider;
        public Slider FeatherSlider => featherSlider;
        public Toggle PerEdgeToggle => perEdgeToggle;

        private static GameObject CreateChild(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, worldPositionStays: false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            return go;
        }

        private static void AddHeader(Transform parent, string text)
        {
            var lbl = AddLabel(parent, text);
            lbl.fontStyle = FontStyle.Bold;
            lbl.fontSize += 2;
            lbl.color = new Color(1f, 0.85f, 0.05f, 1f);
        }

        private static Text AddLabel(Transform parent, string text)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 14;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 22;
            return t;
        }

        private static void AddSpacer(Transform parent, float h)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = h;
            le.preferredHeight = h;
        }

        private static InputField AddInputField(Transform parent, string label,
            string initial, System.Action<string> onChanged)
        {
            var row = AddRow(parent, "row:" + label);
            var lbl = AddLabel(row, label);
            lbl.rectTransform.sizeDelta = new Vector2(60, 22);

            var inputGO = new GameObject("Input", typeof(RectTransform));
            inputGO.transform.SetParent(row, worldPositionStays: false);
            var img = inputGO.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(inputGO.transform, worldPositionStays: false);
            var textComp = textGO.AddComponent<Text>();
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.fontSize = 14;
            textComp.color = Color.white;
            textComp.alignment = TextAnchor.MiddleLeft;
            textComp.supportRichText = false;
            var textRT = (RectTransform)textGO.transform;
            textRT.anchorMin = Vector2.zero; textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(6, 0); textRT.offsetMax = new Vector2(-6, 0);

            var input = inputGO.AddComponent<InputField>();
            input.textComponent = textComp;
            input.text = initial;
            input.onValueChanged.AddListener(v => onChanged?.Invoke(v));

            var le = inputGO.AddComponent<LayoutElement>();
            le.minHeight = 24;
            le.preferredHeight = 24;
            le.flexibleWidth = 1;
            return input;
        }

        private static (Slider slider, Text valueLabel) AddLabeledSlider(
            Transform parent, string label, float min, float max, float initial,
            System.Action<float> onChanged)
        {
            var row = AddRow(parent, "row:" + label);
            var lbl = AddLabel(row, label);
            lbl.rectTransform.sizeDelta = new Vector2(70, 22);

            var sliderGO = new GameObject("Slider", typeof(RectTransform));
            sliderGO.transform.SetParent(row, worldPositionStays: false);
            var bg = sliderGO.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            var slider = sliderGO.AddComponent<Slider>();

            // Fill area / fill.
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGO.transform, worldPositionStays: false);
            var faRT = (RectTransform)fillArea.transform;
            faRT.anchorMin = new Vector2(0, 0.25f);
            faRT.anchorMax = new Vector2(1, 0.75f);
            faRT.offsetMin = new Vector2(0, 0);
            faRT.offsetMax = new Vector2(0, 0);
            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, worldPositionStays: false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(1f, 0.85f, 0.05f, 1f);
            var fillRT = (RectTransform)fill.transform;
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
            slider.fillRect = fillRT;

            // Handle.
            var handleArea = new GameObject("HandleSlideArea", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGO.transform, worldPositionStays: false);
            var haRT = (RectTransform)handleArea.transform;
            haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
            haRT.offsetMin = new Vector2(6, 0); haRT.offsetMax = new Vector2(-6, 0);
            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, worldPositionStays: false);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            var handleRT = (RectTransform)handle.transform;
            handleRT.sizeDelta = new Vector2(10, 0);
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImg;

            slider.minValue = min; slider.maxValue = max; slider.value = initial;
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));

            var le = sliderGO.AddComponent<LayoutElement>();
            le.minHeight = 20; le.preferredHeight = 20; le.flexibleWidth = 1;

            // Value readout at the right.
            var valLabel = AddLabel(row, initial.ToString("F2"));
            valLabel.rectTransform.sizeDelta = new Vector2(48, 22);
            valLabel.alignment = TextAnchor.MiddleRight;
            return (slider, valLabel);
        }

        private static Toggle AddToggle(Transform parent, string label, bool initial,
            System.Action<bool> onChanged)
        {
            var row = AddRow(parent, "row:" + label);
            var box = new GameObject("Box", typeof(RectTransform));
            box.transform.SetParent(row, worldPositionStays: false);
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            ((RectTransform)box.transform).sizeDelta = new Vector2(18, 18);

            var check = new GameObject("Check", typeof(RectTransform));
            check.transform.SetParent(box.transform, worldPositionStays: false);
            var checkImg = check.AddComponent<Image>();
            checkImg.color = new Color(1f, 0.85f, 0.05f, 1f);
            var checkRT = (RectTransform)check.transform;
            checkRT.anchorMin = new Vector2(0.15f, 0.15f);
            checkRT.anchorMax = new Vector2(0.85f, 0.85f);
            checkRT.offsetMin = Vector2.zero; checkRT.offsetMax = Vector2.zero;

            var toggle = box.AddComponent<Toggle>();
            toggle.graphic = checkImg;
            toggle.isOn = initial;
            toggle.onValueChanged.AddListener(v => onChanged?.Invoke(v));

            var lbl = AddLabel(row, label);
            lbl.rectTransform.sizeDelta = new Vector2(0, 22);
            return toggle;
        }

        private static Transform AddRow(Transform parent, string name)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, worldPositionStays: false);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 26;
            le.preferredHeight = 26;
            return row.transform;
        }

        private static void AddButton(Transform parent, string label, System.Action onClick)
        {
            var btn = new GameObject("Btn:" + label, typeof(RectTransform));
            btn.transform.SetParent(parent, worldPositionStays: false);
            var img = btn.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            var b = btn.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick?.Invoke());
            var le = btn.AddComponent<LayoutElement>();
            le.minHeight = 24; le.preferredHeight = 24; le.flexibleWidth = 1;
            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(btn.transform, worldPositionStays: false);
            var t = textGO.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 13;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            var trt = (RectTransform)textGO.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        }
    }
}
