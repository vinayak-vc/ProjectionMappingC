using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Canvas-space alignment reference: a full-wall pattern defined in the SHARED
    /// content/canvas space (not per projector raster), fed through
    /// PMSDKExternalContent so it spans every projector via the normal split-slice +
    /// warp path. Because the corner pins pre-warp it, the lines land straight and
    /// continuous on the physical wall even when the projectors themselves are
    /// tilted/keystoned — a per-raster test pattern cannot do that.
    ///
    /// Use it to:
    ///  - draw the end-to-end "big plus" + level lines across the whole wall,
    ///  - spot calibration residuals instantly (any kink/step at a projector seam
    ///    is a pin disagreement — touch up the nearest corners or re-run auto-align).
    ///
    /// Note: lines are level relative to the CANVAS. If the whole canvas is rotated
    /// vs gravity (camera tilt during auto-align), the pattern is uniformly tilted;
    /// checking against a laser level / chalk line once fixes that by rotating the
    /// canvas. Straightness and seam continuity need no physical reference.
    ///
    /// Toggled with C in calibration mode (PMSDKCalibrationManager).
    /// </summary>
    [RequireComponent(typeof(PMSDKExternalContent))]
    public class PMSDKCanvasReferencePattern : MonoBehaviour
    {
        [Tooltip("Texture width; make it ~projectorWidth * projectorCount for crisp lines.")]
        public int Width = 3840;
        public int Height = 1080;

        [Header("Pattern")]
        [Tooltip("End-to-end horizontal + vertical centre lines (the big plus).")]
        public bool CentrePlus = true;
        [Tooltip("Additional evenly spaced level lines (horizon references).")]
        public int HorizontalLines = 3;
        [Tooltip("Additional evenly spaced vertical lines.")]
        public int VerticalLines = 7;
        [Tooltip("Line thickness in texture pixels.")]
        public int Thickness = 3;
        public Color LineColor = Color.white;
        public Color PlusColor = new Color(1f, 0.9f, 0.1f, 1f);
        public Color Background = new Color(0.12f, 0.12f, 0.16f, 1f);

        private Texture2D texture;
        private PMSDKExternalContent content;

        public bool Active { get; private set; }

        public void Toggle()
        {
            if (Active) Deactivate(); else Activate();
        }

        public void Activate()
        {
            if (content == null) content = GetComponent<PMSDKExternalContent>();
            if (texture == null) Generate();
            content.Source = texture;
            Active = true;
        }

        public void Deactivate()
        {
            if (content != null && ReferenceEquals(content.Source, texture)) content.Source = null;
            Active = false;
        }

        private void OnDisable()
        {
            Deactivate();
        }

        /// <summary>Regenerate the pattern texture (after changing knobs).</summary>
        public void Generate()
        {
            if (texture == null || texture.width != Width || texture.height != Height)
            {
                if (texture != null) Destroy(texture);
                texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
                {
                    name = "PMSDK_CanvasReference",
                    hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            var px = new Color[Width * Height];
            Fill(px, Width, Height, CentrePlus, HorizontalLines, VerticalLines, Thickness,
                 LineColor, PlusColor, Background);
            texture.SetPixels(px);
            texture.Apply(false);
        }

        /// <summary>Pure rasterizer (static + deterministic for tests).</summary>
        public static void Fill(Color[] px, int w, int h, bool plus, int hLines, int vLines,
                                int thickness, Color line, Color plusColor, Color bg)
        {
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            // Evenly spaced reference lines (excluding the centre, drawn by the plus).
            for (int n = 1; n <= hLines; n++)
            {
                int y = h * n / (hLines + 1);
                DrawH(px, w, h, y, thickness, line);
            }
            for (int n = 1; n <= vLines; n++)
            {
                int x = w * n / (vLines + 1);
                DrawV(px, w, h, x, thickness, line);
            }

            if (plus)
            {
                DrawH(px, w, h, h / 2, thickness * 2, plusColor);
                DrawV(px, w, h, w / 2, thickness * 2, plusColor);
                // Border so the canvas extents are visible end to end.
                DrawH(px, w, h, thickness, thickness, plusColor);
                DrawH(px, w, h, h - 1 - thickness, thickness, plusColor);
                DrawV(px, w, h, thickness, thickness, plusColor);
                DrawV(px, w, h, w - 1 - thickness, thickness, plusColor);
            }
        }

        private static void DrawH(Color[] px, int w, int h, int y, int thickness, Color c)
        {
            for (int dy = -thickness / 2; dy <= thickness / 2; dy++)
            {
                int yy = y + dy;
                if (yy < 0 || yy >= h) continue;
                for (int x = 0; x < w; x++) px[yy * w + x] = c;
            }
        }

        private static void DrawV(Color[] px, int w, int h, int x, int thickness, Color c)
        {
            for (int dx = -thickness / 2; dx <= thickness / 2; dx++)
            {
                int xx = x + dx;
                if (xx < 0 || xx >= w) continue;
                for (int y = 0; y < h; y++) px[y * w + xx] = c;
            }
        }
    }
}
