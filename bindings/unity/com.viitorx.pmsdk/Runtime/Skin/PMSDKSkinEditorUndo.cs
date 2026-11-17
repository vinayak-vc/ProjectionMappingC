using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Undo/redo stack for the Skin Editor (M-Sk2 Phase C). A "snapshot" is a captured
    /// <see cref="PMSDKSkinSetJson.Set"/> — the full scene state serializable to JSON.
    /// Apply restores by matching skins on <c>SkinId</c> (updates in place) and spawning /
    /// destroying to reconcile.
    ///
    /// Coalescing: back-to-back records tagged with the same <paramref name="reason"/>
    /// inside <see cref="CoalesceSeconds"/> collapse into one. That way a stream of
    /// per-frame arrow-key nudges only eats one undo slot, while a Draw + a follow-up
    /// Drag land as distinct undos. Matches the F2 calibration undo feel
    /// (<c>PMSDKCalibrationManager</c>).
    /// </summary>
    public class PMSDKSkinEditorUndo
    {
        /// <summary>Two records of the same reason within this window collapse.</summary>
        public float CoalesceSeconds = 0.7f;
        /// <summary>Stack depth cap — sized like the F2 undo.</summary>
        public int MaxDepth = 100;

        private readonly Stack<Entry> undo = new Stack<Entry>();
        private readonly Stack<Entry> redo = new Stack<Entry>();

        private struct Entry
        {
            public PMSDKSkinSetJson.Set State;
            public string Reason;
            public float Time;
        }

        public int UndoDepth => undo.Count;
        public int RedoDepth => redo.Count;

        /// <summary>Snapshot the controller's current state, tagged with a reason.</summary>
        public void Record(PMSDKSkinEditorController controller, string reason)
        {
            if (controller == null) return;
            float now = Time.realtimeSinceStartup;

            // Coalesce: if the top-of-stack has the same reason within the window,
            // replace it rather than growing the stack. The captured state is the LATEST
            // pre-op snapshot so the user's undo lands them BEFORE the whole burst.
            if (undo.Count > 0)
            {
                var top = undo.Peek();
                if (top.Reason == reason && (now - top.Time) < CoalesceSeconds)
                {
                    // Keep the earlier snapshot (top) — it captures the state before
                    // the burst started. Just refresh the timestamp so the coalesce
                    // window slides.
                    var refreshed = top;
                    refreshed.Time = now;
                    undo.Pop();
                    undo.Push(refreshed);
                    redo.Clear();
                    return;
                }
            }

            undo.Push(new Entry
            {
                State = PMSDKSkinSetJson.Capture(controller),
                Reason = reason,
                Time = now,
            });
            redo.Clear();
            TrimToDepth();
        }

        /// <summary>Undo the last recorded op. Returns true if something was popped.</summary>
        public bool Undo(PMSDKSkinEditorController controller)
        {
            if (controller == null || undo.Count == 0) return false;
            // Snapshot the CURRENT state onto the redo stack before restoring the previous.
            redo.Push(new Entry
            {
                State = PMSDKSkinSetJson.Capture(controller),
                Reason = "redo-checkpoint",
                Time = Time.realtimeSinceStartup,
            });
            var target = undo.Pop();
            PMSDKSkinSetJson.Apply(controller, target.State);
            return true;
        }

        /// <summary>Redo the last undone op. Returns true if something was popped.</summary>
        public bool Redo(PMSDKSkinEditorController controller)
        {
            if (controller == null || redo.Count == 0) return false;
            undo.Push(new Entry
            {
                State = PMSDKSkinSetJson.Capture(controller),
                Reason = "undo-checkpoint",
                Time = Time.realtimeSinceStartup,
            });
            var target = redo.Pop();
            PMSDKSkinSetJson.Apply(controller, target.State);
            return true;
        }

        public void Clear() { undo.Clear(); redo.Clear(); }

        private void TrimToDepth()
        {
            if (undo.Count <= MaxDepth) return;
            // Stack has no bottom-drop; convert, trim, rebuild. Not hot path; happens ≤ 1x
            // per record over the cap.
            var list = new List<Entry>(undo);
            list.Reverse(); // list[0] is oldest
            while (list.Count > MaxDepth) list.RemoveAt(0);
            undo.Clear();
            foreach (var e in list) undo.Push(e);
        }
    }
}
