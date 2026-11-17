using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// "Skin from photo" flow for the Skin Workspace: pick an image, let the SDK (C++ SkinGen)
    /// find the object and its parts, then choose how to turn them into skins (pixel-accurate mask
    /// quad, polygon with background-coloured holes, or plain polygon outline).
    /// </summary>
    [DisallowMultipleComponent]
    public class PMSDKPhotoSkinDialog : MonoBehaviour
    {
        private const int MaxPolygonPoints = 128;

        private Canvas canvas;
        private Text titleText;
        private Text bodyText;
        private readonly Button[] buttons = new Button[4];
        private readonly Text[] buttonLabels = new Text[4];
        private readonly Action[] actions = new Action[4];

        private PMSDKSkinEditorController controller;
        private PMSDKPhotoExtraction extraction;
        private string photoName;
        private readonly System.Collections.Generic.List<PMSDKSkinEditorInput> pausedInputs =
            new System.Collections.Generic.List<PMSDKSkinEditorInput>();

        private void OnEnable()
        {
            PMSDKUi.EnsureEventSystem();
            BuildUi();
            SetVisible(false);
        }

        private void OnDisable()
        {
            ResumeInputs();
            if (canvas != null) {
                Destroy(canvas.gameObject);
                canvas = null;
            }
        }

        /// <summary>Ask for an image and run the extraction. Does nothing if the user cancels the file dialog.</summary>
        public void Begin(PMSDKSkinEditorController target)
        {
            controller = target;
            if (!PMSDKFileDialog.IsSupported) {
                ShowMessage("File dialog is Windows-only", "Photo skins need the Windows file dialog in this build.");
                return;
            }
            string path;
            if (!PMSDKFileDialog.TryPickFile(PMSDKFileDialogKind.Image, null, out path)) {
                return;
            }
            Process(path);
        }

        /// <summary>Run the extraction on a known file (also used by tests and drag-drop).</summary>
        public void Process(string path)
        {
            photoName = Path.GetFileNameWithoutExtension(path);
            if (!PMSDKPhotoSkin.IsAvailable()) {
                ShowMessage("Update needed",
                    "Photo skins need the updated ProjectionMappingSDK.dll (it contains the new photo module). Copy the new DLL into the project's Plugins folder and restart.");
                return;
            }
            PMSDKPhotoExtraction result;
            string error;
            if (!PMSDKPhotoSkin.TryExtract(path, MaxPolygonPoints, out result, out error)) {
                ShowMessage("Could not make skins from this photo", error);
                return;
            }
            extraction = result;
            ShowChoices();
        }

        // ------------------------------------------------------------------ steps

        private void ShowChoices()
        {
            StringBuilder body = new StringBuilder();
            body.Append(photoName).Append("   (found with: ").Append(extraction.UsedMode).Append(")\n\n");
            body.Append(extraction.Parts.Count == 1 ? "1 part:" : extraction.Parts.Count + " parts (largest first):").Append('\n');
            for (int i = 0; i < extraction.Parts.Count && i < 6; i++) {
                PMSDKPhotoPart p = extraction.Parts[i];
                int holes = 0;
                for (int c = 0; c < p.Contours.Count; c++) {
                    if (p.Contours[c].IsHole) {
                        holes++;
                    }
                }
                body.Append("  ").Append(i + 1).Append(".  ").Append(Mathf.RoundToInt(p.AreaFraction * 100f)).Append("% of the image");
                body.Append(holes > 0 ? ",  " + holes + (holes == 1 ? " hole" : " holes") : "").Append('\n');
            }
            body.Append("\nChoose how to build the skins. You can still adjust the corners afterwards.");
            Present("Skin from photo", body.ToString(),
                "Pixel-accurate mask (best)", delegate { Create(PMSDKPhotoSkinMode.QuadWithMask); },
                "Polygon, holes filled with background colour", delegate { Create(PMSDKPhotoSkinMode.PolygonFillHoles); },
                "Polygon outline only", delegate { Create(PMSDKPhotoSkinMode.PolygonOutline); },
                "Cancel", Close);
        }

        private void Create(PMSDKPhotoSkinMode mode)
        {
            System.Collections.Generic.List<PMSDKSkin> skins = PMSDKPhotoSkinBuilder.CreateSkins(controller, extraction, mode, photoName);
            Close();
            if (skins.Count == 0) {
                ShowMessage("No skins were created", "The photo had no usable parts.");
            }
        }

        private void ShowMessage(string title, string message)
        {
            Present(title, message, "OK", Close, null, null, null, null, null, null);
        }

        private void Close()
        {
            SetVisible(false);
            ResumeInputs();
            extraction = null;
        }

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            canvas = PMSDKUi.CreateCanvas("PMSDK Photo Skin", 0, 3900);
            RectTransform root = (RectTransform)canvas.transform;
            RectTransform shade = PMSDKUi.NewRect("Shade", root);
            PMSDKUi.AddImage(shade.gameObject, new Color(0f, 0f, 0f, 0.65f));
            PMSDKUi.Stretch(shade, 0f, 0f, 0f, 0f);

            RectTransform panel = PMSDKUi.NewRect("Dialog", shade);
            PMSDKUi.AddImage(panel.gameObject, PMSDKUi.Panel);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(620f, 460f);
            VerticalLayoutGroup vlg = PMSDKUi.AddVertical(panel.gameObject, 20, 8f);
            vlg.childControlHeight = true;

            titleText = PMSDKUi.AddText(panel, "", 20, PMSDKUi.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            PMSDKUi.SetLayout(titleText.gameObject, -1f, 34f, 1f, -1f);

            bodyText = PMSDKUi.AddText(panel, "", 14, PMSDKUi.TextMain, TextAnchor.UpperLeft, FontStyle.Normal);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            PMSDKUi.SetLayout(bodyText.gameObject, -1f, 190f, 1f, -1f);

            for (int i = 0; i < buttons.Length; i++) {
                int index = i;
                buttons[i] = PMSDKUi.AddButton(panel, "", delegate { OnButton(index); });
                PMSDKUi.SetLayout(buttons[i].gameObject, -1f, 34f, 1f, -1f);
                buttonLabels[i] = buttons[i].GetComponentInChildren<Text>();
            }
        }

        private void Present(string title, string body, string l0, Action a0, string l1, Action a1,
            string l2, Action a2, string l3, Action a3)
        {
            SetVisible(true);
            PauseInputs();
            titleText.text = title;
            bodyText.text = body;
            SetButton(0, l0, a0);
            SetButton(1, l1, a1);
            SetButton(2, l2, a2);
            SetButton(3, l3, a3);
            PMSDKUi.SetButtonActive(buttons[0], a0 != null);
        }

        private void SetButton(int index, string label, Action action)
        {
            buttons[index].gameObject.SetActive(action != null);
            actions[index] = action;
            if (label != null) {
                buttonLabels[index].text = label;
            }
        }

        private void OnButton(int index)
        {
            Action action = actions[index];
            if (action != null) {
                action();
            }
        }

        private void SetVisible(bool visible)
        {
            if (canvas != null) {
                canvas.enabled = visible;
            }
        }

        private void PauseInputs()
        {
            if (pausedInputs.Count > 0) {
                return;
            }
            PMSDKSkinEditorInput[] inputs = FindObjectsByType<PMSDKSkinEditorInput>(FindObjectsSortMode.None);
            for (int i = 0; i < inputs.Length; i++) {
                if (inputs[i].InputEnabled) {
                    inputs[i].InputEnabled = false;
                    pausedInputs.Add(inputs[i]);
                }
            }
        }

        private void ResumeInputs()
        {
            for (int i = 0; i < pausedInputs.Count; i++) {
                if (pausedInputs[i] != null) {
                    pausedInputs[i].InputEnabled = true;
                }
            }
            pausedInputs.Clear();
        }
    }
}
