using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Editor
{
    /// <summary>
    /// Generates <c>Assets/PMSDKDemo/SkinPlayback.unity</c> — a self-contained *content*
    /// scene that loads a saved <see cref="PMSDKSkinSet"/> at Play time via
    /// <see cref="PMSDKSkinLoader"/>. Mirrors the projector + services rig from
    /// <see cref="PMSDKSkinEditorSceneGenerator"/>, but drops the editor rig
    /// (Controller/Input/Overlay/HUD/CanvasInspector/CanvasList) since this scene is
    /// for playback, not authoring.
    ///
    /// The generator picks a default SkinSet asset by convention
    /// (<c>Assets/PMSDKDemo/Skins/SkinSet.asset</c>) and, if that isn't present,
    /// searches the project for the newest <c>PMSDKSkinSet</c>. When none is found
    /// it still writes the scene with a null <see cref="PMSDKSkinLoader.SkinSet"/>
    /// and prompts the user to assign one — the scene stays useful as a template.
    ///
    /// Idempotent: overwrites <see cref="ScenePath"/> in place, creating parent
    /// folders as needed.
    /// </summary>
    public static class PMSDKSkinPlaybackSceneGenerator
    {
        private const string DemoFolder      = "Assets/PMSDKDemo";
        private const string ScenePath       = "Assets/PMSDKDemo/SkinPlayback.unity";
        private const string DefaultSkinSet  = "Assets/PMSDKDemo/Skins/SkinSet.asset";

        [MenuItem("Tools/Projection Mapping/Skins/Generate Skin Playback Scene", priority = 211)]
        public static void Generate()
        {
            EnsureFolder(DemoFolder);
            EnsureLayer("TransparentFX");

            // Save the current scene so an accidental overwrite is recoverable.
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.Log("Skin Playback scene generation canceled (unsaved changes).");
                    return;
                }
            }

            // Resolve the SkinSet asset: default path first, then any-newest fallback.
            var skinSet = AssetDatabase.LoadAssetAtPath<PMSDKSkinSet>(DefaultSkinSet);
            string resolvedPath = DefaultSkinSet;
            if (skinSet == null)
            {
                var guids = AssetDatabase.FindAssets("t:vxpmsdk.Components.PMSDKSkinSet");
                if (guids != null && guids.Length > 0)
                {
                    // Newest-modified wins so re-running "Save Skin Set…" and this
                    // generator in sequence picks up the latest authored set.
                    string newest = null;
                    System.DateTime newestTime = System.DateTime.MinValue;
                    foreach (var g in guids)
                    {
                        var p = AssetDatabase.GUIDToAssetPath(g);
                        var t = File.GetLastWriteTimeUtc(p);
                        if (t > newestTime) { newest = p; newestTime = t; }
                    }
                    if (newest != null)
                    {
                        skinSet = AssetDatabase.LoadAssetAtPath<PMSDKSkinSet>(newest);
                        resolvedPath = newest;
                    }
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- runtime services ----
            var services = new GameObject("PMSDK Runtime Services");
            services.AddComponent<PMSDKDisplayActivator>();
            services.AddComponent<PMSDKCalibrationManager>();

            // ---- projector camera (Display 2, ortho) — matches the editor scene ----
            var projGO = new GameObject("PMSDK_Projector_Playback");
            var cam = projGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f);
            cam.targetDisplay = 1; // Display 2 (0-indexed)
            int fxLayer = LayerMask.NameToLayer("TransparentFX");
            cam.cullingMask = (fxLayer >= 0 ? (1 << fxLayer) : 0) | (1 << LayerMask.NameToLayer("UI"));

            projGO.transform.position = new Vector3(16f / 9f * 0.5f, 0.5f, -5f);
            projGO.transform.rotation = Quaternion.identity;
            var projector = projGO.AddComponent<PMSDKProjector>();

            // ---- loader (spawns the SkinSet at Play) ----
            var loaderGO = new GameObject("PMSDK Skin Playback");
            loaderGO.transform.SetParent(projGO.transform, worldPositionStays: false);

            var loader = loaderGO.AddComponent<PMSDKSkinLoader>();
            loader.SkinSet = skinSet; // may be null — user is prompted below
            loader.Projector = projector;
            loader.SkinLayerName = "TransparentFX";
            loader.ReloadFromSidecar = false; // opt-in: users who edit the .json alongside can enable

            // Bindings placeholder — users add per-skin runtime overrides here.
            // Kept empty by default so `defaultContent` from the SkinSet takes effect
            // without further wiring (the loader's inspector shows this fallback path).
            loaderGO.AddComponent<PMSDKSkinBindings>();

            // ---- save ----
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ---- user-facing summary ----
            string status = skinSet != null
                ? $"Assigned SkinSet: {resolvedPath}\n\nPress Play — the loader will spawn " +
                  $"the {(skinSet.Data != null ? skinSet.Data.skins?.Count ?? 0 : 0)} skin(s) " +
                  "on Display 2 using each skin's defaultContent."
                : "No PMSDKSkinSet asset was found in the project.\n\n" +
                  "1. Open the Skin Editor scene, author skins, and run " +
                  "`Tools > Projection Mapping > Skins > Save Skin Set…`\n" +
                  "2. Then select the 'PMSDK Skin Playback' GameObject in this scene and " +
                  "drag the .asset into the loader's Skin Set field.";

            EditorUtility.DisplayDialog(
                "Skin Playback Scene",
                "Created " + ScenePath + "\n\n" + status + "\n\n" +
                "To override media per skin at runtime, add entries to the sibling " +
                "PMSDKSkinBindings component (SkinId + Mode + payload). " +
                "For CLI overrides pass `-skinset <path>` to the built player.",
                "OK");

            Debug.Log($"Generated Skin Playback scene: {ScenePath} (SkinSet: {(skinSet != null ? resolvedPath : "unassigned")})");
        }

        // ---- helpers (mirror of the editor generator) ----

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void EnsureLayer(string name)
        {
            if (LayerMask.NameToLayer(name) < 0)
                Debug.LogWarning("PMSDKSkinPlaybackSceneGenerator: layer '" + name +
                                 "' is missing. Skin rendering may look off until it is added.");
        }
    }
}
