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
            Assert.AreEqual(10, PMSDKSkinPresets.All.Length,
                "Ten iconic presets are the shipping baseline.");
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
        public void Preset_Diorama_HasFrameAndStage()
        {
            Assert.IsTrue(PMSDKSkinPresets.TryFind("diorama", out var p));
            Assert.AreEqual(2, p.Skins.Length);
            Assert.AreEqual("frame", p.Skins[0].Name);
            Assert.AreEqual("stage", p.Skins[1].Name);
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
