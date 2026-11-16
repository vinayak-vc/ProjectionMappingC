using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>Camera pixel -> content UV correspondences measured through one skin.</summary>
    public sealed class PMSDKGrayCaptureResult
    {
        public bool Success;
        public string Message = "";
        public int CameraWidth;
        public int CameraHeight;
        /// <summary>Raw camera pixel of each sample (top-left origin).</summary>
        public List<Vector2> CameraPixels = new List<Vector2>();
        /// <summary>Content UV (y up) shown at that camera pixel.</summary>
        public List<Vector2> ContentUvs = new List<Vector2>();
        /// <summary>The all-white and all-black captures (lit-area mask / facet segmentation).</summary>
        public byte[] White;
        public byte[] Black;
    }

    /// <summary>
    /// Projects robust Gray code (patterns + inverses, white/black references) in a skin's CONTENT
    /// space (through its current warp, via <see cref="PMSDKCalibrationDisplay.ShowPattern"/>) and
    /// decodes, per camera pixel, which content texel lit it (D-046/D-047). With a full-raster skin
    /// (corners at the raster corners, no correction) content UV equals the projector raster position,
    /// which gives the dense camera -> projector map used to place skins on detected surfaces.
    /// </summary>
    public static class PMSDKContentGrayCode
    {
        /// <summary>Frames projected for a given resolution (white, black, then pattern + inverse per bit).</summary>
        public static int FrameCount(int resolution)
        {
            return 2 + 2 * PMSDKGrayCodeDecode.PatternCount(resolution, resolution);
        }

        /// <param name="resolution">Pattern size in texels per side (power of two recommended, 64..2048).</param>
        /// <param name="sampleStep">Keep every n-th camera pixel in x and y (1 = all).</param>
        public static IEnumerator Capture(PMSDKSkin skin, PMSDKCalibrationDisplay display, IPMSDKCalibrationCamera camera, int resolution,
            int settleFrames, int minContrast, int sampleStep, Action<PMSDKGrayCaptureResult> onDone)
        {
            PMSDKGrayCaptureResult result = new PMSDKGrayCaptureResult();
            int size = Mathf.Clamp(resolution, 16, 2048);
            int step = Mathf.Max(1, sampleStep);
            int patterns = PMSDKGrayCodeDecode.PatternCount(size, size);
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            texture.name = "PMSDK_ContentGrayCode";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            byte[] gray = new byte[size * size];
            byte[] rgb = new byte[size * size * 3];
            byte[][] captures = new byte[patterns][];
            byte[][] inverses = new byte[patterns][];
            try {
                display.ShowPattern(skin, texture);
                for (int frame = 0; frame < 2 + 2 * patterns; frame++) {
                    if (frame == 0) {
                        Fill(gray, 255);
                    } else if (frame == 1) {
                        Fill(gray, 0);
                    } else {
                        int index = (frame - 2) / 2;
                        PMSDKGrayCodeDecode.GeneratePattern(index, size, size, gray);
                        if (((frame - 2) & 1) == 1) {
                            for (int i = 0; i < gray.Length; i++) {
                                gray[i] = (byte)(255 - gray[i]);
                            }
                        }
                    }
                    for (int i = 0; i < gray.Length; i++) {
                        rgb[i * 3] = gray[i];
                        rgb[i * 3 + 1] = gray[i];
                        rgb[i * 3 + 2] = gray[i];
                    }
                    texture.LoadRawTextureData(rgb);
                    texture.Apply(false);
                    for (int f = 0; f < settleFrames; f++) {
                        yield return null;
                    }
                    byte[] captured = camera.CaptureLuminance();
                    if (captured == null || captured.Length < camera.Width * camera.Height) {
                        result.Message = "The camera returned no frame during the Gray-code sequence.";
                        onDone?.Invoke(result);
                        yield break;
                    }
                    if (frame == 0) {
                        result.White = captured;
                    } else if (frame == 1) {
                        result.Black = captured;
                    } else if (((frame - 2) & 1) == 0) {
                        captures[(frame - 2) / 2] = captured;
                    } else {
                        inverses[(frame - 2) / 2] = captured;
                    }
                }
            } finally {
                if (Application.isPlaying) {
                    UnityEngine.Object.Destroy(texture);
                } else {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            int width = camera.Width;
            int height = camera.Height;
            PMSDKGrayCodeDecode.Correspondence[] decoded = PMSDKGrayCodeDecode.Decode(captures, inverses, result.White, result.Black,
                width, height, size, size, minContrast);
            for (int y = 0; y < height; y += step) {
                for (int x = 0; x < width; x += step) {
                    PMSDKGrayCodeDecode.Correspondence c = decoded[y * width + x];
                    if (!c.Valid) {
                        continue;
                    }
                    result.CameraPixels.Add(new Vector2(x, y));
                    result.ContentUvs.Add(new Vector2((c.ProjectorX + 0.5f) / size, (c.ProjectorY + 0.5f) / size));
                }
            }
            result.CameraWidth = width;
            result.CameraHeight = height;
            result.Success = result.CameraPixels.Count > 0;
            result.Message = result.Success
                ? "Decoded " + result.CameraPixels.Count + " samples."
                : "No camera pixel was lit by the skin (check that the camera sees it and the room is dim).";
            onDone?.Invoke(result);
        }

        private static void Fill(byte[] buffer, byte value)
        {
            for (int i = 0; i < buffer.Length; i++) {
                buffer[i] = value;
            }
        }
    }
}
