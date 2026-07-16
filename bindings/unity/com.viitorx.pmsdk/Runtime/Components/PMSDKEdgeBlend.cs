using System;
using UnityEngine;
using vxpmsdk;

namespace vxpmsdk.Components
{
    [ExecuteAlways]
    [RequireComponent(typeof(PMSDKMeshWarp))]
    public class PMSDKEdgeBlend : MonoBehaviour
    {
        [Header("Blend Sizes (0 to 1)")]
        [Range(0, 1)] public float LeftEdge = 0.0f;
        [Range(0, 1)] public float RightEdge = 0.0f;
        [Range(0, 1)] public float TopEdge = 0.0f;
        [Range(0, 1)] public float BottomEdge = 0.0f;

        [Header("Gamma Correction")]
        [Tooltip("Projector display gamma (typically ~2.2). The blend ramp exponent is set to 1/Gamma so the two projectors' light SUMS TO FULL across the overlap. Setting this to the projector gamma removes the dark seam; a value of 1 (linear ramp) leaves a dark band on real gamma-2.2 projectors.")]
        public float Gamma = 2.2f;
        [Range(0, 1)] public float BlackLevel = 0.0f;

        private IntPtr nativeBlendConfig = IntPtr.Zero;
        public IntPtr NativeBlendConfig => nativeBlendConfig;

        private static readonly int BlackLevelId = Shader.PropertyToID("_BlackLevel");
        private MeshRenderer meshRenderer;

        private void OnEnable()
        {
            if (nativeBlendConfig == IntPtr.Zero)
            {
                nativeBlendConfig = NativeBindings.pmsdk_blendconfig_create();
            }
        }

        private void OnDisable()
        {
            if (nativeBlendConfig != IntPtr.Zero)
            {
                NativeBindings.pmsdk_blendconfig_destroy(nativeBlendConfig);
                nativeBlendConfig = IntPtr.Zero;
            }
        }

        private void Update()
        {
            if (nativeBlendConfig != IntPtr.Zero)
            {
                NativeBindings.pmsdk_blendconfig_set_black_level(nativeBlendConfig, BlackLevel);

                // Native applies pow(x, gammaArg) to the ramp. To make two projectors'
                // emitted light sum to full across the overlap, the ramp exponent must
                // be 1/projectorGamma (so (x^(1/g))^g + ((1-x)^(1/g))^g = 1). Passing
                // the projector gamma directly (2.2) would darken the seam instead.
                float rampExponent = 1f / Mathf.Max(0.01f, Gamma);

                IntPtr left = NativeBindings.pmsdk_blendconfig_get_left_edge(nativeBlendConfig);
                IntPtr right = NativeBindings.pmsdk_blendconfig_get_right_edge(nativeBlendConfig);
                IntPtr top = NativeBindings.pmsdk_blendconfig_get_top_edge(nativeBlendConfig);
                IntPtr bottom = NativeBindings.pmsdk_blendconfig_get_bottom_edge(nativeBlendConfig);

                if (left != IntPtr.Zero)
                {
                    NativeBindings.pmsdk_edgeblend_set_size(left, LeftEdge);
                    NativeBindings.pmsdk_edgeblend_set_gamma(left, rampExponent);
                }
                if (right != IntPtr.Zero)
                {
                    NativeBindings.pmsdk_edgeblend_set_size(right, RightEdge);
                    NativeBindings.pmsdk_edgeblend_set_gamma(right, rampExponent);
                }
                if (top != IntPtr.Zero)
                {
                    NativeBindings.pmsdk_edgeblend_set_size(top, TopEdge);
                    NativeBindings.pmsdk_edgeblend_set_gamma(top, rampExponent);
                }
                if (bottom != IntPtr.Zero)
                {
                    NativeBindings.pmsdk_edgeblend_set_size(bottom, BottomEdge);
                    NativeBindings.pmsdk_edgeblend_set_gamma(bottom, rampExponent);
                }
            }

            // Drive the shader's uniform black-level floor.
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.sharedMaterial != null &&
                meshRenderer.sharedMaterial.HasProperty(BlackLevelId))
            {
                meshRenderer.sharedMaterial.SetFloat(BlackLevelId, BlackLevel);
            }
        }
    }
}
