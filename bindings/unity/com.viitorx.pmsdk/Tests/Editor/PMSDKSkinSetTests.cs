using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode tests for the M-Sk2.5 surface:
    ///   - <see cref="PMSDKSkinSet"/> ScriptableObject captures a controller state
    ///     and round-trips through JSON.
    ///   - <see cref="PMSDKSkinLoader"/> spawns skins from a set + applies default content.
    ///   - <see cref="PMSDKSkinBindings"/> overrides default content per <c>SkinId</c>.
    /// </summary>
    public class PMSDKSkinSetTests
    {
        private GameObject projGO;
        private GameObject ctrlGO;
        private PMSDKSkinEditorController controller;

        [SetUp]
        public void Setup()
        {
            projGO = new GameObject("TestProjector", typeof(Camera));
            projGO.AddComponent<PMSDKProjector>();

            ctrlGO = new GameObject("SkinEditor");
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
            foreach (var l in Object.FindObjectsByType<PMSDKSkinLoader>(FindObjectsSortMode.None))
                Object.DestroyImmediate(l.gameObject);
            if (ctrlGO != null) Object.DestroyImmediate(ctrlGO);
            if (projGO != null) Object.DestroyImmediate(projGO);
        }

        // ---- PMSDKSkinSet: capture + JSON round-trip ----

        [Test]
        public void SkinSet_CapturesControllerState()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AddDefaultShape();

            var set = ScriptableObject.CreateInstance<PMSDKSkinSet>();
            try
            {
                set.CaptureFrom(controller);
                Assert.AreEqual(controller.Skins.Count, set.Data.skins.Count);
                // Order isn't stable at equal ZOrder — assert the set of shapes present.
                var shapes = new HashSet<PMSDKSkinShapeKind>();
                foreach (var s in set.Data.skins)
                    shapes.Add(PMSDKSkinSetJson.StringToShape(s.shape));
                Assert.IsTrue(shapes.Contains(PMSDKSkinShapeKind.Rectangle));
                Assert.IsTrue(shapes.Contains(PMSDKSkinShapeKind.Polygon));
            }
            finally { Object.DestroyImmediate(set); }
        }

        [Test]
        public void SkinSet_JsonRoundTrip_PreservesSkins()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var set = ScriptableObject.CreateInstance<PMSDKSkinSet>();
            try
            {
                set.CaptureFrom(controller);
                string json = set.ToJson();
                Assert.IsNotEmpty(json);

                var reloaded = ScriptableObject.CreateInstance<PMSDKSkinSet>();
                try
                {
                    reloaded.FromJson(json);
                    Assert.AreEqual(set.Data.skins.Count, reloaded.Data.skins.Count);
                    Assert.AreEqual(set.Data.skins[0].id, reloaded.Data.skins[0].id);
                    Assert.AreEqual(set.Data.skins[0].name, reloaded.Data.skins[0].name);
                }
                finally { Object.DestroyImmediate(reloaded); }
            }
            finally { Object.DestroyImmediate(set); }
        }

        [Test]
        public void SkinSet_SaveLoadJson_RoundTripsViaFilesystem()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Ellipse);
            controller.AddDefaultShape();
            var set = ScriptableObject.CreateInstance<PMSDKSkinSet>();
            var path = Path.Combine(Application.temporaryCachePath,
                "pmsdk_skinset_asset_test_" + System.Guid.NewGuid().ToString("N") + ".json");
            try
            {
                set.CaptureFrom(controller);
                Assert.IsTrue(set.SaveJsonTo(path));
                Assert.IsTrue(File.Exists(path));

                var reloaded = ScriptableObject.CreateInstance<PMSDKSkinSet>();
                try
                {
                    Assert.IsTrue(reloaded.LoadJsonFrom(path));
                    Assert.AreEqual(1, reloaded.Data.skins.Count);
                    Assert.AreEqual(set.Data.skins[0].id, reloaded.Data.skins[0].id);
                }
                finally { Object.DestroyImmediate(reloaded); }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                Object.DestroyImmediate(set);
            }
        }

        // ---- Schema extension: default content survives capture ----

        [Test]
        public void CaptureFrom_TestPatternMode_SerializesFields()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var s = controller.Selected;
            var content = s.GetComponent<PMSDKSkinContent>();
            content.Mode = PMSDKSkinContentMode.TestPattern;
            content.TestPatternType = PMSDKTestPatternType.ColorBars;
            content.TestPatternSize = 256;
            content.TestPatternGridSize = 16;

            var set = ScriptableObject.CreateInstance<PMSDKSkinSet>();
            try
            {
                set.CaptureFrom(controller);
                var dc = set.Data.skins[0].defaultContent;
                Assert.AreEqual("TestPattern", dc.mode);
                Assert.AreEqual("ColorBars", dc.testPatternType);
                Assert.AreEqual(256, dc.testPatternSize);
                Assert.AreEqual(16, dc.testPatternGridSize);
            }
            finally { Object.DestroyImmediate(set); }
        }

        // ---- PMSDKSkinLoader: spawn ----

        [Test]
        public void Loader_SpawnAll_CreatesOneSkinPerData()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AddDefaultShape();
            var set = ScriptableObject.CreateInstance<PMSDKSkinSet>();
            set.CaptureFrom(controller);

            // Wipe the "authored" skins to isolate the loader.
            foreach (var s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);
            controller.RefreshSkinList();
            Assert.AreEqual(0, controller.Skins.Count);

            // Spawn via loader on a new GameObject.
            var loaderGO = new GameObject("Loader");
            var loader = loaderGO.AddComponent<PMSDKSkinLoader>();
            loader.SkinSet = set;
            loader.Projector = controller.Projector;

            try
            {
                loader.SpawnAll();
                Assert.AreEqual(2, loader.Spawned.Count);
                // Reconciles on SkinId — spawned ids should match the set.
                var expectedIds = new List<string>();
                foreach (var d in set.Data.skins) expectedIds.Add(d.id);
                var actualIds = new List<string>();
                foreach (var s in loader.Spawned) actualIds.Add(s.SkinId);
                CollectionAssert.AreEquivalent(expectedIds, actualIds);
            }
            finally
            {
                Object.DestroyImmediate(loaderGO);
                Object.DestroyImmediate(set);
            }
        }

        [Test]
        public void Loader_DefaultContent_TestPatternApplied()
        {
            // Author a skin with a TestPattern default content, save, wipe, load.
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var content = controller.Selected.GetComponent<PMSDKSkinContent>();
            content.Mode = PMSDKSkinContentMode.TestPattern;
            content.TestPatternType = PMSDKTestPatternType.GrayRamp;

            var set = ScriptableObject.CreateInstance<PMSDKSkinSet>();
            set.CaptureFrom(controller);

            foreach (var s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);
            controller.RefreshSkinList();

            var loaderGO = new GameObject("Loader");
            var loader = loaderGO.AddComponent<PMSDKSkinLoader>();
            loader.SkinSet = set;
            loader.Projector = controller.Projector;
            try
            {
                loader.SpawnAll();
                Assert.AreEqual(1, loader.Spawned.Count);
                var spawnedContent = loader.Spawned[0].GetComponent<PMSDKSkinContent>();
                Assert.AreEqual(PMSDKSkinContentMode.TestPattern, spawnedContent.Mode);
                Assert.AreEqual(PMSDKTestPatternType.GrayRamp, spawnedContent.TestPatternType);
            }
            finally
            {
                Object.DestroyImmediate(loaderGO);
                Object.DestroyImmediate(set);
            }
        }

        [Test]
        public void Loader_SpawnAll_IsIdempotent()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var set = ScriptableObject.CreateInstance<PMSDKSkinSet>();
            set.CaptureFrom(controller);
            foreach (var s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);

            var loaderGO = new GameObject("Loader");
            var loader = loaderGO.AddComponent<PMSDKSkinLoader>();
            loader.SkinSet = set;
            loader.Projector = controller.Projector;
            try
            {
                loader.SpawnAll();
                loader.SpawnAll(); // Should NOT duplicate.
                Assert.AreEqual(1, loader.Spawned.Count);
            }
            finally
            {
                Object.DestroyImmediate(loaderGO);
                Object.DestroyImmediate(set);
            }
        }

        [Test]
        public void Loader_DespawnAll_ClearsSpawned()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var set = ScriptableObject.CreateInstance<PMSDKSkinSet>();
            set.CaptureFrom(controller);
            foreach (var s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);

            var loaderGO = new GameObject("Loader");
            var loader = loaderGO.AddComponent<PMSDKSkinLoader>();
            loader.SkinSet = set;
            loader.Projector = controller.Projector;
            try
            {
                loader.SpawnAll();
                Assert.AreEqual(1, loader.Spawned.Count);
                loader.DespawnAll();
                Assert.AreEqual(0, loader.Spawned.Count);
            }
            finally
            {
                Object.DestroyImmediate(loaderGO);
                Object.DestroyImmediate(set);
            }
        }

        [Test]
        public void Loader_OverrideJsonPath_TakesPrecedence()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var set = ScriptableObject.CreateInstance<PMSDKSkinSet>();
            set.CaptureFrom(controller);

            // Write a DIFFERENT set (two skins) to a temp JSON file.
            controller.PickTool(PMSDKSkinEditorController.Tool.Ellipse);
            controller.AddDefaultShape();
            var otherSet = PMSDKSkinSetJson.Capture(controller);
            var path = Path.Combine(Application.temporaryCachePath,
                "override_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, PMSDKSkinSetJson.Serialize(otherSet));

            foreach (var s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);

            var loaderGO = new GameObject("Loader");
            var loader = loaderGO.AddComponent<PMSDKSkinLoader>();
            loader.SkinSet = set;                 // 1 skin (rectangle only)
            loader.OverrideJsonPath = path;       // 2 skins (rect + ellipse) — should win
            loader.Projector = controller.Projector;
            try
            {
                loader.SpawnAll();
                Assert.AreEqual(2, loader.Spawned.Count,
                    "OverrideJsonPath should take precedence over the SkinSet asset.");
            }
            finally
            {
                Object.DestroyImmediate(loaderGO);
                Object.DestroyImmediate(set);
                if (File.Exists(path)) File.Delete(path);
            }
        }

        // ---- PMSDKSkinBindings ----

        [Test]
        public void Bindings_TryGet_MatchesById()
        {
            var go = new GameObject("Bindings");
            var b = go.AddComponent<PMSDKSkinBindings>();
            b.Overrides.Add(new PMSDKSkinBindings.Binding { SkinId = "front", Opacity = 0.75f });
            try
            {
                Assert.IsTrue(b.TryGet("front", out var got));
                Assert.AreEqual(0.75f, got.Opacity, 1e-5f);
                Assert.IsFalse(b.TryGet("nope", out _));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Bindings_Apply_OverridesTexture()
        {
            var tex = new Texture2D(2, 2);
            try
            {
                var go = new GameObject("Skin", typeof(MeshFilter), typeof(MeshRenderer));
                var content = go.AddComponent<PMSDKSkinContent>();
                var binding = new PMSDKSkinBindings.Binding
                {
                    SkinId = "x",
                    Mode = PMSDKSkinContentMode.Texture,
                    Texture = tex,
                };
                PMSDKSkinBindings.Apply(content, binding);
                Assert.AreEqual(PMSDKSkinContentMode.Texture, content.Mode);
                Assert.AreSame(tex, content.StaticTexture);
                Object.DestroyImmediate(go);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void Loader_BindingsOverride_BeatsDefaultContent()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var authoredContent = controller.Selected.GetComponent<PMSDKSkinContent>();
            authoredContent.Mode = PMSDKSkinContentMode.TestPattern;
            authoredContent.TestPatternType = PMSDKTestPatternType.Checkerboard;

            var set = ScriptableObject.CreateInstance<PMSDKSkinSet>();
            set.CaptureFrom(controller);
            var skinId = set.Data.skins[0].id;

            foreach (var s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);

            var loaderGO = new GameObject("Loader");
            var loader = loaderGO.AddComponent<PMSDKSkinLoader>();
            loader.SkinSet = set;
            loader.Projector = controller.Projector;

            var overrideRT = new RenderTexture(64, 64, 0);
            overrideRT.Create();
            var bindings = loaderGO.AddComponent<PMSDKSkinBindings>();
            bindings.Overrides.Add(new PMSDKSkinBindings.Binding
            {
                SkinId = skinId,
                Mode = PMSDKSkinContentMode.RenderTexture,
                RenderTexture = overrideRT,
            });
            try
            {
                loader.SpawnAll();
                var content = loader.Spawned[0].GetComponent<PMSDKSkinContent>();
                Assert.AreEqual(PMSDKSkinContentMode.RenderTexture, content.Mode,
                    "Bindings override should beat the SkinSet's TestPattern default.");
                Assert.AreSame(overrideRT, content.ExternalRenderTexture);
            }
            finally
            {
                Object.DestroyImmediate(loaderGO);
                Object.DestroyImmediate(set);
                if (overrideRT != null) { overrideRT.Release(); Object.DestroyImmediate(overrideRT); }
            }
        }
    }
}
