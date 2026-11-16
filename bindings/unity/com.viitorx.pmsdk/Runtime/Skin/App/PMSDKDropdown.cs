using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Minimal code-built dropdown: a button showing "Label: current" that opens a popup list
    /// under it. Clicking an item raises <see cref="ItemPicked"/>; clicking anywhere else closes
    /// the popup. The popup is parented to the canvas root so it draws above the panels.
    /// </summary>
    public class PMSDKDropdown
    {
        private const float ItemHeight = 30f;
        private const float MaxPopupHeight = 360f;

        public event Action<string> ItemPicked;

        private readonly RectTransform canvasRoot;
        private readonly Button button;
        private readonly string caption;
        private readonly float popupWidth;
        private List<string> items = new List<string>();
        private string current = "";
        private GameObject popupRoot;

        public Button Button {
            get { return button; }
        }

        public PMSDKDropdown(Transform barParent, RectTransform canvasRootRect, string captionText, float width)
        {
            canvasRoot = canvasRootRect;
            caption = captionText;
            popupWidth = width;
            button = PMSDKUi.AddButton(barParent, "", Toggle);
            PMSDKUi.SetLayout(button.gameObject, width, 32f, 0f, -1f);
            Text label = button.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            PMSDKUi.Stretch(label.rectTransform, 10f, 0f, 22f, 0f);
            RefreshLabel();
        }

        public void SetItems(List<string> newItems, string currentItem)
        {
            items = newItems != null ? newItems : new List<string>();
            current = currentItem ?? "";
            RefreshLabel();
            if (popupRoot != null) {
                Close();
            }
        }

        public void Close()
        {
            if (popupRoot != null) {
                UnityEngine.Object.Destroy(popupRoot);
                popupRoot = null;
            }
        }

        private void RefreshLabel()
        {
            PMSDKUi.SetButtonLabel(button, caption + ": " + current + "  ▾");
        }

        private void Toggle()
        {
            if (popupRoot != null) {
                Close();
                return;
            }
            Open();
        }

        private void Open()
        {
            popupRoot = PMSDKUi.NewRect("Dropdown Popup", canvasRoot).gameObject;
            RectTransform rootRect = (RectTransform)popupRoot.transform;
            PMSDKUi.Stretch(rootRect, 0f, 0f, 0f, 0f);

            // Full-screen click catcher: any click outside the list closes it.
            Image blocker = PMSDKUi.AddImage(popupRoot, new Color(0f, 0f, 0f, 0.001f));
            Button blockerButton = popupRoot.AddComponent<Button>();
            blockerButton.targetGraphic = blocker;
            blockerButton.transition = Selectable.Transition.None;
            blockerButton.onClick.AddListener(Close);

            RectTransform panel = PMSDKUi.NewRect("List", rootRect);
            PMSDKUi.AddImage(panel.gameObject, PMSDKUi.PanelHeader);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0f, 1f);

            Vector3[] corners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners);
            Vector3 bottomLeft = canvasRoot.InverseTransformPoint(corners[0]);
            panel.anchoredPosition = new Vector2(bottomLeft.x, bottomLeft.y - 2f);

            float height = Mathf.Min(Mathf.Max(items.Count, 1) * (ItemHeight + 2f) + 8f, MaxPopupHeight);
            panel.sizeDelta = new Vector2(popupWidth, height);

            RectTransform content;
            RectTransform scroll = PMSDKUi.AddScrollView(panel, out content);
            PMSDKUi.Stretch(scroll, 4f, 4f, 4f, 4f);

            if (items.Count == 0) {
                PMSDKUi.AddDimLabel(content, "  (none yet)");
            }
            for (int i = 0; i < items.Count; i++) {
                string item = items[i];
                Button b = PMSDKUi.AddButton(content, item, delegate { Pick(item); });
                Text t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                PMSDKUi.Stretch(t.rectTransform, 10f, 0f, 4f, 0f);
                PMSDKUi.SetLayout(b.gameObject, -1f, ItemHeight, 1f, -1f);
                if (string.Equals(item, current, StringComparison.OrdinalIgnoreCase)) {
                    PMSDKUi.SetButtonActive(b, true);
                }
            }
        }

        private void Pick(string item)
        {
            Close();
            Action<string> handler = ItemPicked;
            if (handler != null) {
                handler(item);
            }
        }
    }
}
