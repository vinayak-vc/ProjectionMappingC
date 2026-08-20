using System;
using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// JSON round-trip for a Skin Editor session and the wire DTO that
    /// <see cref="PMSDKSkinSet"/> wraps for asset persistence. Uses Unity's built-in
    /// <see cref="JsonUtility"/> — Vector2s serialize as <c>{"x":..,"y":..}</c> objects
    /// rather than the tuple form <c>[x, y]</c> the design doc envisioned. The schema
    /// deviation is documented in D-037 and kept behind the <see cref="SchemaVersion"/>
    /// so a later migration can bump to canonical tuples.
    ///
    /// Snapshot capture (undo) and file save/load both funnel through this — a snapshot
    /// is a captured <see cref="Set"/>; restore applies it back onto the live scene,
    /// reconciling on <c>SkinId</c>.
    /// </summary>
    public static class PMSDKSkinSetJson
    {
        public const int SchemaVersion = 1;

        // -------- DTO tree (JsonUtility-friendly, matches docs/multi-skin-design.md
        // shape except Vector2s stay in JsonUtility form) --------

        [Serializable]
        public class Set
        {
            public int version = SchemaVersion;
            public string name = "SkinSet";
            public ProjectorRef projector = new ProjectorRef();
            public List<Skin> skins = new List<Skin>();

            // Editor-session state — persisted so `Undo` / `Load` restore selection too.
            public int selectedIndex = -1;
            public int selectedCorner = 0;
        }

        [Serializable] public class ProjectorRef
        {
            public string id;
            public Vector2Int resolution = new Vector2Int(1920, 1080);
            public int displayIndex;
        }

        [Serializable] public class Skin
        {
            public string id;
            public string name;
            public string shape;   // "rectangle" | "ellipse" | "polygon" | "freeform"
            public string warp;    // "projective" | "polygon" (derived; stored for portability)
            public List<Vector2> vertices = new List<Vector2>();
            public MaskCfg mask = new MaskCfg();
            public TransformCfg transform = new TransformCfg();
            public ColorCfg color = new ColorCfg();
            public float opacity = 1f;
            public float softMask = 0f;
            public int zOrder = 0;
            public DefaultContent defaultContent = new DefaultContent();
        }

        [Serializable] public class MaskCfg
        {
            public string kind = "none";     // "none" | "ellipse" | "customAlpha"
            public float feather = 0f;       // scalar in v1; v1.1 UI can populate perEdgeFeather
            public List<float> perEdgeFeather = new List<float>();
        }

        [Serializable] public class TransformCfg
        {
            public Vector4 crop = new Vector4(0, 0, 1, 1); // x,y,w,h in [0..1]
            public int rotation = 0;                       // 0 | 90 | 180 | 270
            public bool mirrorX;
            public bool mirrorY;
        }

        [Serializable] public class ColorCfg
        {
            public Vector3 gain = Vector3.one;
            public Vector3 offset = Vector3.zero;
            public Vector3 gamma = Vector3.one;
        }

        [Serializable] public class DefaultContent
        {
            public string mode = "None";     // None | Texture | RenderTexture | VideoClip | TestPattern
            public string path;              // Unity-project-relative or absolute
            public bool loop = true;
            public bool mute;
            public double startTime;
            public string testPatternType = "Checkerboard";
            public int testPatternSize = 512;
            public int testPatternGridSize = 8;
        }

        // -------- capture / apply (Editor scene ↔ Set) --------

        /// <summary>Build a <see cref="Set"/> from the controller's current scene state.
        /// Pure — does not touch the scene.</summary>
        public static Set Capture(PMSDKSkinEditorController controller)
        {
            var set = new Set
            {
                name = "SkinSet",
                selectedIndex = controller.SelectedSkinIndex,
                selectedCorner = controller.SelectedCornerIndex,
                projector = new ProjectorRef
                {
                    id = controller.Projector != null ? controller.Projector.name : "",
                    displayIndex = controller.Projector != null && controller.Projector.TryGetComponent(out Camera cam)
                        ? cam.targetDisplay : 0,
                    resolution = new Vector2Int(Screen.width, Screen.height),
                },
            };
            foreach (var s in controller.Skins)
            {
                if (s == null) continue;
                set.skins.Add(CaptureSkin(s));
            }
            return set;
        }

        internal static Skin CaptureSkin(PMSDKSkin s)
        {
            var content = s.GetComponent<PMSDKSkinContent>();
            var color = s.GetComponent<PMSDKColorCorrection>();
            var transform = s.GetComponent<PMSDKOutputTransform>();
            return new Skin
            {
                id = s.SkinId,
                name = s.DisplayName,
                shape = ShapeToString(s.Kind),
                warp = WarpForShape(s.Kind),
                vertices = new List<Vector2>(s.Vertices ?? new List<Vector2>()),
                mask = new MaskCfg { kind = MaskForShape(s.Kind), feather = s.SoftMask },
                transform = CaptureTransform(transform),
                color = CaptureColor(color),
                opacity = s.Opacity,
                softMask = s.SoftMask,
                zOrder = s.ZOrder,
                defaultContent = CaptureDefaultContent(content),
            };
        }

        /// <summary>Apply a captured <see cref="Set"/> back to the controller's scene.
        /// Skins with matching <see cref="PMSDKSkin.SkinId"/> are updated in place;
        /// unknown ids are spawned; scene skins whose id is not in the snapshot are
        /// destroyed.</summary>
        public static void Apply(PMSDKSkinEditorController controller, Set set)
        {
            if (controller == null || set == null) return;

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

        internal static void ApplyToSkin(PMSDKSkin s, Skin dto)
        {
            s.SkinId = dto.id;
            s.DisplayName = dto.name;
            s.Kind = StringToShape(dto.shape);
            s.Vertices = new List<Vector2>(dto.vertices);
            s.Opacity = dto.opacity;
            s.SoftMask = dto.softMask;
            s.ZOrder = dto.zOrder;
            ApplyDefaultContent(s, dto.defaultContent);
        }

        // -------- (de)serialize --------

        public static string Serialize(PMSDKSkinEditorController controller, bool prettyPrint = true) =>
            JsonUtility.ToJson(Capture(controller), prettyPrint);

        public static string Serialize(Set set, bool prettyPrint = true) =>
            JsonUtility.ToJson(set, prettyPrint);

        public static Set Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonUtility.FromJson<Set>(json);
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

        public static string ContentModeToString(PMSDKSkinContentMode m) => m.ToString();

        public static PMSDKSkinContentMode StringToContentMode(string s) =>
            System.Enum.TryParse<PMSDKSkinContentMode>(s ?? "None", true, out var v)
                ? v : PMSDKSkinContentMode.None;

        public static string TestPatternTypeToString(PMSDKTestPatternType t) => t.ToString();

        public static PMSDKTestPatternType StringToTestPatternType(string s) =>
            System.Enum.TryParse<PMSDKTestPatternType>(s ?? "Checkerboard", true, out var v)
                ? v : PMSDKTestPatternType.Checkerboard;

        // -------- private helpers --------

        private static string WarpForShape(PMSDKSkinShapeKind k) =>
            (k == PMSDKSkinShapeKind.Rectangle || k == PMSDKSkinShapeKind.Ellipse) ? "projective" : "polygon";

        private static string MaskForShape(PMSDKSkinShapeKind k) =>
            k == PMSDKSkinShapeKind.Ellipse ? "ellipse" : "none";

        private static TransformCfg CaptureTransform(PMSDKOutputTransform t)
        {
            if (t == null) return new TransformCfg();
            // PMSDKOutputTransform stores rotation as an enum (0/90/180/270) and mirrorX/Y.
            // The specific field names live in that component; we mirror them best-effort
            // via reflection-free explicit access — component may not exist on every skin,
            // and defaults are fine when absent.
            var cfg = new TransformCfg();
            // Skin editor doesn't currently attach PMSDKOutputTransform, so this stays default.
            // Kept in the schema so a future scene-level Output Transform survives round-trip.
            return cfg;
        }

        private static ColorCfg CaptureColor(PMSDKColorCorrection c)
        {
            if (c == null) return new ColorCfg();
            return new ColorCfg
            {
                gain   = new Vector3(c.Gain.r,   c.Gain.g,   c.Gain.b),
                offset = new Vector3(c.Offset.r, c.Offset.g, c.Offset.b),
                gamma  = c.OutputGamma,
            };
        }

        private static DefaultContent CaptureDefaultContent(PMSDKSkinContent content)
        {
            var dc = new DefaultContent();
            if (content == null) return dc;
            dc.mode = ContentModeToString(content.Mode);
            switch (content.Mode)
            {
                case PMSDKSkinContentMode.Texture:
                    dc.path = ResolveAssetPathOrEmpty(content.StaticTexture);
                    break;
                case PMSDKSkinContentMode.RenderTexture:
                    dc.path = ResolveAssetPathOrEmpty(content.ExternalRenderTexture);
                    break;
                case PMSDKSkinContentMode.VideoClip:
                    dc.path = ResolveAssetPathOrEmpty(content.VideoClip);
                    dc.loop = content.Loop;
                    dc.mute = content.Mute;
                    dc.startTime = content.StartTime;
                    break;
                case PMSDKSkinContentMode.TestPattern:
                    dc.testPatternType = TestPatternTypeToString(content.TestPatternType);
                    dc.testPatternSize = content.TestPatternSize;
                    dc.testPatternGridSize = content.TestPatternGridSize;
                    break;
            }
            return dc;
        }

        private static string ResolveAssetPathOrEmpty(UnityEngine.Object o)
        {
#if UNITY_EDITOR
            if (o == null) return "";
            return UnityEditor.AssetDatabase.GetAssetPath(o);
#else
            return o == null ? "" : o.name;
#endif
        }

        private static void ApplyDefaultContent(PMSDKSkin s, DefaultContent dc)
        {
            if (dc == null) return;
            var content = s.GetComponent<PMSDKSkinContent>();
            if (content == null) content = s.gameObject.AddComponent<PMSDKSkinContent>();

            var mode = StringToContentMode(dc.mode);
            content.Mode = mode;

            switch (mode)
            {
                case PMSDKSkinContentMode.Texture:
#if UNITY_EDITOR
                    if (!string.IsNullOrEmpty(dc.path))
                        content.StaticTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(dc.path);
#endif
                    break;
                case PMSDKSkinContentMode.RenderTexture:
#if UNITY_EDITOR
                    if (!string.IsNullOrEmpty(dc.path))
                        content.ExternalRenderTexture =
                            UnityEditor.AssetDatabase.LoadAssetAtPath<RenderTexture>(dc.path);
#endif
                    break;
                case PMSDKSkinContentMode.VideoClip:
#if UNITY_EDITOR
                    if (!string.IsNullOrEmpty(dc.path))
                        content.VideoClip =
                            UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(dc.path);
#endif
                    content.Loop = dc.loop;
                    content.Mute = dc.mute;
                    content.StartTime = dc.startTime;
                    break;
                case PMSDKSkinContentMode.TestPattern:
                    content.TestPatternType = StringToTestPatternType(dc.testPatternType);
                    content.TestPatternSize = Mathf.Max(16, dc.testPatternSize);
                    content.TestPatternGridSize = Mathf.Max(1, dc.testPatternGridSize);
                    break;
            }
        }
    }
}
