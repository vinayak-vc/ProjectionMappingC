using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using vxpmsdk.Components;

namespace vxpmsdk.Editor
{
    /// <summary>
    /// Generates <c>Assets/PMSDKDemo/SkinEditor.unity</c> — a self-contained scene that
    /// hosts the Skin Editor rig (projector camera on Display 2, `PMSDKSkinEditorController`
    /// + Input + Overlay + HUD on a "PMSDK Skin Editor" GameObject, Display activator on
    /// runtime services). Same pattern as <see cref="PMSDKDemoSceneGenerator"/>: idempotent,
    /// creates any missing folders, opens the freshly-saved scene.
    ///
    /// The scene is intentionally minimal. Content sources come from Skins the user adds
    /// via `Add Skin > ...` (M-Sk1 menu) or draws directly in the editor scene. See
    /// D-037 for the design rationale (Skin Editor is dedicated to authoring; content
    /// scenes load the saved skin set separately in M-Sk2.5).
    /// </summary>
    public static class PMSDKSkinEditorSceneGenerator
    {
        private const string DemoFolder = "Assets/PMSDKDemo";
        private const string ScenePath  = "Assets/PMSDKDemo/SkinEditor.unity";

        [MenuItem("Tools/Projection Mapping/Skins/Generate Skin Editor Scene", priority = 210)]
        public static void Generate()
        {
            EnsureFolder(DemoFolder);
            EnsureLayer("TransparentFX"); // built-in layer 1 — always present, but check anyway.

            // Save the current scene so the user can undo an accidental overwrite.
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.Log("Skin Editor scene generation canceled (unsaved changes).");
                    return;
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- runtime services ----
            var services = new GameObject("PMSDK Runtime Services");
            services.AddComponent<PMSDKDisplayActivator>();
            // A CalibrationManager isn't required for the Skin Editor scene, but drop one
            // in with services so F2-style calibration still works out of the box.
            services.AddComponent<PMSDKCalibrationManager>();

            // ---- projector camera (Display 2, ortho) ----
            var projGO = new GameObject("PMSDK_Projector_SkinEditor");
            var cam = projGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f); // near-black; skins visible against it
            cam.targetDisplay = 1; // Display 2 (0-indexed)
            int fxLayer = LayerMask.NameToLayer("TransparentFX");
            cam.cullingMask = (fxLayer >= 0 ? (1 << fxLayer) : 0) | (1 << LayerMask.NameToLayer("UI"));

            projGO.transform.position = new Vector3(16f / 9f * 0.5f, 0.5f, -5f);
            projGO.transform.rotation = Quaternion.identity;
            var projector = projGO.AddComponent<PMSDKProjector>();

            // ---- editor rig ----
            var editorGO = new GameObject("PMSDK Skin Editor");
            var controller = editorGO.AddComponent<PMSDKSkinEditorController>();
            controller.Projector = projector;
            controller.SkinLayerName = "TransparentFX";

            editorGO.AddComponent<PMSDKSkinEditorInput>();
            var overlay = editorGO.AddComponent<PMSDKSkinEditorOverlay>();
            overlay.Controller = controller;
            var hud = editorGO.AddComponent<PMSDKSkinEditorHUD>();
            hud.Controller = controller;

            // ---- save ----
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Skin Editor Scene",
                "Created " + ScenePath + "\n\n" +
                "Enter Play mode, then:\n" +
                "  R / E / P / F  — pick a tool\n" +
                "  drag on the projector output — draw a rectangle / ellipse\n" +
                "  drag a corner or edge midpoint — reshape\n" +
                "  N — add a default shape\n" +
                "  F1 — help\n\n" +
                "Requires Display 2 for the drawing canvas. Add a display to the Game view " +
                "(Display 2) or the projector output will fall back to Display 1.",
                "OK");

            Debug.Log("Generated Skin Editor scene: " + ScenePath);
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

        private static void EnsureLayer(string name)
        {
            // Built-in layers 0..7 include TransparentFX at layer 1. If a user-facing layer
            // needs to be added programmatically, ProjectSettings/TagManager.asset would be
            // edited here — for phase A we assume the built-in layer is available.
            if (LayerMask.NameToLayer(name) < 0)
                Debug.LogWarning("PMSDKSkinEditorSceneGenerator: layer '" + name +
                                 "' is missing. Skin rendering may look off until it is added.");
        }
    }
}
