using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode tests for the M-Sk4 v1.5 UGUI Canvas skin list. Verifies canvas + rows
    /// are built, IMGUI HUD's DrawList flips off while active, rows reflect + write back
    /// to the skin state, and the reorder API (<see cref="PMSDKSkinEditorController.MoveSkinToIndex"/>)
    /// works when driven directly. Drag-event simulation is skipped — the UGUI event
    /// dispatch trips ShouldRunBehaviour asserts in EditMode; the reorder LOGIC is
    /// covered by direct MoveSkinToIndex tests.
    /// </summary>
    public class PMSDKSkinEditorCanvasListTests
    {
        private GameObject projGO;
        private GameObject editorGO;
        private PMSDKSkinEditorController controller;
        private PMSDKSkinEditorHUD imguiHud;
        private PMSDKSkinEditorCanvasList canvasList;

        [SetUp]
        public void Setup()
        {
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

            canvasList = editorGO.AddComponent<PMSDKSkinEditorCanvasList>();
            canvasList.Controller = controller;
            canvasList.RebuildForTests();
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
            Assert.IsTrue(canvasList.CanvasBuilt);
            var canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas);
        }

        [Test]
        public void OnEnable_HidesImguiSkinList()
        {
            Assert.IsFalse(imguiHud.DrawList,
                "Canvas list should hide the IMGUI skin list while active.");
        }

        // ---- row lifecycle ----

        [Test]
        public void RowsPopulate_MatchSkinCount()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.AddDefaultShape();
            controller.AddDefaultShape();

            canvasList.RebuildForTests();
            Assert.AreEqual(3, canvasList.Rows.Count,
                "One row per skin should be present after RebuildForTests.");
        }

        [Test]
        public void RowSyncVisuals_ReflectsSkinName()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.Selected.DisplayName = "Front";
            canvasList.RebuildForTests();

            var row = canvasList.Rows[0];
            row.SyncVisuals();
            // The row's label goes through the AddButton's child Text (index 0 child of the button GO).
            var buttons = row.GetComponentsInChildren<Button>();
            Assert.Greater(buttons.Length, 0);
            var labelText = buttons[0].GetComponentInChildren<Text>();
            StringAssert.Contains("Front", labelText.text);
        }

        // ---- reorder API ----

        [Test]
        public void MoveSkinToIndex_ReordersStack()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var a = controller.Selected;
            controller.AddDefaultShape();
            var b = controller.Selected;
            controller.AddDefaultShape();
            var c = controller.Selected;
            controller.RefreshSkinList();

            // Assign predictable initial z-orders: a=1, b=2, c=3 → visual order c,b,a.
            a.ZOrder = 1; b.ZOrder = 2; c.ZOrder = 3;
            controller.RefreshSkinList();
            Assert.AreSame(c, controller.Skins[0]);
            Assert.AreSame(b, controller.Skins[1]);
            Assert.AreSame(a, controller.Skins[2]);

            // Move `a` to slot 0 (top of stack).
            controller.MoveSkinToIndex(a, 0);
            Assert.AreSame(a, controller.Skins[0], "Reorder should move `a` to the top.");
            Assert.AreSame(c, controller.Skins[1]);
            Assert.AreSame(b, controller.Skins[2]);
        }

        [Test]
        public void MoveSkinToIndex_ClampToBounds()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var a = controller.Selected;
            controller.AddDefaultShape();
            var b = controller.Selected;
            controller.RefreshSkinList();

            // Clamped upward — target well past the end.
            controller.MoveSkinToIndex(a, 999);
            var lastIdx = controller.Skins.Count - 1;
            Assert.AreSame(a, controller.Skins[lastIdx]);

            // Clamped downward — target below 0.
            controller.MoveSkinToIndex(b, -5);
            Assert.AreSame(b, controller.Skins[0]);
        }

        [Test]
        public void MoveSkinToIndex_NoOpAtSameIndex()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var a = controller.Selected;
            controller.AddDefaultShape();
            controller.RefreshSkinList();

            int idx = controller.Skins.IndexOf(a);
            int undoBefore = controller.Undo.UndoDepth;
            controller.MoveSkinToIndex(a, idx);
            Assert.AreEqual(undoBefore, controller.Undo.UndoDepth,
                "Same-index move should be a no-op and not eat an undo slot.");
        }

        [Test]
        public void MoveSkinToIndex_UndoRestoresPriorOrder()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            var a = controller.Selected;
            controller.AddDefaultShape();
            var b = controller.Selected;
            controller.RefreshSkinList();

            int aIndexBefore = controller.Skins.IndexOf(a);
            int bIndexBefore = controller.Skins.IndexOf(b);

            // Move `a` to `b`'s slot.
            controller.MoveSkinToIndex(a, bIndexBefore);
            Assert.AreEqual(bIndexBefore, controller.Skins.IndexOf(a));

            // Undo should restore.
            controller.UndoLast();
            Assert.AreEqual(aIndexBefore, controller.Skins.IndexOf(a));
            Assert.AreEqual(bIndexBefore, controller.Skins.IndexOf(b));
        }
    }
}
