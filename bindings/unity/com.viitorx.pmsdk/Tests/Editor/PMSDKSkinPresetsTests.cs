using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode tests for the M-Sk4 v1.6 preset catalog. Validates every shipping
    /// preset's shape data + the apply flow (spawn N skins, z-order stack, replace
    /// vs add-alongside, id lookup).
    /// </summary>
    public class PMSDKSkinPresetsTests
    {
        private GameObject projGO;
        private GameObject ctrlGO;
        private PMSDKSkinEditorController controller;

        [SetUp]
        public void Setup()
        {
            projGO = new GameObject("Proj", typeof(Camera));
            projGO.AddComponent<PMSDKProjector>();

            ctrlGO = new GameObject("Ctrl");
            controller = ctrlGO.AddComponent<PMSDKSkinEditorController>();
            controller.Projector = projGO.GetComponent<PMSDKProjector>();
            controller.SkinLayerName = "TransparentFX";
            controller.enabled = false; controller.enabled = true;
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);
            if (ctrlGO != null) Object.DestroyImmediate(ctrlGO);
            if (projGO != null) Object.DestroyImmediate(projGO);
        }

        // ---- catalog integrity ----

        [Test]
        public void Catalog_ContainsExpectedCount()
        {
            // v1.6 = 10, v1.7 = +1 hexagon, v1.8 = +7 (honeycomb-7, silhouette,
            // arcade-2, staircase, corner-2, column-n, logo-wordmark) → 18.
            Assert.AreEqual(18, PMSDKSkinPresets.All.Length,
                "Catalog size drifted — update this assertion when adding/removing a preset.");
        }

        [Test]
        public void Catalog_IdsAreUnique()
        {
            var seen = new HashSet<string>();
            foreach (var p in PMSDKSkinPresets.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(p.Id), "Preset id must be non-empty.");
                Assert.IsTrue(seen.Add(p.Id), $"Duplicate preset id: '{p.Id}'.");
            }
        }

        [Test]
        public void Catalog_EveryPresetHasSkinsAndValidVertexCounts()
        {
            foreach (var p in PMSDKSkinPresets.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(p.DisplayName), $"Preset '{p.Id}' needs a display name.");
                Assert.IsNotNull(p.Skins, $"Preset '{p.Id}' Skins array is null.");
                Assert.Greater(p.Skins.Length, 0, $"Preset '{p.Id}' has no skins.");
                foreach (var s in p.Skins)
                {
                    Assert.IsNotNull(s.Vertices, $"Preset '{p.Id}', skin '{s.Name}' has null Vertices.");
                    int n = s.Vertices.Length;
                    switch (s.Kind)
                    {
                        case PMSDKSkinShapeKind.Rectangle:
                        case PMSDKSkinShapeKind.Ellipse:
                            Assert.AreEqual(4, n,
                                $"Preset '{p.Id}', skin '{s.Name}' ({s.Kind}) must have exactly 4 vertices; had {n}.");
                            break;
                        case PMSDKSkinShapeKind.Polygon:
                            Assert.GreaterOrEqual(n, 3,
                                $"Preset '{p.Id}', skin '{s.Name}' (Polygon) needs ≥3 vertices; had {n}.");
                            Assert.LessOrEqual(n, PMSDKSkinMeshBuilder.MaxPolygonVertices,
                                $"Preset '{p.Id}', skin '{s.Name}' (Polygon) exceeds cap.");
                            break;
                        case PMSDKSkinShapeKind.Freeform:
                            Assert.GreaterOrEqual(n, 3,
                                $"Preset '{p.Id}', skin '{s.Name}' (Freeform) needs ≥3 vertices; had {n}.");
                            Assert.LessOrEqual(n, PMSDKSkinMeshBuilder.MaxFreeformVertices,
                                $"Preset '{p.Id}', skin '{s.Name}' (Freeform) exceeds cap.");
                            break;
                    }
                    // Every vertex must be in raster range.
                    for (int i = 0; i < n; i++)
                    {
                        var v = s.Vertices[i];
                        Assert.That(v.x, Is.InRange(0f, 1f),
                            $"Preset '{p.Id}', skin '{s.Name}' vertex {i}.x = {v.x} outside raster [0..1].");
                        Assert.That(v.y, Is.InRange(0f, 1f),
                            $"Preset '{p.Id}', skin '{s.Name}' vertex {i}.y = {v.y} outside raster [0..1].");
                    }
                }
            }
        }

        [Test]
        public void TryFind_ReturnsPresetByIdOrFalse()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("box-iso-3", out var iso3));
            Assert.AreEqual(3, iso3.Skins.Length);
            Assert.IsFalse(PMSDKSkinPresets.TryFind("does-not-exist", out _));
        }

        [Test]
        public void DisplayNames_MatchAllLength()
        {
            var names = PMSDKSkinPresets.DisplayNames();
            Assert.AreEqual(PMSDKSkinPresets.All.Length, names.Length);
            for (int i = 0; i < names.Length; i++)
                Assert.IsFalse(string.IsNullOrEmpty(names[i]));
        }

        // ---- ApplyPreset flow ----

        [Test]
        public void ApplyPreset_Iso3_SpawnsThreeRectangleSkins()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("box-iso-3", out var preset));
            controller.ApplyPreset(preset);

            Assert.AreEqual(3, controller.Skins.Count);
            foreach (var s in controller.Skins)
            {
                Assert.AreEqual(PMSDKSkinShapeKind.Rectangle, s.Kind);
                Assert.AreEqual(4, s.Vertices.Count);
            }

            // Skin names come from the preset's SkinDef.Name entries.
            var names = new HashSet<string>();
            foreach (var s in controller.Skins) names.Add(s.DisplayName);
            Assert.IsTrue(names.Contains("front"));
            Assert.IsTrue(names.Contains("right"));
            Assert.IsTrue(names.Contains("top"));
        }

        [Test]
        public void ApplyPreset_ReplaceExisting_DefaultsToTrue()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.AddDefaultShape();
            Assert.AreEqual(2, controller.Skins.Count);

            Assert.IsTrue(PMSDKSkinPresets.TryFind("box-iso-3", out var preset));
            controller.ApplyPreset(preset); // default replaceExisting = true

            // Original two skins gone; three new ones from the preset.
            Assert.AreEqual(3, controller.Skins.Count);
        }

        [Test]
        public void ApplyPreset_AddAlongside_KeepsExistingSkins()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            Assert.AreEqual(1, controller.Skins.Count);

            Assert.IsTrue(PMSDKSkinPresets.TryFind("box-iso-3", out var preset));
            controller.ApplyPreset(preset, replaceExisting: false);

            Assert.AreEqual(4, controller.Skins.Count,
                "add-alongside should keep the existing skin AND add all preset skins.");
        }

        [Test]
        public void ApplyPreset_ZOrderStack_LastSpawnedOnTop()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("box-iso-3", out var preset));
            controller.ApplyPreset(preset);
            // RefreshSkinList sorts by ZOrder DESC so Skins[0] is topmost. We assigned
            // ZOrder = i (0..N-1) so the LAST spawned SkinDef ends up at index 0.
            Assert.AreEqual(preset.Skins[preset.Skins.Length - 1].Name, controller.Skins[0].DisplayName);
        }

        [Test]
        public void ApplyPresetById_UnknownId_ReturnsFalse()
        {
            Assert.IsFalse(controller.ApplyPresetById("does-not-exist"));
            Assert.AreEqual(0, controller.Skins.Count);
        }

        [Test]
        public void ApplyPreset_RecordsSingleUndoEntry()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("box-iso-3", out var preset));
            int before = controller.Undo.UndoDepth;
            controller.ApplyPreset(preset);
            // The preset records ONE undo entry; individual spawns don't push additional
            // per-skin records (they go through SpawnSkinFromSnapshot which doesn't Record).
            Assert.AreEqual(before + 1, controller.Undo.UndoDepth);
        }

        [Test]
        public void ApplyPreset_UndoRestoresPriorState()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var priorId = controller.Selected.SkinId;

            Assert.IsTrue(PMSDKSkinPresets.TryFind("box-iso-3", out var preset));
            controller.ApplyPreset(preset);
            Assert.AreEqual(3, controller.Skins.Count);

            controller.UndoLast();
            Assert.AreEqual(1, controller.Skins.Count);
            Assert.AreEqual(priorId, controller.Skins[0].SkinId,
                "Undo should restore the pre-preset skin (by id).");
        }

        // ---- shape-specific sanity ----

        [Test]
        public void Preset_Dome_HasEllipseSkin()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("dome", out var p));
            Assert.AreEqual(1, p.Skins.Length);
            Assert.AreEqual(PMSDKSkinShapeKind.Ellipse, p.Skins[0].Kind);
        }

        [Test]
        public void Preset_Star_HasTenVertices()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("star", out var p));
            Assert.AreEqual(1, p.Skins.Length);
            Assert.AreEqual(PMSDKSkinShapeKind.Polygon, p.Skins[0].Kind);
            Assert.AreEqual(10, p.Skins[0].Vertices.Length);
        }

        [Test]
        public void Preset_Facade3x2_HasSixRectangles()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("facade-3x2", out var p));
            Assert.AreEqual(6, p.Skins.Length);
            foreach (var s in p.Skins) Assert.AreEqual(PMSDKSkinShapeKind.Rectangle, s.Kind);
        }

        [Test]
        public void Preset_Arch_IsAPolygonWithSeveralVertices()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("arch", out var p));
            Assert.AreEqual(1, p.Skins.Length);
            Assert.AreEqual(PMSDKSkinShapeKind.Polygon, p.Skins[0].Kind);
            Assert.GreaterOrEqual(p.Skins[0].Vertices.Length, 7,
                "Arch should have enough vertices to trace a semi-circular top.");
        }

        [Test]
        public void Preset_Hexagon_IsPolygonWithSixVertices()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("hexagon", out var p));
            Assert.AreEqual(1, p.Skins.Length);
            Assert.AreEqual(PMSDKSkinShapeKind.Polygon, p.Skins[0].Kind);
            Assert.AreEqual(6, p.Skins[0].Vertices.Length,
                "A regular hexagon has exactly six vertices.");
        }

        [Test]
        public void Preset_Hexagon_HasRoughlyEqualEdgeLengths()
        {
            // Regular polygon sanity: consecutive edges within a tight tolerance.
            Assert.IsTrue(PMSDKSkinPresets.TryFind("hexagon", out var p));
            var v = p.Skins[0].Vertices;
            float e0 = Vector2.Distance(v[0], v[1]);
            for (int i = 1; i < v.Length; i++)
            {
                float ei = Vector2.Distance(v[i], v[(i + 1) % v.Length]);
                Assert.That(ei, Is.EqualTo(e0).Within(1e-3f),
                    $"Hexagon edge {i} length {ei} differs from edge 0 length {e0}.");
            }
        }

        [Test]
        public void Preset_Diorama_HasFrameAndStage()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("diorama", out var p));
            Assert.AreEqual(2, p.Skins.Length);
            Assert.AreEqual("frame", p.Skins[0].Name);
            Assert.AreEqual("stage", p.Skins[1].Name);
        }

        // ---- v1.8 per-preset sanity ----

        [Test]
        public void Preset_Honeycomb7_HasSevenHexagonPolygons()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("honeycomb-7", out var p));
            Assert.AreEqual(7, p.Skins.Length, "One center + six neighbors = seven cells.");
            foreach (var s in p.Skins)
            {
                Assert.AreEqual(PMSDKSkinShapeKind.Polygon, s.Kind);
                Assert.AreEqual(6, s.Vertices.Length, $"Cell '{s.Name}' should be a hexagon.");
            }
        }

        [Test]
        public void Preset_Honeycomb7_CellNamesCoverAllDirections()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("honeycomb-7", out var p));
            var names = new HashSet<string>();
            foreach (var s in p.Skins) names.Add(s.Name);
            foreach (var expected in new[] { "center", "e", "w", "ne", "nw", "se", "sw" })
                Assert.IsTrue(names.Contains(expected), $"Missing honeycomb cell '{expected}'.");
        }

        [Test]
        public void Preset_Silhouette_IsSinglePolygonWithinCap()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("silhouette", out var p));
            Assert.AreEqual(1, p.Skins.Length);
            Assert.AreEqual(PMSDKSkinShapeKind.Polygon, p.Skins[0].Kind);
            int n = p.Skins[0].Vertices.Length;
            Assert.GreaterOrEqual(n, 15, "Silhouette should have enough vertices to trace head+arms+legs.");
            Assert.LessOrEqual(n, PMSDKSkinMeshBuilder.MaxPolygonVertices,
                "Silhouette exceeded the polygon vertex cap.");
        }

        [Test]
        public void Preset_Arcade2_HasTwoArchPolygons()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("arcade-2", out var p));
            Assert.AreEqual(2, p.Skins.Length);
            foreach (var s in p.Skins)
            {
                Assert.AreEqual(PMSDKSkinShapeKind.Polygon, s.Kind);
                Assert.GreaterOrEqual(s.Vertices.Length, 7,
                    $"Arcade arch '{s.Name}' should trace a semicircular top.");
            }
            var names = new HashSet<string>();
            foreach (var s in p.Skins) names.Add(s.Name);
            Assert.IsTrue(names.Contains("arch-l"));
            Assert.IsTrue(names.Contains("arch-r"));
        }

        [Test]
        public void Preset_Staircase_HasFiveRectanglesStackedByY()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("staircase", out var p));
            Assert.AreEqual(5, p.Skins.Length);
            float prevBottom = -1f;
            for (int i = 0; i < p.Skins.Length; i++)
            {
                var s = p.Skins[i];
                Assert.AreEqual(PMSDKSkinShapeKind.Rectangle, s.Kind);
                // Rect() layout: y-min at index 0 and 1, y-max at 2 and 3.
                float yMin = Mathf.Min(s.Vertices[0].y, s.Vertices[1].y);
                Assert.Greater(yMin, prevBottom,
                    $"Step {i} does not sit above step {i - 1}.");
                prevBottom = yMin;
            }
        }

        [Test]
        public void Preset_Staircase_StepsNarrowGoingUp()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("staircase", out var p));
            float prevWidth = float.MaxValue;
            foreach (var s in p.Skins)
            {
                float xMin = Mathf.Min(s.Vertices[0].x, s.Vertices[3].x);
                float xMax = Mathf.Max(s.Vertices[1].x, s.Vertices[2].x);
                float w = xMax - xMin;
                Assert.Less(w, prevWidth,
                    $"Step '{s.Name}' width {w} is not narrower than previous width {prevWidth}.");
                prevWidth = w;
            }
        }

        [Test]
        public void Preset_BuildingCorner_TwoFacesShareCentralVerticalEdge()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("corner-2", out var p));
            Assert.AreEqual(2, p.Skins.Length);
            // Both should contain the vertices (0.50, 0.15) and (0.50, 0.85) — the
            // shared central edge that makes the "corner" wrap read as one continuous
            // surface once the user drags either face's outer corners.
            bool ContainsPoint(Vector2[] v, Vector2 target)
            {
                foreach (var pt in v)
                    if (Mathf.Abs(pt.x - target.x) < 1e-4f && Mathf.Abs(pt.y - target.y) < 1e-4f)
                        return true;
                return false;
            }
            foreach (var s in p.Skins)
            {
                Assert.AreEqual(PMSDKSkinShapeKind.Rectangle, s.Kind);
                Assert.IsTrue(ContainsPoint(s.Vertices, new Vector2(0.50f, 0.15f)),
                    $"Face '{s.Name}' missing shared bottom vertex (0.50, 0.15).");
                Assert.IsTrue(ContainsPoint(s.Vertices, new Vector2(0.50f, 0.85f)),
                    $"Face '{s.Name}' missing shared top vertex (0.50, 0.85).");
            }
        }

        [Test]
        public void Preset_ColumnN_CatalogEntryDefaultsToSixStrips()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("column-n", out var p));
            Assert.AreEqual(6, p.Skins.Length,
                "Catalog 'column-n' entry ships with N = 6 for one-click apply.");
            foreach (var s in p.Skins)
                Assert.AreEqual(PMSDKSkinShapeKind.Rectangle, s.Kind);
        }

        [Test]
        public void MakeColumnN_ProducesN_ContiguousStrips()
        {
            for (int n = 3; n <= 12; n++)
            {
                var p = PMSDKSkinPresets.MakeColumnN(n);
                Assert.AreEqual(n, p.Skins.Length, $"MakeColumnN({n}) should produce {n} strips.");
                // Consecutive strips share an x-edge.
                for (int i = 1; i < n; i++)
                {
                    float prevRight = Mathf.Max(p.Skins[i - 1].Vertices[1].x, p.Skins[i - 1].Vertices[2].x);
                    float currLeft  = Mathf.Min(p.Skins[i].Vertices[0].x,   p.Skins[i].Vertices[3].x);
                    Assert.That(currLeft, Is.EqualTo(prevRight).Within(1e-4f),
                        $"MakeColumnN({n}) strip {i} does not butt strip {i - 1}: {currLeft} vs {prevRight}.");
                }
            }
        }

        [Test]
        public void MakeColumnN_ClampsToRange3To12()
        {
            Assert.AreEqual(3, PMSDKSkinPresets.MakeColumnN(0).Skins.Length,
                "Below-range N clamps to 3.");
            Assert.AreEqual(12, PMSDKSkinPresets.MakeColumnN(999).Skins.Length,
                "Above-range N clamps to 12.");
        }

        [Test]
        public void Preset_LogoWordmark_HasFourRectangles()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("logo-wordmark", out var p));
            Assert.AreEqual(4, p.Skins.Length);
            foreach (var s in p.Skins) Assert.AreEqual(PMSDKSkinShapeKind.Rectangle, s.Kind);
        }

        // ---- geometry validity ----

        [Test]
        public void Preset_AllPolygonSkins_HavePositiveArea()
        {
            foreach (var p in PMSDKSkinPresets.All)
            {
                foreach (var s in p.Skins)
                {
                    if (s.Kind != PMSDKSkinShapeKind.Polygon) continue;
                    var list = new List<Vector2>(s.Vertices);
                    // Signed area magnitude > 0 → not degenerate.
                    float area = Mathf.Abs(PMSDKSkinMeshBuilder.SignedArea(list));
                    Assert.Greater(area, 1e-4f,
                        $"Preset '{p.Id}' polygon '{s.Name}' is degenerate (area {area}).");
                }
            }
        }
    }
}
