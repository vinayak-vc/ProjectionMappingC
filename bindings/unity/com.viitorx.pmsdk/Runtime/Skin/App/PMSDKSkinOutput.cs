using System;
using UnityEngine;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Routes the projector camera so the UI can show it AND the real output can run:
    ///   - the projector camera renders into a 16:9 RenderTexture (the preview shown in the
    ///     Workspace / Player UI, including the editor overlay handles);
    ///   - a clean clone camera (no overlay, skin layer only) renders to the output display
    ///     (Display 2 by default) for the real projector;
    ///   - <see cref="FullscreenPreview"/> shows the preview fullscreen on the primary display
    ///     (single-monitor shows / quick checks); Esc or the UI toggles it back.
    /// </summary>
    [DisallowMultipleComponent]
    public class PMSDKSkinOutput : MonoBehaviour
    {
        [SerializeField] private PMSDKProjector projector;
        [SerializeField] private int outputDisplayIndex = 1;
        [SerializeField] private int previewWidth = 1920;
        [SerializeField] private int previewHeight = 1080;
        [SerializeField] private string skinLayerName = "TransparentFX";

        private Camera projectorCamera;
        private Camera outputCamera;
        private RenderTexture previewTexture;
        private Canvas fullscreenCanvas;
        private RectTransform fullscreenRect;
        private bool fullscreenPreview;

        /// <summary>The rect showing the raster in the fullscreen view (null when not fullscreen);
        /// the editor maps the mouse through it so points can be dragged in that view too.</summary>
        public RectTransform FullscreenRect {
            get { return fullscreenRect; }
        }

        public event Action<bool> FullscreenChanged;

        public PMSDKProjector Projector {
            get { return projector; }
            set { projector = value; }
        }

        public RenderTexture PreviewTexture {
            get { return previewTexture; }
        }

        public bool FullscreenPreview {
            get { return fullscreenPreview; }
            set { SetFullscreen(value); }
        }

        /// <summary>Number of connected displays beyond the primary one.</summary>
        public static int ExtraDisplayCount {
            get { return Mathf.Max(0, Display.displays.Length - 1); }
        }

        private void OnEnable()
        {
            if (projector == null) {
                projector = FindFirstObjectByType<PMSDKProjector>();
            }
            if (projector == null) {
                Debug.LogWarning("PMSDKSkinOutput: no PMSDKProjector found; preview/output disabled.", this);
                return;
            }
            projectorCamera = projector.GetComponent<Camera>();
            if (projectorCamera == null) {
                Debug.LogWarning("PMSDKSkinOutput: projector has no Camera.", this);
                return;
            }

            previewTexture = new RenderTexture(previewWidth, previewHeight, 16, RenderTextureFormat.ARGB32);
            previewTexture.name = "PMSDKSkinPreview";
            previewTexture.filterMode = FilterMode.Bilinear;
            previewTexture.Create();
            projectorCamera.targetTexture = previewTexture;

            CreateOutputCamera();
            ActivateOutputDisplay();
            ApplyBackground(PMSDKSkinStore.BackgroundColor);
        }

        /// <summary>Set (and remember) the colour shown wherever no skin covers the output —
        /// e.g. a grey wall can use a matching grey instead of black.</summary>
        public void SetBackground(Color color)
        {
            color.a = 1f;
            PMSDKSkinStore.BackgroundColor = color;
            ApplyBackground(color);
        }

        public Color BackgroundColor {
            get { return PMSDKSkinStore.BackgroundColor; }
        }

        private void ApplyBackground(Color color)
        {
            if (projectorCamera != null) {
                projectorCamera.clearFlags = CameraClearFlags.SolidColor;
                projectorCamera.backgroundColor = color;
            }
            if (outputCamera != null) {
                outputCamera.backgroundColor = color;
            }
        }

        private void OnDisable()
        {
            SetFullscreen(false);
            if (projectorCamera != null && projectorCamera.targetTexture == previewTexture) {
                projectorCamera.targetTexture = null;
            }
            if (outputCamera != null) {
                Destroy(outputCamera.gameObject);
                outputCamera = null;
            }
            if (previewTexture != null) {
                previewTexture.Release();
                Destroy(previewTexture);
                previewTexture = null;
            }
        }

        private void Update()
        {
            if (fullscreenPreview && Input.GetKeyDown(KeyCode.Escape)) {
                SetFullscreen(false);
            }
        }

        private void CreateOutputCamera()
        {
            GameObject go = new GameObject("PMSDK Output Camera");
            go.transform.SetParent(projectorCamera.transform.parent, false);
            go.transform.position = projectorCamera.transform.position;
            go.transform.rotation = projectorCamera.transform.rotation;
            go.transform.localScale = projectorCamera.transform.localScale;

            outputCamera = go.AddComponent<Camera>();
            outputCamera.CopyFrom(projectorCamera);
            outputCamera.targetTexture = null;
            outputCamera.targetDisplay = outputDisplayIndex;
            outputCamera.clearFlags = CameraClearFlags.SolidColor;
            outputCamera.backgroundColor = PMSDKSkinStore.BackgroundColor;
            int skinLayer = LayerMask.NameToLayer(skinLayerName);
            outputCamera.cullingMask = skinLayer >= 0 ? (1 << skinLayer) : projectorCamera.cullingMask;
            outputCamera.depth = projectorCamera.depth + 1f;
        }

        private void ActivateOutputDisplay()
        {
            if (outputDisplayIndex > 0 && outputDisplayIndex < Display.displays.Length) {
                Display.displays[outputDisplayIndex].Activate();
            }
        }

        private void SetFullscreen(bool value)
        {
            if (fullscreenPreview == value) {
                return;
            }
            if (value && previewTexture == null) {
                return;
            }
            fullscreenPreview = value;
            if (value) {
                BuildFullscreenCanvas();
            } else if (fullscreenCanvas != null) {
                Destroy(fullscreenCanvas.gameObject);
                fullscreenCanvas = null;
                fullscreenRect = null;
            }
            Action<bool> handler = FullscreenChanged;
            if (handler != null) {
                handler(value);
            }
        }

        private void BuildFullscreenCanvas()
        {
            fullscreenCanvas = PMSDKUi.CreateCanvas("PMSDK Fullscreen Output", 0, 5000);
            RectTransform rt = PMSDKUi.NewRect("Output", fullscreenCanvas.transform);
            PMSDKUi.Stretch(rt, 0f, 0f, 0f, 0f);
            RawImage raw = rt.gameObject.AddComponent<RawImage>();
            raw.texture = previewTexture;
            raw.color = Color.white;
            raw.raycastTarget = false;
            AspectRatioFitter fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)previewWidth / previewHeight;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            fullscreenRect = rt;

            // Small hint so the keyboard-driven editing in this view is discoverable.
            Text hint = PMSDKUi.AddText(fullscreenCanvas.transform,
                "Output view  -  drag points with the mouse, arrows nudge, R/E/P/F draw, N add shape, Ctrl+Z undo   |   F3, F11 or Esc: back",
                13, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerCenter, FontStyle.Normal);
            PMSDKUi.Stretch(hint.rectTransform, 0f, 0f, 0f, 6f);
        }
    }
}
