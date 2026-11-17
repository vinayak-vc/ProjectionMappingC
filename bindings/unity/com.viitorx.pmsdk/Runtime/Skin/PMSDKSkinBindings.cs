using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Optional companion to <see cref="PMSDKSkinLoader"/>: override each skin's content
    /// source per <see cref="PMSDKSkin.SkinId"/> from the CONTENT scene, without touching
    /// the authored <see cref="PMSDKSkinSet"/>.
    ///
    /// Bindings take precedence over the skin set's <c>defaultContent</c>: content
    /// authors can point a saved "front" skin at a different video clip in each scene
    /// while a tech-op keeps the geometry identical.
    ///
    /// A missing entry falls through to the SkinSet's <c>defaultContent</c>. So authored
    /// defaults still play if the scene doesn't override.
    /// </summary>
    [DisallowMultipleComponent]
    public class PMSDKSkinBindings : MonoBehaviour
    {
        [System.Serializable]
        public class Binding
        {
            [Tooltip("PMSDKSkin.SkinId this binding overrides. Case-sensitive.")]
            public string SkinId;

            public PMSDKSkinContentMode Mode = PMSDKSkinContentMode.None;

            // Source-specific fields — only the one matching Mode is read.
            public Texture Texture;
            public RenderTexture RenderTexture;
            public VideoClip VideoClip;
            [Tooltip("VideoUrl mode — HTTP/HTTPS or file:// URL.")]
            public string VideoUrl;
            [Tooltip("Camera mode — a scene camera to render into a per-skin RT.")]
            public Camera SourceCamera;
            [Tooltip("Webcam mode — device name. Empty = first available.")]
            public string WebcamDeviceName;
            [Tooltip("ExternalTexture mode — arbitrary texture (Spout/NDI/etc).")]
            public Texture ExternalTexture;

            [Header("Playback (VideoClip / VideoUrl)")]
            public bool Loop = true;
            public bool Mute = false;
            public double StartTime = 0.0;
            [Range(0.1f, 8f)] public float PlaybackRate = 1f;

            [Header("Common")]
            [Range(0f, 1f)] public float Opacity = 1f;
        }

        public List<Binding> Overrides = new List<Binding>();

        public bool TryGet(string skinId, out Binding binding)
        {
            for (int i = 0; i < Overrides.Count; i++)
            {
                if (Overrides[i] != null && Overrides[i].SkinId == skinId)
                {
                    binding = Overrides[i];
                    return true;
                }
            }
            binding = null;
            return false;
        }

        /// <summary>Push the binding into an existing <see cref="PMSDKSkinContent"/>.</summary>
        public static void Apply(PMSDKSkinContent content, Binding b)
        {
            if (content == null || b == null) return;
            content.Mode = b.Mode;
            switch (b.Mode)
            {
                case PMSDKSkinContentMode.Texture:         content.StaticTexture = b.Texture; break;
                case PMSDKSkinContentMode.RenderTexture:   content.ExternalRenderTexture = b.RenderTexture; break;
                case PMSDKSkinContentMode.VideoClip:
                    content.VideoClip = b.VideoClip;
                    content.Loop = b.Loop;
                    content.Mute = b.Mute;
                    content.StartTime = b.StartTime;
                    content.PlaybackRate = b.PlaybackRate;
                    break;
                case PMSDKSkinContentMode.VideoUrl:
                    content.VideoUrl = b.VideoUrl;
                    content.Loop = b.Loop;
                    content.Mute = b.Mute;
                    content.StartTime = b.StartTime;
                    content.PlaybackRate = b.PlaybackRate;
                    break;
                case PMSDKSkinContentMode.Camera:          content.SourceCamera = b.SourceCamera; break;
                case PMSDKSkinContentMode.Webcam:          content.WebcamDeviceName = b.WebcamDeviceName ?? ""; break;
                case PMSDKSkinContentMode.ExternalTexture: content.ExternalTexture = b.ExternalTexture; break;
                // None / TestPattern have no per-scene binding fields worth overriding.
            }
        }
    }
}
