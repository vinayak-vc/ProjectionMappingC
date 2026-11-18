using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Diagnostic picture for a surface report (D-051), drawn over the camera's white capture (raw pixels):
    /// each facet tinted in its own colour, and at every grid intersection of the content a green dot where the
    /// intersection should be on the physical surface (uniform grid, unfolded) and a red dot where the projector
    /// actually puts it, joined by a yellow error vector. A projection that is correct on the surface shows the
    /// dots on top of each other, whatever the camera's perspective or lens.
    /// </summary>
    public static class PMSDKSurfaceDiagnostics
    {
        private static readonly Color32[] FacetColours = {
            new Color32(66, 133, 244, 255), new Color32(244, 160, 0, 255), new Color32(15, 157, 88, 255), new Color32(171, 71, 188, 255),
            new Color32(0, 172, 193, 255), new Color32(255, 112, 67, 255), new Color32(158, 157, 36, 255), new Color32(92, 107, 192, 255),
        };

        private const float BucketMm = 20f;

        public static void WritePng(string path, byte[] white, int width, int height, PMSDKSurfaceGeometry geometry, PMSDKCameraIntrinsics intrinsics,
            List<Vector2> pixels, List<Vector2> uvs, PMSDKSurfaceReport report)
        {
            Color32[] image = new Color32[width * height];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    byte g = (byte)(white[y * width + x] * 3 / 5);
                    Color32 c = new Color32(g, g, g, 255);
                    int plane = geometry != null ? geometry.LabelAt(new Vector2(x, y)) : -1;
                    if (plane >= 0) {
                        Color32 tint = FacetColours[plane % FacetColours.Length];
                        c = new Color32((byte)((c.r * 2 + tint.r) / 3), (byte)((c.g * 2 + tint.g) / 3), (byte)((c.b * 2 + tint.b) / 3), 255);
                    }
                    image[y * width + x] = c;
                }
            }

            Vector2[] undistorted;
            string error;
            if (geometry != null && PMSDKAutoAdjustNative.TryUndistort(pixels.ToArray(), intrinsics, out undistorted, out error)) {
                // Surface position of every sample, bucketed so "which camera pixel lies at this surface point" is cheap.
                Dictionary<long, List<int>> buckets = new Dictionary<long, List<int>>();
                Vector2[] surface = new Vector2[pixels.Count];
                bool[] onSurface = new bool[pixels.Count];
                for (int i = 0; i < pixels.Count; i++) {
                    int plane;
                    onSurface[i] = geometry.TryToSurface(pixels[i], undistorted[i], out surface[i], out plane);
                    if (onSurface[i]) {
                        long key = Key(surface[i]);
                        List<int> list;
                        if (!buckets.TryGetValue(key, out list)) {
                            list = new List<int>();
                            buckets[key] = list;
                        }
                        list.Add(i);
                    }
                }
                float radiusU = 0.2f / report.CellsU;
                float radiusV = 0.2f / report.CellsV;
                for (int j = 0; j <= report.CellsV; j++) {
                    for (int i = 0; i <= report.CellsU; i++) {
                        Vector2 node = new Vector2((float)i / report.CellsU, (float)j / report.CellsV);
                        Vector2 measured;
                        if (!TryMeasuredPixel(pixels, uvs, node, radiusU, radiusV, out measured)) {
                            continue;
                        }
                        Vector2 expected;
                        if (!TryPixelAtSurface(report.ExpectedSurface(node), surface, onSurface, pixels, buckets, out expected)) {
                            continue;
                        }
                        DrawLine(image, width, height, expected, measured, new Color32(255, 235, 59, 255));
                        DrawDot(image, width, height, expected, 3, new Color32(0, 230, 118, 255));
                        DrawDot(image, width, height, measured, 2, new Color32(255, 23, 68, 255));
                    }
                }
            }

            // Image rows run bottom-up in Unity; camera rows top-down.
            Color32[] flipped = new Color32[image.Length];
            for (int y = 0; y < height; y++) {
                System.Array.Copy(image, y * width, flipped, (height - 1 - y) * width, width);
            }
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try {
                texture.SetPixels32(flipped);
                texture.Apply(false);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllBytes(path, texture.EncodeToPNG());
            } finally {
                if (Application.isPlaying) {
                    Object.Destroy(texture);
                } else {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        private static bool TryMeasuredPixel(List<Vector2> pixels, List<Vector2> uvs, Vector2 node, float radiusU, float radiusV, out Vector2 pixel)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < uvs.Count; i++) {
                if (Mathf.Abs(uvs[i].x - node.x) <= radiusU && Mathf.Abs(uvs[i].y - node.y) <= radiusV) {
                    sum += pixels[i];
                    count++;
                }
            }
            pixel = count > 0 ? sum / count : Vector2.zero;
            return count >= 3;
        }

        private static bool TryPixelAtSurface(Vector2 mm, Vector2[] surface, bool[] onSurface, List<Vector2> pixels, Dictionary<long, List<int>> buckets,
            out Vector2 pixel)
        {
            pixel = Vector2.zero;
            int bx = Mathf.FloorToInt(mm.x / BucketMm);
            int by = Mathf.FloorToInt(mm.y / BucketMm);
            float best = BucketMm * BucketMm;
            int bestIndex = -1;
            for (int dy = -1; dy <= 1; dy++) {
                for (int dx = -1; dx <= 1; dx++) {
                    List<int> list;
                    if (!buckets.TryGetValue(((long)(bx + dx) << 32) ^ (uint)(by + dy), out list)) {
                        continue;
                    }
                    foreach (int i in list) {
                        float d = (surface[i] - mm).sqrMagnitude;
                        if (onSurface[i] && d < best) {
                            best = d;
                            bestIndex = i;
                        }
                    }
                }
            }
            if (bestIndex < 0) {
                return false;
            }
            pixel = pixels[bestIndex];
            return true;
        }

        private static long Key(Vector2 mm)
        {
            return ((long)Mathf.FloorToInt(mm.x / BucketMm) << 32) ^ (uint)Mathf.FloorToInt(mm.y / BucketMm);
        }

        private static void DrawDot(Color32[] image, int width, int height, Vector2 centre, int radius, Color32 colour)
        {
            int cx = Mathf.RoundToInt(centre.x);
            int cy = Mathf.RoundToInt(centre.y);
            for (int y = cy - radius; y <= cy + radius; y++) {
                for (int x = cx - radius; x <= cx + radius; x++) {
                    if (x >= 0 && y >= 0 && x < width && y < height && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius) {
                        image[y * width + x] = colour;
                    }
                }
            }
        }

        private static void DrawLine(Color32[] image, int width, int height, Vector2 a, Vector2 b, Color32 colour)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)));
            for (int s = 0; s <= steps; s++) {
                Vector2 p = steps > 0 ? Vector2.Lerp(a, b, (float)s / steps) : a;
                int x = Mathf.RoundToInt(p.x);
                int y = Mathf.RoundToInt(p.y);
                if (x >= 0 && y >= 0 && x < width && y < height) {
                    image[y * width + x] = colour;
                }
            }
        }
    }
}
