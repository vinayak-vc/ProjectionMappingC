using System;
using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Temporarily changes what the projector shows during Auto Adjust measurements and puts it all
    /// back on <see cref="Dispose"/>: hides skins, shows a pattern on one skin (masks and warp map
    /// suppressed, opacity 1), and floods the projector background (every camera that renders the
    /// skins' layer) with a solid colour. Each change is recorded once, before the first change.
    /// </summary>
    public sealed class PMSDKCalibrationDisplay : IDisposable
    {
        private readonly List<PMSDKSkin> skins = new List<PMSDKSkin>();
        private readonly Dictionary<MeshRenderer, bool> rendererStates = new Dictionary<MeshRenderer, bool>();
        private readonly Dictionary<PMSDKSkin, SkinState> skinStates = new Dictionary<PMSDKSkin, SkinState>();
        private readonly Dictionary<Camera, CameraState> cameraStates = new Dictionary<Camera, CameraState>();
        private readonly int layerMask;

        private sealed class SkinState
        {
            public Texture Override;
            public float Opacity;
            public bool SuppressMasks;
            public bool SuppressWarpMap;
        }

        private sealed class CameraState
        {
            public CameraClearFlags ClearFlags;
            public Color Background;
        }

        /// <param name="allSkins">Every skin that may be on screen.</param>
        /// <param name="skinLayer">Layer the skins render on (cameras rendering it get the flood colour).</param>
        public PMSDKCalibrationDisplay(IEnumerable<PMSDKSkin> allSkins, int skinLayer)
        {
            if (allSkins != null) {
                foreach (PMSDKSkin skin in allSkins) {
                    if (skin != null && !skins.Contains(skin)) {
                        skins.Add(skin);
                    }
                }
            }
            layerMask = 1 << Mathf.Clamp(skinLayer, 0, 31);
        }

        /// <summary>Hides every known skin except <paramref name="keep"/> (null hides all).</summary>
        public void HideAllExcept(PMSDKSkin keep)
        {
            foreach (PMSDKSkin skin in skins) {
                MeshRenderer renderer = skin != null ? skin.GetComponent<MeshRenderer>() : null;
                if (renderer == null) {
                    continue;
                }
                if (!rendererStates.ContainsKey(renderer)) {
                    rendererStates[renderer] = renderer.enabled;
                }
                renderer.enabled = skin == keep && rendererStates[renderer];
            }
        }

        /// <summary>
        /// Shows <paramref name="pattern"/> on <paramref name="skin"/> with masks suppressed; the warp map is
        /// suppressed too unless <paramref name="keepWarpMap"/> (verifying a built map).
        /// </summary>
        public void ShowPattern(PMSDKSkin skin, Texture pattern, bool keepWarpMap = false)
        {
            if (skin == null) {
                return;
            }
            if (!skinStates.ContainsKey(skin)) {
                skinStates[skin] = new SkinState {
                    Override = skin.TestPatternOverride,
                    Opacity = skin.Opacity,
                    SuppressMasks = skin.SuppressMasks,
                    SuppressWarpMap = skin.SuppressWarpMap,
                };
            }
            MeshRenderer renderer = skin.GetComponent<MeshRenderer>();
            if (renderer != null) {
                if (!rendererStates.ContainsKey(renderer)) {
                    rendererStates[renderer] = renderer.enabled;
                }
                renderer.enabled = true;
            }
            skin.TestPatternOverride = pattern;
            skin.Opacity = 1f;
            skin.SuppressMasks = true;
            skin.SuppressWarpMap = !keepWarpMap;
        }

        /// <summary>Sets the projector background (cameras rendering the skin layer) to a solid colour.</summary>
        public void SetBackground(Color color)
        {
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) {
                if (camera == null || (camera.cullingMask & layerMask) == 0) {
                    continue;
                }
                if (!cameraStates.ContainsKey(camera)) {
                    cameraStates[camera] = new CameraState { ClearFlags = camera.clearFlags, Background = camera.backgroundColor };
                }
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = color;
            }
        }

        /// <summary>Puts the projector background back as it was.</summary>
        public void RestoreBackground()
        {
            foreach (KeyValuePair<Camera, CameraState> entry in cameraStates) {
                if (entry.Key != null) {
                    entry.Key.clearFlags = entry.Value.ClearFlags;
                    entry.Key.backgroundColor = entry.Value.Background;
                }
            }
            cameraStates.Clear();
        }

        public void Dispose()
        {
            RestoreBackground();
            foreach (KeyValuePair<PMSDKSkin, SkinState> entry in skinStates) {
                if (entry.Key != null) {
                    entry.Key.TestPatternOverride = entry.Value.Override;
                    entry.Key.Opacity = entry.Value.Opacity;
                    entry.Key.SuppressMasks = entry.Value.SuppressMasks;
                    entry.Key.SuppressWarpMap = entry.Value.SuppressWarpMap;
                }
            }
            skinStates.Clear();
            foreach (KeyValuePair<MeshRenderer, bool> entry in rendererStates) {
                if (entry.Key != null) {
                    entry.Key.enabled = entry.Value;
                }
            }
            rendererStates.Clear();
        }
    }
}
