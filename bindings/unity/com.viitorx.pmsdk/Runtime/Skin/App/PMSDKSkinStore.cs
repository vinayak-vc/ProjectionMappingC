using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// The on-disk library of named skin sets shared by the Editor and Player modes of the
    /// standalone app: <c>persistentDataPath/SkinSets/&lt;name&gt;.json</c>, plus
    /// <c>active.txt</c> remembering which set is current. The Player only ever rewrites the
    /// <c>defaultContent</c> of a skin; geometry is written exclusively by the Editor.
    /// Set names are sanitised (letters, digits, space, '-', '_') so a name can never escape
    /// the folder.
    /// </summary>
    public static class PMSDKSkinStore
    {
        public const string DefaultSetName = "Default";
        private const string FolderName = "SkinSets";
        private const string ActiveFileName = "active.txt";
        private const string BackgroundFileName = "background.txt";
        private const int MaxNameLength = 40;

        public static string Folder {
            get {
                return Path.Combine(Application.persistentDataPath, FolderName);
            }
        }

        /// <summary>Name of the set the app is currently using (Editor saves into it, Player plays it).</summary>
        public static string ActiveName {
            get {
                try {
                    string file = Path.Combine(Folder, ActiveFileName);
                    if (File.Exists(file)) {
                        string stored = SanitizeName(File.ReadAllText(file));
                        if (stored.Length > 0) {
                            return stored;
                        }
                    }
                } catch (Exception ex) {
                    Debug.LogWarning("PMSDKSkinStore: could not read active set - " + ex.Message);
                }
                List<string> names = ListNames();
                return names.Count > 0 ? names[0] : DefaultSetName;
            }
            set {
                string clean = SanitizeName(value);
                if (clean.Length == 0) {
                    return;
                }
                try {
                    Directory.CreateDirectory(Folder);
                    File.WriteAllText(Path.Combine(Folder, ActiveFileName), clean);
                } catch (Exception ex) {
                    Debug.LogWarning("PMSDKSkinStore: could not write active set - " + ex.Message);
                }
            }
        }

        /// <summary>Output background colour (the area no skin covers), shared by Editor and
        /// Player and stored as RRGGBB in <c>background.txt</c>. Default black.</summary>
        public static Color BackgroundColor {
            get {
                return CurrentBackground;
            }
            set {
                cachedBackground = value;
                cachedBackgroundValid = true;
                BackgroundVersion++;
                try {
                    Directory.CreateDirectory(Folder);
                    File.WriteAllText(Path.Combine(Folder, BackgroundFileName), ColorUtility.ToHtmlStringRGB(value));
                } catch (Exception ex) {
                    Debug.LogWarning("PMSDKSkinStore: could not write background - " + ex.Message);
                }
            }
        }

        private static Color cachedBackground = Color.black;
        private static bool cachedBackgroundValid;

        /// <summary>Incremented every time the background colour is set; per-frame users compare it
        /// instead of reading the file.</summary>
        public static int BackgroundVersion { get; private set; }

        /// <summary>The output background colour, read from disk once and then cached.</summary>
        public static Color CurrentBackground {
            get {
                if (!cachedBackgroundValid) {
                    cachedBackground = Color.black;
                    try {
                        string file = Path.Combine(Folder, BackgroundFileName);
                        if (File.Exists(file)) {
                            Color parsed;
                            if (TryParseHexColor(File.ReadAllText(file), out parsed)) {
                                cachedBackground = parsed;
                            }
                        }
                    } catch (Exception ex) {
                        Debug.LogWarning("PMSDKSkinStore: could not read background - " + ex.Message);
                    }
                    cachedBackgroundValid = true;
                    BackgroundVersion++;
                }
                return cachedBackground;
            }
        }

        /// <summary>Folder for photo-derived silhouette masks (PNG), shared by all skin sets.</summary>
        public static string MasksFolder {
            get {
                return Path.Combine(Application.persistentDataPath, "SkinMasks");
            }
        }

        /// <summary>A bare file name lives in <see cref="MasksFolder"/>; a rooted path is used as is.</summary>
        public static string ResolveMaskPath(string maskFile)
        {
            if (string.IsNullOrEmpty(maskFile)) {
                return "";
            }
            return Path.IsPathRooted(maskFile) ? maskFile : Path.Combine(MasksFolder, maskFile);
        }

        /// <summary>Accepts "RRGGBB" or "#RRGGBB" (surrounding whitespace ignored).</summary>
        public static bool TryParseHexColor(string text, out Color color)
        {
            color = Color.black;
            if (string.IsNullOrWhiteSpace(text)) {
                return false;
            }
            string trimmed = text.Trim();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal)) {
                trimmed = "#" + trimmed;
            }
            if (trimmed.Length != 7) {
                return false;
            }
            return ColorUtility.TryParseHtmlString(trimmed, out color);
        }

        /// <summary>Path of the active set's JSON file (what the controller's Save/Load use).</summary>
        public static string DefaultPath {
            get {
                return PathFor(ActiveName);
            }
        }

        /// <summary>Keep letters, digits, space, '-' and '_'; trim; cap the length. Returns "" when nothing is left.</summary>
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) {
                return "";
            }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < name.Length && sb.Length < MaxNameLength; i++) {
                char c = name[i];
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_') {
                    sb.Append(c);
                }
            }
            return sb.ToString().Trim();
        }

        public static string PathFor(string name)
        {
            return Path.Combine(Folder, SanitizeName(name) + ".json");
        }

        public static bool Exists()
        {
            return File.Exists(DefaultPath);
        }

        public static bool Exists(string name)
        {
            string clean = SanitizeName(name);
            return clean.Length > 0 && File.Exists(PathFor(clean));
        }

        /// <summary>Names of all saved sets, alphabetical.</summary>
        public static List<string> ListNames()
        {
            List<string> names = new List<string>();
            try {
                if (!Directory.Exists(Folder)) {
                    return names;
                }
                string[] files = Directory.GetFiles(Folder, "*.json");
                for (int i = 0; i < files.Length; i++) {
                    names.Add(Path.GetFileNameWithoutExtension(files[i]));
                }
                names.Sort(StringComparer.OrdinalIgnoreCase);
            } catch (Exception ex) {
                Debug.LogWarning("PMSDKSkinStore: could not list sets - " + ex.Message);
            }
            return names;
        }

        /// <summary>Create an empty set file. Returns false if the name is invalid or already exists.</summary>
        public static bool CreateEmpty(string name)
        {
            string clean = SanitizeName(name);
            if (clean.Length == 0 || Exists(clean)) {
                return false;
            }
            PMSDKSkinSetJson.Set set = new PMSDKSkinSetJson.Set();
            set.name = clean;
            return SaveTo(PathFor(clean), set);
        }

        /// <summary>Read the active set, or null if none exists / it is unreadable.</summary>
        public static PMSDKSkinSetJson.Set Load()
        {
            return LoadFrom(DefaultPath);
        }

        public static PMSDKSkinSetJson.Set LoadFrom(string path)
        {
            try {
                if (!File.Exists(path)) {
                    return null;
                }
                return PMSDKSkinSetJson.Deserialize(File.ReadAllText(path));
            } catch (Exception ex) {
                Debug.LogWarning("PMSDKSkinStore: load failed - " + ex.Message);
                return null;
            }
        }

        /// <summary>Deep copy via JSON so callers never mutate a ScriptableObject's data.</summary>
        public static PMSDKSkinSetJson.Set Clone(PMSDKSkinSetJson.Set set)
        {
            if (set == null) {
                return null;
            }
            return PMSDKSkinSetJson.Deserialize(JsonUtility.ToJson(set));
        }

        /// <summary>Write into the active set's file.</summary>
        public static bool Save(PMSDKSkinSetJson.Set set)
        {
            return SaveTo(DefaultPath, set);
        }

        public static bool SaveTo(string path, PMSDKSkinSetJson.Set set)
        {
            if (set == null) {
                return false;
            }
            try {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, PMSDKSkinSetJson.Serialize(set, true));
                if (File.Exists(path)) {
                    File.Delete(path);
                }
                File.Move(tmp, path);
                return true;
            } catch (Exception ex) {
                Debug.LogError("PMSDKSkinStore: save failed - " + ex.Message);
                return false;
            }
        }

        /// <summary>Replace one skin's default content inside <paramref name="set"/> and write the active file.</summary>
        public static bool SaveContent(PMSDKSkinSetJson.Set set, string skinId, PMSDKSkinContent content)
        {
            if (set == null || set.skins == null || content == null) {
                return false;
            }
            for (int i = 0; i < set.skins.Count; i++) {
                if (set.skins[i] != null && set.skins[i].id == skinId) {
                    PMSDKSkinSetJson.DefaultContent captured = PMSDKSkinSetJson.CaptureDefaultContent(content);
                    captured.loop = content.Loop;
                    captured.mute = content.Mute;
                    set.skins[i].defaultContent = captured;
                    return Save(set);
                }
            }
            Debug.LogWarning("PMSDKSkinStore: skin id not found in set: " + skinId);
            return false;
        }
    }
}
