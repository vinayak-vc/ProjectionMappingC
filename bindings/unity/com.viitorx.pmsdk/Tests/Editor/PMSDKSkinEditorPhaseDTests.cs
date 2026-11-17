using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode tests for M-Sk4 polish: snap-to-grid, snap-to-vertex, multi-select,
    /// group nudge, visibility toggle. Pure logic tests (no scene navigation).
    /// </summary>
    public class PMSDKSkinEditorPhaseDTests
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
            if (ctrlGO != null) Object.DestroyImmediate(ctrlGO);
            if (projGO != null) Object.DestroyImmediate(projGO);
        }

        // ---- PMSDKSkinEditorSnap (pure math) ----

        [Test]
        public void SnapToGrid_WithinTolerance_SnapsToNearestStep()
        {
            Assert.AreEqual(0.5f, PMSDKSkinEditorSnap.SnapToGrid(0.509f, 0.05f, 0.02f), 1e-5f);
            Assert.AreEqual(0.5f, PMSDKSkinEditorSnap.SnapToGrid(0.492f, 0.05f, 0.02f), 1e-5f);
        }

        [Test]
        public void SnapToGrid_OutsideTolerance_ReturnsInput()
        {
            Assert.AreEqual(0.53f, PMSDKSkinEditorSnap.SnapToGrid(0.53f, 0.05f, 0.005f), 1e-5f);
        }

        [Test]
        public void SnapToGrid_StepZero_ReturnsInput()
        {
            Assert.AreEqual(0.512f, PMSDKSkinEditorSnap.SnapToGrid(0.512f, 0f, 1f), 1e-5f);
        }

        [Test]
        public void SnapToVertices_XOnly_SetsGuideAndSnaps()
        {
            var candidates = new List<Vector2> { new Vector2(0.3f, 0.7f) };
            var v = PMSDKSkinEditorSnap.SnapToVertices(
                new Vector2(0.302f, 0.5f), candidates, 0.01f,
                out bool sx, out bool sy, out float lx, out float ly);
            Assert.IsTrue(sx);
            Assert.IsFalse(sy);
            Assert.AreEqual(0.3f, lx, 1e-5f);
            Assert.AreEqual(0.3f, v.x, 1e-5f);
            Assert.AreEqual(0.5f, v.y, 1e-5f);
        }

        [Test]
        public void SnapToVertices_BothAxes_SetsBothGuides()
        {
            var candidates = new List<Vector2> { new Vector2(0.4f, 0.6f) };
            var v = PMSDKSkinEditorSnap.SnapToVertices(
                new Vector2(0.402f, 0.598f), candidates, 0.01f,
                out bool sx, out bool sy, out _, out _);
            Assert.IsTrue(sx);
            Assert.IsTrue(sy);
            Assert.AreEqual(0.4f, v.x, 1e-5f);
            Assert.AreEqual(0.6f, v.y, 1e-5f);
        }

        [Test]
        public void CollectVertices_ExcludesTheDraggedSkin()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var a = controller.Selected;
            controller.AddDefaultShape();
            var b = controller.Selected;

            var vertsExcludingA = PMSDKSkinEditorSnap.CollectVertices(controller.Skins, a);
            Assert.AreEqual(b.Vertices.Count, vertsExcludingA.Count,
                "Only vertices from skins OTHER than the dragged one should be collected.");
        }

        // ---- Snap applied via ApplySnaps during drag ----

        [Test]
        public void ApplySnaps_GridSnapsCornerDuringDrag()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var s = controller.Selected;
            controller.SnapGridStep = 0.05f;
            controller.SnapGridTolerance = 0.02f;
            controller.SnapAlignTolerance = 0f;

            // Grab corner 0 (BL) which the default add puts at (0.35, 0.35), then drag
            // close to (0.202, 0.2) — expect (0.20, 0.20) via grid snap.
            controller.PickTool(PMSDKSkinEditorController.Tool.Select);
            controller.OnPointerDown(new Vector2(s.Vertices[0].x + 0.001f, s.Vertices[0].y + 0.001f));
            controller.OnPointerDrag(new Vector2(0.202f, 0.201f));

            Assert.That(s.Vertices[0].x, Is.EqualTo(0.20f).Within(1e-3f));
            Assert.That(s.Vertices[0].y, Is.EqualTo(0.20f).Within(1e-3f));
        }

        [Test]
        public void ApplySnaps_AlignSnapExposesGuides()
        {
            // Two skins side-by-side. Drag skin A's corner near skin B's vertex — alignment
            // snap should fire and populate SnapGuides.
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var a = controller.Selected;
            // Force a specific vertex on `a` to make the assertion deterministic.
            a.Vertices = new List<Vector2> {
                new Vector2(0.10f, 0.10f), new Vector2(0.40f, 0.10f),
                new Vector2(0.40f, 0.40f), new Vector2(0.10f, 0.40f),
            };
            controller.AddDefaultShape();
            var b = controller.Selected;
            b.Vertices = new List<Vector2> {
                new Vector2(0.60f, 0.60f), new Vector2(0.90f, 0.60f),
                new Vector2(0.90f, 0.90f), new Vector2(0.60f, 0.90f),
            };

            controller.SnapGridStep = 0f;
            controller.SnapAlignTolerance = 0.02f;

            // Drag corner 0 of `a` (currently 0.10, 0.10) to (0.602, 0.15) — snap X to
            // b's vertex 0 at x = 0.60.
            controller.SelectedSkinIndex = 0;
            controller.PickTool(PMSDKSkinEditorController.Tool.Select);
            controller.OnPointerDown(new Vector2(0.101f, 0.101f));
            controller.OnPointerDrag(new Vector2(0.602f, 0.15f));

            Assert.IsTrue(controller.SnapGuides.XActive);
            Assert.That(controller.SnapGuides.XLine, Is.EqualTo(0.60f).Within(1e-4f));
            Assert.That(a.Vertices[0].x, Is.EqualTo(0.60f).Within(1e-4f));

            // Pointer-up clears guides.
            controller.OnPointerUp(new Vector2(0.602f, 0.15f));
            Assert.IsFalse(controller.SnapGuides.XActive);
        }

        // ---- Multi-select ----

        [Test]
        public void ToggleInSelection_AddsToMultiSelect()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.AddDefaultShape();
            controller.AddDefaultShape();

            int other = (controller.SelectedSkinIndex + 1) % controller.Skins.Count;
            controller.ToggleInSelection(other);
            Assert.AreEqual(2, controller.SelectionGroupCount);
            Assert.IsTrue(controller.IsInSelectionGroup(other));
        }

        [Test]
        public void ToggleInSelection_TogglesOff()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.AddDefaultShape();
            int other = (controller.SelectedSkinIndex + 1) % controller.Skins.Count;
            controller.ToggleInSelection(other);
            controller.ToggleInSelection(other);
            Assert.AreEqual(1, controller.SelectionGroupCount);
        }

        [Test]
        public void ToggleInSelection_PrimaryClick_PromotesOrClears()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var skinA = controller.Selected;
            controller.AddDefaultShape();
            var skinB = controller.Selected;

            // Re-resolve indices AFTER both spawns (skin list sort is unstable at equal ZOrder).
            int a = controller.Skins.IndexOf(skinA);
            int b = controller.Skins.IndexOf(skinB);
            controller.SelectedSkinIndex = b;
            controller.ToggleInSelection(a); // add A into multi

            // Now toggle the PRIMARY (B) — should demote it and promote A to primary.
            controller.ToggleInSelection(b);
            Assert.AreSame(skinA, controller.Selected,
                "Toggling the primary should promote a member of the multi-select.");
            Assert.AreEqual(1, controller.SelectionGroupCount);
        }

        [Test]
        public void NudgeSelectedGroup_MovesEverySelectedSkin()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var s1 = controller.Selected;
            controller.AddDefaultShape();
            var s2 = controller.Selected;
            controller.ToggleInSelection(controller.Skins.IndexOf(s1));

            controller.SelectedCornerIndex = 0;
            var b1 = s1.Vertices[0];
            var b2 = s2.Vertices[0];
            controller.NudgeSelectedGroup(new Vector2(0.05f, 0f));

            Assert.That(s1.Vertices[0].x, Is.EqualTo(b1.x + 0.05f).Within(1e-4f));
            Assert.That(s2.Vertices[0].x, Is.EqualTo(b2.x + 0.05f).Within(1e-4f));
        }

        [Test]
        public void NudgeSelectedGroup_EmptyMulti_FallsBackToSingle()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var s = controller.Selected;
            controller.SelectedCornerIndex = 0;
            var before = s.Vertices[0];
            controller.NudgeSelectedGroup(new Vector2(0.01f, 0f));
            Assert.That(s.Vertices[0].x, Is.EqualTo(before.x + 0.01f).Within(1e-4f));
        }

        [Test]
        public void ClearMultiSelection_EmptiesTheSet()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.AddDefaultShape();
            controller.ToggleInSelection((controller.SelectedSkinIndex + 1) % controller.Skins.Count);
            controller.ClearMultiSelection();
            Assert.AreEqual(1, controller.SelectionGroupCount);
        }

        // ---- Visibility ----

        [Test]
        public void SetSkinVisibility_TogglesMeshRenderer()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var s = controller.Selected;
            Assert.IsTrue(controller.IsVisible(s));
            controller.SetSkinVisibility(s, false);
            Assert.IsFalse(controller.IsVisible(s));
            controller.SetSkinVisibility(s, true);
            Assert.IsTrue(controller.IsVisible(s));
        }

        [Test]
        public void ToggleVisibilitySelected_TogglesPrimary()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var s = controller.Selected;
            controller.ToggleVisibilitySelected();
            Assert.IsFalse(controller.IsVisible(s));
            controller.ToggleVisibilitySelected();
            Assert.IsTrue(controller.IsVisible(s));
        }
    }
}
