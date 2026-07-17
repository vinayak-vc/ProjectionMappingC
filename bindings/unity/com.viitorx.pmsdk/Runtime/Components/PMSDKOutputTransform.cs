using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Output orientation for a projector: rotate the content 0/90/180/270 and/or
    /// mirror it, driven into the PMSDK/UnlitWarp shader's UV transform. Needed for
    /// physically rotated (portrait) projectors, ceiling-mounted flips, and rear
    /// projection (mirror X). Defaults are a no-op.
    ///
    /// This transforms the sampled content only; corner-pin/grid warp still operate
    /// in raster space, so alignment survives a rotation change.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    public class PMSDKOutputTransform : MonoBehaviour
    {
        public enum Rotation { None = 0, CW90 = 1, Rot180 = 2, CW270 = 3 }

        public Rotation Rotate = Rotation.None;
        public bool MirrorX = false;
        public bool MirrorY = false;

        private static readonly int RotId = Shader.PropertyToID("_UVRotation");
        private static readonly int MirrorXId = Shader.PropertyToID("_MirrorX");
        private static readonly int MirrorYId = Shader.PropertyToID("_MirrorY");

        private MeshRenderer meshRenderer;

        private void OnEnable()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            Apply();
        }

        private void OnDisable()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            var mat = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            if (mat != null && mat.HasProperty(RotId))
            {
                mat.SetFloat(RotId, 0f);
                mat.SetFloat(MirrorXId, 0f);
                mat.SetFloat(MirrorYId, 0f);
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
            if (mat == null || !mat.HasProperty(RotId)) return;
            mat.SetFloat(RotId, (float)(int)Rotate);
            mat.SetFloat(MirrorXId, MirrorX ? 1f : 0f);
            mat.SetFloat(MirrorYId, MirrorY ? 1f : 0f);
        }
    }
}
