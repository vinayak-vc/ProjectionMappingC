using System;
using System.IO;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>Per-surface calibration state as stored on disk.</summary>
    [Serializable]
    public class PMSDKSurfaceCalibration
    {
        public string id;               // GameObject name of the warp surface
        public int targetDisplay;       // projector camera's target display (informational)
        public Vector2 tl = new Vector2(0, 1);
        public Vector2 tr = new Vector2(1, 1);
        public Vector2 bl = new Vector2(0, 0);
        public Vector2 br = new Vector2(1, 0);
        public float blendLeft;
        public float blendRight;
        public float blendTop;
        public float blendBottom;
        public float gamma = 2.2f;
        public float blackLevel;

        // N x M grid warp (overrides the 4-corner pin while gridEnabled).
        public bool gridEnabled;
        public int gridColumns = 3;
        public int gridRows = 3;
        public Vector2[] gridPoints;

        // Camera-view target rectangle (UI-normalized, bottom-left, TL,TR,BR,BL).
        // Camera-placement-dependent, but saved so re-aligning without moving the
        // camera doesn't require re-marking.
        public bool hasTarget;
        public Vector2[] targetCorners;

        // Camera-measured luminance (vignette) gain map. Stored quantized + base64 so a
        // fine map stays compact in the JSON. Row-major, y-up, each cell in [0,1].
        public bool lumCompEnabled;
        public int lumGainWidth;
        public int lumGainHeight;
        public string lumGainData;

        // Camera-measured per-region black-level uplift map (same encoding as the gain
        // map). Row-major, y-up, each cell a signal lift in [0,1].
        public bool blackLiftEnabled;
        public int blackLiftWidth;
        public int blackLiftHeight;
        public string blackLiftData;
    }

    /// <summary>
    /// Compact, lossless-enough encoding for a luminance gain map: each [0,1] cell
    /// quantized to one byte and base64-encoded. 8 bits over the usable 0.5..1 gain
    /// range is ~0.2% brightness steps — below the visible threshold — while keeping a
    /// 96x96 map at ~12 KB in the calibration JSON instead of a giant float array.
    /// </summary>
    public static class PMSDKGainCodec
    {
        public static string Encode(float[] gain)
        {
            if (gain == null || gain.Length == 0) return null;
            var bytes = new byte[gain.Length];
            for (int i = 0; i < gain.Length; i++)
            {
                int q = Mathf.RoundToInt(Mathf.Clamp01(gain[i]) * 255f);
                bytes[i] = (byte)Mathf.Clamp(q, 0, 255);
            }
            return Convert.ToBase64String(bytes);
        }

        /// <summary>Decode to a float[] of exactly expectedCount cells; null if malformed.</summary>
        public static float[] Decode(string data, int expectedCount)
        {
            if (string.IsNullOrEmpty(data) || expectedCount <= 0) return null;
            try
            {
                var bytes = Convert.FromBase64String(data);
                if (bytes.Length != expectedCount) return null;
                var gain = new float[expectedCount];
                for (int i = 0; i < expectedCount; i++) gain[i] = bytes[i] / 255f;
                return gain;
            }
            catch { return null; }
        }
    }

    [Serializable]
    public class PMSDKCalibrationFile
    {
        public int version = 1;
        public string savedAtUtc;
        public PMSDKSurfaceCalibration[] surfaces;
    }

    /// <summary>
    /// Calibration persistence. JSON in Application.persistentDataPath so standalone
    /// builds calibrate once and silently reload on every later boot.
    /// CLI overrides: "-calibfile &lt;path&gt;" changes the file, "-calibrate" forces
    /// calibration mode on startup.
    /// </summary>
    public static class PMSDKCalibrationIO
    {
        public const string DefaultFileName = "pmsdk_calibration.json";

        public static string ResolvePath(string overridePath = null)
        {
            if (!string.IsNullOrEmpty(overridePath))
            {
                return overridePath;
            }
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-calibfile", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return Path.Combine(Application.persistentDataPath, DefaultFileName);
        }

        public static bool ForceCalibrateRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-calibrate", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------- Named presets ----------------
        // Presets are sibling files of the main calibration file:
        //   pmsdk_preset_<name>.json
        // so cues like "day"/"night"/"rehearsal" can be saved and recalled on site.

        private const string PresetPrefix = "pmsdk_preset_";

        /// <summary>Filesystem-safe preset name (invalid chars replaced with '_').</summary>
        public static string SanitizePresetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "default";
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name.Trim())
            {
                bool bad = c == '.' || System.Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0;
                sb.Append(bad ? '_' : c);
            }
            return sb.Length == 0 ? "default" : sb.ToString();
        }

        /// <summary>Path of the named preset, next to the main calibration file.</summary>
        public static string PresetPath(string basePath, string name)
        {
            string dir = Path.GetDirectoryName(basePath);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            return Path.Combine(dir, PresetPrefix + SanitizePresetName(name) + ".json");
        }

        /// <summary>Names of all presets stored next to the main calibration file.</summary>
        public static string[] ListPresets(string basePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(basePath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return new string[0];
                var files = Directory.GetFiles(dir, PresetPrefix + "*.json");
                var names = new string[files.Length];
                for (int i = 0; i < files.Length; i++)
                {
                    string f = Path.GetFileNameWithoutExtension(files[i]);
                    names[i] = f.Substring(PresetPrefix.Length);
                }
                System.Array.Sort(names, System.StringComparer.OrdinalIgnoreCase);
                return names;
            }
            catch { return new string[0]; }
        }

        public static bool DeletePreset(string basePath, string name)
        {
            try
            {
                string p = PresetPath(basePath, name);
                if (!File.Exists(p)) return false;
                File.Delete(p);
                return true;
            }
            catch { return false; }
        }

        public static PMSDKCalibrationFile Load(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }
                var file = JsonUtility.FromJson<PMSDKCalibrationFile>(File.ReadAllText(path));
                if (file == null || file.surfaces == null)
                {
                    Debug.LogWarning($"[PMSDK] Calibration file at '{path}' is empty or malformed — ignoring.");
                    return null;
                }
                return file;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PMSDK] Failed to read calibration file '{path}': {e.Message}");
                return null;
            }
        }

        public static bool Save(string path, PMSDKCalibrationFile file)
        {
            try
            {
                file.savedAtUtc = DateTime.UtcNow.ToString("o");
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                // Write-then-swap so a crash mid-write can't corrupt the last good file.
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(file, prettyPrint: true));
                File.Copy(tmp, path, overwrite: true);
                File.Delete(tmp);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PMSDK] Failed to save calibration to '{path}': {e.Message}");
                return false;
            }
        }
    }
}
