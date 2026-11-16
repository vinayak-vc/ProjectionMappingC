using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Reusable "what plays on this skin" UI block (source buttons, file path, Browse...,
    /// loop/mute). Built in code under a parent that has a vertical layout group. Used by
    /// the Skin Workspace inspector and by every card in the Skin Player.
    /// Fires <see cref="ContentChanged"/> after any change so the host can save / mark dirty.
    /// </summary>
    public class PMSDKSkinMediaPanel
    {
        public event Action<PMSDKSkinContent> ContentChanged;

        private readonly RectTransform root;
        private readonly Button videoButton;
        private readonly Button imageButton;
        private readonly Button patternButton;
        private readonly Button webcamButton;
        private readonly Button noneButton;
        private readonly InputField pathField;
        private readonly Text statusLabel;
        private readonly Toggle loopToggle;
        private readonly Toggle muteToggle;
        private readonly Button patternCycleButton;
        private readonly GameObject videoOptionsRow;
        private readonly GameObject patternRow;

        private PMSDKSkinContent content;
        private bool suppress;
        private string lastDirectory;

        public RectTransform Root {
            get { return root; }
        }

        public PMSDKSkinMediaPanel(Transform parent, bool compact)
        {
            root = PMSDKUi.NewRect("Media Panel", parent);
            PMSDKUi.AddVertical(root.gameObject, 0, 4f);
            ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform sourceRow = PMSDKUi.NewRect("Source Row", root);
            PMSDKUi.SetLayout(sourceRow.gameObject, -1f, 30f, 1f, -1f);
            HorizontalLayoutGroup hlg = PMSDKUi.AddHorizontal(sourceRow.gameObject, 0, 3f);
            hlg.childForceExpandWidth = true;
            videoButton = AddSourceButton(sourceRow, "Video", OnVideoClicked);
            imageButton = AddSourceButton(sourceRow, "Image", OnImageClicked);
            patternButton = AddSourceButton(sourceRow, "Pattern", OnPatternClicked);
            webcamButton = AddSourceButton(sourceRow, "Webcam", OnWebcamClicked);
            noneButton = AddSourceButton(sourceRow, "None", OnNoneClicked);

            RectTransform pathRow = PMSDKUi.NewRect("Path Row", root);
            PMSDKUi.SetLayout(pathRow.gameObject, -1f, 28f, 1f, -1f);
            HorizontalLayoutGroup pathLayout = PMSDKUi.AddHorizontal(pathRow.gameObject, 0, 4f);
            pathLayout.childForceExpandWidth = false;
            pathField = PMSDKUi.AddInputField(pathRow, "", "Paste a file path, or Browse...", OnPathEdited);
            PMSDKUi.SetLayout(pathField.gameObject, -1f, 28f, 1f, -1f);
            Button browse = PMSDKUi.AddButton(pathRow, "Browse...", OnBrowseClicked);
            PMSDKUi.SetLayout(browse.gameObject, 80f, 28f, 0f, -1f);

            statusLabel = PMSDKUi.AddDimLabel(root, "");

            videoOptionsRow = PMSDKUi.NewRect("Video Options", root).gameObject;
            PMSDKUi.SetLayout(videoOptionsRow, -1f, 24f, 1f, -1f);
            HorizontalLayoutGroup optLayout = PMSDKUi.AddHorizontal(videoOptionsRow, 0, 12f);
            optLayout.childForceExpandWidth = true;
            loopToggle = PMSDKUi.AddToggle(videoOptionsRow.transform, "Loop", true, OnLoopChanged);
            muteToggle = PMSDKUi.AddToggle(videoOptionsRow.transform, "Mute", false, OnMuteChanged);

            patternRow = PMSDKUi.NewRect("Pattern Row", root).gameObject;
            PMSDKUi.SetLayout(patternRow, -1f, 28f, 1f, -1f);
            PMSDKUi.AddHorizontal(patternRow, 0, 4f);
            patternCycleButton = PMSDKUi.AddButton(patternRow.transform, "Next pattern", OnPatternCycle);
            PMSDKUi.SetLayout(patternCycleButton.gameObject, -1f, 28f, 1f, -1f);

            if (compact) {
                browse.GetComponentInChildren<Text>().fontSize = 12;
            }
            Bind(null);
        }

        /// <summary>Point the panel at a content (or null to show an empty/disabled state).</summary>
        public void Bind(PMSDKSkinContent target)
        {
            content = target;
            Refresh();
        }

        public void Refresh()
        {
            suppress = true;
            bool has = content != null;
            PMSDKSkinContentMode mode = has ? content.Mode : PMSDKSkinContentMode.None;

            PMSDKUi.SetButtonActive(videoButton, mode == PMSDKSkinContentMode.VideoUrl);
            PMSDKUi.SetButtonActive(imageButton, mode == PMSDKSkinContentMode.ImageFile);
            PMSDKUi.SetButtonActive(patternButton, mode == PMSDKSkinContentMode.TestPattern);
            PMSDKUi.SetButtonActive(webcamButton, mode == PMSDKSkinContentMode.Webcam);
            PMSDKUi.SetButtonActive(noneButton, has && mode == PMSDKSkinContentMode.None);

            bool fileMode = mode == PMSDKSkinContentMode.VideoUrl || mode == PMSDKSkinContentMode.ImageFile;
            pathField.interactable = has;
            if (!pathField.isFocused) {
                pathField.text = fileMode ? PMSDKSkinMedia.CurrentPath(content) : "";
            }

            videoOptionsRow.SetActive(mode == PMSDKSkinContentMode.VideoUrl);
            patternRow.SetActive(mode == PMSDKSkinContentMode.TestPattern);
            if (has) {
                loopToggle.SetIsOnWithoutNotify(content.Loop);
                muteToggle.SetIsOnWithoutNotify(content.Mute);
            }

            if (!has) {
                statusLabel.text = "Select a skin to choose its media.";
                statusLabel.color = PMSDKUi.TextDim;
            } else if (PMSDKSkinMedia.IsFileMissing(content)) {
                statusLabel.text = "File not found: " + PMSDKSkinMedia.CurrentPath(content);
                statusLabel.color = PMSDKUi.Danger;
            } else {
                statusLabel.text = PMSDKSkinMedia.Describe(content);
                statusLabel.color = PMSDKUi.TextDim;
            }
            suppress = false;
        }

        private static Button AddSourceButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            Button b = PMSDKUi.AddButton(parent, label, onClick);
            PMSDKUi.SetLayout(b.gameObject, -1f, 30f, 1f, -1f);
            return b;
        }

        // ---- handlers ----

        private void OnVideoClicked()
        {
            PickAndAssign(PMSDKFileDialogKind.Video);
        }

        private void OnImageClicked()
        {
            PickAndAssign(PMSDKFileDialogKind.Image);
        }

        private void OnBrowseClicked()
        {
            PickAndAssign(PMSDKFileDialogKind.Any);
        }

        private void OnPatternClicked()
        {
            if (content == null) {
                return;
            }
            PMSDKSkinMedia.AssignTestPattern(content);
            Changed();
        }

        private void OnWebcamClicked()
        {
            if (content == null) {
                return;
            }
            PMSDKSkinMedia.AssignWebcam(content);
            Changed();
        }

        private void OnNoneClicked()
        {
            if (content == null) {
                return;
            }
            PMSDKSkinMedia.Clear(content);
            Changed();
        }

        private void OnPatternCycle()
        {
            if (content == null) {
                return;
            }
            Array values = Enum.GetValues(typeof(PMSDKTestPatternType));
            int index = Array.IndexOf(values, content.TestPatternType);
            content.TestPatternType = (PMSDKTestPatternType)values.GetValue((index + 1) % values.Length);
            Changed();
        }

        private void OnLoopChanged(bool value)
        {
            if (suppress || content == null) {
                return;
            }
            content.Loop = value;
            Changed();
        }

        private void OnMuteChanged(bool value)
        {
            if (suppress || content == null) {
                return;
            }
            content.Mute = value;
            Changed();
        }

        private void OnPathEdited(string text)
        {
            if (suppress || content == null) {
                return;
            }
            if (string.IsNullOrWhiteSpace(text)) {
                return;
            }
            if (PMSDKSkinMedia.TryAssignFile(content, text)) {
                Changed();
            } else {
                statusLabel.text = "Unsupported file type (use a video such as mp4, mov, avi, webm, mkv, or an image png/jpg).";
                statusLabel.color = PMSDKUi.Danger;
            }
        }

        private void PickAndAssign(PMSDKFileDialogKind kind)
        {
            if (content == null) {
                return;
            }
            if (!PMSDKFileDialog.IsSupported) {
                statusLabel.text = "File dialog is Windows-only. Paste a path into the field.";
                statusLabel.color = PMSDKUi.TextDim;
                return;
            }
            string picked;
            string start = lastDirectory;
            if (string.IsNullOrEmpty(start)) {
                string existing = PMSDKSkinMedia.CurrentPath(content);
                start = string.IsNullOrEmpty(existing) ? null : SafeDirectory(existing);
            }
            if (!PMSDKFileDialog.TryPickFile(kind, start, out picked)) {
                return;
            }
            lastDirectory = SafeDirectory(picked);
            if (PMSDKSkinMedia.TryAssignFile(content, picked)) {
                Changed();
            } else {
                statusLabel.text = "Unsupported file type: " + Path.GetExtension(picked);
                statusLabel.color = PMSDKUi.Danger;
            }
        }

        private static string SafeDirectory(string path)
        {
            try {
                return Path.GetDirectoryName(path);
            } catch (ArgumentException) {
                return null;
            }
        }

        private void Changed()
        {
            Refresh();
            Action<PMSDKSkinContent> handler = ContentChanged;
            if (handler != null) {
                handler(content);
            }
        }
    }
}
