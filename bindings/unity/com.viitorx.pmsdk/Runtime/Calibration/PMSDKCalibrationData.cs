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
