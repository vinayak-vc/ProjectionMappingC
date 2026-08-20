using UnityEditor;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Editor
{
    /// <summary>
    /// Editor menu bridges for <see cref="PMSDKSkinPresets"/> — one menu item per
    /// preset under `Tools > Projection Mapping > Skins > Presets`. Each item finds
    /// the current <see cref="PMSDKSkinEditorController"/> in the open scene and
    /// applies the preset via <c>ApplyPresetById</c>.
    ///
    /// Priorities are grouped so beginner presets (Single Rectangle, Boxes) sort
    /// first, followed by curved / grid targets. Menu priorities 300-399 are
    /// reserved for this feature.
    /// </summary>
    public static class PMSDKSkinPresetsMenu
    {
        private const string Root = "Tools/Projection Mapping/Skins/Presets/";

        [MenuItem(Root + "1. Single Rectangle", priority = 300)]
        private static void P1() => Apply("single-rect");

        [MenuItem(Root + "2. Box: 3 Faces Isometric ★", priority = 301)]
        private static void P2() => Apply("box-iso-3");

        [MenuItem(Root + "3. Box: 2 Faces (Top+Front)", priority = 302)]
        private static void P3() => Apply("box-2");

        [MenuItem(Root + "4. Column / Cylinder (3 strips)", priority = 310)]
        private static void P4() => Apply("column-3");

        [MenuItem(Root + "5. Triangular Prism (2 faces)", priority = 311)]
        private static void P5() => Apply("prism-2");

        [MenuItem(Root + "6. Facade Grid 3x2", priority = 320)]
        private static void P6() => Apply("facade-3x2");

        [MenuItem(Root + "7. Arch / Tunnel", priority = 330)]
        private static void P7() => Apply("arch");

        [MenuItem(Root + "8. Dome / Sphere", priority = 331)]
        private static void P8() => Apply("dome");

        [MenuItem(Root + "9. Star (5-point)", priority = 340)]
        private static void P9() => Apply("star");

        [MenuItem(Root + "10. Diorama Stage (Frame+Stage)", priority = 350)]
        private static void P10() => Apply("diorama");

        // ---- shared apply path ----

        private static void Apply(string presetId)
        {
            var controller = Object.FindFirstObjectByType<PMSDKSkinEditorController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Skin Preset",
                    "No PMSDKSkinEditorController found in the open scene. Open (or generate) the " +
                    "Skin Editor scene first via `Tools > Projection Mapping > Skins > Generate Skin Editor Scene`.",
                    "OK");
                return;
            }
            if (!PMSDKSkinPresets.TryFind(presetId, out var preset))
            {
                Debug.LogError($"PMSDKSkinPresetsMenu: unknown preset id '{presetId}'.");
                return;
            }

            // Ask before destroying user's work if any skins already exist.
            var existing = Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None);
            bool replace = true;
            if (existing != null && existing.Length > 0)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Apply Preset",
                    $"'{preset.DisplayName}' will add {preset.Skins.Length} skin(s) to the scene.\n\n" +
                    $"{existing.Length} skin(s) already exist. Replace them or add alongside?",
                    "Replace",
                    "Cancel",
                    "Add alongside");
                if (choice == 1) return; // canceled
                replace = choice == 0;
            }

            controller.ApplyPreset(preset, replaceExisting: replace);
            Debug.Log($"PMSDKSkinPresetsMenu: applied '{preset.DisplayName}' ({preset.Skins.Length} skin(s)).");
        }
    }
}
