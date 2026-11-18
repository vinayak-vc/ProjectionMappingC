using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Camera-space surface marks (D-049/D-050): the calibration camera's photo is saved for the user (or an AI
    /// cutout) to mark, and the marked file is read back as a camera mask (raw camera size, row 0 = top,
    /// 255 = surface). Two kinds of file are accepted:
    ///   - a copy of the photo with the surface outlined or filled in pure red (hand marking);
    ///   - a black-and-white mask, white = surface (what an AI cutout produces).
    /// The file may be the camera size or any whole-number upscale of it. Each skin made from a mark keeps its
    /// camera region as <c>&lt;skinId&gt;_camera.png</c> in <see cref="PMSDKSkinStore.MasksFolder"/> so its warp
    /// map can be rebuilt from the same region later.
    /// </summary>
    public static class PMSDKCameraMark
    {
        public const string PhotoFileName = "camera.png";
        public const string MarkedFileName = "camera_marked.png";

        // Pure red: strong red, little green and blue (also survives JPEG-like smoothing of the line).
        private const int RedMin = 150;
        private const int RedMargin = 80;
        // Fewer red pixels than this means the file is a black-and-white mask, not a red mark.
        private const int MinRedPixels = 50;

        /// <summary>Folder holding the camera photo and the marked copy.</summary>
        public static string Folder
        {
            get { return Path.Combine(Application.persistentDataPath, "CameraMarks"); }
        }

        public static string PhotoPath
        {
            get { return Path.Combine(Folder, PhotoFileName); }
        }

        public static string MarkedPath
        {
            get { return Path.Combine(Folder, MarkedFileName); }
        }

        /// <summary>Writes a camera frame (row 0 = top) as a grayscale PNG.</summary>
        public static void SavePhoto(string path, byte[] frame, int width, int height)
        {
            if (frame == null || width <= 0 || height <= 0 || frame.Length < width * height) {
                throw new ArgumentException("The camera frame is empty or smaller than its size.");
            }
            PMSDKPhotoSkinBuilder.WriteMaskPng(path, frame, width, height, true);
        }

        /// <summary>
        /// Reads a marked file as a camera mask of <paramref name="cameraWidth"/> x <paramref name="cameraHeight"/>.
        /// </summary>
        public static bool TryLoadMask(string path, int cameraWidth, int cameraHeight, out byte[] mask, out string error)
        {
            mask = null;
            if (!File.Exists(path)) {
                error = "No marked image at " + path + ".";
                return false;
            }
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try {
                if (!texture.LoadImage(File.ReadAllBytes(path), false)) {
                    error = "Could not read the marked image " + path + ".";
                    return false;
                }
                return TryMakeMask(texture.GetPixels32(), texture.width, texture.height, cameraWidth, cameraHeight, out mask, out error);
            } finally {
                if (Application.isPlaying) {
                    UnityEngine.Object.Destroy(texture);
                } else {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        /// <summary>
        /// Converts marked pixels (Unity order: row 0 = bottom) to a camera mask (row 0 = top). Red marks are
        /// filled at the file's own resolution (a thin outline stays closed) and then reduced to the camera size.
        /// </summary>
        public static bool TryMakeMask(Color32[] pixels, int width, int height, int cameraWidth, int cameraHeight, out byte[] mask,
            out string error)
        {
            mask = null;
            if (pixels == null || width <= 0 || height <= 0 || pixels.Length != width * height || cameraWidth <= 0 || cameraHeight <= 0) {
                error = "The marked image is empty.";
                return false;
            }
            if (width % cameraWidth != 0 || height % cameraHeight != 0 || width / cameraWidth != height / cameraHeight) {
                error = "The marked image is " + width + " x " + height + "; it must be the camera photo size (" + cameraWidth + " x " +
                        cameraHeight + ") or a whole-number upscale of it.";
                return false;
            }
            int scale = width / cameraWidth;

            byte[] full = new byte[width * height];
            int red = 0;
            for (int y = 0; y < height; y++) {
                int source = (height - 1 - y) * width;
                for (int x = 0; x < width; x++) {
                    Color32 c = pixels[source + x];
                    if (c.r >= RedMin && c.r - c.g >= RedMargin && c.r - c.b >= RedMargin) {
                        full[y * width + x] = 255;
                        red++;
                    }
                }
            }
            if (red >= MinRedPixels) {
                FillEnclosed(full, width, height);
            } else {
                for (int y = 0; y < height; y++) {
                    int source = (height - 1 - y) * width;
                    for (int x = 0; x < width; x++) {
                        Color32 c = pixels[source + x];
                        full[y * width + x] = (c.r + c.g + c.b) / 3 > 127 ? (byte)255 : (byte)0;
                    }
                }
            }

            mask = new byte[cameraWidth * cameraHeight];
            int half = scale * scale / 2;
            int marked = 0;
            for (int y = 0; y < cameraHeight; y++) {
                for (int x = 0; x < cameraWidth; x++) {
                    int on = 0;
                    for (int dy = 0; dy < scale; dy++) {
                        int row = (y * scale + dy) * width + x * scale;
                        for (int dx = 0; dx < scale; dx++) {
                            if (full[row + dx] != 0) {
                                on++;
                            }
                        }
                    }
                    if (on > half) {
                        mask[y * cameraWidth + x] = 255;
                        marked++;
                    }
                }
            }
            if (marked == 0) {
                mask = null;
                error = "Nothing is marked: outline or fill the surface in pure red, or use a black-and-white mask (white = surface).";
                return false;
            }
            error = "";
            return true;
        }

        /// <summary>File holding the camera region a skin was made from (bare name inside the masks folder).</summary>
        public static string RegionFileFor(PMSDKSkin skin)
        {
            return skin.SkinId + "_camera.png";
        }

        public static void SaveRegion(PMSDKSkin skin, byte[] mask, int width, int height)
        {
            PMSDKPhotoSkinBuilder.WriteMaskPng(PMSDKSkinStore.ResolveMaskPath(RegionFileFor(skin)), mask, width, height, true);
        }

        /// <summary>False with an empty <paramref name="error"/> when the skin was not made from a mark.</summary>
        public static bool TryLoadRegion(PMSDKSkin skin, out byte[] mask, out int width, out int height, out string error)
        {
            mask = null;
            width = 0;
            height = 0;
            error = "";
            if (skin == null || string.IsNullOrEmpty(skin.SkinId)) {
                return false;
            }
            string path = PMSDKSkinStore.ResolveMaskPath(RegionFileFor(skin));
            if (!File.Exists(path) || !TryGetImageSize(path, out width, out height)) {
                return false;
            }
            return TryLoadMask(path, width, height, out mask, out error);
        }

        /// <summary>Pixel size of a PNG/JPG file (false when it cannot be read).</summary>
        public static bool TryGetImageSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (!File.Exists(path)) {
                return false;
            }
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try {
                if (!texture.LoadImage(File.ReadAllBytes(path), false)) {
                    return false;
                }
                width = texture.width;
                height = texture.height;
                return true;
            } finally {
                if (Application.isPlaying) {
                    UnityEngine.Object.Destroy(texture);
                } else {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        /// <summary>Fills every pixel the border cannot reach without crossing a marked pixel (the inside of
        /// closed outlines); marked pixels stay marked.</summary>
        private static void FillEnclosed(byte[] mask, int width, int height)
        {
            bool[] outside = new bool[mask.Length];
            Stack<int> pending = new Stack<int>();
            for (int x = 0; x < width; x++) {
                pending.Push(x);
                pending.Push((height - 1) * width + x);
            }
            for (int y = 0; y < height; y++) {
                pending.Push(y * width);
                pending.Push(y * width + width - 1);
            }
            while (pending.Count > 0) {
                int index = pending.Pop();
                if (outside[index] || mask[index] != 0) {
                    continue;
                }
                outside[index] = true;
                int x = index % width;
                int y = index / width;
                if (x > 0) {
                    pending.Push(index - 1);
                }
                if (x < width - 1) {
                    pending.Push(index + 1);
                }
                if (y > 0) {
                    pending.Push(index - width);
                }
                if (y < height - 1) {
                    pending.Push(index + width);
                }
            }
            for (int i = 0; i < mask.Length; i++) {
                if (!outside[i]) {
                    mask[i] = 255;
                }
            }
        }
    }
}
