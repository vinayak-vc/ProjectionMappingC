using System;
using System.IO;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Stateless helpers for assigning on-disk media (video / image files that are NOT
    /// Unity assets) to a <see cref="PMSDKSkinContent"/>, and for describing the current
    /// assignment in artist-friendly words. Shared by the Skin Workspace (editor) and the
    /// Skin Player UI.
    /// </summary>
    public static class PMSDKSkinMedia
    {
        // Includes formats the player often cannot decode (mkv, flv, ts ...): they are accepted so
        // the "convert with FFmpeg" flow can offer to fix them.
        private static readonly string[] VideoExtensions =
        {
            ".mp4", ".mov", ".m4v", ".avi", ".wmv", ".webm",
            ".mkv", ".flv", ".mpg", ".mpeg", ".ts", ".mts", ".m2ts", ".3gp", ".ogv",
        };
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".tga" };

        public static bool IsVideoFile(string path)
        {
            return HasExtension(path, VideoExtensions);
        }

        public static bool IsImageFile(string path)
        {
            return HasExtension(path, ImageExtensions);
        }

        /// <summary>Assign a file to the content, picking Video or Image mode from the
        /// extension. Returns false (and leaves the content untouched) for unsupported types
        /// or empty paths.</summary>
        public static bool TryAssignFile(PMSDKSkinContent content, string path)
        {
            if (content == null || string.IsNullOrWhiteSpace(path)) {
                return false;
            }
            string cleaned = path.Trim().Trim('"');
            if (IsVideoFile(cleaned)) {
                AssignVideo(content, cleaned);
                return true;
            }
            if (IsImageFile(cleaned)) {
                AssignImage(content, cleaned);
                return true;
            }
            return false;
        }

        public static void AssignVideo(PMSDKSkinContent content, string path)
        {
            if (content == null) {
                return;
            }
            content.VideoUrl = path;
            content.Mode = PMSDKSkinContentMode.VideoUrl;
            content.Play = true;
        }

        public static void AssignImage(PMSDKSkinContent content, string path)
        {
            if (content == null) {
                return;
            }
            content.ImagePath = path;
            content.Mode = PMSDKSkinContentMode.ImageFile;
        }

        public static void AssignTestPattern(PMSDKSkinContent content)
        {
            if (content == null) {
                return;
            }
            content.Mode = PMSDKSkinContentMode.TestPattern;
        }

        public static void AssignWebcam(PMSDKSkinContent content)
        {
            if (content == null) {
                return;
            }
            content.Mode = PMSDKSkinContentMode.Webcam;
        }

        public static void Clear(PMSDKSkinContent content)
        {
            if (content == null) {
                return;
            }
            content.Mode = PMSDKSkinContentMode.None;
        }

        /// <summary>The file path behind the current mode, or empty when the mode has none.</summary>
        public static string CurrentPath(PMSDKSkinContent content)
        {
            if (content == null) {
                return "";
            }
            switch (content.Mode) {
                case PMSDKSkinContentMode.VideoUrl: return content.VideoUrl ?? "";
                case PMSDKSkinContentMode.ImageFile: return content.ImagePath ?? "";
                default: return "";
            }
        }

        /// <summary>Short label such as "Video  Blast.mp4" or "Test pattern".</summary>
        public static string Describe(PMSDKSkinContent content)
        {
            if (content == null) {
                return "No media";
            }
            switch (content.Mode) {
                case PMSDKSkinContentMode.None:
                    return "No media";
                case PMSDKSkinContentMode.VideoUrl:
                    return "Video  " + FileLabel(content.VideoUrl);
                case PMSDKSkinContentMode.ImageFile:
                    return "Image  " + FileLabel(content.ImagePath);
                case PMSDKSkinContentMode.TestPattern:
                    return "Test pattern  " + content.TestPatternType;
                case PMSDKSkinContentMode.Webcam:
                    return "Webcam";
                case PMSDKSkinContentMode.VideoClip:
                    return "Video clip";
                case PMSDKSkinContentMode.Texture:
                    return "Texture";
                default:
                    return content.Mode.ToString();
            }
        }

        /// <summary>True when the assigned file path does not exist on disk (so the UI can warn).</summary>
        public static bool IsFileMissing(PMSDKSkinContent content)
        {
            if (content == null) {
                return false;
            }
            if (content.Mode != PMSDKSkinContentMode.VideoUrl && content.Mode != PMSDKSkinContentMode.ImageFile) {
                return false;
            }
            string path = CurrentPath(content);
            if (string.IsNullOrEmpty(path)) {
                return false;
            }
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            return !File.Exists(path);
        }

        private static string FileLabel(string path)
        {
            if (string.IsNullOrEmpty(path)) {
                return "(none)";
            }
            try {
                return Path.GetFileName(path);
            } catch (ArgumentException) {
                return path;
            }
        }

        private static bool HasExtension(string path, string[] extensions)
        {
            if (string.IsNullOrWhiteSpace(path)) {
                return false;
            }
            string ext;
            try {
                ext = Path.GetExtension(path.Trim().Trim('"'));
            } catch (ArgumentException) {
                return false;
            }
            for (int i = 0; i < extensions.Length; i++) {
                if (string.Equals(ext, extensions[i], StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }
    }
}
