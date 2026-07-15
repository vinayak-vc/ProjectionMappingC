using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Camera-assisted auto-align (calibration P3). For each projector surface it:
    ///   1. Temporarily takes over the surface: identity corner-pin, edge blend off,
    ///      an unlit material it can drive with pattern textures.
    ///   2. Displays a white and black reference, then the Gray-code column/row
    ///      patterns, capturing each through an IPMSDKCalibrationCamera.
    ///   3. Decodes the captures into a camera-pixel -> projector-raster correspondence
    ///      (PMSDKGrayCodeDecode) and fits a camera->projector homography
    ///      (PMSDKHomography) by least squares over every valid pixel.
    ///   4. Maps the four target corners (where the projection SHOULD land, in camera
    ///      space) through the homography to projector-raster coordinates and writes
    ///      them into the PMSDKCornerPin. The operator then fine-tunes by hand (P1/P2).
    ///
    /// This needs no metric calibration (unlike the native stereo triangulation path),
    /// which is what makes it a one-button on-site operation. The default target is the
    /// full observed projection quad; a caller can supply a real target (marked screen
    /// bounds, or the camera-space rectangle of the physical surface).
    ///
    /// Runs as a coroutine so pattern display and capture span real frames. Results are
    /// reported through callbacks; the calibration manager drives it from the 'A' key.
    /// </summary>
    public class PMSDKAutoAlign : MonoBehaviour
    {
        public struct Result
        {
            public bool Success;
            public string SurfaceId;
            public int ValidPixels;
            public float ReprojectionError; // mean, in camera pixels
            public string Message;
        }

        [Tooltip("Gray-code resolution used for correspondence. Only needs to be fine enough for a stable homography; 128 (7+7=14 patterns) is plenty.")]
        public int PatternResolution = 128;
        [Tooltip("Frames to wait after displaying a pattern before capturing (lets the display/camera settle).")]
        public int SettleFrames = 2;
        [Tooltip("Minimum white-black contrast (0..255) for a camera pixel to count as lit by this projector.")]
        public int MinContrast = 30;

        public bool IsRunning { get; private set; }

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        /// <summary>
        /// Align one surface. targetCornersCameraNorm: the four camera-space points
        /// (normalized 0..1, TL,TR,BR,BL order) where the projection should land; pass
        /// null to use the full observed quad (identity — useful for verification).
        /// </summary>
        public IEnumerator AlignSurface(
            PMSDKCalibrationManager.Surface surface,
            IPMSDKCalibrationCamera camera,
            Vector2[] targetCornersCameraNorm,
            System.Action<Result> onDone)
        {
            IsRunning = true;
            var result = new Result { SurfaceId = surface != null ? surface.Id : "<null>" };

            if (surface == null || surface.CornerPin == null || surface.Warp == null || camera == null)
            {
                result.Message = "Missing surface, corner pin, warp, or camera.";
                onDone?.Invoke(result);
                IsRunning = false;
                yield break;
            }

            // --- take over the surface ---
            var renderer = surface.Warp.GetComponent<MeshRenderer>();
            Material savedMaterial = renderer != null ? renderer.sharedMaterial : null;
            bool savedTestPattern = surface.TestPattern != null && surface.TestPattern.enabled;
            bool savedBlend = surface.Blend != null && surface.Blend.enabled;
            var savedCorners = new[]
            {
                surface.CornerPin.TopLeft, surface.CornerPin.TopRight,
                surface.CornerPin.BottomRight, surface.CornerPin.BottomLeft
            };

            if (surface.TestPattern != null) surface.TestPattern.enabled = false;
            if (surface.Blend != null) surface.Blend.enabled = false;
            // Measure with an identity warp so decoded projector coords are the raster.
            surface.CornerPin.TopLeft = new Vector2(0, 1);
            surface.CornerPin.TopRight = new Vector2(1, 1);
            surface.CornerPin.BottomRight = new Vector2(1, 0);
            surface.CornerPin.BottomLeft = new Vector2(0, 0);

            var patternMat = new Material(Shader.Find("Unlit/Texture")) { hideFlags = HideFlags.DontSave };
            var patternTex = new Texture2D(PatternResolution, PatternResolution, TextureFormat.R8, false)
            {
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            if (renderer != null) renderer.sharedMaterial = patternMat;

            camera.Begin();

            int projW = PatternResolution, projH = PatternResolution;
            int count = PMSDKGrayCodeDecode.PatternCount(projW, projH);
            int camW = camera.Width, camH = camera.Height;
            var pixels = new byte[projW * projH];

            // white reference
            FillPattern(pixels, 255);
            yield return DisplayAndSettle(patternTex, patternMat, pixels);
            byte[] white = camera.CaptureLuminance();

            // black reference
            FillPattern(pixels, 0);
            yield return DisplayAndSettle(patternTex, patternMat, pixels);
            byte[] black = camera.CaptureLuminance();

            // gray-code patterns
            var captures = new byte[count][];
            for (int k = 0; k < count; k++)
            {
                PMSDKGrayCodeDecode.GeneratePattern(k, projW, projH, pixels);
                yield return DisplayAndSettle(patternTex, patternMat, pixels);
                captures[k] = camera.CaptureLuminance();
            }

            camera.End();

            // --- restore the surface before solving (cheap; solve can't fail it) ---
            if (renderer != null) renderer.sharedMaterial = savedMaterial;
            if (surface.TestPattern != null) surface.TestPattern.enabled = savedTestPattern;
            if (surface.Blend != null) surface.Blend.enabled = savedBlend;
            Object.Destroy(patternMat);
            Object.Destroy(patternTex);

            // --- decode + fit camera->projector homography ---
            var corr = PMSDKGrayCodeDecode.Decode(captures, white, black, camW, camH, projW, projH, MinContrast);

            var camPts = new List<Vector2>();
            var projPts = new List<Vector2>();
            // Subsample for speed and to avoid over-weighting; a few thousand spread
            // points give a rock-solid homography.
            int step = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt((camW * camH) / 4000f)));
            for (int y = 0; y < camH; y += step)
            {
                for (int x = 0; x < camW; x += step)
                {
                    var c = corr[y * camW + x];
                    if (!c.Valid) continue;
                    // Normalize both spaces to 0..1 so the solve is well-conditioned
                    // and the corner-pin output is already in raster units.
                    camPts.Add(new Vector2((x + 0.5f) / camW, (y + 0.5f) / camH));
                    projPts.Add(new Vector2((c.ProjectorX + 0.5f) / projW, (c.ProjectorY + 0.5f) / projH));
                }
            }
            result.ValidPixels = camPts.Count;

            if (camPts.Count < 4)
            {
                result.Success = false;
                result.Message = $"Only {camPts.Count} lit correspondence points — is the camera pointed at this projector, and is the room dark enough?";
                // leave corners as the operator had them
                surface.CornerPin.TopLeft = savedCorners[0];
                surface.CornerPin.TopRight = savedCorners[1];
                surface.CornerPin.BottomRight = savedCorners[2];
                surface.CornerPin.BottomLeft = savedCorners[3];
                onDone?.Invoke(result);
                IsRunning = false;
                yield break;
            }

            var camToProj = PMSDKHomography.Fit(camPts, projPts);
            if (!camToProj.Valid)
            {
                result.Success = false;
                result.Message = "Homography solve failed (degenerate correspondence).";
                surface.CornerPin.TopLeft = savedCorners[0];
                surface.CornerPin.TopRight = savedCorners[1];
                surface.CornerPin.BottomRight = savedCorners[2];
                surface.CornerPin.BottomLeft = savedCorners[3];
                onDone?.Invoke(result);
                IsRunning = false;
                yield break;
            }

            // mean reprojection error (camera-normalized -> projector-normalized)
            double err = 0;
            for (int i = 0; i < camPts.Count; i++)
            {
                err += Vector2.Distance(camToProj.Apply(camPts[i]), projPts[i]);
            }
            result.ReprojectionError = (float)(err / camPts.Count) * projW; // report in projector px

            // --- target corners (camera space, normalized) -> projector raster ---
            Vector2[] target = targetCornersCameraNorm ?? DefaultTargetFromCorrespondence(corr, camW, camH);
            surface.CornerPin.TopLeft = camToProj.Apply(target[0]);
            surface.CornerPin.TopRight = camToProj.Apply(target[1]);
            surface.CornerPin.BottomRight = camToProj.Apply(target[2]);
            surface.CornerPin.BottomLeft = camToProj.Apply(target[3]);

            result.Success = true;
            result.Message = $"Aligned {result.SurfaceId}: {result.ValidPixels} pts, reproj {result.ReprojectionError:F2}px.";
            onDone?.Invoke(result);
            IsRunning = false;
        }

        /// <summary>
        /// Fallback target when the caller supplies none: the bounding box of the lit
        /// region in camera space. Mapping this through the homography reproduces the
        /// current projection (identity check) — the real value comes from passing a
        /// deliberate target.
        /// </summary>
        private static Vector2[] DefaultTargetFromCorrespondence(PMSDKGrayCodeDecode.Correspondence[] corr, int camW, int camH)
        {
            float minX = 1, minY = 1, maxX = 0, maxY = 0;
            for (int y = 0; y < camH; y++)
            {
                for (int x = 0; x < camW; x++)
                {
                    if (!corr[y * camW + x].Valid) continue;
                    float nx = (x + 0.5f) / camW, ny = (y + 0.5f) / camH;
                    if (nx < minX) minX = nx; if (nx > maxX) maxX = nx;
                    if (ny < minY) minY = ny; if (ny > maxY) maxY = ny;
                }
            }
            // Camera luminance is top-left origin (y grows downward), so the smallest
            // normalized y is the visual TOP.
            return new[]
            {
                new Vector2(minX, minY), // TL
                new Vector2(maxX, minY), // TR
                new Vector2(maxX, maxY), // BR
                new Vector2(minX, maxY)  // BL
            };
        }

        private IEnumerator DisplayAndSettle(Texture2D tex, Material mat, byte[] pixels)
        {
            tex.SetPixelData(pixels, 0);
            tex.Apply(false);
            mat.SetTexture(MainTexId, tex);
            for (int i = 0; i < Mathf.Max(1, SettleFrames); i++)
            {
                yield return null;
            }
        }

        private static void FillPattern(byte[] buffer, byte value)
        {
            for (int i = 0; i < buffer.Length; i++) buffer[i] = value;
        }
    }
}
