using UnityEngine;

namespace vxpmsdk.Components
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class PMSDKTestPattern : MonoBehaviour
    {
        [Header("Pattern")]
        [Tooltip("Professional pattern set: alignment checker, focus grid, convergence targets, SMPTE-style bars, gray ramp, solids. Cycle with Y in calibration mode.")]
        public PMSDKTestPatternType Pattern = PMSDKTestPatternType.Checkerboard;

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
        // Serialized so the original screen material survives domain reloads and
        // scene reloads while the pattern is enabled. Without this, the runtime
        // test-pattern material gets saved into the scene as the "real" material
        // and the original (e.g. the RenderTexture screen material) is lost.
        [SerializeField, HideInInspector] private Material originalMaterial;
        private Material testPatternMaterial;
        private Texture2D testTexture;

        private void OnEnable()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            // Capture only once: if originalMaterial is already set we are recovering
            // from a reload and sharedMaterial may be a stale test-pattern material.
            if (originalMaterial == null)
            {
                originalMaterial = meshRenderer.sharedMaterial;
            }

            GenerateTestPattern();

            // Create a simple unlit material to display the texture
            Shader unlitShader = Shader.Find("Unlit/Texture");
            if (unlitShader != null)
            {
                testPatternMaterial = new Material(unlitShader);
                testPatternMaterial.name = "PMSDK_TestPattern (runtime)";
                // Never serialize the runtime pattern material/texture into the scene.
                testPatternMaterial.hideFlags = HideFlags.DontSave;
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
                // Clear so the next OnEnable re-captures whatever material the user
                // has assigned in the meantime.
                originalMaterial = null;
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

        /// <summary>Advance to the next pattern type (wraps) and regenerate.</summary>
        public void CyclePattern(int delta = 1)
        {
            int count = System.Enum.GetValues(typeof(PMSDKTestPatternType)).Length;
            Pattern = (PMSDKTestPatternType)(((int)Pattern + delta + count) % count);
            Regenerate();
        }

        /// <summary>Regenerate the pattern texture in place (after changing Pattern/knobs).</summary>
        public void Regenerate()
        {
            if (testTexture == null) return;
            FillPattern();
        }

        private void GenerateTestPattern()
        {
            int texSize = 1024;
            testTexture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            testTexture.hideFlags = HideFlags.DontSave;
            testTexture.filterMode = FilterMode.Point;
            testTexture.wrapMode = TextureWrapMode.Clamp;
            FillPattern();
        }

        private void FillPattern()
        {
            int texSize = testTexture.width;
            Color[] pixels = new Color[texSize * texSize];
            PMSDKTestPatterns.Generate(Pattern, pixels, texSize, GridSize, Color1, Color2,
                new PMSDKTestPatterns.Overlays
                {
                    Border = ShowBorder, BorderColor = BorderColor,
                    Crosshair = ShowCrosshair, CrosshairColor = CrosshairColor,
                    Circle = ShowCircle, CircleColor = CircleColor
                });
            testTexture.SetPixels(pixels);
            testTexture.Apply();
        }
    }
}
