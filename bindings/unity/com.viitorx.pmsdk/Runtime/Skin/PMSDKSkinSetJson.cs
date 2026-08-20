using System;
using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// JSON round-trip for a Skin Editor session (M-Sk2 Phase C). Uses Unity's built-in
    /// <see cref="JsonUtility"/> with a typed DTO tree; the output schema is close to but
    /// not identical to the canonical schema documented in <c>docs/multi-skin-design.md</c>
    /// (Vector2 lists serialize as <c>{"x":..,"y":..}</c> objects, not tuple arrays).
    /// M-Sk2.5 will migrate to the canonical <c>PMSDKSkinSet</c> ScriptableObject +
    /// canonical JSON sidecar; this file exists so the Skin Editor scene isn't a
    /// dead-end at Phase C.
    ///
    /// Snapshot capture (undo) also goes through this — a snapshot is just a captured
    /// JSON string; restore parses back and re-applies the state to the live scene,
    /// preserving object identity where possible (skins matched by SkinId).
    /// </summary>
    public static class PMSDKSkinSetJson
    {
        public const int SchemaVersion = 1;

        // -------- DTO tree (JsonUtility-friendly) --------

        [Serializable]
        public class Set
        {
            public int version = SchemaVersion;
            public string name = "Untitled";
            public ProjectorRef projector;
            public List<Skin> skins = new List<Skin>();
            public int selectedIndex = -1;
            public int selectedCorner = 0;
        }

        [Serializable] public class ProjectorRef
        {
            public string id;
            public int displayIndex;
        }

        [Serializable] public class Skin
        {
            public string id;
            public string name;
            public string shape;   // "rectangle" | "ellipse" | "polygon" | "freeform"
            public List<Vector2> vertices = new List<Vector2>();
            public float opacity = 1f;
            public float softMask = 0f;
            public int zOrder = 0;
        }

        // -------- capture / apply --------

        /// <summary>
        /// Build a <see cref="Set"/> from the controller's current scene state. Pure —
        /// does not touch the scene.
        /// </summary>
        public static Set Capture(PMSDKSkinEditorController controller)
        {
            var set = new Set
            {
                name = "SkinSet",
                selectedIndex = controller.SelectedSkinIndex,
                selectedCorner = controller.SelectedCornerIndex,
                projector = controller.Projector == null ? null : new ProjectorRef
                {
                    id = controller.Projector.name,
                    displayIndex = controller.Projector.TryGetComponent(out Camera cam) ? cam.targetDisplay : 0,
                },
            };
            foreach (var s in controller.Skins)
            {
                if (s == null) continue;
                set.skins.Add(new Skin
                {
                    id = s.SkinId,
                    name = s.DisplayName,
                    shape = ShapeToString(s.Kind),
                    vertices = new List<Vector2>(s.Vertices ?? new List<Vector2>()),
                    opacity = s.Opacity,
                    softMask = s.SoftMask,
                    zOrder = s.ZOrder,
                });
            }
            return set;
        }

        /// <summary>
        /// Apply a captured <see cref="Set"/> back to the controller's scene. Skins with
        /// matching <see cref="PMSDKSkin.SkinId"/> are updated in place; unknown ids are
        /// spawned; scene skins whose id is not in the snapshot are destroyed. Selection
        /// is restored last.
        /// </summary>
        public static void Apply(PMSDKSkinEditorController controller, Set set)
        {
            if (controller == null || set == null) return;

            // Snapshot scene skins by id for O(1) lookup.
            controller.RefreshSkinList();
            var byId = new Dictionary<string, PMSDKSkin>();
            foreach (var s in controller.Skins) if (s != null && !string.IsNullOrEmpty(s.SkinId)) byId[s.SkinId] = s;

            var keep = new HashSet<string>();
            foreach (var sk in set.skins)
            {
                keep.Add(sk.id);
                if (byId.TryGetValue(sk.id, out var existing) && existing != null)
                {
                    ApplyToSkin(existing, sk);
                }
                else
                {
                    var spawned = controller.SpawnSkinFromSnapshot(StringToShape(sk.shape),
                        new List<Vector2>(sk.vertices), sk.id, sk.name);
                    if (spawned != null) ApplyToSkin(spawned, sk);
                }
            }

            // Destroy any live skin whose id isn't in the snapshot.
            foreach (var kv in byId)
            {
                if (!keep.Contains(kv.Key) && kv.Value != null)
                {
                    if (Application.isEditor && !Application.isPlaying) UnityEngine.Object.DestroyImmediate(kv.Value.gameObject);
                    else UnityEngine.Object.Destroy(kv.Value.gameObject);
                }
            }

            controller.RefreshSkinList();
            controller.SelectedSkinIndex = Mathf.Clamp(set.selectedIndex, -1, controller.Skins.Count - 1);
            controller.SelectedCornerIndex = Mathf.Max(0, set.selectedCorner);
        }

        private static void ApplyToSkin(PMSDKSkin s, Skin dto)
        {
            s.SkinId = dto.id;
            s.DisplayName = dto.name;
            s.Kind = StringToShape(dto.shape);
            s.Vertices = new List<Vector2>(dto.vertices);
            s.Opacity = dto.opacity;
            s.SoftMask = dto.softMask;
            s.ZOrder = dto.zOrder;
        }

        // -------- (de)serialize --------

        public static string Serialize(PMSDKSkinEditorController controller, bool prettyPrint = true) =>
            JsonUtility.ToJson(Capture(controller), prettyPrint);

        public static Set Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var set = JsonUtility.FromJson<Set>(json);
            return set;
        }

        // -------- string ⇄ enum --------

        public static string ShapeToString(PMSDKSkinShapeKind k) => k switch
        {
            PMSDKSkinShapeKind.Rectangle => "rectangle",
            PMSDKSkinShapeKind.Ellipse   => "ellipse",
            PMSDKSkinShapeKind.Polygon   => "polygon",
            PMSDKSkinShapeKind.Freeform  => "freeform",
            _ => "rectangle",
        };

        public static PMSDKSkinShapeKind StringToShape(string s) => s switch
        {
            "ellipse"  => PMSDKSkinShapeKind.Ellipse,
            "polygon"  => PMSDKSkinShapeKind.Polygon,
            "freeform" => PMSDKSkinShapeKind.Freeform,
            _          => PMSDKSkinShapeKind.Rectangle,
        };
    }
}
