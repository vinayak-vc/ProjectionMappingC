using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Operator console shown on the control display (Display 1) while calibration
    /// mode is active: selection readout, current values, step hints, save state,
    /// an F1 help overlay, a loupe (magnified view around the selected corner) and
    /// live per-projector thumbnails (click one to select that surface).
    /// Built entirely at runtime.
    /// </summary>
    public class PMSDKCalibrationHUD : MonoBehaviour
    {
        private const string HelpText =
            "PMSDK CALIBRATION — KEYS\n" +
            "F2            toggle calibration mode\n" +
            "F1            toggle this help\n" +
            "PgUp / PgDn   select projector surface (or click a thumbnail)\n" +
            "Tab           cycle corner (blend mode: cycle edge)\n" +
            "Arrows        nudge corner   [Shift] coarse x5   [Ctrl] fine x0.1\n" +
            "Mouse drag    move handle directly   [Alt] fine drag\n" +
            "B             blend mode: arrows = width / gamma, zones tinted\n" +
            "G             grid-warp mode: Tab point, arrows move, [ ] cols, - = rows\n" +
            "N / Shift+N   black level up / down\n" +
            "T / Shift+T   test pattern: selected / all\n" +
            "Y / Shift+Y   cycle pattern: checker/focus/convergence/bars/ramp/solids\n" +
            "C             canvas reference: end-to-end level plus across ALL projectors\n" +
            "R / Ctrl+R    reset corner / reset surface\n" +
            "A / Shift+A   auto-align (camera): selected / all\n" +
            "M             mark target rectangle in the camera view, then A\n" +
            "Ctrl+Z        undo\n" +
            "Ctrl+1..4     load preset slot   Ctrl+Shift+1..4  save preset slot\n" +
            "V             A/B compare (swap with pre-preset state)\n" +
            "Ctrl+S        save          Esc  exit (auto-saves)";

        private const float ThumbWidth = 192f;
        private const float ThumbHeight = 108f;

        private PMSDKCalibrationManager manager;
        private Canvas canvas;
        private Text statusText;
        private Text helpTextUi;
        private RawImage loupeImage;
        private GameObject loupePanel;
        private RectTransform thumbRow;

        // Target-mark preview (camera view + 4 draggable corner markers).
        private GameObject targetPanel;
        private RawImage targetPreview;
        private RectTransform targetPreviewRect;
        private Text targetInstruction;
        private readonly RectTransform[] targetMarkers = new RectTransform[4];
        private readonly Image[] targetMarkerImages = new Image[4];
        private readonly Text[] targetMarkerLabels = new Text[4];
        private bool draggingTarget;
        private static readonly string[] TargetCornerNames = { "TL", "TR", "BR", "BL" };
        private readonly List<RawImage> thumbImages = new List<RawImage>();
        private readonly List<Image> thumbFrames = new List<Image>();

        public static PMSDKCalibrationHUD Create(PMSDKCalibrationManager manager)
        {
            var go = new GameObject("PMSDK Calibration HUD");
            go.transform.SetParent(manager.transform, false);
            var hud = go.AddComponent<PMSDKCalibrationHUD>();
            hud.manager = manager;
            hud.Build();
            return hud;
        }

        public void SetVisible(bool visible)
        {
            if (canvas != null)
            {
                canvas.gameObject.SetActive(visible);
            }
        }

        private void Build()
        {
            var canvasObj = new GameObject("Canvas");
            canvasObj.transform.SetParent(transform, false);
            canvasObj.layer = LayerMask.NameToLayer("UI");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = 0; // operator/control display
            canvas.sortingOrder = 200;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>(); // thumbnail click-select

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Dark strip behind the status line so it reads over any content.
            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvasObj.transform, false);
            panel.layer = canvasObj.layer;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.75f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(0f, 96f);

            statusText = CreateText(panel.transform, font, 20, TextAnchor.UpperLeft);
            var statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = Vector2.one;
            statusRect.offsetMin = new Vector2(16f, 8f);
            statusRect.offsetMax = new Vector2(-16f, -8f);

            // Loupe: magnified view around the selected corner, docked top-right
            // under the status strip.
            loupePanel = new GameObject("LoupePanel");
            loupePanel.transform.SetParent(canvasObj.transform, false);
            loupePanel.layer = canvasObj.layer;
            var loupeBg = loupePanel.AddComponent<Image>();
            loupeBg.color = new Color(0f, 0f, 0f, 0.75f);
            var loupeRect = loupePanel.GetComponent<RectTransform>();
            loupeRect.anchorMin = new Vector2(1f, 1f);
            loupeRect.anchorMax = new Vector2(1f, 1f);
            loupeRect.pivot = new Vector2(1f, 1f);
            loupeRect.anchoredPosition = new Vector2(-8f, -104f);
            loupeRect.sizeDelta = new Vector2(216f, 216f);

            var loupeImgObj = new GameObject("Loupe");
            loupeImgObj.transform.SetParent(loupePanel.transform, false);
            loupeImgObj.layer = canvasObj.layer;
            loupeImage = loupeImgObj.AddComponent<RawImage>();
            var loupeImgRect = loupeImage.GetComponent<RectTransform>();
            loupeImgRect.anchorMin = Vector2.zero;
            loupeImgRect.anchorMax = Vector2.one;
            loupeImgRect.offsetMin = new Vector2(8f, 8f);
            loupeImgRect.offsetMax = new Vector2(-8f, -8f);
            CreateCrosshair(loupeImage.transform);

            // Thumbnail row, docked bottom-left.
            var rowObj = new GameObject("Thumbnails");
            rowObj.transform.SetParent(canvasObj.transform, false);
            rowObj.layer = canvasObj.layer;
            thumbRow = rowObj.AddComponent<RectTransform>();
            thumbRow.anchorMin = new Vector2(0f, 0f);
            thumbRow.anchorMax = new Vector2(0f, 0f);
            thumbRow.pivot = new Vector2(0f, 0f);
            thumbRow.anchoredPosition = new Vector2(8f, 8f);

            BuildTargetPanel(canvasObj.transform, font);

            var helpPanel = new GameObject("HelpPanel");
            helpPanel.transform.SetParent(canvasObj.transform, false);
            helpPanel.layer = canvasObj.layer;
            var helpImg = helpPanel.AddComponent<Image>();
            helpImg.color = new Color(0f, 0f, 0f, 0.85f);
            var helpRect = helpPanel.GetComponent<RectTransform>();
            helpRect.anchorMin = new Vector2(0.5f, 0.5f);
            helpRect.anchorMax = new Vector2(0.5f, 0.5f);
            helpRect.sizeDelta = new Vector2(760f, 460f);

            helpTextUi = CreateText(helpPanel.transform, font, 22, TextAnchor.MiddleLeft);
            var helpTextRect = helpTextUi.GetComponent<RectTransform>();
            helpTextRect.anchorMin = Vector2.zero;
            helpTextRect.anchorMax = Vector2.one;
            helpTextRect.offsetMin = new Vector2(24f, 16f);
            helpTextRect.offsetMax = new Vector2(-24f, -16f);
            helpTextUi.text = HelpText;
        }

        private void CreateCrosshair(Transform parent)
        {
            for (int i = 0; i < 2; i++)
            {
                var line = new GameObject(i == 0 ? "CrossH" : "CrossV");
                line.transform.SetParent(parent, false);
                line.layer = parent.gameObject.layer;
                var img = line.AddComponent<Image>();
                img.color = new Color(1f, 0.9f, 0.1f, 0.6f);
                img.raycastTarget = false;
                var r = line.GetComponent<RectTransform>();
                r.anchorMin = new Vector2(0.5f, 0.5f);
                r.anchorMax = new Vector2(0.5f, 0.5f);
                r.sizeDelta = i == 0 ? new Vector2(60f, 2f) : new Vector2(2f, 60f);
            }
        }

        private void RebuildThumbnails()
        {
            foreach (Transform child in thumbRow)
            {
                Destroy(child.gameObject);
            }
            thumbImages.Clear();
            thumbFrames.Clear();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < manager.Surfaces.Count; i++)
            {
                int index = i; // capture for the click handler
                var frameObj = new GameObject("Thumb_" + manager.Surfaces[i].Id);
                frameObj.transform.SetParent(thumbRow, false);
                frameObj.layer = thumbRow.gameObject.layer;
                var frame = frameObj.AddComponent<Image>();
                frame.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                var frameRect = frameObj.GetComponent<RectTransform>();
                frameRect.anchorMin = Vector2.zero;
                frameRect.anchorMax = Vector2.zero;
                frameRect.pivot = Vector2.zero;
                frameRect.anchoredPosition = new Vector2(i * (ThumbWidth + 12f), 0f);
                frameRect.sizeDelta = new Vector2(ThumbWidth + 6f, ThumbHeight + 26f);

                var button = frameObj.AddComponent<Button>();
                button.onClick.AddListener(() => manager.SetSelectedSurface(index));

                var imgObj = new GameObject("Image");
                imgObj.transform.SetParent(frameObj.transform, false);
                imgObj.layer = frameObj.layer;
                var raw = imgObj.AddComponent<RawImage>();
                raw.raycastTarget = false;
                var rawRect = raw.GetComponent<RectTransform>();
                rawRect.anchorMin = new Vector2(0f, 0f);
                rawRect.anchorMax = new Vector2(0f, 0f);
                rawRect.pivot = Vector2.zero;
                rawRect.anchoredPosition = new Vector2(3f, 3f);
                rawRect.sizeDelta = new Vector2(ThumbWidth, ThumbHeight);

                var label = CreateText(frameObj.transform, font, 14, TextAnchor.UpperCenter);
                label.raycastTarget = false;
                int displayNumber = manager.Surfaces[i].ProjectorCamera != null ? manager.Surfaces[i].ProjectorCamera.targetDisplay + 1 : 1;
                label.text = $"[{i + 1}] Display {displayNumber} — {manager.Surfaces[i].Id}";
                var labelRect = label.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 1f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.pivot = new Vector2(0.5f, 1f);
                labelRect.anchoredPosition = new Vector2(0f, -4f);
                labelRect.sizeDelta = new Vector2(0f, 18f);

                thumbImages.Add(raw);
                thumbFrames.Add(frame);
            }
        }

        private void BuildTargetPanel(Transform parent, Font font)
        {
            targetPanel = new GameObject("TargetPanel");
            targetPanel.transform.SetParent(parent, false);
            targetPanel.layer = ((RectTransform)parent).gameObject.layer;
            var bg = targetPanel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);
            var panelRect = targetPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 470f);

            var title = CreateText(targetPanel.transform, font, 22, TextAnchor.UpperCenter);
            title.text = "MARK TARGET RECTANGLE — drag the 4 corners onto the screen edges";
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -8f);
            titleRect.sizeDelta = new Vector2(0f, 28f);

            // Camera preview (16:9), centered.
            var previewObj = new GameObject("Preview");
            previewObj.transform.SetParent(targetPanel.transform, false);
            previewObj.layer = targetPanel.layer;
            targetPreview = previewObj.AddComponent<RawImage>();
            targetPreview.color = Color.white;
            // Camera luminance is top-left origin; flip V so the preview shows upright.
            targetPreview.uvRect = new Rect(0f, 1f, 1f, -1f);
            targetPreviewRect = targetPreview.GetComponent<RectTransform>();
            targetPreviewRect.anchorMin = targetPreviewRect.anchorMax = new Vector2(0.5f, 0.5f);
            targetPreviewRect.sizeDelta = new Vector2(680f, 383f);
            targetPreviewRect.anchoredPosition = new Vector2(0f, -6f);

            for (int i = 0; i < 4; i++)
            {
                var m = new GameObject("TargetMarker_" + TargetCornerNames[i]);
                m.transform.SetParent(targetPreviewRect, false);
                m.layer = targetPanel.layer;
                targetMarkerImages[i] = m.AddComponent<Image>();
                targetMarkerImages[i].raycastTarget = false;
                targetMarkers[i] = m.GetComponent<RectTransform>();
                targetMarkers[i].sizeDelta = new Vector2(22f, 22f);

                var label = CreateText(m.transform, font, 14, TextAnchor.LowerLeft);
                label.raycastTarget = false;
                label.text = TargetCornerNames[i];
                var lr = label.GetComponent<RectTransform>();
                lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.anchoredPosition = new Vector2(14f, 14f);
                lr.sizeDelta = new Vector2(40f, 18f);
                targetMarkerLabels[i] = label;
            }

            targetInstruction = CreateText(targetPanel.transform, font, 16, TextAnchor.LowerCenter);
            var instrRect = targetInstruction.GetComponent<RectTransform>();
            instrRect.anchorMin = new Vector2(0f, 0f);
            instrRect.anchorMax = new Vector2(1f, 0f);
            instrRect.pivot = new Vector2(0.5f, 0f);
            instrRect.anchoredPosition = new Vector2(0f, 8f);
            instrRect.sizeDelta = new Vector2(0f, 24f);
            targetInstruction.text = "Tab select   arrows/drag move   A align to target   R reset   M/Esc cancel";

            targetPanel.SetActive(false);
        }

        private void UpdateTargetPanel()
        {
            bool active = manager.TargetMarkMode;
            targetPanel.SetActive(active);
            if (!active) { draggingTarget = false; return; }

            targetPreview.texture = manager.TargetPreview;

            Vector2 size = targetPreviewRect.rect.size;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = manager.GetTargetCorner(i); // UI-normalized, bottom-left
                // Preview rect pivot is centre; convert 0..1 to centre-relative pixels.
                targetMarkers[i].anchoredPosition = new Vector2((p.x - 0.5f) * size.x, (p.y - 0.5f) * size.y);
                bool sel = manager.SelectedTargetCorner == i;
                targetMarkers[i].sizeDelta = Vector2.one * (sel ? 34f : 22f);
                targetMarkerImages[i].color = sel ? new Color(1f, 0.9f, 0.1f, 0.95f) : new Color(0f, 1f, 0.4f, 0.8f);
            }

            HandleTargetMouse(size);
        }

        private void HandleTargetMouse(Vector2 size)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(targetPreviewRect, Input.mousePosition, null))
                {
                    // Select the nearest corner and begin dragging it.
                    Vector2 n = LocalToNormalized(size);
                    int best = 0; float bestD = float.MaxValue;
                    for (int i = 0; i < 4; i++)
                    {
                        float d = Vector2.Distance(n, manager.GetTargetCorner(i));
                        if (d < bestD) { bestD = d; best = i; }
                    }
                    manager.SelectTargetCorner(best);
                    draggingTarget = true;
                }
            }
            else if (draggingTarget && Input.GetMouseButton(0))
            {
                manager.SetTargetCorner(manager.SelectedTargetCorner, LocalToNormalized(size));
            }
            if (Input.GetMouseButtonUp(0)) draggingTarget = false;
        }

        private Vector2 LocalToNormalized(Vector2 size)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(targetPreviewRect, Input.mousePosition, null, out Vector2 local);
            // local is centre-relative; convert to 0..1 bottom-left.
            return new Vector2(Mathf.Clamp01(local.x / size.x + 0.5f), Mathf.Clamp01(local.y / size.y + 0.5f));
        }

        private static Text CreateText(Transform parent, Font font, int size, TextAnchor anchor)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private void LateUpdate()
        {
            if (canvas == null || !canvas.gameObject.activeSelf || manager == null)
            {
                return;
            }

            helpTextUi.transform.parent.gameObject.SetActive(manager.HelpVisible);
            UpdateTargetPanel();

            var s = manager.Selected;
            if (s == null)
            {
                statusText.text = "PMSDK CALIBRATION — no surfaces found";
                return;
            }

            if (manager.TargetMarkMode)
            {
                statusText.text = "PMSDK CALIBRATION — MARK TARGET\nPlace the 4 corners on the physical screen, then A to align " + s.Id + ".";
                return;
            }

            if (manager.IsAutoAligning)
            {
                statusText.text = "PMSDK CALIBRATION — AUTO-ALIGN RUNNING\n" + (manager.AutoAlignStatus ?? "") + "\ndisplaying structured-light patterns, please wait…";
                return;
            }

            if (thumbImages.Count != manager.Surfaces.Count)
            {
                RebuildThumbnails();
            }
            for (int i = 0; i < thumbImages.Count; i++)
            {
                thumbImages[i].texture = manager.Surfaces[i].Thumbnail;
                thumbFrames[i].color = i == manager.SelectedSurfaceIndex
                    ? new Color(1f, 0.9f, 0.1f, 0.95f)
                    : new Color(0.2f, 0.2f, 0.2f, 0.9f);
            }

            // Loupe only makes sense while corner editing.
            loupePanel.SetActive(!manager.BlendSubmode);
            if (loupeImage != null)
            {
                loupeImage.texture = manager.LoupeRT;
            }

            int displayNumber = s.ProjectorCamera != null ? s.ProjectorCamera.targetDisplay + 1 : 1;
            string line1 = $"PMSDK CALIBRATION   Projector [{manager.SelectedSurfaceIndex + 1}/{manager.Surfaces.Count}]  {s.Id}  (Display {displayNumber})";

            string line2;
            if (manager.GridEditMode && s.Grid != null)
            {
                int col = s.Grid.PointCount > 0 ? manager.SelectedGridPoint % s.Grid.Columns : 0;
                int row = s.Grid.PointCount > 0 ? manager.SelectedGridPoint / s.Grid.Columns : 0;
                Vector2 gp = s.Grid.PointCount > 0 ? s.Grid.GetPointByIndex(manager.SelectedGridPoint) : Vector2.zero;
                line2 = $"GRID {s.Grid.Columns}x{s.Grid.Rows}   point [{col},{row}] ({gp.x:F3}, {gp.y:F3})   arrows/drag move   [ ] cols   - = rows   R reset";
            }
            else if (manager.BlendSubmode)
            {
                string edge = PMSDKCalibrationManager.EdgeNames[manager.SelectedEdge];
                float gamma = s.Blend != null ? s.Blend.Gamma : 0f;
                float black = s.Blend != null ? s.Blend.BlackLevel : 0f;
                line2 = $"BLEND  edge {edge}   L {F(s, 0)}  R {F(s, 1)}  T {F(s, 2)}  B {F(s, 3)}   gamma {gamma:F2}   black {black:F2}";
            }
            else
            {
                Vector2 pos = PMSDKCalibrationManager.GetCorner(s.CornerPin, manager.SelectedCorner);
                string corner = PMSDKCalibrationManager.CornerNames[manager.SelectedCorner];
                string drag = manager.IsDragging ? "   [dragging]" : "";
                line2 = $"CORNER {corner}   pos ({pos.x:F4}, {pos.y:F4})   arrows/drag  [Shift] coarse  [Ctrl/Alt] fine{drag}";
            }

            string saveState = manager.Dirty ? "* UNSAVED CHANGES — Ctrl+S to save" : "saved";
            string undo = manager.UndoDepth > 0 ? $"   undo x{manager.UndoDepth}" : "";
            string aa = string.IsNullOrEmpty(manager.AutoAlignStatus) ? "" : $"   |   {manager.AutoAlignStatus}";
            string line3 = $"{saveState}{undo}   |   F1 help   A auto-align   Esc exit{aa}";

            statusText.text = line1 + "\n" + line2 + "\n" + line3;
        }

        private static string F(PMSDKCalibrationManager.Surface s, int edge)
        {
            if (s.Blend == null) return "-";
            switch (edge)
            {
                case 0: return s.Blend.LeftEdge.ToString("F2");
                case 1: return s.Blend.RightEdge.ToString("F2");
                case 2: return s.Blend.TopEdge.ToString("F2");
                default: return s.Blend.BottomEdge.ToString("F2");
            }
        }
    }
}
