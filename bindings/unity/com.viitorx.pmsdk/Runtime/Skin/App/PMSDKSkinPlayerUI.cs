using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Show-time UI for the SkinPlayback scene: one card per saved skin with its live media
    /// picker (choose a different video/image from disk), a preview of the projector output,
    /// fullscreen output, and a button to jump to the Editor. Skin geometry is read-only here;
    /// only each skin's <c>defaultContent</c> is written back to the shared skin-set file.
    /// </summary>
    [DisallowMultipleComponent]
    public class PMSDKSkinPlayerUI : MonoBehaviour
    {
        private const float TopBarHeight = 48f;
        private const float StatusBarHeight = 28f;
        private const float ListWidth = 470f;

        [SerializeField] private PMSDKSkinLoader loader;

        private PMSDKSkinOutput output;
        private Canvas canvas;
        private RawImage previewImage;
        private RectTransform cardRoot;
        private Text statusLeft;
        private Text statusRight;
        private Button pauseButton;
        private PMSDKDropdown setDropdown;

        private PMSDKSkinSetJson.Set workingSet;
        private readonly List<PMSDKSkinMediaPanel> panels = new List<PMSDKSkinMediaPanel>();
        private int builtSpawnCount = -1;
        private bool paused;
        private string savedText = "";

        private void Awake()
        {
            if (loader == null) {
                loader = FindFirstObjectByType<PMSDKSkinLoader>();
            }
            PrepareSharedSet();
        }

        private void OnEnable()
        {
            output = GetComponent<PMSDKSkinOutput>();
            if (output == null) {
                output = gameObject.AddComponent<PMSDKSkinOutput>();
            }
            PMSDKUi.EnsureEventSystem();
            BuildUi();
            if (GetComponent<PMSDKVideoConvertFlow>() == null) {
                gameObject.AddComponent<PMSDKVideoConvertFlow>();
            }
            PMSDKVideoConvertFlow.VideoReplaced += OnVideoReplaced;
        }

        private void OnVideoReplaced(PMSDKSkinContent content)
        {
            if (content == null) {
                return;
            }
            PMSDKSkin skin = content.GetComponent<PMSDKSkin>();
            if (skin != null) {
                OnMediaChanged(skin, content);
            }
            for (int i = 0; i < panels.Count; i++) {
                panels[i].Refresh();
            }
        }

        private void OnDisable()
        {
            PMSDKVideoConvertFlow.VideoReplaced -= OnVideoReplaced;
            if (canvas != null) {
                Destroy(canvas.gameObject);
                canvas = null;
            }
            panels.Clear();
        }

        /// <summary>Make sure the shared file exists (first run on a machine: seed it from the
        /// set baked into the scene) and that the loader reads from it.</summary>
        private void PrepareSharedSet()
        {
            if (loader == null) {
                Debug.LogWarning("PMSDKSkinPlayerUI: no PMSDKSkinLoader in the scene.", this);
                return;
            }
            if (PMSDKSkinStore.Exists()) {
                loader.OverrideJsonPath = PMSDKSkinStore.DefaultPath;
            }
            PMSDKSkinSetJson.Set resolved = loader.ResolveSet();
            workingSet = PMSDKSkinStore.Clone(resolved);
            if (!PMSDKSkinStore.Exists() && workingSet != null) {
                PMSDKSkinStore.Save(workingSet);
                loader.OverrideJsonPath = PMSDKSkinStore.DefaultPath;
            }
        }

        private void RefreshSetDropdown()
        {
            if (setDropdown == null) {
                return;
            }
            List<string> names = PMSDKSkinStore.ListNames();
            string active = PMSDKSkinStore.ActiveName;
            if (!names.Contains(active)) {
                names.Add(active);
                names.Sort(System.StringComparer.OrdinalIgnoreCase);
            }
            setDropdown.SetItems(names, active);
        }

        private void SwitchSet(string name)
        {
            string clean = PMSDKSkinStore.SanitizeName(name);
            if (clean.Length == 0 || !PMSDKSkinStore.Exists(clean)) {
                savedText = "Skin set not found: " + name;
                return;
            }
            if (string.Equals(clean, PMSDKSkinStore.ActiveName, System.StringComparison.OrdinalIgnoreCase)) {
                return;
            }
            PMSDKSkinStore.ActiveName = clean;
            loader.OverrideJsonPath = PMSDKSkinStore.DefaultPath;
            workingSet = PMSDKSkinStore.Load();
            loader.SpawnAll();
            builtSpawnCount = -2;
            savedText = "Playing '" + clean + "'";
            RefreshSetDropdown();
        }

        // ------------------------------------------------------------------ build

        private void BuildUi()
        {
            canvas = PMSDKUi.CreateCanvas("PMSDK Player", 0, 100);
            RectTransform root = (RectTransform)canvas.transform;
            Image backdrop = PMSDKUi.AddImage(PMSDKUi.NewRect("Backdrop", root).gameObject, PMSDKUi.Backdrop);
            PMSDKUi.Stretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);

            BuildTopBar(root);
            BuildCardList(root);
            BuildPreview(root);
            BuildStatusBar(root);
        }

        private void BuildTopBar(RectTransform root)
        {
            RectTransform bar = PMSDKUi.NewRect("Top Bar", root);
            PMSDKUi.AddImage(bar.gameObject, PMSDKUi.PanelHeader);
            PMSDKUi.Place(bar, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -TopBarHeight), Vector2.zero);
            HorizontalLayoutGroup hlg = PMSDKUi.AddHorizontal(bar.gameObject, 8, 6f);
            hlg.childAlignment = TextAnchor.MiddleLeft;

            Text title = PMSDKUi.AddText(bar, "SKIN PLAYER", 15, PMSDKUi.Good, TextAnchor.MiddleLeft, FontStyle.Bold);
            PMSDKUi.SetLayout(title.gameObject, 150f, 32f, 0f, -1f);

            setDropdown = new PMSDKDropdown(bar, root, "Skin set", 260f);
            setDropdown.ItemPicked += SwitchSet;
            RefreshSetDropdown();

            RectTransform flex = PMSDKUi.NewRect("Flex", bar);
            PMSDKUi.SetLayout(flex.gameObject, 0f, -1f, 1f, -1f);

            pauseButton = PMSDKUi.AddButton(bar, "Pause all", OnPauseClicked);
            PMSDKUi.SetLayout(pauseButton.gameObject, 100f, 32f, 0f, -1f);
            Button fullscreen = PMSDKUi.AddButton(bar, "Fullscreen output (F11)", OnFullscreenClicked);
            PMSDKUi.SetLayout(fullscreen.gameObject, 190f, 32f, 0f, -1f);
            Button edit = PMSDKUi.AddButton(bar, "Edit skins  >", OnEditClicked);
            PMSDKUi.SetLayout(edit.gameObject, 130f, 32f, 0f, -1f);
            PMSDKUi.SetButtonActive(edit, true);
        }

        private void BuildCardList(RectTransform root)
        {
            RectTransform panel = PMSDKUi.NewRect("Card Panel", root);
            PMSDKUi.AddImage(panel.gameObject, PMSDKUi.Panel);
            PMSDKUi.Place(panel, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, StatusBarHeight), new Vector2(ListWidth, -TopBarHeight));

            RectTransform content;
            RectTransform scroll = PMSDKUi.AddScrollView(panel, out content);
            PMSDKUi.Stretch(scroll, 0f, 0f, 0f, 0f);

            PMSDKUi.AddSectionHeader(content, "Output background");
            RectTransform backgroundBody = PMSDKUi.NewRect("Background Body", content);
            PMSDKUi.AddVertical(backgroundBody.gameObject, 10, 6f);
            ContentSizeFitter backgroundFit = backgroundBody.gameObject.AddComponent<ContentSizeFitter>();
            backgroundFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            new PMSDKBackgroundPicker(backgroundBody, output);
            PMSDKUi.AddSpacer(content, 6f);
            PMSDKUi.AddSectionHeader(content, "Skins");

            cardRoot = PMSDKUi.NewRect("Cards", content);
            PMSDKUi.AddVertical(cardRoot.gameObject, 0, 0f);
            ContentSizeFitter cardsFit = cardRoot.gameObject.AddComponent<ContentSizeFitter>();
            cardsFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void BuildPreview(RectTransform root)
        {
            RectTransform area = PMSDKUi.NewRect("Preview Area", root);
            PMSDKUi.Place(area, Vector2.zero, Vector2.one,
                new Vector2(ListWidth + 14f, StatusBarHeight + 14f), new Vector2(-14f, -(TopBarHeight + 14f)));
            RectTransform frame = PMSDKUi.NewRect("Preview", area);
            PMSDKUi.AddImage(frame.gameObject, Color.black);
            AspectRatioFitter fitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;
            RectTransform raw = PMSDKUi.NewRect("Raster", frame);
            PMSDKUi.Stretch(raw, 0f, 0f, 0f, 0f);
            previewImage = raw.gameObject.AddComponent<RawImage>();
            previewImage.color = Color.white;
            previewImage.raycastTarget = false;
        }

        private void BuildStatusBar(RectTransform root)
        {
            RectTransform bar = PMSDKUi.NewRect("Status Bar", root);
            PMSDKUi.AddImage(bar.gameObject, PMSDKUi.PanelHeader);
            PMSDKUi.Place(bar, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, StatusBarHeight));
            statusLeft = PMSDKUi.AddText(bar, "", 12, PMSDKUi.TextMain, TextAnchor.MiddleLeft, FontStyle.Normal);
            PMSDKUi.Stretch(statusLeft.rectTransform, 12f, 0f, 320f, 0f);
            statusRight = PMSDKUi.AddText(bar, "", 12, PMSDKUi.TextDim, TextAnchor.MiddleRight, FontStyle.Normal);
            PMSDKUi.Stretch(statusRight.rectTransform, 320f, 0f, 12f, 0f);
        }

        // ------------------------------------------------------------------ per-frame

        private void Update()
        {
            if (canvas == null || loader == null) {
                return;
            }
            if (previewImage.texture == null && output.PreviewTexture != null) {
                previewImage.texture = output.PreviewTexture;
            }
            if (canvas.enabled == output.FullscreenPreview) {
                canvas.enabled = !output.FullscreenPreview;
            }
            if (Input.GetKeyDown(KeyCode.F11) && !PMSDKUi.IsTextFieldFocused()) {
                output.FullscreenPreview = !output.FullscreenPreview;
            }
            if (Input.GetKeyDown(KeyCode.F10) && !PMSDKUi.IsTextFieldFocused()) {
                OnEditClicked();
                return;
            }

            if (loader.Spawned.Count != builtSpawnCount) {
                RebuildCards();
            }
            statusLeft.text = BuildStatus();
            statusRight.text = savedText + (PMSDKSkinOutput.ExtraDisplayCount > 0
                ? "   |   Output: Display 2 active" : "   |   Output: no 2nd display");
        }

        private string BuildStatus()
        {
            int count = loader.Spawned.Count;
            if (count == 0) {
                return "No skins saved yet. Press 'Edit skins' to create some.";
            }
            return count + (count == 1 ? " skin" : " skins") + " loaded. Pick media on any card to change what plays.";
        }

        private void RebuildCards()
        {
            builtSpawnCount = loader.Spawned.Count;
            panels.Clear();
            for (int i = cardRoot.childCount - 1; i >= 0; i--) {
                Destroy(cardRoot.GetChild(i).gameObject);
            }
            if (loader.Spawned.Count == 0) {
                PMSDKUi.AddDimLabel(cardRoot, "  No skins found in the saved set.");
                return;
            }
            // Highest z first, same order the Editor lists them.
            List<PMSDKSkin> ordered = new List<PMSDKSkin>(loader.Spawned);
            ordered.Sort(delegate (PMSDKSkin a, PMSDKSkin b) { return b.ZOrder.CompareTo(a.ZOrder); });
            for (int i = 0; i < ordered.Count; i++) {
                if (ordered[i] != null) {
                    AddCard(i + 1, ordered[i]);
                }
            }
        }

        private void AddCard(int number, PMSDKSkin skin)
        {
            PMSDKSkinContent content = skin.GetComponent<PMSDKSkinContent>();
            RectTransform card = PMSDKUi.NewRect("Card " + skin.DisplayName, cardRoot);
            PMSDKUi.AddImage(card.gameObject, PMSDKUi.Backdrop);
            PMSDKUi.AddVertical(card.gameObject, 0, 0f);
            ContentSizeFitter fit = card.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform header = PMSDKUi.NewRect("Card Header", card);
            PMSDKUi.AddImage(header.gameObject, PMSDKUi.PanelHeader);
            PMSDKUi.SetLayout(header.gameObject, -1f, 30f, 1f, -1f);
            Text title = PMSDKUi.AddText(header, number + "   " + skin.DisplayName + "   (" + skin.Kind + ")",
                14, PMSDKUi.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            PMSDKUi.Stretch(title.rectTransform, 10f, 0f, 6f, 0f);

            RectTransform body = PMSDKUi.NewRect("Card Body", card);
            PMSDKUi.AddVertical(body.gameObject, 10, 4f);
            ContentSizeFitter bodyFit = body.gameObject.AddComponent<ContentSizeFitter>();
            bodyFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            PMSDKSkinMediaPanel panel = new PMSDKSkinMediaPanel(body, true);
            panel.Bind(content);
            panel.ContentChanged += delegate (PMSDKSkinContent changed) { OnMediaChanged(skin, changed); };
            panels.Add(panel);

            PMSDKUi.AddSpacer(cardRoot, 6f);
        }

        private void OnMediaChanged(PMSDKSkin skin, PMSDKSkinContent content)
        {
            if (skin == null || content == null) {
                return;
            }
            if (PMSDKSkinStore.SaveContent(workingSet, skin.SkinId, content)) {
                savedText = "Saved " + System.DateTime.Now.ToString("HH:mm:ss");
            } else {
                savedText = "SAVE FAILED - see log";
            }
        }

        // ------------------------------------------------------------------ actions

        private void OnPauseClicked()
        {
            paused = !paused;
            for (int i = 0; i < loader.Spawned.Count; i++) {
                PMSDKSkin skin = loader.Spawned[i];
                if (skin == null) {
                    continue;
                }
                PMSDKSkinContent content = skin.GetComponent<PMSDKSkinContent>();
                if (content != null) {
                    content.Play = !paused;
                }
            }
            PMSDKUi.SetButtonLabel(pauseButton, paused ? "Play all" : "Pause all");
            PMSDKUi.SetButtonActive(pauseButton, paused);
        }

        private void OnFullscreenClicked()
        {
            output.FullscreenPreview = true;
        }

        private void OnEditClicked()
        {
            PMSDKSkinApp.Switch(PMSDKSkinAppMode.Editor);
        }
    }
}
