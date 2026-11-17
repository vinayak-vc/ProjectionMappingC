using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Small code-built UGUI kit (legacy Text/InputField, no prefabs, no TextMeshPro) that
    /// gives the Skin Workspace and Skin Player one consistent dark, MadMapper-inspired look.
    /// </summary>
    public static class PMSDKUi
    {
        // ---- theme ----
        public static readonly Color Backdrop = new Color(0.086f, 0.086f, 0.094f, 1f);
        public static readonly Color Panel = new Color(0.141f, 0.141f, 0.153f, 1f);
        public static readonly Color PanelHeader = new Color(0.188f, 0.188f, 0.204f, 1f);
        public static readonly Color Field = new Color(0.075f, 0.075f, 0.082f, 1f);
        public static readonly Color ButtonBase = new Color(0.239f, 0.239f, 0.259f, 1f);
        public static readonly Color Accent = new Color(0.961f, 0.639f, 0.137f, 1f);
        public static readonly Color AccentDim = new Color(0.45f, 0.30f, 0.07f, 1f);
        public static readonly Color TextMain = new Color(0.86f, 0.86f, 0.88f, 1f);
        public static readonly Color TextDim = new Color(0.55f, 0.55f, 0.58f, 1f);
        public static readonly Color TextOnAccent = new Color(0.08f, 0.06f, 0.02f, 1f);
        public static readonly Color Danger = new Color(0.86f, 0.28f, 0.24f, 1f);
        public static readonly Color Good = new Color(0.36f, 0.78f, 0.45f, 1f);

        private static Font cachedFont;

        public static Font GetFont()
        {
            if (cachedFont == null) {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return cachedFont;
        }

        // ---- event system ----

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) {
                return;
            }
            GameObject go = new GameObject("PMSDK EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        /// <summary>True while an InputField has keyboard focus (so global hotkeys must not fire).</summary>
        public static bool IsTextFieldFocused()
        {
            EventSystem es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) {
                return false;
            }
            InputField field = es.currentSelectedGameObject.GetComponent<InputField>();
            return field != null && field.isFocused;
        }

        // ---- canvas ----

        public static Canvas CreateCanvas(string name, int targetDisplay, int sortingOrder)
        {
            GameObject go = new GameObject(name);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = targetDisplay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        // ---- rects ----

        public static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float left, float top, float right, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        public static Image AddImage(GameObject go, Color color)
        {
            Image image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static LayoutElement SetLayout(GameObject go, float prefWidth, float prefHeight, float flexWidth, float flexHeight)
        {
            LayoutElement le = go.GetComponent<LayoutElement>();
            if (le == null) {
                le = go.AddComponent<LayoutElement>();
            }
            if (prefWidth >= 0f) {
                le.preferredWidth = prefWidth;
            }
            if (prefHeight >= 0f) {
                le.preferredHeight = prefHeight;
                le.minHeight = prefHeight;
            }
            if (flexWidth >= 0f) {
                le.flexibleWidth = flexWidth;
            }
            if (flexHeight >= 0f) {
                le.flexibleHeight = flexHeight;
            }
            return le;
        }

        public static VerticalLayoutGroup AddVertical(GameObject go, int padding, float spacing)
        {
            VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(padding, padding, padding, padding);
            vlg.spacing = spacing;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            return vlg;
        }

        public static HorizontalLayoutGroup AddHorizontal(GameObject go, int padding, float spacing)
        {
            HorizontalLayoutGroup hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(padding, padding, padding, padding);
            hlg.spacing = spacing;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            return hlg;
        }

        // ---- text ----

        public static Text AddText(Transform parent, string text, int size, Color color, TextAnchor anchor, FontStyle style)
        {
            RectTransform rt = NewRect("Text", parent);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = GetFont();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        public static Text AddLabel(Transform parent, string text)
        {
            Text t = AddText(parent, text, 14, TextMain, TextAnchor.MiddleLeft, FontStyle.Normal);
            SetLayout(t.gameObject, -1f, 24f, 1f, -1f);
            return t;
        }

        public static Text AddDimLabel(Transform parent, string text)
        {
            Text t = AddText(parent, text, 12, TextDim, TextAnchor.MiddleLeft, FontStyle.Normal);
            SetLayout(t.gameObject, -1f, 20f, 1f, -1f);
            return t;
        }

        /// <summary>Section header bar (UPPERCASE small caps feel) used for panel sections.</summary>
        public static RectTransform AddSectionHeader(Transform parent, string title)
        {
            RectTransform bar = NewRect("Section " + title, parent);
            AddImage(bar.gameObject, PanelHeader);
            SetLayout(bar.gameObject, -1f, 26f, 1f, -1f);
            Text t = AddText(bar, title.ToUpperInvariant(), 12, TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            Stretch(t.rectTransform, 10f, 0f, 6f, 0f);
            return bar;
        }

        public static void AddSpacer(Transform parent, float height)
        {
            RectTransform rt = NewRect("Spacer", parent);
            SetLayout(rt.gameObject, -1f, height, 1f, -1f);
        }

        // ---- button ----

        public static Button AddButton(Transform parent, string label, UnityAction onClick)
        {
            RectTransform rt = NewRect("Button " + label, parent);
            Image bg = AddImage(rt.gameObject, ButtonBase);
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;
            Text t = AddText(rt, label, 13, TextMain, TextAnchor.MiddleCenter, FontStyle.Normal);
            Stretch(t.rectTransform, 4f, 0f, 4f, 0f);
            SetLayout(rt.gameObject, -1f, 30f, -1f, -1f);
            if (onClick != null) {
                button.onClick.AddListener(onClick);
            }
            return button;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            Text t = button.GetComponentInChildren<Text>();
            if (t != null) {
                t.text = label;
            }
        }

        /// <summary>Paint a button as "active" (accent) or normal. Used for tool / mode toggles.</summary>
        public static void SetButtonActive(Button button, bool active)
        {
            Image bg = button.GetComponent<Image>();
            Text t = button.GetComponentInChildren<Text>();
            if (bg != null) {
                bg.color = active ? Accent : ButtonBase;
            }
            if (t != null) {
                t.color = active ? TextOnAccent : TextMain;
            }
        }

        public static void SetButtonColor(Button button, Color color)
        {
            Image bg = button.GetComponent<Image>();
            if (bg != null) {
                bg.color = color;
            }
        }

        // ---- input field ----

        public static InputField AddInputField(Transform parent, string initial, string placeholder, UnityAction<string> onEndEdit)
        {
            RectTransform rt = NewRect("InputField", parent);
            Image bg = AddImage(rt.gameObject, Field);
            InputField field = rt.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;
            SetLayout(rt.gameObject, -1f, 28f, 1f, -1f);

            Text text = AddText(rt, "", 13, TextMain, TextAnchor.MiddleLeft, FontStyle.Normal);
            Stretch(text.rectTransform, 8f, 2f, 8f, 2f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.supportRichText = false;

            Text ph = AddText(rt, placeholder, 13, TextDim, TextAnchor.MiddleLeft, FontStyle.Italic);
            Stretch(ph.rectTransform, 8f, 2f, 8f, 2f);

            field.textComponent = text;
            field.placeholder = ph;
            field.text = initial ?? "";
            field.lineType = InputField.LineType.SingleLine;
            if (onEndEdit != null) {
                field.onEndEdit.AddListener(onEndEdit);
            }
            return field;
        }

        // ---- slider ----

        public static Slider AddSlider(Transform parent, float min, float max, float value, UnityAction<float> onChange)
        {
            RectTransform root = NewRect("Slider", parent);
            SetLayout(root.gameObject, -1f, 22f, 1f, -1f);

            RectTransform background = NewRect("Background", root);
            AddImage(background.gameObject, Field);
            Place(background, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -3f), new Vector2(0f, 3f));

            RectTransform fillArea = NewRect("Fill Area", root);
            Place(fillArea, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -3f), new Vector2(0f, 3f));
            RectTransform fill = NewRect("Fill", fillArea);
            AddImage(fill.gameObject, Accent);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            RectTransform handleArea = NewRect("Handle Slide Area", root);
            Stretch(handleArea, 6f, 0f, 6f, 0f);
            RectTransform handle = NewRect("Handle", handleArea);
            Image handleImage = AddImage(handle.gameObject, TextMain);
            handle.sizeDelta = new Vector2(12f, 0f);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            if (onChange != null) {
                slider.onValueChanged.AddListener(onChange);
            }
            return slider;
        }

        // ---- toggle ----

        public static Toggle AddToggle(Transform parent, string label, bool value, UnityAction<bool> onChange)
        {
            RectTransform root = NewRect("Toggle " + label, parent);
            SetLayout(root.gameObject, -1f, 24f, 1f, -1f);
            HorizontalLayoutGroup hlg = AddHorizontal(root.gameObject, 0, 8f);
            hlg.childAlignment = TextAnchor.MiddleLeft;

            RectTransform box = NewRect("Box", root);
            Image boxImage = AddImage(box.gameObject, Field);
            SetLayout(box.gameObject, 20f, 20f, 0f, 0f);
            RectTransform check = NewRect("Check", box);
            Image checkImage = AddImage(check.gameObject, Accent);
            Stretch(check, 4f, 4f, 4f, 4f);

            Text t = AddText(root, label, 13, TextMain, TextAnchor.MiddleLeft, FontStyle.Normal);
            SetLayout(t.gameObject, -1f, 24f, 1f, -1f);

            Toggle toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.SetIsOnWithoutNotify(value);
            if (onChange != null) {
                toggle.onValueChanged.AddListener(onChange);
            }
            return toggle;
        }

        // ---- scroll view ----

        /// <summary>Vertical scroll view; children added to <paramref name="content"/> stack
        /// top-down. Returns the root rect (place it yourself).</summary>
        public static RectTransform AddScrollView(Transform parent, out RectTransform content)
        {
            RectTransform root = NewRect("ScrollView", parent);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            AddImage(root.gameObject, new Color(0f, 0f, 0f, 0f));

            RectTransform viewport = NewRect("Viewport", root);
            Stretch(viewport, 0f, 0f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);
            VerticalLayoutGroup vlg = AddVertical(content.gameObject, 0, 2f);
            vlg.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return root;
        }
    }
}
