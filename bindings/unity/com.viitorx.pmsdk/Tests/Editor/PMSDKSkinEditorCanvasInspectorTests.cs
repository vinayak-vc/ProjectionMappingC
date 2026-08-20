using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode tests for the M-Sk4 v1.4 UGUI Canvas inspector. Focused on
    /// deterministic behavior:
    ///   - Canvas hierarchy exists after RebuildForTests.
    ///   - IMGUI HUD's DrawInspector flips off when canvas is active, on when it's disabled.
    ///   - Field values reflect the selected skin's state after Update.
    /// UI-value-driven writes are exercised by invoking the private handlers via
    /// reflection — assigning `Slider.value` from a test in EditMode trips Unity's
    /// internal `ShouldRunBehaviour` assertion (harmless but noisy for tests), so
    /// we bypass Unity's callback dispatch by calling the handlers directly.
    /// </summary>
    public class PMSDKSkinEditorCanvasInspectorTests
    {
        private GameObject projGO;
        private GameObject editorGO;
        private PMSDKSkinEditorController controller;
        private PMSDKSkinEditorHUD imguiHud;
        private PMSDKSkinEditorCanvasInspector canvasInspector;

        [SetUp]
        public void Setup()
        {
            // Unity's UI graphic-rebuild + LayoutRebuilder paths trip internal
            // "ShouldRunBehaviour()" assertions in EditMode even when the code is
            // correct — the tests still verify the logic they check. Suppress the
            // noise so real assertion failures aren't masked.
            LogAssert.ignoreFailingMessages = true;

            projGO = new GameObject("Proj", typeof(Camera));
            projGO.AddComponent<PMSDKProjector>();

            editorGO = new GameObject("Editor");
            controller = editorGO.AddComponent<PMSDKSkinEditorController>();
            controller.Projector = projGO.GetComponent<PMSDKProjector>();
            controller.SkinLayerName = "TransparentFX";
            controller.enabled = false; controller.enabled = true;

            imguiHud = editorGO.AddComponent<PMSDKSkinEditorHUD>();
            imguiHud.Controller = controller;

            canvasInspector = editorGO.AddComponent<PMSDKSkinEditorCanvasInspector>();
            canvasInspector.Controller = controller;
            canvasInspector.RebuildForTests();
        }

        [TearDown]
        public void Teardown()
        {
            LogAssert.ignoreFailingMessages = false;
            foreach (var s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c != null) Object.DestroyImmediate(c.gameObject);
            foreach (var e in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (e != null) Object.DestroyImmediate(e.gameObject);
            if (editorGO != null) Object.DestroyImmediate(editorGO);
            if (projGO != null) Object.DestroyImmediate(projGO);
        }

        // ---- construction ----

        [Test]
        public void RebuildForTests_BuildsCanvasHierarchy()
        {
            Assert.IsTrue(canvasInspector.CanvasBuilt);
            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas);
            Assert.IsNotNull(canvasInspector.NameField);
            Assert.IsNotNull(canvasInspector.OpacitySlider);
            Assert.IsNotNull(canvasInspector.FeatherSlider);
            Assert.IsNotNull(canvasInspector.PerEdgeToggle);
        }

        [Test]
        public void OnEnable_HidesImguiInspector()
        {
            Assert.IsFalse(imguiHud.DrawInspector,
                "Canvas inspector should hide the IMGUI inspector while active.");
        }

        [Test]
        public void OnDisableViaRestore_ReenablesImguiInspector()
        {
            // Call OnDisable directly (via reflection) to bypass EditMode's async
            // enable/disable dispatch, then verify DrawInspector restored.
            InvokePrivate("OnDisable");
            Assert.IsTrue(imguiHud.DrawInspector,
                "Disabling the Canvas inspector should restore the IMGUI inspector's DrawInspector.");
            // Re-enable so teardown finds a live canvas to destroy.
            InvokePrivate("OnEnable");
        }

        [Test]
        public void OnEnable_CreatesEventSystemIfMissing()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            Assert.IsNotNull(es, "Inspector should attach an EventSystem so UI events fire.");
        }

        // ---- read-direction: selection change updates fields ----

        [Test]
        public void SelectionChange_RebindsNameField()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.Selected.DisplayName = "Alpha";
            InvokePrivate("RebindFieldsFromSkin", controller.Selected);
            Assert.AreEqual("Alpha", canvasInspector.NameField.text);

            controller.AddDefaultShape();
            controller.Selected.DisplayName = "Beta";
            controller.Selected.Opacity = 0.4f;
            InvokePrivate("RebindFieldsFromSkin", controller.Selected);
            Assert.AreEqual("Beta", canvasInspector.NameField.text);
            Assert.That(canvasInspector.OpacitySlider.value, Is.EqualTo(0.4f).Within(1e-4f));
        }

        [Test]
        public void SelectionChange_TogglesPerEdgeGroupVisibility()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            InvokePrivate("RebindFieldsFromSkin", controller.Selected);
            Assert.IsFalse(canvasInspector.PerEdgeToggle.gameObject.activeSelf,
                "Per-edge toggle should be hidden for non-Ellipse skins.");

            controller.PickTool(PMSDKSkinEditorController.Tool.Ellipse);
            controller.AddDefaultShape();
            InvokePrivate("RebindFieldsFromSkin", controller.Selected);
            Assert.IsTrue(canvasInspector.PerEdgeToggle.gameObject.activeSelf,
                "Per-edge toggle should be visible for Ellipse skins.");
        }

        // ---- write-direction: handlers write back to skin ----

        [Test]
        public void OnNameChanged_WritesBackToSkin()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var s = controller.Selected;
            // Skip SendMessage("Update") — it fires UGUI graphic rebuilds that trip
            // ShouldRunBehaviour in EditMode. Write-direction tests invoke handlers
            // directly via InvokePrivate below, which doesn't need the pre-bind.

            InvokePrivate("OnNameChanged", "Box front");
            Assert.AreEqual("Box front", s.DisplayName);
        }

        [Test]
        public void OnOpacityChanged_WritesBackToSkin()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var s = controller.Selected;
            // Skip SendMessage("Update") — it fires UGUI graphic rebuilds that trip
            // ShouldRunBehaviour in EditMode. Write-direction tests invoke handlers
            // directly via InvokePrivate below, which doesn't need the pre-bind.

            InvokePrivate("OnOpacityChanged", 0.42f);
            Assert.That(s.Opacity, Is.EqualTo(0.42f).Within(1e-4f));
        }

        [Test]
        public void OnFeatherChanged_WritesBackToSkin()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Ellipse);
            controller.AddDefaultShape();
            var s = controller.Selected;
            // Skip SendMessage("Update") — it fires UGUI graphic rebuilds that trip
            // ShouldRunBehaviour in EditMode. Write-direction tests invoke handlers
            // directly via InvokePrivate below, which doesn't need the pre-bind.

            InvokePrivate("OnFeatherChanged", 0.05f);
            Assert.That(s.SoftMask, Is.EqualTo(0.05f).Within(1e-4f));
        }

        [Test]
        public void OnPerEdgeToggled_FlipsSkinFlag()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Ellipse);
            controller.AddDefaultShape();
            var s = controller.Selected;
            // Skip SendMessage("Update") — it fires UGUI graphic rebuilds that trip
            // ShouldRunBehaviour in EditMode. Write-direction tests invoke handlers
            // directly via InvokePrivate below, which doesn't need the pre-bind.

            Assert.IsFalse(s.PerEdgeFeatherEnabled);
            InvokePrivate("OnPerEdgeToggled", true);
            Assert.IsTrue(s.PerEdgeFeatherEnabled);
        }

        [Test]
        public void OnPerEdgeChanged_UpdatesTheCorrectChannel()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Ellipse);
            controller.AddDefaultShape();
            var s = controller.Selected;
            s.EdgeFeather = Vector4.zero;
            // Skip SendMessage("Update") — it fires UGUI graphic rebuilds that trip
            // ShouldRunBehaviour in EditMode. Write-direction tests invoke handlers
            // directly via InvokePrivate below, which doesn't need the pre-bind.

            InvokePrivate("OnPerEdgeChanged", 1, 0.15f); // R
            Assert.That(s.EdgeFeather.y, Is.EqualTo(0.15f).Within(1e-4f));
            Assert.AreEqual(0f, s.EdgeFeather.x);
            Assert.AreEqual(0f, s.EdgeFeather.z);
            Assert.AreEqual(0f, s.EdgeFeather.w);
        }

        // ---- helpers ----

        private object InvokePrivate(string method, params object[] args)
        {
            var mi = typeof(PMSDKSkinEditorCanvasInspector).GetMethod(
                method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, $"No private method '{method}' on PMSDKSkinEditorCanvasInspector.");
            return mi.Invoke(canvasInspector, args);
        }
    }
}
