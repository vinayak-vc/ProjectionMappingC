using System.IO;
using UnityEditor;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Editor
{
    /// <summary>
    /// Save / load flow between the Skin Editor scene and a <see cref="PMSDKSkinSet"/>
    /// asset (M-Sk2.5). Sits under <c>Tools > Projection Mapping > Skins</c>.
    ///
    /// Save writes the ScriptableObject asset + a JSON sidecar next to it (D-037: SO for
    /// Unity, JSON for portability). Load reads either — the sidecar wins if present.
    /// </summary>
    public static class PMSDKSkinSetMenu
    {
        private const string DefaultFolder = "Assets/PMSDKDemo/Skins";

        [MenuItem("Tools/Projection Mapping/Skins/Save Skin Set...", priority = 220)]
        public static void SaveSkinSet()
        {
            var controller = Object.FindFirstObjectByType<PMSDKSkinEditorController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Save Skin Set",
                    "No PMSDKSkinEditorController found in the open scene. Open the Skin " +
                    "Editor scene (Tools > Projection Mapping > Skins > Generate Skin Editor " +
                    "Scene) first.", "OK");
                return;
            }

            EnsureFolder(DefaultFolder);
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Skin Set", "SkinSet", "asset",
                "Choose where to save the Skin Set asset.", DefaultFolder);
            if (string.IsNullOrEmpty(path)) return;

            // Reuse an existing asset if the user picked its path, so the .asset's GUID
            // stays stable (references from content scenes survive the overwrite).
            var existing = AssetDatabase.LoadAssetAtPath<PMSDKSkinSet>(path);
            var set = existing != null ? existing : ScriptableObject.CreateInstance<PMSDKSkinSet>();
            set.CaptureFrom(controller);
            set.Data.name = Path.GetFileNameWithoutExtension(path);

            if (existing == null)
            {
                AssetDatabase.CreateAsset(set, path);
            }
            else
            {
                EditorUtility.SetDirty(set);
            }

            // Sidecar JSON — same folder, same base name, .json extension.
            string sidecar = Path.ChangeExtension(path, ".json");
            set.SidecarPath = ""; // convention: empty = "same folder, same base name"
            set.SaveJsonTo(sidecar);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = set;
            EditorGUIUtility.PingObject(set);
            Debug.Log($"Saved Skin Set → {path} + sidecar {sidecar} ({set.Data.skins.Count} skins).");
        }

        [MenuItem("Tools/Projection Mapping/Skins/Load Skin Set...", priority = 221)]
        public static void LoadSkinSet()
        {
            var controller = Object.FindFirstObjectByType<PMSDKSkinEditorController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Load Skin Set",
                    "No PMSDKSkinEditorController in the scene — open the Skin Editor scene first.",
                    "OK");
                return;
            }

            string path = EditorUtility.OpenFilePanel("Load Skin Set", DefaultFolder, "asset,json");
            if (string.IsNullOrEmpty(path)) return;

            // Project-relative → asset load. Absolute path outside project → JSON only.
            if (path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
            {
                var rel = MakeProjectRelative(path);
                var so = AssetDatabase.LoadAssetAtPath<PMSDKSkinSet>(rel);
                if (so == null)
                {
                    EditorUtility.DisplayDialog("Load Skin Set",
                        "Not a PMSDKSkinSet asset: " + rel, "OK");
                    return;
                }
                Undo.RecordObject(controller, "Load Skin Set");
                PMSDKSkinSetJson.Apply(controller, so.Data);
            }
            else if (path.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
            {
                Undo.RecordObject(controller, "Load Skin Set JSON");
                controller.LoadFrom(path);
            }
        }

        [MenuItem("Tools/Projection Mapping/Skins/New Empty Skin Set Asset...", priority = 230)]
        public static void NewEmptySkinSet()
        {
            EnsureFolder(DefaultFolder);
            string path = EditorUtility.SaveFilePanelInProject(
                "New Skin Set", "SkinSet", "asset",
                "Choose where to save the new (empty) Skin Set asset.", DefaultFolder);
            if (string.IsNullOrEmpty(path)) return;

            var set = ScriptableObject.CreateInstance<PMSDKSkinSet>();
            set.Data.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(set, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = set;
            EditorGUIUtility.PingObject(set);
        }

        // ---- helpers ----

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string MakeProjectRelative(string absolutePath)
        {
            var dataPath = Application.dataPath.Replace('\\', '/');
            var norm = absolutePath.Replace('\\', '/');
            if (norm.StartsWith(dataPath, System.StringComparison.OrdinalIgnoreCase))
                return "Assets" + norm.Substring(dataPath.Length);
            return norm;
        }
    }
}
