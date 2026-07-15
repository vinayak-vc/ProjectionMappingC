using UnityEngine;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Per-surface calibration visuals rendered on that surface's projector output:
    /// four corner handles (selected corner = yellow, pulsing, larger) and a big
    /// identify badge ("PROJECTOR 2 — Left_Screen") so the on-site technician knows
    /// which physical beam they are editing. Built entirely at runtime.
    /// </summary>
    public class PMSDKCalibrationOverlay : MonoBehaviour
    {
        private const float HandleSize = 26f;
        private const float SelectedHandleSize = 44f;
        private static readonly Color UnselectedColor = new Color(0f, 1f, 0f, 0.65f);
        private static readonly Color SelectedColor = new Color(1f, 0.9f, 0.1f, 0.95f);
        private static readonly Color BadgeSelected = new Color(1f, 0.9f, 0.1f, 0.9f);
        private static readonly Color BadgeUnselected = new Color(1f, 1f, 1f, 0.35f);

        private static readonly Color BlendTint = new Color(0f, 0.8f, 1f, 0.18f);
        private static readonly Color BlendTintSelected = new Color(1f, 0.55f, 0f, 0.4f);

        private PMSDKCalibrationManager manager;
        private PMSDKCalibrationManager.Surface surface;
        private Canvas canvas;
        private readonly RectTransform[] handles = new RectTransform[4];
        private readonly Image[] handleImages = new Image[4];
        private readonly RectTransform[] blendZones = new RectTransform[4]; // L R T B
        private readonly Image[] blendZoneImages = new Image[4];
        private Text badge;

        public static PMSDKCalibrationOverlay GetOrCreate(PMSDKCalibrationManager manager, PMSDKCalibrationManager.Surface surface)
        {
            if (surface.Warp == null) return null;
            // Parent under the MANAGER, never under the warp surface: the surface
            // carries a 16:9 X-scale, and a ScreenSpaceCamera canvas inheriting that
            // scale renders all overlay geometry stretched (blend zones ended up
            // outside the camera frustum entirely).
            foreach (var existing in manager.GetComponentsInChildren<PMSDKCalibrationOverlay>(true))
            {
                if (existing.surface != null && existing.surface.Warp == surface.Warp)
                {
                    existing.manager = manager;
                    existing.surface = surface;
                    return existing;
                }
            }
            var go = new GameObject("PMSDK Calibration Overlay - " + surface.Id);
            go.transform.SetParent(manager.transform, false);
            var overlay = go.AddComponent<PMSDKCalibrationOverlay>();
            overlay.manager = manager;
            overlay.surface = surface;
            overlay.SetVisible(false);
            return overlay;
        }

        public void SetVisible(bool visible)
        {
            if (visible && canvas == null)
            {
                Build();
            }
            if (canvas != null)
            {
                canvas.gameObject.SetActive(visible);
            }
        }

        private void Build()
        {
            if (surface.ProjectorCamera == null)
            {
                return; // no camera to attach to yet; manager rediscovers on next enter
            }

            var canvasObj = new GameObject("Canvas");
            canvasObj.transform.SetParent(transform, false);
            canvasObj.layer = LayerMask.NameToLayer("UI");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = surface.ProjectorCamera;
            canvas.planeDistance = surface.ProjectorCamera.nearClipPlane + 0.1f;
            canvas.sortingOrder = 100;
            canvasObj.AddComponent<CanvasScaler>();

            // Blend-zone tint rects (under handles): visualize each edge's falloff
            // band while in blend submode.
            for (int i = 0; i < 4; i++)
            {
                var z = new GameObject("BlendZone_" + PMSDKCalibrationManager.EdgeNames[i]);
                z.transform.SetParent(canvasObj.transform, false);
                z.layer = canvasObj.layer;
                blendZoneImages[i] = z.AddComponent<Image>();
                blendZones[i] = z.GetComponent<RectTransform>();
                blendZones[i].sizeDelta = Vector2.zero;
                z.SetActive(false);
            }

            for (int i = 0; i < 4; i++)
            {
                var h = new GameObject("Handle_" + PMSDKCalibrationManager.CornerNames[i]);
                h.transform.SetParent(canvasObj.transform, false);
                h.layer = canvasObj.layer;
                handleImages[i] = h.AddComponent<Image>();
                handles[i] = h.GetComponent<RectTransform>();
                handles[i].sizeDelta = new Vector2(HandleSize, HandleSize);
            }

            var badgeObj = new GameObject("IdentifyBadge");
            badgeObj.transform.SetParent(canvasObj.transform, false);
            badgeObj.layer = canvasObj.layer;
            badge = badgeObj.AddComponent<Text>();
            badge.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            badge.fontSize = 42;
            badge.alignment = TextAnchor.UpperCenter;
            badge.horizontalOverflow = HorizontalWrapMode.Overflow;
            badge.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = badge.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -40);
            int displayNumber = surface.ProjectorCamera.targetDisplay + 1;
            badge.text = $"PROJECTOR {displayNumber}\n{surface.Id}";
        }

        private void LateUpdate()
        {
            if (canvas == null || !canvas.gameObject.activeSelf || surface?.CornerPin == null || manager == null)
            {
                return;
            }

            bool isSelectedSurface = manager.Selected == surface;
            float pulse = 0.7f + 0.3f * Mathf.PingPong(Time.unscaledTime * 2f, 1f);

            for (int i = 0; i < 4; i++)
            {
                if (handles[i] == null) continue;
                Vector2 pos = PMSDKCalibrationManager.GetCorner(surface.CornerPin, i);
                handles[i].anchorMin = pos;
                handles[i].anchorMax = pos;
                handles[i].anchoredPosition = Vector2.zero;

                bool isSelectedCorner = isSelectedSurface && !manager.BlendSubmode && manager.SelectedCorner == i;
                handles[i].sizeDelta = Vector2.one * (isSelectedCorner ? SelectedHandleSize : HandleSize);
                Color c = isSelectedCorner ? SelectedColor : UnselectedColor;
                if (isSelectedCorner) c.a *= pulse;
                handleImages[i].color = c;
            }

            if (badge != null)
            {
                badge.color = isSelectedSurface ? BadgeSelected : BadgeUnselected;
            }

            UpdateBlendZones(isSelectedSurface);
        }

        private void UpdateBlendZones(bool isSelectedSurface)
        {
            bool show = manager.BlendSubmode && surface.Blend != null;
            for (int i = 0; i < 4; i++)
            {
                if (blendZones[i] == null) continue;
                if (!show)
                {
                    blendZones[i].gameObject.SetActive(false);
                    continue;
                }
                float w;
                switch (i)
                {
                    case 0: w = surface.Blend.LeftEdge;
                        blendZones[i].anchorMin = new Vector2(0f, 0f);
                        blendZones[i].anchorMax = new Vector2(w, 1f);
                        break;
                    case 1: w = surface.Blend.RightEdge;
                        blendZones[i].anchorMin = new Vector2(1f - w, 0f);
                        blendZones[i].anchorMax = new Vector2(1f, 1f);
                        break;
                    case 2: w = surface.Blend.TopEdge;
                        blendZones[i].anchorMin = new Vector2(0f, 1f - w);
                        blendZones[i].anchorMax = new Vector2(1f, 1f);
                        break;
                    default: w = surface.Blend.BottomEdge;
                        blendZones[i].anchorMin = new Vector2(0f, 0f);
                        blendZones[i].anchorMax = new Vector2(1f, w);
                        break;
                }
                blendZones[i].offsetMin = Vector2.zero;
                blendZones[i].offsetMax = Vector2.zero;
                bool active = w > 0.0001f;
                blendZones[i].gameObject.SetActive(active);
                if (active)
                {
                    bool isSelectedEdge = isSelectedSurface && manager.SelectedEdge == i;
                    blendZoneImages[i].color = isSelectedEdge ? BlendTintSelected : BlendTint;
                }
            }
        }
    }
}
