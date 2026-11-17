using UnityEditor;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Editor
{
    /// <summary>
    /// Menu items for creating and converting <see cref="PMSDKSkin"/> GameObjects. Sits
    /// under `Tools > Projection Mapping > Skins`.
    ///
    /// Design (D-036, D-037, docs/multi-skin-design.md):
    /// - "Add Skin > Rectangle/Ellipse/Polygon/Freeform" creates a fully-configured skin
    ///   GameObject next to the current selection.
    /// - "Convert Projector to Multi-Skin" is a stub for M-Sk2; for M-Sk1 it just points
    ///   the user to the Add Skin path (multi-skin is opt-in per skin child).
    /// </summary>
    public static class PMSDKSkinMenu
    {
        private const string MenuRoot = "Tools/Projection Mapping/Skins/";

        [MenuItem(MenuRoot + "Add Skin/Rectangle", priority = 100)]
        private static void AddRectangle() => CreateSkin(PMSDKSkinShapeKind.Rectangle);

        [MenuItem(MenuRoot + "Add Skin/Ellipse", priority = 101)]
        private static void AddEllipse() => CreateSkin(PMSDKSkinShapeKind.Ellipse);

        [MenuItem(MenuRoot + "Add Skin/Polygon (Hexagon)", priority = 110)]
        private static void AddPolygon() => CreateSkin(PMSDKSkinShapeKind.Polygon);

        [MenuItem(MenuRoot + "Add Skin/Freeform (empty)", priority = 111)]
        private static void AddFreeform() => CreateSkin(PMSDKSkinShapeKind.Freeform);

        [MenuItem(MenuRoot + "Convert Projector to Multi-Skin", priority = 200)]
        private static void ConvertProjector()
        {
            // v1 note: a PMSDKProjector already tolerates multiple sibling PMSDKSkin
            // children — there's nothing to "convert". This menu item exists to nudge users
            // in the right direction and, in M-Sk2, will strip the classic split-slice
            // material off any pre-existing single warp surface.
            EditorUtility.DisplayDialog(
                "Convert Projector to Multi-Skin",
                "M-Sk1: to add skins to a projector, use `Add Skin > ...` while the projector " +
                "(or a child under it) is selected. The projector's ortho camera will render " +
                "the skins alongside any existing warp surface. Stripping the classic split-slice " +
                "material is deferred to M-Sk2.",
                "OK");
        }

        // -------- creation --------

        private static void CreateSkin(PMSDKSkinShapeKind kind)
        {
            // Resolve which projector the new skin belongs to. Priority:
            //   1. Selected projector.
            //   2. Selected object's PMSDKProjector in the hierarchy above.
            //   3. First PMSDKProjector in the scene.
            //   4. Otherwise: create a rootless skin (user must assign Projector).
            PMSDKProjector projector = null;
            Transform parent = null;
            if (Selection.activeGameObject != null)
            {
                projector = Selection.activeGameObject.GetComponentInParent<PMSDKProjector>();
                parent = Selection.activeGameObject.transform.parent;
            }
            if (projector == null)
                projector = Object.FindFirstObjectByType<PMSDKProjector>();
            if (parent == null && projector != null)
                parent = projector.transform.parent;

            // Layer: match the projector camera's culling mask if we can find a warp
            // surface on the current mask. Otherwise fall back to TransparentFX (the
            // conventional raster-space layer per docs/unity-architecture.md §2).
            int layer = LayerMask.NameToLayer("TransparentFX");
            if (projector != null)
            {
                var existingWarp = Object.FindFirstObjectByType<PMSDKMeshWarp>();
                if (existingWarp != null) layer = existingWarp.gameObject.layer;
            }
            if (layer < 0) layer = 0; // TransparentFX absent? use Default.

            string skinName = $"Skin_{kind}";
            var go = new GameObject(skinName, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = layer;
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            // Match the existing warp-surface convention (docs/unity-architecture.md §2):
            // identity rotation, 16:9 horizontal stretch so the [0,1]^2 raster fills the
            // projector's ortho view.
            go.transform.localScale = new Vector3(16f / 9f, 1f, 1f);

            // Renderer setup — assign the package's UnlitWarp material shader as a shared
            // material asset would be ideal, but at M-Sk1 we let Unity assign a default and
            // the user swaps for PMSDK/UnlitWarp. For a fully-configured demo the shader is
            // set explicitly below.
            var mr = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("PMSDK/UnlitWarp");
            if (shader != null)
            {
                var mat = new Material(shader) { name = $"{skinName}_Material" };
                mr.sharedMaterial = mat;
            }

            // Configure skin + companions per kind.
            var skin = go.AddComponent<PMSDKSkin>();
            skin.Kind = kind;
            skin.SkinId = System.Guid.NewGuid().ToString("N").Substring(0, 8);
            skin.DisplayName = skinName;
            skin.Vertices = MakeDefaultVertices(kind);
            skin.Projector = projector;

            if (IsQuadKind(kind))
            {
                var mw = go.AddComponent<PMSDKMeshWarp>();
                mw.Projector = projector;
                go.AddComponent<PMSDKCornerPin>();
            }
            // PMSDKSkinContent is optional but useful — default to None so nothing is
            // allocated until the user picks a source.
            go.AddComponent<PMSDKSkinContent>();

            Undo.RegisterCreatedObjectUndo(go, "Create PMSDK Skin");
            Selection.activeGameObject = go;

            if (projector == null)
            {
                Debug.LogWarning(
                    $"Created {skinName} but no PMSDKProjector found in the scene. Assign one on the " +
                    $"skin (and on the child PMSDKMeshWarp if present) before entering play mode.",
                    go);
            }
            else
            {
                Debug.Log($"Created {skinName} under projector '{projector.name}' (kind={kind}).", go);
            }
        }

        private static bool IsQuadKind(PMSDKSkinShapeKind k) =>
            k == PMSDKSkinShapeKind.Rectangle || k == PMSDKSkinShapeKind.Ellipse;

        private static System.Collections.Generic.List<Vector2> MakeDefaultVertices(PMSDKSkinShapeKind k)
        {
            var list = new System.Collections.Generic.List<Vector2>();
            switch (k)
            {
                case PMSDKSkinShapeKind.Rectangle:
                case PMSDKSkinShapeKind.Ellipse:
                    // Center a 50% x 50% quad in the raster so a fresh skin is visible.
                    list.Add(new Vector2(0.25f, 0.25f));
                    list.Add(new Vector2(0.75f, 0.25f));
                    list.Add(new Vector2(0.75f, 0.75f));
                    list.Add(new Vector2(0.25f, 0.75f));
                    break;

                case PMSDKSkinShapeKind.Polygon:
                    // Regular hexagon centered at (0.5, 0.5) with radius 0.25.
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Mathf.PI * 2f * i / 6f + Mathf.PI * 0.5f; // start pointing up
                        list.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.25f, 0.5f + Mathf.Sin(a) * 0.25f));
                    }
                    break;

                case PMSDKSkinShapeKind.Freeform:
                    // Triangle stub — user extends by clicking (M-Sk2). Three points is the
                    // minimum for a valid polygon.
                    list.Add(new Vector2(0.30f, 0.25f));
                    list.Add(new Vector2(0.70f, 0.25f));
                    list.Add(new Vector2(0.50f, 0.75f));
                    break;
            }
            return list;
        }
    }
}
