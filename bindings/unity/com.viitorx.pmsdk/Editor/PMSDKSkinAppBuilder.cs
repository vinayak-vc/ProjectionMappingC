using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Editor
{
    /// <summary>
    /// Builds the single-EXE skin app: the Player scene first (so the EXE boots straight into
    /// playback) and the Editor scene second. Scenes are passed explicitly to the build, so
    /// neither EditorBuildSettings nor any PlayerSettings are modified.
    /// </summary>
    public static class PMSDKSkinAppBuilder
    {
        private const string ExeName = "SkinApp.exe";

        [MenuItem("Tools/Projection Mapping/Skins/Build Skin App (Windows EXE)", priority = 220)]
        public static void BuildFromMenu()
        {
            string folder = EditorUtility.SaveFolderPanel("Choose the output folder for the Skin App", "", "SkinApp");
            if (string.IsNullOrEmpty(folder)) {
                return;
            }
            Build(folder);
        }

        /// <summary>Build to <paramref name="outputFolder"/>. Returns true on success.</summary>
        public static bool Build(string outputFolder)
        {
            string playerScene = FindScene(PMSDKSkinApp.PlayerSceneName);
            string editorScene = FindScene(PMSDKSkinApp.EditorSceneName);
            if (playerScene == null || editorScene == null) {
                Debug.LogError("PMSDKSkinAppBuilder: could not find scenes '" + PMSDKSkinApp.PlayerSceneName
                    + "' and '" + PMSDKSkinApp.EditorSceneName + "' in the project.");
                return false;
            }
            if (string.IsNullOrEmpty(outputFolder)) {
                Debug.LogError("PMSDKSkinAppBuilder: output folder is empty.");
                return false;
            }
            Directory.CreateDirectory(outputFolder);

            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = new string[] { playerScene, editorScene };
            options.locationPathName = Path.Combine(outputFolder, ExeName);
            options.target = BuildTarget.StandaloneWindows64;
            options.options = BuildOptions.None;

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded) {
                Debug.Log("PMSDKSkinAppBuilder: build succeeded (" + (summary.totalSize / (1024 * 1024)) + " MB) -> "
                    + options.locationPathName);
                return true;
            }
            Debug.LogError("PMSDKSkinAppBuilder: build " + summary.result + " (" + summary.totalErrors + " errors).");
            return false;
        }

        private static string FindScene(string sceneName)
        {
            string[] guids = AssetDatabase.FindAssets(sceneName + " t:Scene");
            for (int i = 0; i < guids.Length; i++) {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (Path.GetFileNameWithoutExtension(path) == sceneName) {
                    return path;
                }
            }
            return null;
        }
    }
}
