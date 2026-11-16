using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>Shape kind of a <see cref="PMSDKSkin"/>. Determines the underlying warp path.</summary>
    public enum PMSDKSkinShapeKind
    {
        /// <summary>4-corner projective quad (uses <see cref="PMSDKCornerPin"/>; homography).</summary>
        Rectangle = 0,
        /// <summary>4-corner projective quad with a circular alpha mask cut in the shader.</summary>
        Ellipse = 1,
        /// <summary>N-vertex polygon (up to <see cref="PMSDKSkinMeshBuilder.MaxPolygonVertices"/>).
        /// Vertex positions ARE the raster-space geometry; no native warp.</summary>
        Polygon = 2,
        /// <summary>Same as Polygon but with a higher vertex cap
        /// (<see cref="PMSDKSkinMeshBuilder.MaxFreeformVertices"/>).</summary>
        Freeform = 3,
    }

    /// <summary>
    /// One "skin" on one projector — an independent content region drawn on the projector's
    /// raster output. Sits alongside (or replaces) the classic single split-slice warp
    /// surface. Multiple skins under one projector let a single projector paint N independent
    /// physical faces (box front / top / right, signage cutouts, ...).
    ///
    /// **Shape kind picks the underlying warp path** (D-036):
    /// - Rectangle / Ellipse → this component requires <see cref="PMSDKMeshWarp"/> +
    ///   <see cref="PMSDKCornerPin"/>; the mesh is a unit-quad and the native
    ///   <c>PerspectiveWarp</c> maps that quad to <see cref="Vertices"/>. Ellipse additionally
    ///   sets <c>_ShapeMask=1</c> so the shader cuts a circular alpha mask in UV space.
    /// - Polygon / Freeform → this component owns a regenerated Unity mesh with vertices AT
    ///   <see cref="Vertices"/> in raster space. No native warp path (the geometry IS the
    ///   shape). PMSDKMeshWarp/PMSDKCornerPin, if present, are auto-disabled.
    ///
    /// Coordinate convention: <see cref="Vertices"/> and all vertex data are normalized
    /// raster-space [0..1] with y-UP (matching <see cref="PMSDKCornerPin"/>'s corner fields
    /// and Unity's world convention). JSON persistence (M-Sk2.5) will use the same
    /// convention for round-trip fidelity.
    ///
    /// Transform contract: the GameObject lives in raster space with identity rotation and
    /// <c>localScale = (aspect, 1, 1)</c>, matching the existing warp-surface convention (see
    /// <c>docs/unity-architecture.md §2</c>). The auto-configurator (Add Skin menu) sets this
    /// up; a manual placement should follow the same rig.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class PMSDKSkin : MonoBehaviour
    {
        /// <summary>Persistent skin identifier used by JSON/loader/bindings (M-Sk2.5).</summary>
        public string SkinId = "skin";

        /// <summary>Human-readable name shown in the HUD (M-Sk2).</summary>
        public string DisplayName = "Skin";

        /// <summary>The projector this skin belongs to. Required for Rectangle/Ellipse skins
        /// (drives the native warp); optional but recommended for polygon skins (used for
        /// layer assignment and skin identification).</summary>
        public PMSDKProjector Projector;

        [Space]
        public PMSDKSkinShapeKind Kind = PMSDKSkinShapeKind.Rectangle;

        /// <summary>Raster-space vertices in [0..1], y-UP.
        /// - Rectangle/Ellipse: exactly 4 in order BL, BR, TR, TL.
        /// - Polygon/Freeform: 3..N in CCW order (CW inputs are normalized in the mesh builder).
        /// </summary>
        public List<Vector2> Vertices = new List<Vector2>
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f),
        };

        [Space]
        [Range(0f, 1f)] public float Opacity = 1f;

        [Range(0f, 0.5f)] public float SoftMask = 0f;

        [Tooltip("M-Sk4 polish: use PMSDKSkin.EdgeFeather (per-edge Top/Right/Bottom/Left) " +
                 "instead of the scalar SoftMask. Applies only to Ellipse skins (the " +
                 "only shape with a shader mask).")]
        public bool PerEdgeFeatherEnabled = false;

        [Tooltip("Per-edge feather in raster/UV units: (Top, Right, Bottom, Left). Ignored " +
                 "unless PerEdgeFeatherEnabled and the skin is an Ellipse.")]
        public Vector4 EdgeFeather = Vector4.zero;

        /// <summary>Higher z-order draws in front. Implemented as a small localPosition.z
        /// offset so it composes with the projector's ortho camera without touching the
        /// renderer's sorting order. Range is clamped to keep skins within the camera's
        /// near/far, but 0..1000 is realistic.</summary>
        public int ZOrder = 0;

        /// <summary>Optional test-pattern override. When non-null, the material samples this
        /// texture instead of the content source. Owned externally (usually by <see cref="PMSDKSkinContent"/>).</summary>
        [HideInInspector] public Texture TestPatternOverride;

        // ---- runtime state ----

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private PMSDKMeshWarp meshWarp;
        private PMSDKCornerPin cornerPin;
        private PMSDKSkinContent content;
        private MaterialPropertyBlock mpb;
        // Cached vertex signature so polygon mesh regen only runs when Vertices actually
        // changed — a per-frame regen would allocate a new Vector3[]/int[] every frame.
        private int lastVerticesHash;
        private PMSDKSkinShapeKind lastAppliedKind = (PMSDKSkinShapeKind)(-1);
        private Mesh ownedMesh;

        private static readonly int ID_ShapeMask    = Shader.PropertyToID("_ShapeMask");
        private static readonly int ID_MaskFeather  = Shader.PropertyToID("_MaskFeather");
        private static readonly int ID_MaskFeather4 = Shader.PropertyToID("_MaskFeather4");
        private static readonly int ID_Opacity      = Shader.PropertyToID("_Opacity");
        private static readonly int ID_MainTex      = Shader.PropertyToID("_MainTex");

        private void OnEnable()
        {
            CacheRefs();
            EnsureMeshForKind(force: true);
            ApplyZOrder();
            SyncQuadDeformation();
            PushMaterialProperties();
        }

        private void OnDisable()
        {
            // The owned mesh is a per-instance clone; destroy it to avoid leaks when the
            // skin GameObject is removed. If we're the assigned sharedMesh, clear the slot.
            if (ownedMesh != null)
            {
                if (meshFilter != null && meshFilter.sharedMesh == ownedMesh) meshFilter.sharedMesh = null;
                if (Application.isEditor && !Application.isPlaying) DestroyImmediate(ownedMesh);
                else Destroy(ownedMesh);
                ownedMesh = null;
                lastAppliedKind = (PMSDKSkinShapeKind)(-1);
            }
        }

        private void OnValidate()
        {
            // Clamp vertex counts to the per-kind caps documented in the design doc.
            if (Vertices == null) Vertices = new List<Vector2>();
            int cap = (Kind == PMSDKSkinShapeKind.Freeform)
                ? PMSDKSkinMeshBuilder.MaxFreeformVertices
                : (Kind == PMSDKSkinShapeKind.Polygon ? PMSDKSkinMeshBuilder.MaxPolygonVertices : 4);
            if (Vertices.Count > cap)
            {
                Debug.LogWarning($"PMSDKSkin '{name}': vertex count {Vertices.Count} exceeds cap {cap} for {Kind}; truncating.", this);
                Vertices.RemoveRange(cap, Vertices.Count - cap);
            }
            // Rectangle/Ellipse require exactly 4 vertices — pad from unit-quad defaults if
            // the user under-specified rather than crashing later.
            if ((Kind == PMSDKSkinShapeKind.Rectangle || Kind == PMSDKSkinShapeKind.Ellipse) && Vertices.Count != 4)
            {
                var defaults = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                while (Vertices.Count < 4) Vertices.Add(defaults[Vertices.Count]);
                if (Vertices.Count > 4) Vertices.RemoveRange(4, Vertices.Count - 4);
            }
        }

        private void Update()
        {
            // Re-cache when EITHER the mesh filter is missing OR we're a quad kind
            // and the corner-pin cache is null. The second condition covers callers
            // (e.g. PMSDKSkinLoader in earlier versions) that add PMSDKSkin BEFORE
            // adding PMSDKCornerPin — OnEnable's cache lands null, and without this
            // check SyncQuadDeformation would never push corners.
            // PMSDKSkinContent is usually added AFTER PMSDKSkin, so also re-cache while it is
            // missing — otherwise polygon/freeform skins never push their content texture
            // (they render white) because they have no corner pin to trigger the re-cache.
            if (meshFilter == null || content == null || (IsQuadKind && cornerPin == null)) CacheRefs();
            EnsureMeshForKind(force: false);
            ApplyZOrder();
            SyncQuadDeformation();
            PushMaterialProperties();
        }

        // ---- helpers ----

        private void CacheRefs()
        {
            meshFilter   = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshWarp     = GetComponent<PMSDKMeshWarp>();
            cornerPin    = GetComponent<PMSDKCornerPin>();
            content      = GetComponent<PMSDKSkinContent>();
            if (mpb == null) mpb = new MaterialPropertyBlock();
        }

        private bool IsQuadKind => Kind == PMSDKSkinShapeKind.Rectangle || Kind == PMSDKSkinShapeKind.Ellipse;

        /// <summary>Rebuild the MeshFilter's mesh when Kind or Vertices change. For quad kinds
        /// the mesh is a fixed unit-quad (only rebuilt on kind change); for polygon kinds the
        /// mesh vertices track <see cref="Vertices"/>.</summary>
        private void EnsureMeshForKind(bool force)
        {
            if (meshFilter == null) return;

            bool kindChanged = force || Kind != lastAppliedKind;
            int currentHash = ComputeVerticesHash();
            bool verticesChanged = currentHash != lastVerticesHash;

            if (IsQuadKind)
            {
                if (kindChanged || ownedMesh == null)
                {
                    ReplaceOwnedMesh(PMSDKSkinMeshBuilder.BuildUnitQuadMesh());
                    lastAppliedKind = Kind;
                    lastVerticesHash = currentHash;
                }
                // Enable native warp components — Rectangle/Ellipse both drive PerspectiveWarp
                // through PMSDKCornerPin.
                if (meshWarp != null && !meshWarp.enabled) meshWarp.enabled = true;
                if (cornerPin != null && !cornerPin.enabled) cornerPin.enabled = true;
            }
            else
            {
                if (kindChanged || verticesChanged || ownedMesh == null)
                {
                    ReplaceOwnedMesh(PMSDKSkinMeshBuilder.BuildPolygonMesh(Vertices));
                    lastAppliedKind = Kind;
                    lastVerticesHash = currentHash;
                }
                // Polygon skins ARE the geometry — the native warp path would overwrite
                // our vertex positions. Disable it (and CornerPin) if present.
                if (meshWarp != null && meshWarp.enabled) meshWarp.enabled = false;
                if (cornerPin != null && cornerPin.enabled) cornerPin.enabled = false;
            }
        }

        private void ReplaceOwnedMesh(Mesh next)
        {
            if (ownedMesh != null)
            {
                if (Application.isEditor && !Application.isPlaying) DestroyImmediate(ownedMesh);
                else Destroy(ownedMesh);
            }
            ownedMesh = next;
            meshFilter.sharedMesh = ownedMesh;
        }

        /// <summary>For quad kinds, push <see cref="Vertices"/>[0..3] into the corner-pin
        /// fields so the native PerspectiveWarp gets the current corners.</summary>
        private void SyncQuadDeformation()
        {
            if (!IsQuadKind || cornerPin == null || Vertices == null || Vertices.Count < 4) return;
            cornerPin.BottomLeft  = Vertices[0];
            cornerPin.BottomRight = Vertices[1];
            cornerPin.TopRight    = Vertices[2];
            cornerPin.TopLeft     = Vertices[3];
        }

        private void ApplyZOrder()
        {
            // Slightly forward = higher zOrder = drawn in front. 0.0005 per rank keeps 1000
            // ranks inside a 1-unit z-range which fits inside the projector camera's near/far.
            const float ZStep = 0.0005f;
            var lp = transform.localPosition;
            float target = -ZOrder * ZStep;
            if (!Mathf.Approximately(lp.z, target))
            {
                lp.z = target;
                transform.localPosition = lp;
            }
        }

        private void PushMaterialProperties()
        {
            if (meshRenderer == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ID_ShapeMask,   Kind == PMSDKSkinShapeKind.Ellipse ? 1f : 0f);
            mpb.SetFloat(ID_MaskFeather, Mathf.Max(SoftMask, 0f));
            // M-Sk4 polish v1.2: push per-edge feather. Default (PerEdgeFeatherEnabled
            // = false) tiles the scalar SoftMask across all four channels so the shader
            // behaves identically to the M-Sk1 scalar path.
            Vector4 f4 = PerEdgeFeatherEnabled
                ? new Vector4(Mathf.Max(EdgeFeather.x, 0f), Mathf.Max(EdgeFeather.y, 0f),
                              Mathf.Max(EdgeFeather.z, 0f), Mathf.Max(EdgeFeather.w, 0f))
                : new Vector4(SoftMask, SoftMask, SoftMask, SoftMask);
            mpb.SetVector(ID_MaskFeather4, f4);
            mpb.SetFloat(ID_Opacity,     Mathf.Clamp01(Opacity));

            // Content texture: test-pattern override > PMSDKSkinContent.CurrentTexture > leave
            // whatever the material already has (typically "white" default from shader).
            Texture tex = TestPatternOverride;
            if (tex == null && content != null) tex = content.CurrentTexture;
            if (tex != null) mpb.SetTexture(ID_MainTex, tex);

            meshRenderer.SetPropertyBlock(mpb);
        }

        private int ComputeVerticesHash()
        {
            // Cheap change-detector: xor of quantized float bits. Not cryptographic — just
            // enough to skip mesh regen when nothing changed. Quantize to 1e-4 raster units
            // (finer than one 1080p pixel) so tiny FP noise doesn't force rebuilds.
            if (Vertices == null || Vertices.Count == 0) return 0;
            unchecked
            {
                int h = 17 + (int)Kind * 31;
                for (int i = 0; i < Vertices.Count; i++)
                {
                    int qx = Mathf.RoundToInt(Vertices[i].x * 10000f);
                    int qy = Mathf.RoundToInt(Vertices[i].y * 10000f);
                    h = h * 31 + qx;
                    h = h * 31 + qy;
                }
                return h;
            }
        }
    }
}
