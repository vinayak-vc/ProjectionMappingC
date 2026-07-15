using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    [RequireComponent(typeof(PMSDKCornerPin))]
    public class PMSDKCornerPinUI : MonoBehaviour
    {
        public float HandleSize = 30f;
        public Color HandleColor = new Color(0, 1, 0, 0.7f); // Transparent Green

        private PMSDKCornerPin cornerPin;
        private Camera projectorCamera;
        
        private GameObject canvasObj;
        private GameObject[] handles = new GameObject[4];

        private void OnEnable()
        {
            cornerPin = GetComponent<PMSDKCornerPin>();
            PMSDKMeshWarp warp = GetComponent<PMSDKMeshWarp>();

            if (warp == null || warp.Projector == null)
            {
                Debug.LogWarning("PMSDKCornerPinUI requires a PMSDKMeshWarp with a linked Projector to spawn the UI Canvas.");
                return;
            }

            projectorCamera = warp.Projector.GetComponent<Camera>();
            if (projectorCamera == null)
            {
                Debug.LogWarning("Projector does not have a Camera component attached.");
                return;
            }

            // Ensure an EventSystem exists in the scene for dragging to work
            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            CreateCanvas();
        }

        private void OnDisable()
        {
            if (canvasObj != null)
            {
                Destroy(canvasObj);
            }
        }

        private void CreateCanvas()
        {
            canvasObj = new GameObject("CornerPin_UI_Canvas");
            canvasObj.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = projectorCamera;
            canvas.planeDistance = projectorCamera.nearClipPlane + 0.1f;
            canvas.sortingOrder = 100; // Render on top of everything

            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create 4 handles
            handles[0] = CreateHandle("TopLeft", cornerPin.TopLeft, (val) => cornerPin.TopLeft = val);
            handles[1] = CreateHandle("TopRight", cornerPin.TopRight, (val) => cornerPin.TopRight = val);
            handles[2] = CreateHandle("BottomLeft", cornerPin.BottomLeft, (val) => cornerPin.BottomLeft = val);
            handles[3] = CreateHandle("BottomRight", cornerPin.BottomRight, (val) => cornerPin.BottomRight = val);
        }

        private GameObject CreateHandle(string name, Vector2 initialNormalizedPos, System.Action<Vector2> updateAction)
        {
            GameObject handle = new GameObject("Handle_" + name);
            handle.transform.SetParent(canvasObj.transform, false);
            handle.layer = LayerMask.NameToLayer("UI");

            Image img = handle.AddComponent<Image>();
            img.color = HandleColor;

            RectTransform rect = handle.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(HandleSize, HandleSize);

            // Anchor exactly at the normalized position
            rect.anchorMin = initialNormalizedPos;
            rect.anchorMax = initialNormalizedPos;
            rect.anchoredPosition = Vector2.zero;

            // Add Drag Event
            EventTrigger trigger = handle.AddComponent<EventTrigger>();
            
            EventTrigger.Entry dragEntry = new EventTrigger.Entry();
            dragEntry.eventID = EventTriggerType.Drag;
            dragEntry.callback.AddListener((data) =>
            {
                PointerEventData ped = (PointerEventData)data;
                
                // Convert screen position to normalized coordinates (0 to 1)
                Vector2 normalizedPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasObj.GetComponent<RectTransform>(), 
                    ped.position, 
                    projectorCamera, 
                    out Vector2 localPoint);

                Rect rectSize = canvasObj.GetComponent<RectTransform>().rect;
                
                // Calculate normalized from -0.5 to +0.5 space to 0 to 1 space
                normalizedPos.x = (localPoint.x - rectSize.x) / rectSize.width;
                normalizedPos.y = (localPoint.y - rectSize.y) / rectSize.height;

                // Clamp to 0-1 bounds
                normalizedPos.x = Mathf.Clamp01(normalizedPos.x);
                normalizedPos.y = Mathf.Clamp01(normalizedPos.y);

                // Update UI Anchor visually
                rect.anchorMin = normalizedPos;
                rect.anchorMax = normalizedPos;
                rect.anchoredPosition = Vector2.zero;

                // Update C++ Warp properties!
                updateAction(normalizedPos);
            });
            trigger.triggers.Add(dragEntry);

            return handle;
        }
    }
}
