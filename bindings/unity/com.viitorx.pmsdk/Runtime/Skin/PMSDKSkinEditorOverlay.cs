using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// GL-immediate line + handle overlay rendered on the projector's output. Draws
    /// every skin's outline plus per-vertex handles; the selected skin gets pulsing
    /// yellow, unselected skins render in dim gray, and the selected corner within
    /// the selected skin is highlighted.
    ///
    /// Renders in <c>OnRenderObject</c> after the projector camera's own draw so the
    /// overlay is always on top. Uses the <c>PMSDK/SkinEditorOverlay</c> shader
    /// (ZTest Always, alpha-blend) which ships alongside this component.
    ///
    /// The overlay draws in the projector's LOCAL space: skins already sit at
    /// <c>localScale=(16/9, 1, 1)</c> under the projector's parent, so raster [0..1]
    /// on a skin maps to world via <c>skin.transform.TransformPoint((v.x, v.y, 0))</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public class PMSDKSkinEditorOverlay : MonoBehaviour
    {
        [Header("Refs")]
        public PMSDKSkinEditorController Controller;
        [Tooltip("Material using PMSDK/SkinEditorOverlay. If null, one is created on Awake.")]
        public Material LineMaterial;

        [Header("Colors")]
        public Color SelectedColor = new Color(1f, 0.85f, 0.05f, 1f);
        public Color SelectedCornerColor = new Color(1f, 1f, 1f, 1f);
        public Color UnselectedColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);
        public Color DrawGuideColor = new Color(0.2f, 0.9f, 1f, 0.8f);

        [Header("Sizes (raster units)")]
        [Tooltip("Half-side of the square drawn at each vertex handle.")]
        public float HandleSize = 0.008f;
        [Tooltip("Extra outline offset from the selected skin (aesthetic).")]
        public float SelectedOutlinePulseAmp = 0.002f;
        public float SelectedOutlinePulseHz = 2f;

        private void Awake()
        {
            EnsureMaterial();
        }

        private void EnsureMaterial()
        {
            if (LineMaterial != null) return;
            var shader = Shader.Find("PMSDK/SkinEditorOverlay");
            if (shader == null)
            {
                // Fall back to Unity's built-in Line shader — same interface, works with GL.
                shader = Shader.Find("Hidden/Internal-Colored");
            }
            if (shader != null)
            {
                LineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
        }

        private void OnRenderObject()
        {
            if (Controller == null || Controller.Projector == null || LineMaterial == null) return;
            var projCam = Controller.Projector.GetComponent<Camera>();
            if (projCam == null || Camera.current != projCam) return;

            LineMaterial.SetPass(0);

            var skins = Controller.Skins;
            if (skins == null || skins.Count == 0) return;

            // Selected pulse: mostly cosmetic, small radius shift so the eye picks up the
            // active skin without having to read a legend.
            float pulse = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * SelectedOutlinePulseHz * Time.time);
            float pulseOffset = pulse * SelectedOutlinePulseAmp;

            GL.Begin(GL.LINES);
            for (int i = 0; i < skins.Count; i++)
            {
                var s = skins[i];
                if (s == null || s.Vertices == null || s.Vertices.Count < 2) continue;
                bool selected = i == Controller.SelectedSkinIndex;
                Color outlineColor = selected ? SelectedColor : UnselectedColor;
                DrawOutline(s, outlineColor, selected ? pulseOffset : 0f);
            }
            GL.End();

            // Handles as GL_QUADS (small filled squares).
            GL.Begin(GL.QUADS);
            for (int i = 0; i < skins.Count; i++)
            {
                var s = skins[i];
                if (s == null || s.Vertices == null) continue;
                bool selected = i == Controller.SelectedSkinIndex;
                DrawHandles(s, selected, Controller.SelectedCornerIndex);
            }
            GL.End();
        }

        // ---- draw helpers ----

        private void DrawOutline(PMSDKSkin skin, Color c, float radiusOffset)
        {
            var verts = skin.Vertices;
            int n = verts.Count;
            var t = skin.transform;
            GL.Color(c);
            for (int i = 0; i < n; i++)
            {
                Vector2 a = verts[i];
                Vector2 b = verts[(i + 1) % n];
                // Small outward push for the selected pulse — pushes each vertex away
                // from the polygon centroid.
                if (radiusOffset > 0f)
                {
                    Vector2 c0 = Centroid(verts);
                    a = a + (a - c0).normalized * radiusOffset;
                    b = b + (b - c0).normalized * radiusOffset;
                }
                Vector3 wa = t.TransformPoint(new Vector3(a.x, a.y, 0f));
                Vector3 wb = t.TransformPoint(new Vector3(b.x, b.y, 0f));
                GL.Vertex(wa); GL.Vertex(wb);
            }
        }

        private void DrawHandles(PMSDKSkin skin, bool selected, int highlightIndex)
        {
            var verts = skin.Vertices;
            var t = skin.transform;
            Color baseColor = selected ? SelectedColor : UnselectedColor;
            for (int i = 0; i < verts.Count; i++)
            {
                bool isSelectedCorner = selected && i == highlightIndex;
                GL.Color(isSelectedCorner ? SelectedCornerColor : baseColor);
                Vector2 v = verts[i];
                Vector3 c0 = t.TransformPoint(new Vector3(v.x - HandleSize, v.y - HandleSize, 0f));
                Vector3 c1 = t.TransformPoint(new Vector3(v.x + HandleSize, v.y - HandleSize, 0f));
                Vector3 c2 = t.TransformPoint(new Vector3(v.x + HandleSize, v.y + HandleSize, 0f));
                Vector3 c3 = t.TransformPoint(new Vector3(v.x - HandleSize, v.y + HandleSize, 0f));
                GL.Vertex(c0); GL.Vertex(c1); GL.Vertex(c2); GL.Vertex(c3);
            }
        }

        private static Vector2 Centroid(System.Collections.Generic.IList<Vector2> vs)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < vs.Count; i++) sum += vs[i];
            return vs.Count == 0 ? sum : sum / vs.Count;
        }
    }
}
