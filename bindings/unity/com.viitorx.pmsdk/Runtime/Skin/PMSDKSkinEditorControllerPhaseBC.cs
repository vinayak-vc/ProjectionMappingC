using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Phase B (drawing polish + editor UI actions) + Phase C (undo/redo, JSON
    /// save/load) additions to <see cref="PMSDKSkinEditorController"/>. Kept in a
    /// partial file so Phase A's core state machine (pointer flow, tool switching,
    /// draw-rectangle) stays cleanly isolated and reviewable.
    /// </summary>
    public partial class PMSDKSkinEditorController
    {
        // ---- Phase B state ----

        /// <summary>The polygon skin currently being built via click-to-draw (P tool).
        /// Non-null between the first click and Close/Cancel/Enter.</summary>
        private PMSDKSkin drawingPolygonSkin;

        /// <summary>The freeform skin currently being traced via drag (F tool).
        /// Non-null between OnPointerDown and OnPointerUp while the F tool is active.</summary>
        private PMSDKSkin drawingFreeformSkin;

        /// <summary>Minimum distance (raster units) between successive freeform points.
        /// Prevents thousands of samples on a slow drag.</summary>
        public float FreeformSampleSpacing = 0.008f;

        /// <summary>Threshold (raster units) at which clicking near the first polygon
        /// vertex closes the shape (Illustrator-style).</summary>
        public float PolygonCloseThreshold = 0.02f;

        /// <summary>Renderers muted by <see cref="ToggleSoloSelected"/>. Solo = mute
        /// everyone but the selected skin. Cleared by <see cref="UnmuteAll"/>.</summary>
        private readonly HashSet<PMSDKSkin> mutedSkins = new HashSet<PMSDKSkin>();

        // ---- Phase C state ----

        /// <summary>Undo/redo stack. Cleared on scene reload; not persisted across
        /// domain reloads (matches F2 calibration semantics).</summary>
        public PMSDKSkinEditorUndo Undo { get; } = new PMSDKSkinEditorUndo();

        /// <summary>Where <see cref="SaveDefault"/> writes; <see cref="LoadDefault"/>
        /// reads. Sits under <see cref="Application.persistentDataPath"/>.</summary>
        public string DefaultSavePath =>
            Path.Combine(Application.persistentDataPath, "pmsdk_skinset_default.json");

        // -------- Phase B: polygon click-to-draw --------

        /// <summary>
        /// Add a vertex to the polygon-in-progress at <paramref name="raster"/>. The first
        /// call creates the polygon skin with a single vertex; subsequent calls append.
        /// If the click is within <see cref="PolygonCloseThreshold"/> of the first
        /// vertex AND we already have ≥3 verts, the polygon closes.
        /// </summary>
        public void AppendPolygonVertex(Vector2 raster)
        {
            raster = PMSDKSkinEditorGeometry.ClampToRaster(raster);
            if (drawingPolygonSkin == null)
            {
                Undo.Record(this, "poly-start");
                drawingPolygonSkin = SpawnSkinFromSnapshot(
                    PMSDKSkinShapeKind.Polygon,
                    new List<Vector2> { raster },
                    System.Guid.NewGuid().ToString("N").Substring(0, 8),
                    "Skin_Polygon");
                RefreshSkinList();
                SelectedSkinIndex = Skins.IndexOf(drawingPolygonSkin);
                UpdateStatus();
                return;
            }

            // Close on click near start (≥3 verts) — Illustrator-style pen close.
            if (drawingPolygonSkin.Vertices.Count >= 3
                && Vector2.Distance(raster, drawingPolygonSkin.Vertices[0]) < PolygonCloseThreshold)
            {
                ClosePolygon();
                return;
            }

            // Enforce the cap so we can't blow past MaxPolygonVertices from click spam.
            if (drawingPolygonSkin.Vertices.Count >= PMSDKSkinMeshBuilder.MaxPolygonVertices)
            {
                Debug.LogWarning($"PMSDKSkinEditor: polygon vertex cap "
                    + $"({PMSDKSkinMeshBuilder.MaxPolygonVertices}) reached — click Enter or near start to close.");
                return;
            }

            drawingPolygonSkin.Vertices.Add(raster);
            SelectedCornerIndex = drawingPolygonSkin.Vertices.Count - 1;
            UpdateStatus();
        }

        /// <summary>Finish the polygon-in-progress. If it has &lt;3 vertices, cancels instead.</summary>
        public void ClosePolygon()
        {
            if (drawingPolygonSkin == null) return;
            if (drawingPolygonSkin.Vertices.Count < 3)
            {
                CancelPolygonDraw();
                return;
            }
            drawingPolygonSkin = null;
            UpdateStatus();
        }

        /// <summary>Abort an in-progress polygon; deletes the transient skin.</summary>
        public void CancelPolygonDraw()
        {
            if (drawingPolygonSkin == null) return;
            var s = drawingPolygonSkin;
            drawingPolygonSkin = null;
            if (Application.isEditor && !Application.isPlaying) DestroyImmediate(s.gameObject);
            else Destroy(s.gameObject);
            RefreshSkinList();
            UpdateStatus();
        }

        public bool IsDrawingPolygon => drawingPolygonSkin != null;

        // -------- Phase B: freeform stroke ------------------------------------

        public void BeginFreeformStroke(Vector2 raster)
        {
            raster = PMSDKSkinEditorGeometry.ClampToRaster(raster);
            Undo.Record(this, "freeform-start");
            drawingFreeformSkin = SpawnSkinFromSnapshot(
                PMSDKSkinShapeKind.Freeform,
                new List<Vector2> { raster },
                System.Guid.NewGuid().ToString("N").Substring(0, 8),
                "Skin_Freeform");
            RefreshSkinList();
            SelectedSkinIndex = Skins.IndexOf(drawingFreeformSkin);
            UpdateStatus();
        }

        public void ContinueFreeformStroke(Vector2 raster)
        {
            if (drawingFreeformSkin == null) return;
            raster = PMSDKSkinEditorGeometry.ClampToRaster(raster);
            var verts = drawingFreeformSkin.Vertices;
            if (verts.Count == 0
                || Vector2.Distance(verts[verts.Count - 1], raster) >= FreeformSampleSpacing)
            {
                if (verts.Count >= PMSDKSkinMeshBuilder.MaxFreeformVertices) return;
                verts.Add(raster);
            }
        }

        public void EndFreeformStroke()
        {
            if (drawingFreeformSkin == null) return;
            // Freeform strokes shorter than 3 pts are unusable — cancel.
            if (drawingFreeformSkin.Vertices.Count < 3)
            {
                var s = drawingFreeformSkin;
                drawingFreeformSkin = null;
                if (Application.isEditor && !Application.isPlaying) DestroyImmediate(s.gameObject);
                else Destroy(s.gameObject);
                RefreshSkinList();
            }
            drawingFreeformSkin = null;
            UpdateStatus();
        }

        // -------- Phase B: insert/remove polygon vertex --------

        /// <summary>Insert a new vertex on the edge running from the selected corner to
        /// the next corner, at the edge midpoint. No-op if the shape can't accept more
        /// verts (rectangle/ellipse are fixed at 4).</summary>
        public void InsertVertexOnSelectedEdge()
        {
            var s = Selected;
            if (s == null || s.Vertices == null || s.Vertices.Count < 2) return;
            if (s.Kind != PMSDKSkinShapeKind.Polygon && s.Kind != PMSDKSkinShapeKind.Freeform) return;
            int cap = s.Kind == PMSDKSkinShapeKind.Freeform
                ? PMSDKSkinMeshBuilder.MaxFreeformVertices
                : PMSDKSkinMeshBuilder.MaxPolygonVertices;
            if (s.Vertices.Count >= cap) return;

            Undo.Record(this, "vertex-insert");
            int a = SelectedCornerIndex;
            int b = (a + 1) % s.Vertices.Count;
            Vector2 mid = 0.5f * (s.Vertices[a] + s.Vertices[b]);
            s.Vertices.Insert(a + 1, mid);
            SelectedCornerIndex = a + 1;
            UpdateStatus();
        }

        /// <summary>Remove the selected corner. No-op if the shape would drop below its
        /// minimum vertex count (3 for polygon/freeform; rectangle/ellipse are fixed).</summary>
        public void RemoveSelectedVertex()
        {
            var s = Selected;
            if (s == null || s.Vertices == null) return;
            if (s.Kind != PMSDKSkinShapeKind.Polygon && s.Kind != PMSDKSkinShapeKind.Freeform) return;
            if (s.Vertices.Count <= 3) return;
            if (SelectedCornerIndex < 0 || SelectedCornerIndex >= s.Vertices.Count) return;

            Undo.Record(this, "vertex-remove");
            s.Vertices.RemoveAt(SelectedCornerIndex);
            SelectedCornerIndex = Mathf.Clamp(SelectedCornerIndex, 0, s.Vertices.Count - 1);
            UpdateStatus();
        }

        // -------- Phase B: duplicate + z-order --------

        /// <summary>Duplicate the selected skin. New skin is offset by (+0.02, +0.02)
        /// so it's visible next to the original; original's <see cref="PMSDKSkin.ZOrder"/> is
        /// preserved on the clone (M-Sk4 will add "send to front on duplicate" as an option).</summary>
        public void DuplicateSelected()
        {
            var s = Selected;
            if (s == null) return;
            Undo.Record(this, "duplicate");
            var copyVerts = new List<Vector2>(s.Vertices.Count);
            for (int i = 0; i < s.Vertices.Count; i++)
            {
                copyVerts.Add(PMSDKSkinEditorGeometry.ClampToRaster(
                    s.Vertices[i] + new Vector2(0.02f, 0.02f)));
            }
            var clone = SpawnSkinFromSnapshot(s.Kind, copyVerts,
                System.Guid.NewGuid().ToString("N").Substring(0, 8),
                s.DisplayName + "_copy");
            if (clone != null)
            {
                clone.Opacity = s.Opacity;
                clone.SoftMask = s.SoftMask;
                clone.ZOrder = s.ZOrder;
                RefreshSkinList();
                SelectedSkinIndex = Skins.IndexOf(clone);
                UpdateStatus();
            }
        }

        /// <summary>Shift the selected skin's <see cref="PMSDKSkin.ZOrder"/> by
        /// <paramref name="direction"/> (+1 forward, -1 backward). Refreshes the skin
        /// list so cycling matches the new stacking order.</summary>
        public void MoveSelectedZOrder(int direction)
        {
            var s = Selected;
            if (s == null || direction == 0) return;
            Undo.Record(this, "zorder");
            s.ZOrder += (int)Mathf.Sign(direction);
            RefreshSkinList();
            SelectedSkinIndex = Skins.IndexOf(s);
            UpdateStatus();
        }

        // -------- Phase B: solo / mute --------

        /// <summary>Solo the selected skin — disables every other skin's MeshRenderer.
        /// If the selected skin was ALREADY the only visible one, this instead unmutes
        /// everyone (i.e. `X` toggles the solo state).</summary>
        public void ToggleSoloSelected()
        {
            var s = Selected;
            if (s == null) return;
            Undo.Record(this, "solo");

            // If the current mute set == "everyone but selected", treat X as unmute-all.
            bool alreadySoloed = mutedSkins.Count > 0;
            foreach (var m in mutedSkins) if (m == s) { alreadySoloed = false; break; }
            if (alreadySoloed)
            {
                bool exactSolo = true;
                foreach (var other in Skins)
                {
                    if (other == s) continue;
                    if (!mutedSkins.Contains(other)) { exactSolo = false; break; }
                }
                if (exactSolo) { UnmuteAll(); return; }
            }

            mutedSkins.Clear();
            foreach (var other in Skins)
            {
                if (other == s || other == null) continue;
                mutedSkins.Add(other);
                var mr = other.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }
            var selfMR = s.GetComponent<MeshRenderer>();
            if (selfMR != null) selfMR.enabled = true;
            UpdateStatus();
        }

        /// <summary>Re-enable every skin's MeshRenderer. Bound to Shift+X.</summary>
        public void UnmuteAll()
        {
            Undo.Record(this, "unmute-all");
            foreach (var m in mutedSkins)
            {
                if (m == null) continue;
                var mr = m.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = true;
            }
            mutedSkins.Clear();
            UpdateStatus();
        }

        public bool IsMuted(PMSDKSkin s) => s != null && mutedSkins.Contains(s);

        // -------- Phase B: test-pattern cycle --------

        /// <summary>Cycle the selected skin's <see cref="PMSDKSkinContent.TestPatternType"/>
        /// by <paramref name="direction"/>. Switches Mode to TestPattern if it wasn't already.
        /// The pattern set comes from <see cref="PMSDKTestPatternType"/>.</summary>
        public void CycleTestPatternSelected(int direction)
        {
            var s = Selected;
            if (s == null) return;
            var content = s.GetComponent<PMSDKSkinContent>();
            if (content == null) content = s.gameObject.AddComponent<PMSDKSkinContent>();
            Undo.Record(this, "pattern-cycle");

            var values = (PMSDKTestPatternType[])System.Enum.GetValues(typeof(PMSDKTestPatternType));
            int idx = System.Array.IndexOf(values, content.TestPatternType);
            if (idx < 0) idx = 0;
            idx = (idx + direction + values.Length) % values.Length;
            content.TestPatternType = values[idx];
            content.Mode = PMSDKSkinContentMode.TestPattern;
            UpdateStatus();
        }

        // -------- Phase C: save / load --------

        /// <summary>Write the current skin set to a JSON file. Overwrites atomically
        /// (writes to `<path>.tmp` then moves) so a mid-write crash can't corrupt the
        /// on-disk state.</summary>
        public bool SaveTo(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, PMSDKSkinSetJson.Serialize(this, prettyPrint: true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                Debug.Log($"PMSDKSkinEditor: saved skin set to {path}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PMSDKSkinEditor: save failed — {ex.Message}");
                return false;
            }
        }

        /// <summary>Load a JSON file and apply it to the scene. Existing skins are
        /// reconciled by <see cref="PMSDKSkin.SkinId"/> — matching ids are updated
        /// in-place, others are spawned, and scene skins missing from the JSON are
        /// destroyed. Selection is restored.</summary>
        public bool LoadFrom(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"PMSDKSkinEditor: no skin set at {path}");
                    return false;
                }
                var json = File.ReadAllText(path);
                var set = PMSDKSkinSetJson.Deserialize(json);
                if (set == null)
                {
                    Debug.LogError("PMSDKSkinEditor: skin set JSON was empty or invalid.");
                    return false;
                }
                Undo.Record(this, "load");
                PMSDKSkinSetJson.Apply(this, set);
                UpdateStatus();
                Debug.Log($"PMSDKSkinEditor: loaded skin set from {path} ({set.skins.Count} skins).");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"PMSDKSkinEditor: load failed — {ex.Message}");
                return false;
            }
        }

        public bool SaveDefault() => SaveTo(DefaultSavePath);
        public bool LoadDefault() => LoadFrom(DefaultSavePath);

        // -------- Phase C: undo/redo --------

        public bool UndoLast() => Undo.Undo(this);
        public bool RedoLast() => Undo.Redo(this);

        // -------- shared spawn used by draw + duplicate + JSON restore --------

        /// <summary>
        /// Spawn a fully-configured skin GameObject with the given id/name/vertices. Used
        /// by all "create a new skin" paths (add-default, drag-to-draw, polygon click,
        /// freeform stroke, duplicate, JSON restore). Public so <see cref="PMSDKSkinSetJson.Apply"/>
        /// can call it during undo/load.
        /// </summary>
        public PMSDKSkin SpawnSkinFromSnapshot(PMSDKSkinShapeKind kind, List<Vector2> vertices,
            string skinId, string displayName)
        {
            int layer = LayerMask.NameToLayer(SkinLayerName);
            if (layer < 0) layer = 0;

            Transform parent = SkinsParent;
            if (parent == null && Projector != null) parent = Projector.transform.parent;

            var go = new GameObject($"Skin_{kind}", typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = layer;
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(16f / 9f, 1f, 1f);

            var mr = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("PMSDK/UnlitWarp");
            if (shader != null)
                mr.sharedMaterial = new Material(shader) { name = $"{go.name}_Material" };

            var skin = go.AddComponent<PMSDKSkin>();
            skin.Kind = kind;
            skin.SkinId = string.IsNullOrEmpty(skinId)
                ? System.Guid.NewGuid().ToString("N").Substring(0, 8)
                : skinId;
            skin.DisplayName = string.IsNullOrEmpty(displayName) ? go.name : displayName;
            skin.Vertices = vertices ?? new List<Vector2>();
            skin.Projector = Projector;

            bool isQuad = kind == PMSDKSkinShapeKind.Rectangle || kind == PMSDKSkinShapeKind.Ellipse;
            if (isQuad)
            {
                var mw = go.AddComponent<PMSDKMeshWarp>();
                mw.Projector = Projector;
                go.AddComponent<PMSDKCornerPin>();
            }
            go.AddComponent<PMSDKSkinContent>();

            RefreshSkinList();
            return skin;
        }
    }
}
