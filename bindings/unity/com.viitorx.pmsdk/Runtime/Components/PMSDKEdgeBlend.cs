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
        public float Gamma = 1.0f;
        [Range(0, 1)] public float BlackLevel = 0.0f;

        private IntPtr nativeBlendConfig = IntPtr.Zero;
        public IntPtr NativeBlendConfig => nativeBlendConfig;

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
                
                IntPtr left = NativeBindings.pmsdk_blendconfig_get_left_edge(nativeBlendConfig);
                IntPtr right = NativeBindings.pmsdk_blendconfig_get_right_edge(nativeBlendConfig);
                IntPtr top = NativeBindings.pmsdk_blendconfig_get_top_edge(nativeBlendConfig);
                IntPtr bottom = NativeBindings.pmsdk_blendconfig_get_bottom_edge(nativeBlendConfig);

                if (left != IntPtr.Zero)
                {
                    NativeBindings.pmsdk_edgeblend_set_size(left, LeftEdge);
                    NativeBindings.pmsdk_edgeblend_set_gamma(left, Gamma);
                }
                if (right != IntPtr.Zero)
                {
                    NativeBindings.pmsdk_edgeblend_set_size(right, RightEdge);
                    NativeBindings.pmsdk_edgeblend_set_gamma(right, Gamma);
                }
                if (top != IntPtr.Zero)
                {
                    NativeBindings.pmsdk_edgeblend_set_size(top, TopEdge);
                    NativeBindings.pmsdk_edgeblend_set_gamma(top, Gamma);
                }
                if (bottom != IntPtr.Zero)
                {
                    NativeBindings.pmsdk_edgeblend_set_size(bottom, BottomEdge);
                    NativeBindings.pmsdk_edgeblend_set_gamma(bottom, Gamma);
                }
            }
        }
    }
}
