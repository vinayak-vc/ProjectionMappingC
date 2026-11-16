using System;
using System.IO;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Auto Adjust warp map on disk (D-046): "PMWM", int32 version (1), int32 width, int32 height,
    /// float rect x, y, width, height (raster, y up), then width * height * 4 floats (u, v, 0, valid),
    /// row 0 at the rect's bottom. Little-endian. Written atomically (.tmp then move).
    /// </summary>
    public static class PMSDKWarpMapFile
    {
        private const int Version = 1;
        private static readonly byte[] Magic = { (byte)'P', (byte)'M', (byte)'W', (byte)'M' };

        /// <summary>Writes a map; throws on invalid sizes.</summary>
        public static void Save(string path, int width, int height, Rect rect, float[] rgba)
        {
            if (width < 2 || height < 2 || rgba == null || rgba.Length != width * height * 4) {
                throw new ArgumentException("Warp map data must hold width * height * 4 floats (width, height >= 2).");
            }
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }
            string tmp = path + ".tmp";
            using (FileStream stream = File.Create(tmp)) {
                using (BinaryWriter writer = new BinaryWriter(stream)) {
                    writer.Write(Magic);
                    writer.Write(Version);
                    writer.Write(width);
                    writer.Write(height);
                    writer.Write(rect.x);
                    writer.Write(rect.y);
                    writer.Write(rect.width);
                    writer.Write(rect.height);
                    byte[] bytes = new byte[rgba.Length * 4];
                    Buffer.BlockCopy(rgba, 0, bytes, 0, bytes.Length);
                    writer.Write(bytes);
                }
            }
            if (File.Exists(path)) {
                File.Delete(path);
            }
            File.Move(tmp, path);
        }

        /// <summary>Reads a map. Returns false with a message for a missing or malformed file.</summary>
        public static bool TryLoad(string path, out int width, out int height, out Rect rect, out float[] rgba, out string error)
        {
            width = 0;
            height = 0;
            rect = new Rect(0f, 0f, 1f, 1f);
            rgba = null;
            if (!File.Exists(path)) {
                error = "Warp map file not found: " + path;
                return false;
            }
            try {
                using (FileStream stream = File.OpenRead(path)) {
                    using (BinaryReader reader = new BinaryReader(stream)) {
                        byte[] magic = reader.ReadBytes(4);
                        if (magic.Length != 4 || magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3]) {
                            error = "Not a warp map file: " + path;
                            return false;
                        }
                        int version = reader.ReadInt32();
                        if (version != Version) {
                            error = "Unsupported warp map version " + version + ": " + path;
                            return false;
                        }
                        width = reader.ReadInt32();
                        height = reader.ReadInt32();
                        if (width < 2 || height < 2 || width > 4096 || height > 4096) {
                            error = "Invalid warp map size " + width + " x " + height + ": " + path;
                            return false;
                        }
                        rect = new Rect(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        int floats = width * height * 4;
                        byte[] bytes = reader.ReadBytes(floats * 4);
                        if (bytes.Length != floats * 4) {
                            error = "Truncated warp map file: " + path;
                            return false;
                        }
                        rgba = new float[floats];
                        Buffer.BlockCopy(bytes, 0, rgba, 0, bytes.Length);
                    }
                }
                error = "";
                return true;
            } catch (IOException ex) {
                error = "Could not read warp map: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// GPU texture for <c>PMSDK/UnlitWarp</c> (_WarpMap): RGBA float, linear, bilinear, clamp;
        /// RG are multiplied by A so filtering never blends in invalid texels. Caller destroys it.
        /// </summary>
        public static Texture2D CreateTexture(int width, int height, float[] rgba)
        {
            float[] premultiplied = new float[rgba.Length];
            for (int i = 0; i < rgba.Length; i += 4) {
                float a = rgba[i + 3] > 0.5f ? 1f : 0f;
                premultiplied[i] = rgba[i] * a;
                premultiplied[i + 1] = rgba[i + 1] * a;
                premultiplied[i + 2] = 0f;
                premultiplied[i + 3] = a;
            }
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            texture.name = "PMSDK_WarpMap";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixelData(premultiplied, 0);
            texture.Apply(false);
            return texture;
        }
    }
}
