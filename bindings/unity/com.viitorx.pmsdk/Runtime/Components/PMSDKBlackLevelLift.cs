using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Applies a camera-measured per-region black-level lift map into the PMSDK/UnlitWarp
    /// shader (`_BlackLiftTex`). It raises each projector's single-projector (non-overlap)
    /// black floor UP to the overlap's doubled black so a blended wall reads as one uniform
    /// (slightly grey) black instead of a brighter central band on dark content — see
    /// <see cref="PMSDKBlackLevelCompensation"/>. This is the additive counterpart of
    /// <see cref="PMSDKLuminanceGain"/> and reuses its raster-space UV1 plumbing.
    ///
    /// Holds no map by default (a no-op). The calibration manager fills it after an
    /// auto-align sweep, or it is restored from the calibration JSON. Requires the surface
    /// material to use PMSDK/UnlitWarp.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class PMSDKBlackLevelLift : MonoBehaviour
    {
        [SerializeField, HideInInspector] private int width;
        [SerializeField, HideInInspector] private int height;
        // Row-major, y-up, each a signal lift in [0,1]. Serialized so the map survives
        // domain reloads and scene saves (JSON persistence is separate).
        [SerializeField, HideInInspector] private float[] lift;

        private Texture2D liftTex;
        private bool dirty;
        private MeshRenderer meshRenderer;

        private static readonly int BlackLiftTexId = Shader.PropertyToID("_BlackLiftTex");
        private static readonly int UseBlackLiftId = Shader.PropertyToID("_UseBlackLift");

        /// <summary>True when a usable lift map is loaded (drives UV1 output in PMSDKMeshWarp).</summary>
        public bool HasMap => lift != null && width > 0 && height > 0 && lift.Length == width * height;
        public int Width => width;
        public int Height => height;

        /// <summary>Install a lift map (row-major, y-up, length w*h, signal units). Pass null/empty to clear.</summary>
        public void SetLiftMap(float[] values, int w, int h)
        {
            if (values == null || w <= 0 || h <= 0 || values.Length != w * h)
            {
                ClearLiftMap();
                return;
            }
            width = w;
            height = h;
            lift = (float[])values.Clone();
            dirty = true;
            Apply();
        }

        public void ClearLiftMap()
        {
            width = 0;
            height = 0;
            lift = null;
            dirty = true;
            Apply();
        }

        /// <summary>Copy of the current map (for persistence); null if none.</summary>
        public float[] GetLiftMap() => HasMap ? (float[])lift.Clone() : null;

        private void OnEnable()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            dirty = true;
            Apply();
        }

        private void OnDisable()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            var mat = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            if (mat != null && mat.HasProperty(UseBlackLiftId))
            {
                mat.SetFloat(UseBlackLiftId, 0f);
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
            if (liftTex == null || liftTex.width != width || liftTex.height != height)
            {
                DestroyTex();
                liftTex = new Texture2D(width, height, TextureFormat.RFloat, false, linear: true)
                {
                    name = "PMSDK_BlackLiftTex",
                    hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                dirty = true;
            }
            if (dirty)
            {
                liftTex.SetPixelData(lift, 0);
                liftTex.Apply(false);
                dirty = false;
            }
        }

        public void Apply()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            var mat = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            if (mat == null || !mat.HasProperty(UseBlackLiftId)) return;

            if (!enabled || !HasMap)
            {
                mat.SetFloat(UseBlackLiftId, 0f);
                return;
            }

            EnsureTex();
            mat.SetTexture(BlackLiftTexId, liftTex);
            mat.SetFloat(UseBlackLiftId, 1f);
        }

        private void DestroyTex()
        {
            if (liftTex == null) return;
            if (Application.isEditor) DestroyImmediate(liftTex);
            else Destroy(liftTex);
            liftTex = null;
        }
    }
}
