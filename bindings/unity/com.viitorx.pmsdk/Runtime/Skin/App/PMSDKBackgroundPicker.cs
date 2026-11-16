using UnityEngine;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Compact "output background" control: colour swatches (black, greys, white, warm wall)
    /// plus a hex field. Drives <see cref="PMSDKSkinOutput.SetBackground"/>. Shared by the
    /// Skin Workspace inspector and the Skin Player.
    /// </summary>
    public class PMSDKBackgroundPicker
    {
        private static readonly string[] SwatchNames = { "Black", "Dark grey", "Grey", "Light grey", "White", "Warm wall" };
        private static readonly string[] SwatchHex = { "000000", "333333", "808080", "B8B8B8", "FFFFFF", "D9CDB8" };

        private readonly PMSDKSkinOutput output;
        private readonly InputField hexField;
        private readonly Text statusLabel;
        private readonly Image preview;

        public PMSDKBackgroundPicker(Transform parent, PMSDKSkinOutput outputRouter)
        {
            output = outputRouter;
            RectTransform root = PMSDKUi.NewRect("Background Picker", parent);
            PMSDKUi.AddVertical(root.gameObject, 0, 4f);
            ContentSizeFitter fit = root.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            PMSDKUi.AddDimLabel(root, "Colour shown where no skin covers the output.");

            RectTransform swatchRow = PMSDKUi.NewRect("Swatches", root);
            PMSDKUi.SetLayout(swatchRow.gameObject, -1f, 34f, 1f, -1f);
            HorizontalLayoutGroup swatchLayout = PMSDKUi.AddHorizontal(swatchRow.gameObject, 0, 4f);
            swatchLayout.childForceExpandWidth = true;
            for (int i = 0; i < SwatchHex.Length; i++) {
                string hex = SwatchHex[i];
                Button b = PMSDKUi.AddButton(swatchRow, "", delegate { ApplyHex(hex); });
                Color swatchColor;
                PMSDKSkinStore.TryParseHexColor(hex, out swatchColor);
                PMSDKUi.SetButtonColor(b, swatchColor);
                PMSDKUi.SetLayout(b.gameObject, -1f, 34f, 1f, -1f);
            }

            RectTransform hexRow = PMSDKUi.NewRect("Hex Row", root);
            PMSDKUi.SetLayout(hexRow.gameObject, -1f, 28f, 1f, -1f);
            HorizontalLayoutGroup hexLayout = PMSDKUi.AddHorizontal(hexRow.gameObject, 0, 6f);
            hexLayout.childForceExpandWidth = false;

            RectTransform previewRect = PMSDKUi.NewRect("Current", hexRow);
            preview = PMSDKUi.AddImage(previewRect.gameObject, Color.black);
            PMSDKUi.SetLayout(previewRect.gameObject, 28f, 28f, 0f, -1f);

            hexField = PMSDKUi.AddInputField(hexRow, "", "Hex, e.g. 9A9A9A", OnHexEdited);
            PMSDKUi.SetLayout(hexField.gameObject, -1f, 28f, 1f, -1f);

            statusLabel = PMSDKUi.AddDimLabel(root, "");
            Refresh();
        }

        public void Refresh()
        {
            Color c = PMSDKSkinStore.BackgroundColor;
            preview.color = c;
            if (!hexField.isFocused) {
                hexField.text = ColorUtility.ToHtmlStringRGB(c);
            }
        }

        private void OnHexEdited(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) {
                return;
            }
            ApplyHex(text);
        }

        private void ApplyHex(string text)
        {
            Color c;
            if (!PMSDKSkinStore.TryParseHexColor(text, out c)) {
                statusLabel.text = "Use 6 hex digits, e.g. 808080.";
                statusLabel.color = PMSDKUi.Danger;
                return;
            }
            if (output != null) {
                output.SetBackground(c);
            } else {
                PMSDKSkinStore.BackgroundColor = c;
            }
            statusLabel.text = "";
            Refresh();
        }
    }
}
