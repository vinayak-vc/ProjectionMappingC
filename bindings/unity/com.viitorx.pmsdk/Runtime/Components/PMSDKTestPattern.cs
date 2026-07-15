using UnityEngine;

namespace vxpmsdk.Components
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class PMSDKTestPattern : MonoBehaviour
    {
        [Header("Grid Settings")]
        public int GridSize = 8;
        public Color Color1 = Color.white;
        public Color Color2 = Color.black;

        [Header("Overlays")]
        public bool ShowBorder = true;
        public Color BorderColor = Color.red;
        
        public bool ShowCrosshair = true;
        public Color CrosshairColor = Color.green;

        public bool ShowCircle = true;
        public Color CircleColor = Color.yellow;

        private MeshRenderer meshRenderer;
        private Material originalMaterial;
        private Material testPatternMaterial;
        private Texture2D testTexture;

        private void OnEnable()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            originalMaterial = meshRenderer.sharedMaterial;

            GenerateTestPattern();
            
            // Create a simple unlit material to display the texture
            Shader unlitShader = Shader.Find("Unlit/Texture");
            if (unlitShader != null)
            {
                testPatternMaterial = new Material(unlitShader);
                testPatternMaterial.mainTexture = testTexture;
                meshRenderer.sharedMaterial = testPatternMaterial;
            }
            else
            {
                Debug.LogWarning("Unlit/Texture shader not found. Test pattern might not render correctly.");
            }
        }

        private void OnDisable()
        {
            if (meshRenderer != null && originalMaterial != null)
            {
                meshRenderer.sharedMaterial = originalMaterial;
            }

            if (testPatternMaterial != null)
            {
                if (Application.isEditor)
                    DestroyImmediate(testPatternMaterial);
                else
                    Destroy(testPatternMaterial);
            }

            if (testTexture != null)
            {
                if (Application.isEditor)
                    DestroyImmediate(testTexture);
                else
                    Destroy(testTexture);
            }
        }

        private void GenerateTestPattern()
        {
            int texSize = 1024;
            testTexture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            testTexture.filterMode = FilterMode.Point;
            testTexture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[texSize * texSize];

            int cellPixelSize = texSize / GridSize;
            float centerX = texSize / 2f;
            float centerY = texSize / 2f;
            float radius = (texSize / 2f) * 0.9f;
            float radiusSq = radius * radius;
            float innerRadiusSq = (radius - 4) * (radius - 4);

            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    int index = y * texSize + x;
                    Color pColor = Color1;

                    // 1. Checkerboard
                    int cx = x / cellPixelSize;
                    int cy = y / cellPixelSize;
                    if ((cx + cy) % 2 == 1)
                    {
                        pColor = Color2;
                    }

                    // 2. Circle
                    if (ShowCircle)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        float distSq = dx * dx + dy * dy;
                        if (distSq <= radiusSq && distSq >= innerRadiusSq)
                        {
                            pColor = CircleColor;
                        }
                    }

                    // 3. Crosshair (2 pixels thick)
                    if (ShowCrosshair)
                    {
                        if (x == texSize / 2 || x == (texSize / 2) - 1 || 
                            y == texSize / 2 || y == (texSize / 2) - 1)
                        {
                            pColor = CrosshairColor;
                        }
                    }

                    // 4. Border (4 pixels thick)
                    if (ShowBorder)
                    {
                        if (x < 4 || x >= texSize - 4 || y < 4 || y >= texSize - 4)
                        {
                            pColor = BorderColor;
                        }
                    }

                    pixels[index] = pColor;
                }
            }

            testTexture.SetPixels(pixels);
            testTexture.Apply();
        }
    }
}
