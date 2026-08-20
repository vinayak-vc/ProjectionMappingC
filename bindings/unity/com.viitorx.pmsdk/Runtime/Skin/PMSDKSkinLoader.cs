using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Instantiate a saved <see cref="PMSDKSkinSet"/> into a content scene at runtime.
    /// The "author once, use in any scene" side of the multi-skin feature (D-037): a
    /// tech-op paints the physical faces once via the Skin Editor scene, saves the SO,
    /// and any playback scene drops this loader onto its projector to get the exact
    /// same geometry.
    ///
    /// Precedence for content per-skin:
    ///   1. <see cref="PMSDKSkinBindings"/> on the same GameObject (scene-level override).
    ///   2. The <c>defaultContent</c> field on the SkinSet's <c>Skin</c> entry.
    ///   3. Nothing (skin renders white until something else sets its texture).
    ///
    /// Load path selection (evaluated at <see cref="SpawnAll"/>):
    ///   1. <see cref="OverrideJsonPath"/> if set (matches the <c>-calibfile</c> CLI pattern).
    ///   2. <see cref="SkinSet"/>'s sidecar JSON if <see cref="ReloadFromSidecar"/> is true and the file exists.
    ///   3. <see cref="SkinSet"/>'s in-memory <c>Data</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public class PMSDKSkinLoader : MonoBehaviour
    {
        [Header("Set")]
        public PMSDKSkinSet SkinSet;
        [Tooltip("If true, at Start() the loader reads the SkinSet's JSON sidecar (if it " +
                 "exists) into the SkinSet's Data before spawning. False = use the SO's " +
                 "in-memory Data as-is (Unity-only workflow).")]
        public bool ReloadFromSidecar = false;

        [Header("Runtime override")]
        [Tooltip("Optional absolute path to a JSON file. If set (either on the component " +
                 "or via -skinset <path> at startup), takes precedence over SkinSet.")]
        public string OverrideJsonPath;

        [Header("Scene refs")]
        [Tooltip("Projector to bind spawned skins to. Auto-detected via PMSDKProjector on " +
                 "the same GameObject or a parent if null.")]
        public PMSDKProjector Projector;

        [Tooltip("Parent transform for spawned skins. Defaults to this transform's parent " +
                 "so spawned skins sit next to the projector (matching the standard rig).")]
        public Transform SkinsParent;

        [Tooltip("Layer for spawned skins. Falls back to TransparentFX.")]
        public string SkinLayerName = "TransparentFX";

        [Tooltip("Also spawn in edit mode (via ExecuteAlways). Useful for previewing a set " +
                 "in the scene without hitting Play.")]
        public bool SpawnInEditMode = false;

        // Public read-only list of spawned skins (populated on SpawnAll, cleared on DespawnAll).
        public IReadOnlyList<PMSDKSkin> Spawned => spawned;
        private readonly List<PMSDKSkin> spawned = new List<PMSDKSkin>();

        // Track whether SpawnAll actually ran, so we can avoid double-spawn on re-enable.
        private bool hasSpawned;

        // ---- lifecycle ----

        private void Awake()
        {
            if (Projector == null) Projector = GetComponentInParent<PMSDKProjector>();
        }

        private void Start()
        {
            if (hasSpawned) return;
            SpawnAll();
        }

        private void OnEnable()
        {
            if (SpawnInEditMode && !Application.isPlaying && !hasSpawned) SpawnAll();
        }

        private void OnDestroy()
        {
            // Owned skins are auto-cleaned by scene unload; but if the loader is
            // destroyed at runtime we should tidy up ours.
            if (hasSpawned) DespawnAll();
        }

        // ---- CLI parse: -skinset <path> ----

        private static string s_cachedCliPath;
        private static bool s_cliParsed;
        private static string GetCliOverridePath()
        {
            if (s_cliParsed) return s_cachedCliPath;
            s_cliParsed = true;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-skinset") { s_cachedCliPath = args[i + 1]; break; }
            return s_cachedCliPath;
        }

        // ---- spawn / despawn ----

        /// <summary>Spawn one <see cref="PMSDKSkin"/> per entry in the resolved set.
        /// Safe to call multiple times — clears prior spawn first.</summary>
        public void SpawnAll()
        {
            DespawnAll();

            var set = ResolveSet();
            if (set == null)
            {
                Debug.LogWarning($"PMSDKSkinLoader on '{name}': no skin set to load.", this);
                return;
            }

            // Reconcile: sort by z-order so lower-z spawn first (rendered behind).
            var ordered = new List<PMSDKSkinSetJson.Skin>(set.skins);
            ordered.Sort((a, b) => a.zOrder.CompareTo(b.zOrder));

            var bindings = GetComponent<PMSDKSkinBindings>();
            foreach (var data in ordered)
            {
                var skin = SpawnOne(data);
                if (skin == null) continue;
                spawned.Add(skin);

                var content = skin.GetComponent<PMSDKSkinContent>();
                if (content == null) content = skin.gameObject.AddComponent<PMSDKSkinContent>();

                // Bindings (scene-level) override the set's defaultContent.
                if (bindings != null && bindings.TryGet(skin.SkinId, out var b))
                {
                    PMSDKSkinBindings.Apply(content, b);
                    skin.Opacity = b.Opacity;
                }
                else
                {
                    PMSDKSkinSetJsonExt.ApplyDefaultContent(skin, content, data.defaultContent);
                }
            }

            hasSpawned = true;
        }

        /// <summary>Tear down every skin this loader spawned.</summary>
        public void DespawnAll()
        {
            foreach (var s in spawned)
            {
                if (s == null) continue;
                if (Application.isEditor && !Application.isPlaying) DestroyImmediate(s.gameObject);
                else Destroy(s.gameObject);
            }
            spawned.Clear();
            hasSpawned = false;
        }

        // ---- set resolution ----

        /// <summary>Resolve which <see cref="PMSDKSkinSetJson.Set"/> to spawn. Follows
        /// the precedence documented on the class.</summary>
        public PMSDKSkinSetJson.Set ResolveSet()
        {
            // 1. Runtime CLI or field override.
            var cliPath = GetCliOverridePath();
            var overridePath = !string.IsNullOrEmpty(cliPath) ? cliPath : OverrideJsonPath;
            if (!string.IsNullOrEmpty(overridePath) && System.IO.File.Exists(overridePath))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(overridePath);
                    var set = PMSDKSkinSetJson.Deserialize(json);
                    if (set != null) return set;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"PMSDKSkinLoader: override path failed — {ex.Message}");
                }
            }

            if (SkinSet == null) return null;
            if (ReloadFromSidecar) SkinSet.ReloadFromSidecar();
            return SkinSet.Data;
        }

        // ---- spawn one skin ----

        private PMSDKSkin SpawnOne(PMSDKSkinSetJson.Skin data)
        {
            int layer = LayerMask.NameToLayer(SkinLayerName);
            if (layer < 0) layer = 0;

            Transform parent = SkinsParent != null ? SkinsParent
                : (Projector != null ? Projector.transform.parent : transform.parent);

            var kind = PMSDKSkinSetJson.StringToShape(data.shape);
            var go = new GameObject($"Skin_{data.name ?? kind.ToString()}",
                typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = layer;
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(16f / 9f, 1f, 1f);

            var shader = Shader.Find("PMSDK/UnlitWarp");
            if (shader != null)
                go.GetComponent<MeshRenderer>().sharedMaterial =
                    new Material(shader) { name = $"{go.name}_Material" };

            var skin = go.AddComponent<PMSDKSkin>();
            skin.Kind = kind;
            skin.SkinId = string.IsNullOrEmpty(data.id)
                ? System.Guid.NewGuid().ToString("N").Substring(0, 8) : data.id;
            skin.DisplayName = string.IsNullOrEmpty(data.name) ? go.name : data.name;
            skin.Vertices = new List<Vector2>(data.vertices ?? new List<Vector2>());
            skin.Opacity = data.opacity;
            skin.SoftMask = data.softMask;
            skin.ZOrder = data.zOrder;
            skin.Projector = Projector;

            bool isQuad = kind == PMSDKSkinShapeKind.Rectangle || kind == PMSDKSkinShapeKind.Ellipse;
            if (isQuad)
            {
                var mw = go.AddComponent<PMSDKMeshWarp>();
                mw.Projector = Projector;
                go.AddComponent<PMSDKCornerPin>();
            }
            // PMSDKSkinContent is added by SpawnAll after this returns, so it can wire
            // the binding / default content in one place.
            return skin;
        }
    }

    /// <summary>
    /// Helpers that need <c>internal</c> access to <see cref="PMSDKSkinSetJson"/>'s
    /// private ApplyDefaultContent path. Kept in this file so the loader is one
    /// consumer surface; the JSON module keeps its private helpers private otherwise.
    /// </summary>
    internal static class PMSDKSkinSetJsonExt
    {
        /// <summary>Apply the SkinSet's <c>defaultContent</c> onto a skin's <see cref="PMSDKSkinContent"/>.</summary>
        public static void ApplyDefaultContent(PMSDKSkin skin, PMSDKSkinContent content,
            PMSDKSkinSetJson.DefaultContent dc)
        {
            if (skin == null || content == null || dc == null) return;
            var mode = PMSDKSkinSetJson.StringToContentMode(dc.mode);
            content.Mode = mode;
            switch (mode)
            {
                case PMSDKSkinContentMode.Texture:
                    content.StaticTexture = LoadTextureByPath(dc.path);
                    break;
                case PMSDKSkinContentMode.RenderTexture:
                    content.ExternalRenderTexture = LoadRTByPath(dc.path);
                    break;
                case PMSDKSkinContentMode.VideoClip:
                    content.VideoClip = LoadVideoByPath(dc.path);
                    content.Loop = dc.loop;
                    content.Mute = dc.mute;
                    content.StartTime = dc.startTime;
                    break;
                case PMSDKSkinContentMode.VideoUrl:
                    content.VideoUrl = dc.url;
                    content.Loop = dc.loop;
                    content.Mute = dc.mute;
                    content.StartTime = dc.startTime;
                    break;
                case PMSDKSkinContentMode.Webcam:
                    content.WebcamDeviceName = dc.webcamDeviceName ?? "";
                    break;
                case PMSDKSkinContentMode.Camera:
                case PMSDKSkinContentMode.ExternalTexture:
                    // Scene-ref modes — user or a scene-side script provides the ref.
                    break;
                case PMSDKSkinContentMode.TestPattern:
                    content.TestPatternType = PMSDKSkinSetJson.StringToTestPatternType(dc.testPatternType);
                    content.TestPatternSize = Mathf.Max(16, dc.testPatternSize);
                    content.TestPatternGridSize = Mathf.Max(1, dc.testPatternGridSize);
                    break;
            }
        }

        private static Texture LoadTextureByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture>(path);
#else
            // Runtime: Resources.Load if the path starts with "Resources/", else give up.
            const string prefix = "Resources/";
            int idx = path.LastIndexOf(prefix, System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var rel = path.Substring(idx + prefix.Length);
                rel = System.IO.Path.ChangeExtension(rel, null);
                return Resources.Load<Texture>(rel);
            }
            return null;
#endif
        }

        private static RenderTexture LoadRTByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
#else
            return null; // no Resources fallback for RTs — usually scene-provided
#endif
        }

        private static UnityEngine.Video.VideoClip LoadVideoByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(path);
#else
            const string prefix = "Resources/";
            int idx = path.LastIndexOf(prefix, System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var rel = path.Substring(idx + prefix.Length);
                rel = System.IO.Path.ChangeExtension(rel, null);
                return Resources.Load<UnityEngine.Video.VideoClip>(rel);
            }
            return null;
#endif
        }
    }
}
