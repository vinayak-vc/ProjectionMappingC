using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode integration tests for <see cref="PMSDKSkinEditorController"/>. Drives the
    /// pointer state machine directly (no <see cref="PMSDKSkinEditorInput"/> in the loop),
    /// so the drawing flow is testable without Play mode.
    /// </summary>
    public class PMSDKSkinEditorControllerTests
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
            // Nudge OnEnable to run RefreshSkinList in a stable state.
            controller.enabled = false; controller.enabled = true;
        }

        [TearDown]
        public void Teardown()
        {
            // Destroy any skins the controller spawned during the test.
            foreach (var s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None))
                Object.DestroyImmediate(s.gameObject);
            if (ctrlGO != null) Object.DestroyImmediate(ctrlGO);
            if (projGO != null) Object.DestroyImmediate(projGO);
        }

        // ---- tool switching ----

        [Test]
        public void PickTool_UpdatesCurrentTool()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            Assert.AreEqual(PMSDKSkinEditorController.Tool.Rectangle, controller.CurrentTool);
            controller.PickTool(PMSDKSkinEditorController.Tool.Ellipse);
            Assert.AreEqual(PMSDKSkinEditorController.Tool.Ellipse, controller.CurrentTool);
        }

        // ---- add-default (N key) ----

        [Test]
        public void AddDefaultShape_Rectangle_CreatesSkinAtCanvasCenter()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();

            Assert.AreEqual(1, controller.Skins.Count);
            var s = controller.Selected;
            Assert.IsNotNull(s);
            Assert.AreEqual(PMSDKSkinShapeKind.Rectangle, s.Kind);
            Assert.AreEqual(4, s.Vertices.Count);
            // Center-ish: bounding-box center within ~0.02 of (0.5, 0.5).
            var b = PMSDKSkinEditorGeometry.Bounds(s.Vertices);
            Assert.That(b.center.x, Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(b.center.y, Is.EqualTo(0.5f).Within(0.02f));
        }

        [Test]
        public void AddDefaultShape_Polygon_CreatesHexagon()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AddDefaultShape();
            Assert.AreEqual(PMSDKSkinShapeKind.Polygon, controller.Selected.Kind);
            Assert.AreEqual(6, controller.Selected.Vertices.Count);
        }

        // ---- draw-rectangle drag flow ----

        [Test]
        public void DrawRectangle_DragCreatesNewSkin()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.OnPointerDown(new Vector2(0.2f, 0.3f));
            // Move past MinDrawDistance (default 0.01) to trigger commit-to-transient.
            controller.OnPointerDrag(new Vector2(0.8f, 0.7f));
            controller.OnPointerUp(new Vector2(0.8f, 0.7f));

            Assert.AreEqual(1, controller.Skins.Count);
            var s = controller.Selected;
            Assert.AreEqual(PMSDKSkinShapeKind.Rectangle, s.Kind);
            Assert.That(s.Vertices[0].x, Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(s.Vertices[2].y, Is.EqualTo(0.7f).Within(1e-4f));
        }

        [Test]
        public void DrawRectangle_ClickWithoutDrag_DoesNotCreate()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.OnPointerDown(new Vector2(0.5f, 0.5f));
            // Nudge by less than MinDrawDistance (0.01).
            controller.OnPointerDrag(new Vector2(0.503f, 0.501f));
            controller.OnPointerUp(new Vector2(0.503f, 0.501f));
            Assert.AreEqual(0, controller.Skins.Count);
        }

        [Test]
        public void DrawEllipse_UsesEllipseKind()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Ellipse);
            controller.OnPointerDown(new Vector2(0.1f, 0.1f));
            controller.OnPointerDrag(new Vector2(0.4f, 0.4f));
            controller.OnPointerUp(new Vector2(0.4f, 0.4f));
            Assert.AreEqual(PMSDKSkinShapeKind.Ellipse, controller.Selected.Kind);
        }

        // ---- corner drag ----

        [Test]
        public void DragCorner_MovesThatVertex()
        {
            // Seed a rectangle skin first.
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.OnPointerDown(new Vector2(0.2f, 0.2f));
            controller.OnPointerDrag(new Vector2(0.8f, 0.8f));
            controller.OnPointerUp(new Vector2(0.8f, 0.8f));
            var s = controller.Selected;
            Assert.IsNotNull(s);

            // Grab corner 0 (BL) at (0.2, 0.2) and drag to (0.15, 0.25).
            controller.PickTool(PMSDKSkinEditorController.Tool.Select);
            controller.OnPointerDown(new Vector2(0.201f, 0.199f));
            controller.OnPointerDrag(new Vector2(0.15f, 0.25f));
            controller.OnPointerUp(new Vector2(0.15f, 0.25f));

            Assert.That(s.Vertices[0].x, Is.EqualTo(0.15f).Within(1e-4f));
            Assert.That(s.Vertices[0].y, Is.EqualTo(0.25f).Within(1e-4f));
            // Other corners must not have moved.
            Assert.That(s.Vertices[2].x, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(s.Vertices[2].y, Is.EqualTo(0.8f).Within(1e-4f));
        }

        // ---- edge midpoint drag ----

        [Test]
        public void DragEdgeMidpoint_MovesBothAdjacentCorners()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.OnPointerDown(new Vector2(0.2f, 0.2f));
            controller.OnPointerDrag(new Vector2(0.8f, 0.8f));
            controller.OnPointerUp(new Vector2(0.8f, 0.8f));
            var s = controller.Selected;

            // Edge 0 (BL→BR) midpoint is (0.5, 0.2). Grab it and drag down by 0.1.
            controller.PickTool(PMSDKSkinEditorController.Tool.Select);
            controller.OnPointerDown(new Vector2(0.5f, 0.205f));
            controller.OnPointerDrag(new Vector2(0.5f, 0.1f));
            controller.OnPointerUp(new Vector2(0.5f, 0.1f));

            // Both corners on that edge should have moved by roughly the drag delta.
            Assert.That(s.Vertices[0].y, Is.EqualTo(0.095f).Within(0.02f));
            Assert.That(s.Vertices[1].y, Is.EqualTo(0.095f).Within(0.02f));
            // TR/TL untouched.
            Assert.That(s.Vertices[2].y, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(s.Vertices[3].y, Is.EqualTo(0.8f).Within(1e-4f));
        }

        // ---- select-by-click ----

        [Test]
        public void ClickInside_SelectsSkin()
        {
            // Two skins with different bounding rects.
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.OnPointerDown(new Vector2(0.1f, 0.1f));
            controller.OnPointerDrag(new Vector2(0.4f, 0.4f));
            controller.OnPointerUp(new Vector2(0.4f, 0.4f));
            controller.OnPointerDown(new Vector2(0.6f, 0.6f));
            controller.OnPointerDrag(new Vector2(0.9f, 0.9f));
            controller.OnPointerUp(new Vector2(0.9f, 0.9f));

            Assert.AreEqual(2, controller.Skins.Count);
            int startIdx = controller.SelectedSkinIndex;

            // Click inside the OTHER skin via the select tool.
            controller.PickTool(PMSDKSkinEditorController.Tool.Select);
            controller.OnPointerDown(new Vector2(0.25f, 0.25f));
            controller.OnPointerUp(new Vector2(0.25f, 0.25f));

            Assert.AreNotEqual(startIdx, controller.SelectedSkinIndex,
                "Clicking inside a different skin must select it.");
        }

        // ---- delete + cycle ----

        [Test]
        public void DeleteSelected_RemovesSkin()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            Assert.AreEqual(1, controller.Skins.Count);
            controller.DeleteSelected();
            Assert.AreEqual(0, controller.Skins.Count);
            Assert.IsNull(controller.Selected);
        }

        [Test]
        public void CycleSkin_WrapsAround()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.AddDefaultShape();
            controller.AddDefaultShape();

            int start = controller.SelectedSkinIndex;
            controller.CycleSkin(+1);
            Assert.AreNotEqual(start, controller.SelectedSkinIndex);
            controller.CycleSkin(+1);
            controller.CycleSkin(+1);
            Assert.AreEqual(start, controller.SelectedSkinIndex, "Full cycle must return to start.");
        }

        [Test]
        public void CycleCorner_WrapsInsideSelectedSkin()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AddDefaultShape(); // hexagon
            controller.SelectedCornerIndex = 0;
            controller.CycleCorner(+1);
            Assert.AreEqual(1, controller.SelectedCornerIndex);
            controller.CycleCorner(+5);
            Assert.AreEqual(0, controller.SelectedCornerIndex, "6-vertex hex should wrap on +6.");
        }

        // ---- nudge ----

        [Test]
        public void NudgeSelectedCorner_UpdatesVertexAndClamps()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var s = controller.Selected;
            controller.SelectedCornerIndex = 0;
            var before = s.Vertices[0];
            controller.NudgeSelectedCorner(new Vector2(0.05f, 0.0f));
            Assert.That(s.Vertices[0].x, Is.EqualTo(before.x + 0.05f).Within(1e-5f));

            // Huge nudge must clamp to [0,1].
            controller.NudgeSelectedCorner(new Vector2(-10f, -10f));
            Assert.That(s.Vertices[0].x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(s.Vertices[0].y, Is.EqualTo(0f).Within(1e-5f));
        }
    }
}
