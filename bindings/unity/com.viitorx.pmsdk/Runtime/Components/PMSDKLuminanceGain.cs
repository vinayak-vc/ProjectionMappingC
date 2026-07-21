using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Applies a camera-measured per-projector luminance gain map into the
    /// PMSDK/UnlitWarp shader (`_GainTex`). The map dims each projector's bright
    /// (centre) regions down to a shared target so vignetting no longer leaves a
    /// brighter/darker band across a blended wall — see <see cref="PMSDKLuminanceCompensation"/>.
    ///
    /// The gain is sampled in RASTER space: PMSDKMeshWarp writes each warped vertex's
    /// raster position into UV1, and the shader looks the gain up there, so the
    /// compensation stays locked to the physical projector vignette as the warp changes.
    ///
    /// Holds no map by default (a no-op). The calibration manager fills it after an
    /// auto-align sweep, or it is restored from the calibration JSON. Requires the
    /// surface material to use PMSDK/UnlitWarp.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class PMSDKLuminanceGain : MonoBehaviour
    {
        [SerializeField, HideInInspector] private int width;
        [SerializeField, HideInInspector] private int height;
        // Row-major, y-up, each in [0,1]. Serialized so the map survives domain reloads
        // and scene saves (persistence to the calibration JSON is separate).
        [SerializeField, HideInInspector] private float[] gain;

        private Texture2D gainTex;
        private bool dirty;
        private MeshRenderer meshRenderer;

        private static readonly int GainTexId = Shader.PropertyToID("_GainTex");
        private static readonly int UseGainTexId = Shader.PropertyToID("_UseGainTex");

        /// <summary>True when a usable gain map is loaded (drives UV1 output in PMSDKMeshWarp).</summary>
        public bool HasMap => gain != null && width > 0 && height > 0 && gain.Length == width * height;
        public int Width => width;
        public int Height => height;

        /// <summary>Install a gain map (row-major, y-up, length w*h). Pass null/empty to clear.</summary>
        public void SetGainMap(float[] values, int w, int h)
        {
            if (values == null || w <= 0 || h <= 0 || values.Length != w * h)
            {
                ClearGainMap();
                return;
            }
            width = w;
            height = h;
            gain = (float[])values.Clone();
            dirty = true;
            Apply();
        }

        public void ClearGainMap()
        {
            width = 0;
            height = 0;
            gain = null;
            dirty = true;
            Apply();
        }

        /// <summary>Copy of the current map (for persistence); null if none.</summary>
        public float[] GetGainMap() => HasMap ? (float[])gain.Clone() : null;

        private void OnEnable()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            dirty = true;
            Apply();
        }

        private void OnDisable()
        {
            // Leave the material as a no-op so a disabled component never dims output.
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            var mat = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            if (mat != null && mat.HasProperty(UseGainTexId))
            {
                mat.SetFloat(UseGainTexId, 0f);
            }
            DestroyTex();
        }

        private void Update()
        {
            Apply();
        }

        private void EnsureTex()
        {
            if (!HasMap) return;
            if (gainTex == null || gainTex.width != width || gainTex.height != height)
            {
                DestroyTex();
                gainTex = new Texture2D(width, height, TextureFormat.RFloat, false, linear: true)
                {
                    name = "PMSDK_GainTex",
                    hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                dirty = true;
            }
            if (dirty)
            {
                gainTex.SetPixelData(gain, 0);
                gainTex.Apply(false);
                dirty = false;
            }
        }

        public void Apply()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            var mat = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            if (mat == null || !mat.HasProperty(UseGainTexId)) return;

            if (!enabled || !HasMap)
            {
                mat.SetFloat(UseGainTexId, 0f);
                return;
            }

            EnsureTex();
            mat.SetTexture(GainTexId, gainTex);
            mat.SetFloat(UseGainTexId, 1f);
        }

        private void DestroyTex()
        {
            if (gainTex == null) return;
            if (Application.isEditor) DestroyImmediate(gainTex);
            else Destroy(gainTex);
            gainTex = null;
        }
    }
}
