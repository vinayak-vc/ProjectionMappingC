using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode tests for Phase B (drawing polish + editor UI actions) and Phase C
    /// (undo/redo, JSON save/load) additions to
    /// <see cref="PMSDKSkinEditorController"/>. Drives the public API directly; no
    /// Play mode, no scene reload.
    /// </summary>
    public class PMSDKSkinEditorPhaseBCTests
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

        // ---- Phase B: polygon click-to-draw ----

        [Test]
        public void AppendPolygonVertex_FirstClickCreatesSkin()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AppendPolygonVertex(new Vector2(0.2f, 0.2f));
            Assert.AreEqual(1, controller.Skins.Count);
            Assert.AreEqual(PMSDKSkinShapeKind.Polygon, controller.Selected.Kind);
            Assert.AreEqual(1, controller.Selected.Vertices.Count);
            Assert.IsTrue(controller.IsDrawingPolygon);
        }

        [Test]
        public void AppendPolygonVertex_MultipleClicks_AppendVertices()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AppendPolygonVertex(new Vector2(0.2f, 0.2f));
            controller.AppendPolygonVertex(new Vector2(0.5f, 0.2f));
            controller.AppendPolygonVertex(new Vector2(0.5f, 0.5f));
            controller.AppendPolygonVertex(new Vector2(0.2f, 0.5f));
            Assert.AreEqual(4, controller.Selected.Vertices.Count);
        }

        [Test]
        public void ClosePolygon_FinishesDraw()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AppendPolygonVertex(new Vector2(0.2f, 0.2f));
            controller.AppendPolygonVertex(new Vector2(0.5f, 0.2f));
            controller.AppendPolygonVertex(new Vector2(0.5f, 0.5f));
            controller.ClosePolygon();
            Assert.IsFalse(controller.IsDrawingPolygon);
            Assert.AreEqual(3, controller.Selected.Vertices.Count);
        }

        [Test]
        public void ClosePolygon_UnderThreeVerts_Cancels()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AppendPolygonVertex(new Vector2(0.2f, 0.2f));
            controller.AppendPolygonVertex(new Vector2(0.5f, 0.2f));
            controller.ClosePolygon();
            Assert.IsFalse(controller.IsDrawingPolygon);
            Assert.AreEqual(0, controller.Skins.Count);
        }

        [Test]
        public void AppendPolygonVertex_ClickNearStartWithThreeVerts_Closes()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AppendPolygonVertex(new Vector2(0.2f, 0.2f));
            controller.AppendPolygonVertex(new Vector2(0.5f, 0.2f));
            controller.AppendPolygonVertex(new Vector2(0.5f, 0.5f));
            // Click within PolygonCloseThreshold of first vertex.
            controller.AppendPolygonVertex(new Vector2(0.201f, 0.199f));
            Assert.IsFalse(controller.IsDrawingPolygon);
            Assert.AreEqual(3, controller.Selected.Vertices.Count);
        }

        [Test]
        public void CancelPolygonDraw_DestroysTransientSkin()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AppendPolygonVertex(new Vector2(0.5f, 0.5f));
            controller.CancelPolygonDraw();
            Assert.AreEqual(0, controller.Skins.Count);
            Assert.IsFalse(controller.IsDrawingPolygon);
        }

        // ---- Phase B: freeform stroke ----

        [Test]
        public void FreeformStroke_TracesAndCloses()
        {
            controller.BeginFreeformStroke(new Vector2(0.2f, 0.5f));
            // Space samples out by more than FreeformSampleSpacing (0.008 default).
            controller.ContinueFreeformStroke(new Vector2(0.3f, 0.55f));
            controller.ContinueFreeformStroke(new Vector2(0.4f, 0.6f));
            controller.ContinueFreeformStroke(new Vector2(0.5f, 0.65f));
            controller.EndFreeformStroke();

            Assert.AreEqual(1, controller.Skins.Count);
            Assert.AreEqual(PMSDKSkinShapeKind.Freeform, controller.Selected.Kind);
            Assert.GreaterOrEqual(controller.Selected.Vertices.Count, 3);
        }

        [Test]
        public void FreeformStroke_TooShort_CancelsOnEnd()
        {
            controller.BeginFreeformStroke(new Vector2(0.5f, 0.5f));
            // Only one point → less than 3 → EndFreeformStroke cancels.
            controller.EndFreeformStroke();
            Assert.AreEqual(0, controller.Skins.Count);
        }

        // ---- Phase B: insert/remove vertex ----

        [Test]
        public void InsertVertexOnSelectedEdge_AddsMidpoint()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AddDefaultShape();
            var s = controller.Selected;
            int before = s.Vertices.Count;
            controller.SelectedCornerIndex = 0;
            controller.InsertVertexOnSelectedEdge();
            Assert.AreEqual(before + 1, s.Vertices.Count);
            // New vertex sits between the old 0 and old 1 (now index 1 and index 2).
            var mid = 0.5f * (s.Vertices[0] + s.Vertices[2]);
            Assert.That(s.Vertices[1].x, Is.EqualTo(mid.x).Within(1e-4f));
        }

        [Test]
        public void RemoveSelectedVertex_RespectsMinCount()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AddDefaultShape(); // hexagon → 6 verts
            var s = controller.Selected;
            controller.SelectedCornerIndex = 0;
            controller.RemoveSelectedVertex();
            Assert.AreEqual(5, s.Vertices.Count);
            controller.RemoveSelectedVertex();
            controller.RemoveSelectedVertex();
            Assert.AreEqual(3, s.Vertices.Count);
            // Would drop below 3 — must be a no-op.
            controller.RemoveSelectedVertex();
            Assert.AreEqual(3, s.Vertices.Count);
        }

        [Test]
        public void InsertRemoveVertex_RectangleIsNoOp()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var s = controller.Selected;
            controller.SelectedCornerIndex = 0;
            controller.InsertVertexOnSelectedEdge();
            Assert.AreEqual(4, s.Vertices.Count); // rects are always 4
            controller.RemoveSelectedVertex();
            Assert.AreEqual(4, s.Vertices.Count);
        }

        // ---- Phase B: duplicate ----

        [Test]
        public void DuplicateSelected_CreatesOffsetCopy()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var orig = controller.Selected;
            var origVerts = new List<Vector2>(orig.Vertices);
            controller.DuplicateSelected();

            Assert.AreEqual(2, controller.Skins.Count);
            var clone = controller.Selected;
            Assert.AreNotSame(orig, clone);
            // Clone should be offset by (0.02, 0.02).
            Assert.That(clone.Vertices[0].x, Is.EqualTo(origVerts[0].x + 0.02f).Within(1e-4f));
            Assert.That(clone.Vertices[0].y, Is.EqualTo(origVerts[0].y + 0.02f).Within(1e-4f));
            Assert.AreNotEqual(orig.SkinId, clone.SkinId);
        }

        // ---- Phase B: z-order ----

        [Test]
        public void MoveSelectedZOrder_UpdatesFieldAndResorts()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var a = controller.Selected;
            controller.AddDefaultShape();
            var b = controller.Selected;
            Assert.AreEqual(0, a.ZOrder);
            Assert.AreEqual(0, b.ZOrder);
            controller.MoveSelectedZOrder(+1);
            Assert.AreEqual(1, b.ZOrder);
            // b now stacked in front — RefreshSkinList should sort it first.
            Assert.AreSame(b, controller.Skins[0]);
        }

        // ---- Phase B: solo/mute ----

        [Test]
        public void ToggleSoloSelected_MutesOthers()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.AddDefaultShape();
            var selected = controller.Selected;
            var other = controller.Skins[1] == selected ? controller.Skins[0] : controller.Skins[1];

            controller.ToggleSoloSelected();
            Assert.IsFalse(other.GetComponent<MeshRenderer>().enabled, "Other skin should be muted after solo.");
            Assert.IsTrue(selected.GetComponent<MeshRenderer>().enabled);
            Assert.IsTrue(controller.IsMuted(other));
        }

        [Test]
        public void UnmuteAll_ReEnablesRenderers()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.AddDefaultShape();
            controller.ToggleSoloSelected();
            controller.UnmuteAll();
            foreach (var s in controller.Skins)
                Assert.IsTrue(s.GetComponent<MeshRenderer>().enabled);
        }

        // ---- Phase B: pattern cycle ----

        [Test]
        public void CycleTestPatternSelected_SetsContentModeAndAdvancesType()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var s = controller.Selected;
            var content = s.GetComponent<PMSDKSkinContent>();
            var before = content.TestPatternType;
            controller.CycleTestPatternSelected(+1);
            Assert.AreEqual(PMSDKSkinContentMode.TestPattern, content.Mode);
            Assert.AreNotEqual(before, content.TestPatternType);
        }

        // ---- Phase C: undo / redo ----

        [Test]
        public void UndoLast_RevertsAddDefault()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            Assert.AreEqual(1, controller.Skins.Count);
            bool ok = controller.UndoLast();
            Assert.IsTrue(ok);
            Assert.AreEqual(0, controller.Skins.Count);
        }

        [Test]
        public void RedoLast_ReappliesUndoneOperation()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.UndoLast();
            Assert.AreEqual(0, controller.Skins.Count);
            bool ok = controller.RedoLast();
            Assert.IsTrue(ok);
            Assert.AreEqual(1, controller.Skins.Count);
        }

        [Test]
        public void UndoLast_RevertsCornerDrag()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.OnPointerDown(new Vector2(0.2f, 0.2f));
            controller.OnPointerDrag(new Vector2(0.8f, 0.8f));
            controller.OnPointerUp(new Vector2(0.8f, 0.8f));
            var s = controller.Selected;
            Assert.That(s.Vertices[0].x, Is.EqualTo(0.2f).Within(1e-4f));

            // Drag corner 0.
            controller.PickTool(PMSDKSkinEditorController.Tool.Select);
            controller.OnPointerDown(new Vector2(0.201f, 0.199f));
            controller.OnPointerDrag(new Vector2(0.05f, 0.05f));
            controller.OnPointerUp(new Vector2(0.05f, 0.05f));
            Assert.That(s.Vertices[0].x, Is.EqualTo(0.05f).Within(1e-4f));

            // Undo the corner drag → corner should be back at (0.2, 0.2).
            controller.UndoLast();
            var recovered = controller.Selected;
            Assert.That(recovered.Vertices[0].x, Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(recovered.Vertices[0].y, Is.EqualTo(0.2f).Within(1e-4f));
        }

        [Test]
        public void UndoStack_ReturnsFalseWhenEmpty()
        {
            Assert.IsFalse(controller.UndoLast());
            Assert.IsFalse(controller.RedoLast());
        }

        [Test]
        public void UndoCoalesce_MergesConsecutiveNudges()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            int depthAfterAdd = controller.Undo.UndoDepth;

            // A burst of nudges — all should collapse into ONE undo entry (coalesce
            // window is 0.7s and these fire in rapid succession).
            controller.NudgeSelectedCorner(new Vector2(0.01f, 0f));
            controller.NudgeSelectedCorner(new Vector2(0.01f, 0f));
            controller.NudgeSelectedCorner(new Vector2(0.01f, 0f));
            Assert.AreEqual(depthAfterAdd + 1, controller.Undo.UndoDepth,
                "Consecutive nudge records should coalesce into one undo entry.");
        }

        // ---- Phase C: JSON round-trip ----

        [Test]
        public void JsonRoundTrip_PreservesShapeAndVertices()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AddDefaultShape();
            var beforeCount = controller.Skins.Count;
            var beforeIds = new List<string>();
            foreach (var s in controller.Skins) beforeIds.Add(s.SkinId);

            string json = PMSDKSkinSetJson.Serialize(controller);
            Assert.IsNotEmpty(json);

            // Wipe all skins, then apply the JSON back.
            var set = PMSDKSkinSetJson.Deserialize(json);
            foreach (var s in controller.Skins) Object.DestroyImmediate(s.gameObject);
            controller.RefreshSkinList();
            Assert.AreEqual(0, controller.Skins.Count);

            PMSDKSkinSetJson.Apply(controller, set);
            Assert.AreEqual(beforeCount, controller.Skins.Count);
            var afterIds = new List<string>();
            foreach (var s in controller.Skins) afterIds.Add(s.SkinId);
            CollectionAssert.AreEquivalent(beforeIds, afterIds);
        }

        [Test]
        public void SaveToLoadFrom_RoundTripsViaFilesystem()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var s = controller.Selected;
            s.DisplayName = "MyBoxFront";
            var origVerts = new List<Vector2>(s.Vertices);
            var origId = s.SkinId;

            string path = Path.Combine(Application.temporaryCachePath, "pmsdk_skinset_test.json");
            try
            {
                Assert.IsTrue(controller.SaveTo(path));
                Assert.IsTrue(File.Exists(path));

                // Clear and reload.
                foreach (var skin in controller.Skins) Object.DestroyImmediate(skin.gameObject);
                controller.RefreshSkinList();

                Assert.IsTrue(controller.LoadFrom(path));
                Assert.AreEqual(1, controller.Skins.Count);
                var reloaded = controller.Skins[0];
                Assert.AreEqual(origId, reloaded.SkinId);
                Assert.AreEqual("MyBoxFront", reloaded.DisplayName);
                Assert.That(reloaded.Vertices[0].x, Is.EqualTo(origVerts[0].x).Within(1e-4f));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void LoadFrom_MissingFile_ReturnsFalse()
        {
            string path = Path.Combine(Application.temporaryCachePath,
                "pmsdk_skinset_does_not_exist_" + System.Guid.NewGuid().ToString("N") + ".json");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no skin set at .*"));
            Assert.IsFalse(controller.LoadFrom(path));
        }
    }
}
