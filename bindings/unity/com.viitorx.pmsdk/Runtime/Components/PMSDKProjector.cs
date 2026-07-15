using System;
using UnityEngine;
using vxpmsdk;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Represents a physical projector in the scene.
    /// Manages the native WarpNode and Projector configurations.
    /// </summary>
    [ExecuteAlways]
    public class PMSDKProjector : MonoBehaviour
    {
        public float AspectRatio = 1.777f; // 16:9
        public float ThrowRatio = 1.2f;

        private IntPtr nativeProjector = IntPtr.Zero;
        private IntPtr nativeWarpNode = IntPtr.Zero;

        public IntPtr NativeWarpNode => nativeWarpNode;

        private void OnEnable()
        {
            if (nativeProjector == IntPtr.Zero)
            {
                nativeProjector = NativeBindings.pmsdk_projector_create();
            }
            if (nativeWarpNode == IntPtr.Zero)
            {
                nativeWarpNode = NativeBindings.pmsdk_warpnode_create();
            }
            UpdateProjectorSettings();
        }

        private void OnDisable()
        {
            if (nativeProjector != IntPtr.Zero)
            {
                NativeBindings.pmsdk_projector_destroy(nativeProjector);
                nativeProjector = IntPtr.Zero;
            }
            if (nativeWarpNode != IntPtr.Zero)
            {
                NativeBindings.pmsdk_warpnode_destroy(nativeWarpNode);
                nativeWarpNode = IntPtr.Zero;
            }
        }

        private void Update()
        {
            // Sync settings to native every frame in editor, or we could only sync when changed
            if (Application.isEditor && !Application.isPlaying)
            {
                UpdateProjectorSettings();
            }
        }

        public void UpdateProjectorSettings()
        {
            if (nativeProjector != IntPtr.Zero)
            {
                NativeBindings.pmsdk_projector_set_aspect_ratio(nativeProjector, AspectRatio);
                NativeBindings.pmsdk_projector_set_throw_ratio(nativeProjector, ThrowRatio);
            }
        }
    }
}
