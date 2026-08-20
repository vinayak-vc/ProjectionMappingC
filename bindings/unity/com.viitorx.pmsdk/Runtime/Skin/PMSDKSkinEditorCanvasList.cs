using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// UGUI-based skin list (M-Sk4 v1.5). Replaces the IMGUI skin list on Display 1
    /// with a proper Canvas panel containing a ScrollRect + VerticalLayoutGroup of
    /// selectable rows. Each row implements drag-reorder via
    /// <see cref="PMSDKSkinCanvasRow"/>: pick up a row, drag up/down, drop → the
    /// controller rewrites z-order so the visual stacking matches the drop position.
    ///
    /// Coexists with <see cref="PMSDKSkinEditorCanvasInspector"/> (right panel) on the
    /// same display; both create their own Canvas at sortingOrder 100. Auto-hides the
    /// sibling IMGUI HUD's list section (<see cref="PMSDKSkinEditorHUD.DrawList"/>) on
    /// enable and restores on disable.
    /// </summary>
    [DisallowMultipleComponent]
    public class PMSDKSkinEditorCanvasList : MonoBehaviour
    {
        public PMSDKSkinEditorController Controller;
        public int TargetDisplay = 0;
        public int PanelWidth = 260;
        public int PanelPadding = 12;
        public float RowHeight = 32f;

        private Canvas canvas;
        private RectTransform panel;
        private RectTransform rowContainer;
        private Text headerLabel;
        private PMSDKSkinEditorHUD imguiHud;
        private bool restoreImguiListOnDisable;

        // Row bookkeeping — rebuilt when Controller.Skins.Count or a skin instance changes.
        private readonly List<PMSDKSkinCanvasRow> rows = new List<PMSDKSkinCanvasRow>();
        private int lastRowSignature; // hash of (count, skin IDs, mute/visibility flags)

        // ---- lifecycle ----

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
            rows.Clear();
        }

        private void Update()
        {
            if (canvas == null || Controller == null) return;
            int sig = ComputeSignature();
            if (sig != lastRowSignature)
            {
                RebuildRows();
                lastRowSignature = sig;
            }
            SyncRowVisualState();
        }

        public bool CanvasBuilt => canvas != null;
        public IReadOnlyList<PMSDKSkinCanvasRow> Rows => rows;

        /// <summary>Public entry point used by tests to bootstrap the canvas
        /// deterministically after Controller assignment.</summary>
        public void RebuildForTests()
        {
            OnDisable();
            OnEnable();
            RebuildRows();
            lastRowSignature = ComputeSignature();
        }

        // ---- IMGUI-HUD interop ----

        private void RebindImguiHud(bool hide)
        {
            if (imguiHud == null) imguiHud = GetComponent<PMSDKSkinEditorHUD>();
            if (imguiHud == null) return;
            if (hide)
            {
                restoreImguiListOnDisable = imguiHud.DrawList;
                imguiHud.DrawList = false;
            }
            else if (restoreImguiListOnDisable)
            {
                imguiHud.DrawList = true;
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
            var canvasGO = new GameObject("PMSDK Canvas List");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = TargetDisplay;
            canvas.sortingOrder = 100;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            // Left-side panel.
            panel = new GameObject("List Panel").AddComponent<RectTransform>();
            panel.SetParent(canvasGO.transform, worldPositionStays: false);
            panel.anchorMin = new Vector2(0f, 0.05f);
            panel.anchorMax = new Vector2(0f, 0.95f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.anchoredPosition = new Vector2(PanelPadding, 0f);
            panel.sizeDelta = new Vector2(PanelWidth, 0f);

            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);

            var vlg = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 6;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            headerLabel = AddLabel(panel.transform, "Skins (0)");
            headerLabel.fontStyle = FontStyle.Bold;
            headerLabel.color = new Color(1f, 0.85f, 0.05f, 1f);

            // Row container.
            var containerGO = new GameObject("RowContainer");
            rowContainer = containerGO.AddComponent<RectTransform>();
            rowContainer.SetParent(panel, worldPositionStays: false);
            var rcVlg = containerGO.AddComponent<VerticalLayoutGroup>();
            rcVlg.spacing = 4;
            rcVlg.childControlWidth = true;
            rcVlg.childForceExpandWidth = true;
            rcVlg.childControlHeight = false;
            rcVlg.childForceExpandHeight = false;
            var fit = containerGO.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var le = containerGO.AddComponent<LayoutElement>();
            le.flexibleHeight = 1;
        }

        // ---- row build ----

        private int ComputeSignature()
        {
            if (Controller == null || Controller.Skins == null) return 0;
            unchecked
            {
                int h = 17 + Controller.Skins.Count * 31;
                for (int i = 0; i < Controller.Skins.Count; i++)
                {
                    var s = Controller.Skins[i];
                    if (s == null) continue;
                    h = h * 31 + (s.SkinId != null ? s.SkinId.GetHashCode() : 0);
                    h = h * 31 + s.ZOrder;
                }
                return h;
            }
        }

        private void RebuildRows()
        {
            if (rowContainer == null) return;
            // Nuke existing rows.
            for (int i = rowContainer.childCount - 1; i >= 0; i--)
            {
                var child = rowContainer.GetChild(i).gameObject;
                if (Application.isEditor && !Application.isPlaying) DestroyImmediate(child);
                else Destroy(child);
            }
            rows.Clear();

            for (int i = 0; i < Controller.Skins.Count; i++)
            {
                var rowGO = new GameObject($"Row {i}", typeof(RectTransform));
                rowGO.transform.SetParent(rowContainer, worldPositionStays: false);
                var rowImg = rowGO.AddComponent<Image>();
                rowImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
                var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 4;
                hlg.padding = new RectOffset(6, 6, 4, 4);
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = true;
                var rle = rowGO.AddComponent<LayoutElement>();
                rle.minHeight = RowHeight;
                rle.preferredHeight = RowHeight;

                var row = rowGO.AddComponent<PMSDKSkinCanvasRow>();
                row.Init(this, i);
                rows.Add(row);
            }
        }

        private void SyncRowVisualState()
        {
            headerLabel.text = $"Skins ({Controller.Skins.Count})";
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null) rows[i].SyncVisuals();
            }
        }

        // ---- reorder callback (called by rows on drop) ----

        internal void OnRowDropped(PMSDKSkinCanvasRow row, int newIndex)
        {
            if (row == null || row.Skin == null) return;
            Controller.MoveSkinToIndex(row.Skin, newIndex);
            // Force rebuild on next Update.
            lastRowSignature = 0;
        }

        // ---- factory helpers ----

        internal static Text AddLabel(Transform parent, string text)
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
            le.flexibleWidth = 1;
            return t;
        }

        internal static Button AddButton(Transform parent, string label, System.Action onClick, float width)
        {
            var btn = new GameObject("Btn:" + label, typeof(RectTransform));
            btn.transform.SetParent(parent, worldPositionStays: false);
            var img = btn.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            var b = btn.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick?.Invoke());
            var le = btn.AddComponent<LayoutElement>();
            le.minWidth = width; le.preferredWidth = width;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(btn.transform, worldPositionStays: false);
            var t = textGO.AddComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 12;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            var trt = (RectTransform)textGO.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            return b;
        }
    }

    /// <summary>
    /// One row in the UGUI skin list. Handles: click-to-select via the row's label
    /// button, z-order ^/v buttons, visibility toggle, AND drag-reorder via
    /// <see cref="IBeginDragHandler"/> / <see cref="IDragHandler"/> / <see cref="IEndDragHandler"/>.
    /// On drop, computes the row's new index (based on its Y position within the
    /// container) and asks the parent list to commit the reorder via
    /// <see cref="PMSDKSkinEditorCanvasList.OnRowDropped"/>.
    /// </summary>
    public class PMSDKSkinCanvasRow : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public PMSDKSkinEditorCanvasList List;
        public int RowIndex;
        public PMSDKSkin Skin => List != null && RowIndex >= 0 && RowIndex < List.Controller.Skins.Count
            ? List.Controller.Skins[RowIndex] : null;

        private Text labelText;
        private Button selectButton;
        private Image rowImage;
        private Vector2 dragOriginalPos;
        private int dragOriginalSibling;
        private bool dragging;

        internal void Init(PMSDKSkinEditorCanvasList list, int rowIndex)
        {
            List = list;
            RowIndex = rowIndex;

            rowImage = GetComponent<Image>();

            // Label as a click-select button spanning most of the row width.
            selectButton = PMSDKSkinEditorCanvasList.AddButton(transform, "Select",
                () => { List.Controller.SelectedSkinIndex = RowIndex; List.Controller.SelectedCornerIndex = 0; },
                width: 140f);
            var textGO = selectButton.transform.GetChild(0);
            labelText = textGO.GetComponent<Text>();
            labelText.alignment = TextAnchor.MiddleLeft;

            PMSDKSkinEditorCanvasList.AddButton(transform, "^",
                () => { List.Controller.SelectedSkinIndex = RowIndex; List.Controller.MoveSelectedZOrder(+1); }, 24f);
            PMSDKSkinEditorCanvasList.AddButton(transform, "v",
                () => { List.Controller.SelectedSkinIndex = RowIndex; List.Controller.MoveSelectedZOrder(-1); }, 24f);
            PMSDKSkinEditorCanvasList.AddButton(transform, "o",
                () => { var s = Skin; if (s != null) List.Controller.SetSkinVisibility(s, !List.Controller.IsVisible(s)); }, 24f);
        }

        public void SyncVisuals()
        {
            if (List == null || List.Controller == null) return;
            var s = Skin;
            if (s == null) { labelText.text = ""; return; }

            bool selected = RowIndex == List.Controller.SelectedSkinIndex;
            bool multi = List.Controller.MultiSelectedIndices.Contains(RowIndex);
            string mutedGlyph = List.Controller.IsMuted(s) ? " (muted)" : "";
            string hiddenGlyph = List.Controller.IsVisible(s) ? "" : " (hidden)";
            labelText.text = $"{RowIndex + 1}. {s.DisplayName}  <{s.Kind}>{mutedGlyph}{hiddenGlyph}";
            labelText.color = selected ? new Color(1f, 0.85f, 0.05f, 1f)
                            : (multi ? new Color(0.3f, 0.8f, 1f, 1f)
                                    : Color.white);
            if (rowImage != null)
                rowImage.color = selected ? new Color(0.30f, 0.22f, 0.02f, 1f)
                                          : new Color(0.15f, 0.15f, 0.15f, 1f);
        }

        // ---- drag-reorder ----

        public void OnBeginDrag(PointerEventData e)
        {
            if (List == null || Skin == null) return;
            dragging = true;
            dragOriginalPos = ((RectTransform)transform).anchoredPosition;
            dragOriginalSibling = transform.GetSiblingIndex();
        }

        public void OnDrag(PointerEventData e)
        {
            if (!dragging) return;
            var rt = (RectTransform)transform;
            var np = rt.anchoredPosition;
            np.y += e.delta.y;
            rt.anchoredPosition = np;

            // Reorder in the hierarchy based on visual position, so the layout group
            // shifts other rows out of the way live.
            int newSibling = ComputeSiblingIndexFromY();
            if (newSibling != transform.GetSiblingIndex())
            {
                transform.SetSiblingIndex(newSibling);
            }
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging) return;
            dragging = false;
            // Compute the target list index from the row's final hierarchy position.
            int newIndex = transform.GetSiblingIndex();
            // Restore anchoredPosition; the layout group will re-lay-out on the next frame.
            var rt = (RectTransform)transform;
            rt.anchoredPosition = Vector2.zero;

            if (newIndex != dragOriginalSibling)
            {
                List.OnRowDropped(this, newIndex);
            }
        }

        private int ComputeSiblingIndexFromY()
        {
            var rt = (RectTransform)transform;
            var parent = transform.parent;
            if (parent == null) return dragOriginalSibling;
            float myY = rt.anchoredPosition.y;
            int idx = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i) == transform) continue;
                var sib = (RectTransform)parent.GetChild(i);
                // Higher anchoredPosition.y = visually ABOVE in a top-anchored VerticalLayoutGroup.
                if (sib.anchoredPosition.y > myY) idx++;
            }
            return Mathf.Clamp(idx, 0, parent.childCount - 1);
        }
    }
}
