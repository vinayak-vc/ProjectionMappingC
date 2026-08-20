using System.IO;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>File / asset kind coming through a Skin Editor drag-drop.</summary>
    public enum PMSDKSkinDropKind
    {
        Unknown = 0,
        Image = 1,             // OS file with an image extension (png/jpg/tga/exr/psd/bmp)
        Video = 2,             // OS file with a video extension (mp4/mov/webm/mkv/avi/m4v)
        Json = 3,              // OS file with a .json extension → treated as skin set
        TextureAsset = 4,      // Project-window drop of a Texture / Texture2D / Sprite
        RenderTextureAsset = 5,
        VideoClipAsset = 6,
        SkinSetAsset = 7,      // Drop of a PMSDKSkinSet ScriptableObject
    }

    /// <summary>
    /// Pure routing helpers for drag-drop file assignment onto a
    /// <see cref="PMSDKSkinContent"/> (M-Sk4 polish). Kept in Runtime so tests can drive
    /// <see cref="ApplyPath"/> directly; the <see cref="ApplyObject"/> path plus the HUD
    /// drop handler stay behind <c>#if UNITY_EDITOR</c> since <c>UnityEditor.DragAndDrop</c>
    /// only exists in the Editor. In standalone builds, drag-drop assignment isn't
    /// available (see docs/multi-skin-design.md for the deferred play-mode file-drop
    /// integration — needs a native plugin).
    /// </summary>
    public static class PMSDKSkinDragDrop
    {
        private static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg", ".tga", ".exr", ".psd", ".bmp" };
        private static readonly string[] VideoExts = { ".mp4", ".mov", ".webm", ".mkv", ".avi", ".m4v" };

        public static PMSDKSkinDropKind KindForPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return PMSDKSkinDropKind.Unknown;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            for (int i = 0; i < ImageExts.Length; i++) if (ImageExts[i] == ext) return PMSDKSkinDropKind.Image;
            for (int i = 0; i < VideoExts.Length; i++) if (VideoExts[i] == ext) return PMSDKSkinDropKind.Video;
            if (ext == ".json") return PMSDKSkinDropKind.Json;
            return PMSDKSkinDropKind.Unknown;
        }

        /// <summary>Route an OS file path to the correct <see cref="PMSDKSkinContent"/>
        /// mode. Handles images (loads via Texture2D.LoadImage), videos (VideoUrl with
        /// file:// URI), and JSON drops — but JSON is signaled back to the caller via a
        /// non-null <paramref name="jsonPathOut"/> since the loader lives on the
        /// controller, not the content. Returns true if the file was accepted.</summary>
        public static bool ApplyPath(PMSDKSkinContent content, string path, out string jsonPathOut)
        {
            jsonPathOut = null;
            if (content == null || string.IsNullOrEmpty(path)) return false;
            var kind = KindForPath(path);
            switch (kind)
            {
                case PMSDKSkinDropKind.Image:
                    return ApplyImagePath(content, path);
                case PMSDKSkinDropKind.Video:
                    content.Mode = PMSDKSkinContentMode.VideoUrl;
                    content.VideoUrl = new System.Uri(path).AbsoluteUri;
                    return true;
                case PMSDKSkinDropKind.Json:
                    // Signal to the caller — a JSON drop means "load this skin set."
                    jsonPathOut = path;
                    return true;
                default:
                    return false;
            }
        }

        private static bool ApplyImagePath(PMSDKSkinContent content, string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"PMSDKSkinDragDrop: image path does not exist: {path}");
                    return false;
                }
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = Path.GetFileNameWithoutExtension(path),
                };
                if (!tex.LoadImage(bytes))
                {
                    Debug.LogWarning($"PMSDKSkinDragDrop: LoadImage failed for {path}");
                    if (Application.isEditor && !Application.isPlaying) Object.DestroyImmediate(tex);
                    else Object.Destroy(tex);
                    return false;
                }
                content.Mode = PMSDKSkinContentMode.Texture;
                content.StaticTexture = tex;
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PMSDKSkinDragDrop: image load failed for {path} — {ex.Message}");
                return false;
            }
        }

#if UNITY_EDITOR
        /// <summary>Route a Unity asset reference (Project window drop) to the right
        /// content mode. Returns true if the object was accepted. For
        /// <see cref="PMSDKSkinSet"/> drops, sets <paramref name="skinSetOut"/> instead
        /// of touching the content (the caller applies it via the controller).</summary>
        public static bool ApplyObject(PMSDKSkinContent content, UnityEngine.Object obj,
            out PMSDKSkinSet skinSetOut)
        {
            skinSetOut = null;
            if (content == null || obj == null) return false;
            switch (obj)
            {
                case PMSDKSkinSet set:
                    skinSetOut = set;
                    return true;
                case RenderTexture rt:
                    content.Mode = PMSDKSkinContentMode.RenderTexture;
                    content.ExternalRenderTexture = rt;
                    return true;
                case UnityEngine.Video.VideoClip vc:
                    content.Mode = PMSDKSkinContentMode.VideoClip;
                    content.VideoClip = vc;
                    return true;
                case Texture t:
                    content.Mode = PMSDKSkinContentMode.Texture;
                    content.StaticTexture = t;
                    return true;
            }
            return false;
        }
#endif
    }
}
