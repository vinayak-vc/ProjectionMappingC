using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Per-projector color / luminance matching, driven into the PMSDK/UnlitWarp
    /// shader. Different projectors (or lamps at different ages) rarely match in
    /// brightness and colour temperature; this brings them into agreement:
    ///   out = pow(saturate(content * Gain + Offset), 1 / OutputGamma)   (per channel)
    ///
    /// Defaults are a no-op (gain 1, offset 0, gamma 1). Add to a warp surface and
    /// adjust per projector. Requires the surface material to use PMSDK/UnlitWarp.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class PMSDKColorCorrection : MonoBehaviour
    {
        [Tooltip("Per-channel multiplier (brightness / white balance).")]
        public Color Gain = Color.white;
        [Tooltip("Per-channel additive offset (lift).")]
        public Color Offset = new Color(0, 0, 0, 0);
        [Tooltip("Per-channel output gamma.")]
        public Vector3 OutputGamma = Vector3.one;

        private static readonly int GainId = Shader.PropertyToID("_ColorGain");
        private static readonly int OffsetId = Shader.PropertyToID("_ColorOffset");
        private static readonly int GammaId = Shader.PropertyToID("_OutGamma");

        private MeshRenderer meshRenderer;

        private void OnEnable()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            Apply();
        }

        private void OnDisable()
        {
            // Restore no-op so a disabled component doesn't leave the material tinted.
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            var mat = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            if (mat != null && mat.HasProperty(GainId))
            {
                mat.SetColor(GainId, Color.white);
                mat.SetColor(OffsetId, new Color(0, 0, 0, 0));
                mat.SetVector(GammaId, Vector3.one);
            }
        }

        private void Update()
        {
            Apply();
        }

        public void Apply()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            var mat = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            if (mat == null || !mat.HasProperty(GainId)) return;
            mat.SetColor(GainId, Gain);
            mat.SetColor(OffsetId, Offset);
            mat.SetVector(GammaId, new Vector4(
                Mathf.Max(0.01f, OutputGamma.x),
                Mathf.Max(0.01f, OutputGamma.y),
                Mathf.Max(0.01f, OutputGamma.z), 0f));
        }
    }
}
