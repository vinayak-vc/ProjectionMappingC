using UnityEngine;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Visible UI for PMSDKProjectorPoseCalibrator, built at runtime:
    ///  - On the projector OUTPUT: a crosshair the operator drives onto the real
    ///    feature, plus green dots at already-captured marks.
    ///  - On the operator console (Display 1): status text + a live preview of the
    ///    virtual twin with a yellow ring on the anchor currently being marked, so the
    ///    operator knows which physical feature to find.
    /// </summary>
    public class PMSDKPoseCalibratorOverlay : MonoBehaviour
    {
        private PMSDKProjectorPoseCalibrator calib;

        // Output-canvas (on the projector camera)
        private Canvas outCanvas;
        private RectTransform crossH, crossV;
        private readonly System.Collections.Generic.List<RectTransform> dots = new System.Collections.Generic.List<RectTransform>();

        // Operator HUD (Display 1)
        private Canvas hudCanvas;
        private Text status;
        private RawImage preview;
        private RectTransform previewRect;
        private RectTransform anchorRing;
        private Camera previewCam;
        private RenderTexture previewRT;

        public static PMSDKPoseCalibratorOverlay Create(PMSDKProjectorPoseCalibrator c)
        {
            var go = new GameObject("PMSDK PoseCalib Overlay");
            go.transform.SetParent(c.transform, false);
            var o = go.AddComponent<PMSDKPoseCalibratorOverlay>();
            o.calib = c;
            o.Build();
            return o;
        }

        private void Build()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int uiLayer = LayerMask.NameToLayer("UI");

            // --- projector output canvas: crosshair + captured dots ---
            if (calib.ProjectorCamera != null)
            {
                var co = new GameObject("OutputCanvas");
                co.transform.SetParent(transform, false);
                co.layer = uiLayer;
                outCanvas = co.AddComponent<Canvas>();
                outCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                outCanvas.worldCamera = calib.ProjectorCamera;
                outCanvas.planeDistance = calib.ProjectorCamera.nearClipPlane + 0.1f;
                outCanvas.sortingOrder = 150;
                co.AddComponent<CanvasScaler>();
                crossH = MakeBar(co.transform, new Vector2(60, 3), new Color(1f, 0.9f, 0.1f, 0.95f));
                crossV = MakeBar(co.transform, new Vector2(3, 60), new Color(1f, 0.9f, 0.1f, 0.95f));
            }

            // --- operator HUD on Display 1 ---
            var ho = new GameObject("PoseHUD");
            ho.transform.SetParent(transform, false);
            ho.layer = uiLayer;
            hudCanvas = ho.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.targetDisplay = 0;
            hudCanvas.sortingOrder = 210;
            ho.AddComponent<CanvasScaler>();

            var panel = new GameObject("Panel"); panel.transform.SetParent(ho.transform, false); panel.layer = uiLayer;
            var pImg = panel.AddComponent<Image>(); pImg.color = new Color(0, 0, 0, 0.8f);
            var pRect = panel.GetComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0.5f, 1f); pRect.anchorMax = new Vector2(0.5f, 1f); pRect.pivot = new Vector2(0.5f, 1f);
            pRect.sizeDelta = new Vector2(760, 70); pRect.anchoredPosition = new Vector2(0, -8);
            status = MakeText(panel.transform, font, 20, TextAnchor.MiddleCenter);
            var sRect = status.GetComponent<RectTransform>(); sRect.anchorMin = Vector2.zero; sRect.anchorMax = Vector2.one; sRect.offsetMin = new Vector2(12, 6); sRect.offsetMax = new Vector2(-12, -6);

            // preview of the twin with the current anchor ringed
            var pv = new GameObject("Preview"); pv.transform.SetParent(ho.transform, false); pv.layer = uiLayer;
            preview = pv.AddComponent<RawImage>();
            previewRect = preview.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0.5f, 1f); previewRect.anchorMax = new Vector2(0.5f, 1f); previewRect.pivot = new Vector2(0.5f, 1f);
            previewRect.sizeDelta = new Vector2(360, 203); previewRect.anchoredPosition = new Vector2(0, -84);
            anchorRing = MakeBar(pv.transform, new Vector2(28, 28), new Color(1f, 0.9f, 0.1f, 0f)); // image; tinted ring via outline below
            var ringImg = anchorRing.GetComponent<Image>(); ringImg.color = new Color(1f, 0.9f, 0.1f, 0.9f);

            // hidden service camera to render the twin preview
            var pcam = new GameObject("PoseCalib Preview Cam"); pcam.transform.SetParent(transform, false);
            previewCam = pcam.AddComponent<Camera>();
            previewCam.enabled = false; previewCam.clearFlags = CameraClearFlags.SolidColor; previewCam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            previewRT = new RenderTexture(360, 203, 16) { name = "PMSDK_PoseCalibPreviewRT" };
            preview.texture = previewRT;
        }

        private void OnDestroy()
        {
            if (previewRT != null) previewRT.Release();
        }

        private RectTransform MakeBar(Transform parent, Vector2 size, Color c)
        {
            var g = new GameObject("Bar"); g.transform.SetParent(parent, false); g.layer = parent.gameObject.layer;
            var img = g.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
            var rt = g.GetComponent<RectTransform>(); rt.sizeDelta = size;
            return rt;
        }

        private static Text MakeText(Transform parent, Font font, int size, TextAnchor a)
        {
            var g = new GameObject("Text"); g.transform.SetParent(parent, false); g.layer = parent.gameObject.layer;
            var t = g.AddComponent<Text>(); t.font = font; t.fontSize = size; t.alignment = a; t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private void LateUpdate()
        {
            bool active = calib != null && calib.Active;
            if (outCanvas != null) outCanvas.gameObject.SetActive(active);
            if (hudCanvas != null) hudCanvas.gameObject.SetActive(active);
            if (!active) return;

            // crosshair at the marker (viewport-anchored)
            if (crossH != null)
            {
                Vector2 m = calib.Marker;
                foreach (var rt in new[] { crossH, crossV }) { rt.anchorMin = m; rt.anchorMax = m; rt.anchoredPosition = Vector2.zero; }
            }

            // captured dots
            var caps = calib.Captured;
            EnsureDots(caps.Count);
            for (int i = 0; i < dots.Count; i++)
            {
                bool show = i < caps.Count;
                dots[i].gameObject.SetActive(show && active);
                if (show) { dots[i].anchorMin = caps[i].Viewport; dots[i].anchorMax = caps[i].Viewport; dots[i].anchoredPosition = Vector2.zero; }
            }

            status.text = calib.Status;
            UpdatePreview();
        }

        private void EnsureDots(int n)
        {
            while (dots.Count < n && outCanvas != null)
            {
                var d = MakeBar(outCanvas.transform, new Vector2(14, 14), new Color(0.1f, 1f, 0.3f, 0.9f));
                dots.Add(d);
            }
        }

        private void UpdatePreview()
        {
            if (previewCam == null || calib.ProjectorCamera == null) return;
            var anchors = calib.ModelAnchors;
            int idx = Mathf.Clamp(calib.CurrentAnchor, 0, anchors.Count - 1);
            if (anchors.Count == 0 || anchors[idx] == null) { anchorRing.gameObject.SetActive(false); return; }

            // frame the twin from a fixed 3/4 angle
            Bounds b = new Bounds(anchors[0].position, Vector3.zero);
            foreach (var a in anchors) if (a != null) b.Encapsulate(a.position);
            Vector3 c = b.center; float radius = Mathf.Max(b.extents.magnitude, 0.5f);
            previewCam.transform.position = c + new Vector3(radius * 1.4f, radius * 1.1f, -radius * 1.8f);
            previewCam.transform.LookAt(c);
            previewCam.cullingMask = calib.ProjectorCamera.cullingMask;
            previewCam.targetTexture = previewRT;
            previewCam.Render();
            previewCam.targetTexture = null;

            // ring the current anchor at its projected position in the preview
            Vector3 vp = previewCam.WorldToViewportPoint(anchors[idx].position);
            bool front = vp.z > 0f;
            anchorRing.gameObject.SetActive(front);
            if (front)
            {
                anchorRing.anchorMin = anchorRing.anchorMax = new Vector2(vp.x, vp.y);
                anchorRing.anchoredPosition = Vector2.zero;
                float pulse = 20f + 10f * Mathf.PingPong(Time.unscaledTime * 3f, 1f);
                anchorRing.sizeDelta = new Vector2(pulse, pulse);
            }
        }
    }
}
