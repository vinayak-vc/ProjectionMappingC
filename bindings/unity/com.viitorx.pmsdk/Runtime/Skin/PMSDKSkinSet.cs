using System.IO;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// A saved Skin Editor session. ScriptableObject asset that a content scene loads
    /// via <see cref="PMSDKSkinLoader"/> — the "author once, use in any scene" payoff of
    /// the multi-skin feature (D-037).
    ///
    /// Storage design:
    /// - The SO carries the deserialized state in <see cref="Data"/>, so the Unity
    ///   inspector shows all skins and their fields directly.
    /// - A JSON sidecar next to the .asset (same base name, .json extension) is
    ///   optional and, when present, is authoritative — <see cref="Reload"/> reads it
    ///   back into <see cref="Data"/>. Editing the JSON externally + re-importing works.
    /// - No JSON sidecar? The SO is the source of truth (Unity-only workflow).
    ///
    /// The shape mirrors <see cref="PMSDKSkinSetJson.Set"/> — the SO is a thin wrapper
    /// so the same wire DTO powers file save/load, undo snapshots, AND asset persistence.
    /// </summary>
    [CreateAssetMenu(fileName = "SkinSet", menuName = "PMSDK/Skin Set", order = 100)]
    public class PMSDKSkinSet : ScriptableObject
    {
        [Tooltip("The captured session state. Edit here or through the Skin Editor scene.")]
        public PMSDKSkinSetJson.Set Data = new PMSDKSkinSetJson.Set();

        [Tooltip("Optional: path to a JSON sidecar. When present, ReloadFromSidecar reads " +
                 "this into Data on demand. Empty = SO is the source of truth.")]
        public string SidecarPath;

        /// <summary>Return the sidecar path next to this asset with the same base name,
        /// or <see cref="SidecarPath"/> if set. Editor-only convenience — at runtime a
        /// SkinSet is loaded via the SO reference and the sidecar isn't required.</summary>
        public string GetSidecarPath()
        {
            if (!string.IsNullOrEmpty(SidecarPath)) return SidecarPath;
#if UNITY_EDITOR
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(assetPath)) return null;
            return Path.ChangeExtension(assetPath, ".json");
#else
            return null;
#endif
        }

        /// <summary>Serialize <see cref="Data"/> to JSON. Independent of file I/O.</summary>
        public string ToJson(bool prettyPrint = true) =>
            PMSDKSkinSetJson.Serialize(Data, prettyPrint);

        /// <summary>Parse JSON into <see cref="Data"/>. Overwrites any current data.</summary>
        public void FromJson(string json)
        {
            var set = PMSDKSkinSetJson.Deserialize(json);
            if (set != null) Data = set;
        }

        /// <summary>Write <see cref="Data"/> to a JSON file (atomic write via .tmp).</summary>
        public bool SaveJsonTo(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, ToJson(prettyPrint: true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PMSDKSkinSet: SaveJsonTo failed — {ex.Message}");
                return false;
            }
        }

        /// <summary>Read <paramref name="path"/> and replace <see cref="Data"/> with the parsed set.</summary>
        public bool LoadJsonFrom(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"PMSDKSkinSet: no JSON at {path}");
                    return false;
                }
                FromJson(File.ReadAllText(path));
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PMSDKSkinSet: LoadJsonFrom failed — {ex.Message}");
                return false;
            }
        }

        /// <summary>Reload <see cref="Data"/> from the JSON sidecar at
        /// <see cref="GetSidecarPath"/>, if one exists.</summary>
        public bool ReloadFromSidecar()
        {
            var path = GetSidecarPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            return LoadJsonFrom(path);
        }

        /// <summary>Capture the current Editor scene into <see cref="Data"/>. Called by
        /// the "Save Skin Set…" menu / the Skin Editor's Ctrl+Shift+S export.</summary>
        public void CaptureFrom(PMSDKSkinEditorController controller)
        {
            if (controller == null) return;
            Data = PMSDKSkinSetJson.Capture(controller);
        }
    }
}
