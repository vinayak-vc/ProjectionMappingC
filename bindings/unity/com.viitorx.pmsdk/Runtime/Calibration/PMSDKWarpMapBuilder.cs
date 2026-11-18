using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>What a warp map makes "correct".</summary>
    public enum PMSDKWarpMapTarget
    {
        /// <summary>The content looks like one flat perspective picture from the camera's position (D-046, O1). Needs
        /// no depth; on a folded surface the physical cell size differs per facet.</summary>
        CameraPerspective = 0,
        /// <summary>Equal physical spacing along the surface: facets are measured with depth and unfolded about their
        /// folds, and the content is laid out uniformly in millimetres (D-051). Needs a depth camera.</summary>
        PhysicalSpacing = 1,
    }

    /// <summary>Outcome of building a warp map for one skin.</summary>
    public sealed class PMSDKWarpMapResult
    {
        public PMSDKSkin Skin;
        public bool Success;
        public string Message = "";
        /// <summary>Decoded camera samples used.</summary>
        public int Samples;
        /// <summary>Fraction of the map that is valid.</summary>
        public float Coverage;
        /// <summary>File written (bare name inside <see cref="PMSDKSkinStore.MasksFolder"/>).</summary>
        public string File;
        /// <summary>Checker score through the new map (when <see cref="PMSDKWarpMapBuilder.Verify"/> and a
        /// <see cref="PMSDKAutoAdjust"/> sits on the same GameObject; camera-perspective target only).</summary>
        public bool Verified;
        public PMSDKAutoAdjustScore Verification;
        /// <summary>Physical-spacing target: surface report through the first map and through the kept map.</summary>
        public PMSDKSurfaceReport SurfaceFirst;
        public PMSDKSurfaceReport Surface;
        /// <summary>Correction passes applied after the first map (physical-spacing target).</summary>
        public int RefinePassesUsed;
        /// <summary>True when the capture no longer fits the projector calibration (camera or projector moved): recalibrate.</summary>
        public bool CalibrationStale;
    }

    /// <summary>
    /// Auto Adjust Phase 3 (D-046, D-051): dense warp map for curved / folded skins. Projects Gray code through the
    /// skin's corner pin, decodes camera pixel -> content UV (= projector raster), and stores, per projector raster
    /// position, the content UV that must be shown there. "Correct" is chosen by <see cref="Target"/>:
    /// <see cref="PMSDKWarpMapTarget.CameraPerspective"/> (best-fit perspective picture in the undistorted camera) or
    /// <see cref="PMSDKWarpMapTarget.PhysicalSpacing"/> (uniform millimetres on the unfolded surface, measured with
    /// depth; the result is then measured through the map and corrected in closed loop). The map is applied in
    /// <c>PMSDK/UnlitWarp</c> (<see cref="PMSDKSkin.WarpMapFile"/>) on top of the corner pin.
    /// The caller records undo before starting.
    /// </summary>
    public class PMSDKWarpMapBuilder : MonoBehaviour
    {
        [SerializeField] private int patternResolution = 512;
        [SerializeField] private int mapSize = 256;
        [SerializeField] private float maxFillDistancePx = 12f;
        [SerializeField] private int settleFrames = 3;
        [SerializeField] private int minContrast = 25;
        [SerializeField] private int sampleStep = 2;
        [SerializeField] private int fitSamples = 6000;
        [SerializeField] private bool hideOtherSkins = true;
        [SerializeField] private bool verify = true;
        [SerializeField] private PMSDKWarpMapTarget target = PMSDKWarpMapTarget.CameraPerspective;
        [SerializeField, Tooltip("Physical-spacing target: content width / height. 0 stretches the content over the surface's extent.")]
        private float contentAspect = 0f;
        [SerializeField, Tooltip("Physical-spacing target: closed-loop correction passes after the first map.")]
        private int refinePasses = 1;
        [SerializeField, Tooltip("Grid cells per content side used by the surface report.")]
        private int reportCellsU = 16;
        [SerializeField] private int reportCellsV = 16;

        // A sample is a decode error when its UV differs from its camera neighbours by more than this many
        // typical neighbour steps.
        private const float NeighbourTolerance = 6f;
        // Trimmed extent of the unfolded surface (fractions), so stray samples do not stretch the content.
        private const float ExtentTrim = 0.002f;
        // A correction pass is kept only when it lowers the RMS by at least this fraction.
        private const float MinImprovement = 0.05f;

        private byte[] cameraRegion;
        private int cameraRegionWidth;
        // True while cameraRegion was derived from this run's own capture (cleared when the run ends).
        private bool regionDerived;
        // Median epipolar error (projector px) above which the projector calibration no longer fits the rig.
        private const float StaleCalibrationPx = 3f;
        private PMSDKSurfaceTolerance tolerance = PMSDKSurfaceTolerance.Default();

        public bool IsRunning { get; private set; }

        public string Status { get; private set; } = "";

        public event Action<string> StatusChanged;

        public int SettleFrames
        {
            get { return settleFrames; }
            set { settleFrames = Mathf.Max(0, value); }
        }

        /// <summary>Gray-code pattern size per side (texels).</summary>
        public int PatternResolution
        {
            get { return patternResolution; }
            set { patternResolution = Mathf.Clamp(value, 16, 2048); }
        }

        public int MapSize
        {
            get { return mapSize; }
            set { mapSize = Mathf.Clamp(value, 16, 2048); }
        }

        public bool Verify
        {
            get { return verify; }
            set { verify = value; }
        }

        public PMSDKWarpMapTarget Target
        {
            get { return target; }
            set { target = value; }
        }

        /// <summary>Physical-spacing target: content width / height (0 = stretch over the surface's extent).</summary>
        public float ContentAspect
        {
            get { return contentAspect; }
            set { contentAspect = Mathf.Max(0f, value); }
        }

        public int RefinePasses
        {
            get { return refinePasses; }
            set { refinePasses = Mathf.Clamp(value, 0, 5); }
        }

        /// <summary>Grid the surface report divides the content into (cells per side).</summary>
        public void SetReportGrid(int cellsU, int cellsV)
        {
            reportCellsU = Mathf.Max(1, cellsU);
            reportCellsV = Mathf.Max(1, cellsV);
        }

        public PMSDKSurfaceTolerance Tolerance
        {
            get { return tolerance; }
            set { tolerance = value; }
        }

        /// <summary>Folder for warp map files; null = <see cref="PMSDKSkinStore.MasksFolder"/> (stored as a bare
        /// name). A custom folder is stored as an absolute path in the skin.</summary>
        public string OutputFolder { get; set; }

        /// <summary>Folder for surface reports (text + diagnostic PNG); null = none written.</summary>
        public string ReportFolder { get; set; }

        /// <summary>
        /// Projector-camera calibration (D-052). When set and made with this camera's image size, its triangulated
        /// depth replaces the depth camera's stereo for the physical-spacing target and surface measurements; it is
        /// valid only while camera and projector stay where they were calibrated.
        /// </summary>
        public PMSDKProjectorCalibration ProjectorCalibration { get; set; }

        /// <summary>
        /// Restricts the map to camera pixels inside <paramref name="mask"/> (raw camera size, row 0 = top, values
        /// above 127 = the surface), e.g. the mark or cutout a skin was made from (D-049). Without it every lit
        /// pixel counts, so a reflection of the surface (glass, glossy floor) gives the same projector position two
        /// contradictory targets. Null clears the region.
        /// </summary>
        public void SetCameraRegion(byte[] mask, int width, int height)
        {
            if (mask != null && (width <= 0 || height <= 0 || mask.Length != width * height)) {
                throw new ArgumentException("The camera region size does not match its pixels.");
            }
            cameraRegion = mask;
            cameraRegionWidth = mask != null ? width : 0;
        }

        public IEnumerator BuildWarpMap(PMSDKSkin skin, IPMSDKCalibrationCamera camera, IList<PMSDKSkin> allSkins,
            Action<PMSDKWarpMapResult> onDone)
        {
            PMSDKWarpMapResult result = new PMSDKWarpMapResult { Skin = skin };
            string problem = Validate(skin, camera);
            if (problem != null) {
                result.Message = problem;
                onDone?.Invoke(result);
                yield break;
            }
            IsRunning = true;
            List<PMSDKSkin> displaySkins = new List<PMSDKSkin>();
            if (allSkins != null) {
                displaySkins.AddRange(allSkins);
            }
            PMSDKCalibrationDisplay display = new PMSDKCalibrationDisplay(displaySkins, skin.gameObject.layer);
            bool cameraStarted = false;
            try {
                do {
                    SetStatus("Starting camera...");
                    cameraStarted = camera.Begin();
                    if (!cameraStarted) {
                        result.Message = "The camera did not start." + PMSDKAutoAdjust.CameraError(camera);
                        break;
                    }
                    PMSDKCameraIntrinsics intrinsics = null;
                    IPMSDKCameraIntrinsicsSource source = camera as IPMSDKCameraIntrinsicsSource;
                    if (source != null) {
                        source.TryGetIntrinsics(out intrinsics);
                    }
                    if (hideOtherSkins) {
                        display.HideAllExcept(skin);
                    }

                    SetStatus("Projecting Gray code (" + PMSDKContentGrayCode.FrameCount(patternResolution) + " frames)...");
                    PMSDKGrayCaptureResult capture = null;
                    IEnumerator sequence = PMSDKContentGrayCode.Capture(skin, display, camera, patternResolution, settleFrames, minContrast,
                        sampleStep, false, delegate(PMSDKGrayCaptureResult r) { capture = r; });
                    while (sequence.MoveNext()) {
                        yield return sequence.Current;
                    }
                    if (capture == null || !capture.Success) {
                        result.Message = capture != null ? capture.Message : "Gray-code capture failed.";
                        break;
                    }

                    if (target == PMSDKWarpMapTarget.PhysicalSpacing) {
                        DeriveRegionIfMissing(capture);
                        string staleMessage;
                        if (IsCalibrationStale(skin, capture, intrinsics, camera, out staleMessage)) {
                            result.CalibrationStale = true;
                            result.Message = staleMessage;
                            break;
                        }
                    }

                    // Depth after the Gray code: the OAK's stereo depth needs a few seconds after start to settle.
                    PMSDKSurfaceGeometry geometry = null;
                    if (target == PMSDKWarpMapTarget.PhysicalSpacing) {
                        SetStatus("Measuring the surface with depth...");
                        string geometryError;
                        if (!TryMeasureGeometry(camera, intrinsics, out geometry, out geometryError)) {
                            result.Message = geometryError;
                            break;
                        }
                    }

                    string error;
                    MapSamples samples;
                    if (!TryComputeMap(skin, capture, intrinsics, geometry, result, out samples, out error)) {
                        result.Message = error;
                        break;
                    }
                    result.Success = true;
                    result.Message = "Warp map built from " + result.Samples + " samples (" + (result.Coverage * 100f).ToString("0") + " % of the map).";

                    if (geometry != null) {
                        IEnumerator loop = MeasureAndRefine(skin, display, camera, intrinsics, geometry, samples, result);
                        while (loop.MoveNext()) {
                            yield return loop.Current;
                        }
                    }
                } while (false);
            } finally {
                display.Dispose();
                if (cameraStarted) {
                    camera.End();
                }
                ClearDerivedRegion();
                IsRunning = false;
            }

            PMSDKAutoAdjust measurer = verify && result.Success && target == PMSDKWarpMapTarget.CameraPerspective
                ? GetComponent<PMSDKAutoAdjust>()
                : null;
            if (measurer != null && !measurer.IsRunning) {
                SetStatus("Verifying through the warp map...");
                bool measured = false;
                PMSDKAutoAdjustScore score = new PMSDKAutoAdjustScore();
                string verifyMessage = "";
                IEnumerator check = measurer.MeasureSkin(skin, camera, allSkins, delegate(bool ok, PMSDKAutoAdjustScore s, string m) {
                    measured = ok;
                    score = s;
                    verifyMessage = m;
                });
                while (check.MoveNext()) {
                    yield return check.Current;
                }
                result.Verified = measured;
                result.Verification = score;
                result.Message += measured ? " Check: " + verifyMessage : " Check failed: " + verifyMessage;
            }
            SetStatus(result.Message);
            onDone?.Invoke(result);
        }

        /// <summary>
        /// Measures the skin as it is projected now (through its warp map, if any) on the physical surface and
        /// reports the grid errors in millimetres (D-051). Needs a depth camera with intrinsics.
        /// </summary>
        public IEnumerator MeasureSurface(PMSDKSkin skin, IPMSDKCalibrationCamera camera, IList<PMSDKSkin> allSkins,
            Action<PMSDKSurfaceReport> onDone)
        {
            PMSDKSurfaceReport report = new PMSDKSurfaceReport();
            string problem = Validate(skin, camera, PMSDKWarpMapTarget.PhysicalSpacing);
            if (problem != null) {
                report.Message = problem;
                onDone?.Invoke(report);
                yield break;
            }
            IsRunning = true;
            List<PMSDKSkin> displaySkins = new List<PMSDKSkin>();
            if (allSkins != null) {
                displaySkins.AddRange(allSkins);
            }
            PMSDKCalibrationDisplay display = new PMSDKCalibrationDisplay(displaySkins, skin.gameObject.layer);
            bool cameraStarted = false;
            try {
                do {
                    SetStatus("Starting camera...");
                    cameraStarted = camera.Begin();
                    if (!cameraStarted) {
                        report.Message = "The camera did not start." + PMSDKAutoAdjust.CameraError(camera);
                        break;
                    }
                    PMSDKCameraIntrinsics intrinsics;
                    ((IPMSDKCameraIntrinsicsSource)camera).TryGetIntrinsics(out intrinsics);
                    if (hideOtherSkins) {
                        display.HideAllExcept(skin);
                    }
                    PMSDKGrayCaptureResult capture = null;
                    PMSDKSurfaceGeometry geometry = null;
                    IEnumerator measure = MeasureThroughMap(skin, display, camera, intrinsics, null,
                        delegate(PMSDKGrayCaptureResult c, PMSDKSurfaceReport r, PMSDKSurfaceGeometry g) {
                            capture = c;
                            report = r;
                            geometry = g;
                        });
                    while (measure.MoveNext()) {
                        yield return measure.Current;
                    }
                    WriteReport(skin, "measure", report, capture, geometry, intrinsics);
                } while (false);
            } finally {
                display.Dispose();
                if (cameraStarted) {
                    camera.End();
                }
                ClearDerivedRegion();
                IsRunning = false;
            }
            SetStatus(report.Valid ? report.Summary() : report.Message);
            onDone?.Invoke(report);
        }

        /// <summary>
        /// Turns camera -> content correspondences into the skin's warp map file (camera-perspective target, or
        /// physical spacing when <paramref name="geometry"/> is given). Public for tests and for hosts that capture
        /// correspondences another way.
        /// </summary>
        public bool TryComputeMap(PMSDKSkin skin, PMSDKGrayCaptureResult capture, PMSDKCameraIntrinsics intrinsics,
            PMSDKWarpMapResult result, out string error)
        {
            MapSamples samples;
            return TryComputeMap(skin, capture, intrinsics, null, result, out samples, out error);
        }

        /// <summary>Samples behind a built map, kept for closed-loop correction.</summary>
        public sealed class MapSamples
        {
            public Vector2[] RawPixels;
            public Vector2[] Raster;
            public Vector2[] Target;
            public int CameraWidth;
            public Rect Rect;
            public float[] Rgba;
            /// <summary>Raster positions of samples that lie on another surface (blanked in the map).</summary>
            public Vector2[] OffSurfaceRaster = new Vector2[0];
        }

        public bool TryComputeMap(PMSDKSkin skin, PMSDKGrayCaptureResult capture, PMSDKCameraIntrinsics intrinsics, PMSDKSurfaceGeometry geometry,
            PMSDKWarpMapResult result, out MapSamples samples, out string error)
        {
            samples = null;
            List<Vector2> cameraPixels;
            List<Vector2> contentUvs;
            SelectSamples(capture, out cameraPixels, out contentUvs);
            int count = cameraPixels.Count;
            if (count < 16) {
                error = "Too few decoded samples (" + count + (cameraRegion != null ? " inside the camera region" : "") + ").";
                return false;
            }
            Vector2[] camera;
            if (!PMSDKAutoAdjustNative.TryUndistort(cameraPixels.ToArray(), intrinsics, out camera, out error)) {
                return false;
            }
            Vector2[] currentUv = contentUvs.ToArray();

            Vector2[] targetUv;
            Vector2[] rawPixels = cameraPixels.ToArray();
            Vector2[] offSurfaceUv = new Vector2[0];
            if (geometry != null) {
                if (!TryPhysicalTargets(geometry, ref rawPixels, ref camera, ref currentUv, out targetUv, out offSurfaceUv, out error)) {
                    return false;
                }
            } else if (!TryPerspectiveTargets(camera, currentUv, out targetUv, out error)) {
                return false;
            }
            count = targetUv.Length;

            // Where each sample is on the projector now (corner pin + correction, without the map).
            Vector2[] raster = ContentToRaster(skin, currentUv);
            Rect rect = RasterRect(raster);
            float[] rgba;
            float coverage;
            if (!PMSDKAutoAdjustNative.TryBuildWarpMap(raster, targetUv, rect, mapSize, mapSize, maxFillDistancePx, out rgba, out coverage, out error)) {
                return false;
            }
            Vector2[] offSurfaceRaster = offSurfaceUv.Length > 0 ? ContentToRaster(skin, offSurfaceUv) : new Vector2[0];
            BlankOffSurface(rgba, mapSize, rect, raster, offSurfaceRaster);
            string fileName;
            if (!TrySaveMap(skin, rect, rgba, out fileName, out error)) {
                return false;
            }
            result.Samples = count;
            result.Coverage = coverage;
            result.File = fileName;
            samples = new MapSamples {
                RawPixels = rawPixels, Raster = raster, Target = targetUv, CameraWidth = capture.CameraWidth, Rect = rect, Rgba = rgba,
                OffSurfaceRaster = offSurfaceRaster,
            };
            error = "";
            return true;
        }

        /// <summary>
        /// Closed loop (physical-spacing target): measure through the map; while it fails and passes remain, shift
        /// each raster position's content UV by (target - shown) and rebuild. A pass that does not lower the RMS by
        /// <see cref="MinImprovement"/> is undone and the loop stops, so it converges instead of oscillating.
        /// </summary>
        private IEnumerator MeasureAndRefine(PMSDKSkin skin, PMSDKCalibrationDisplay display, IPMSDKCalibrationCamera camera,
            PMSDKCameraIntrinsics intrinsics, PMSDKSurfaceGeometry geometry, MapSamples samples, PMSDKWarpMapResult result)
        {
            PMSDKGrayCaptureResult shown = null;
            PMSDKSurfaceReport report = null;
            IEnumerator measure = MeasureThroughMap(skin, display, camera, intrinsics, geometry, delegate(PMSDKGrayCaptureResult c, PMSDKSurfaceReport r, PMSDKSurfaceGeometry g) {
                shown = c;
                report = r;
            });
            while (measure.MoveNext()) {
                yield return measure.Current;
            }
            result.SurfaceFirst = report;
            result.Surface = report;
            WriteReport(skin, "build", report, shown, geometry, intrinsics);
            MapSamples best = samples;
            for (int pass = 0; pass < refinePasses && report != null && report.Valid && !report.Pass && shown != null && shown.Success; pass++) {
                Vector2[] corrected = CorrectTargets(best, shown);
                float[] rgba;
                float coverage;
                string error;
                if (!PMSDKAutoAdjustNative.TryBuildWarpMap(best.Raster, corrected, best.Rect, mapSize, mapSize, maxFillDistancePx, out rgba, out coverage, out error)) {
                    break;
                }
                BlankOffSurface(rgba, mapSize, best.Rect, best.Raster, best.OffSurfaceRaster);
                string fileName;
                if (!TrySaveMap(skin, best.Rect, rgba, out fileName, out error)) {
                    break;
                }
                SetStatus("Correction pass " + (pass + 1) + ": measuring...");
                PMSDKGrayCaptureResult nextShown = null;
                PMSDKSurfaceReport next = null;
                measure = MeasureThroughMap(skin, display, camera, intrinsics, geometry, delegate(PMSDKGrayCaptureResult c, PMSDKSurfaceReport r, PMSDKSurfaceGeometry g) {
                    nextShown = c;
                    next = r;
                });
                while (measure.MoveNext()) {
                    yield return measure.Current;
                }
                if (next == null || !next.Valid || next.RmsMm > report.RmsMm * (1f - MinImprovement)) {
                    // No meaningful improvement: restore the previous map and stop.
                    TrySaveMap(skin, best.Rect, best.Rgba, out fileName, out error);
                    break;
                }
                best = new MapSamples {
                    RawPixels = best.RawPixels, Raster = best.Raster, Target = best.Target, CameraWidth = best.CameraWidth, Rect = best.Rect, Rgba = rgba,
                    OffSurfaceRaster = best.OffSurfaceRaster,
                };
                result.Coverage = coverage;
                result.RefinePassesUsed = pass + 1;
                shown = nextShown;
                report = next;
                result.Surface = report;
                WriteReport(skin, "pass" + (pass + 1), report, shown, geometry, intrinsics);
            }
            if (report != null) {
                result.Message += " Surface: " + (report.Valid ? report.Summary() : report.Message);
                if (result.RefinePassesUsed > 0 && result.SurfaceFirst != null && result.SurfaceFirst.Valid) {
                    result.Message += " (first map RMS " + result.SurfaceFirst.RmsMm.ToString("0.00") + " mm, " + result.RefinePassesUsed + " correction pass(es))";
                }
            }
        }

        /// <summary>New content UV per raster sample: the map's current value plus what is still missing at that
        /// camera pixel (target - shown).</summary>
        public static Vector2[] CorrectTargets(MapSamples samples, PMSDKGrayCaptureResult shown)
        {
            Dictionary<int, Vector2> shownByPixel = new Dictionary<int, Vector2>(shown.CameraPixels.Count);
            for (int i = 0; i < shown.CameraPixels.Count; i++) {
                Vector2 p = shown.CameraPixels[i];
                shownByPixel[(int)p.y * samples.CameraWidth + (int)p.x] = shown.ContentUvs[i];
            }
            Vector2[] corrected = new Vector2[samples.Raster.Length];
            for (int i = 0; i < corrected.Length; i++) {
                Vector2 p = samples.RawPixels[i];
                Vector2 seen;
                Vector2 current;
                if (shownByPixel.TryGetValue((int)p.y * samples.CameraWidth + (int)p.x, out seen) &&
                    TrySampleMap(samples.Rgba, MapSizeOf(samples), samples.Rect, samples.Raster[i], out current)) {
                    corrected[i] = current + (samples.Target[i] - seen);
                } else {
                    corrected[i] = samples.Target[i];
                }
            }
            return corrected;
        }

        private static int MapSizeOf(MapSamples samples)
        {
            return Mathf.RoundToInt(Mathf.Sqrt(samples.Rgba.Length / 4f));
        }

        /// <summary>Bilinear map lookup with the shader's validity rule (RG = uv * a, A = valid).</summary>
        public static bool TrySampleMap(float[] rgba, int size, Rect rect, Vector2 raster, out Vector2 uv)
        {
            uv = Vector2.zero;
            float fx = (raster.x - rect.x) / rect.width * size - 0.5f;
            float fy = (raster.y - rect.y) / rect.height * size - 0.5f;
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;
            Vector2 sum = Vector2.zero;
            float a = 0f;
            for (int j = 0; j < 2; j++) {
                for (int i = 0; i < 2; i++) {
                    int px = Mathf.Clamp(x0 + i, 0, size - 1);
                    int py = Mathf.Clamp(y0 + j, 0, size - 1);
                    float w = (i == 0 ? 1f - tx : tx) * (j == 0 ? 1f - ty : ty);
                    int k = (py * size + px) * 4;
                    sum += new Vector2(rgba[k], rgba[k + 1]) * w;
                    a += rgba[k + 3] * w;
                }
            }
            if (a < 0.5f) {
                return false;
            }
            uv = sum / a;
            return true;
        }

        /// <summary>Gray code through the map, then (when <paramref name="geometry"/> is null) depth, then the report.</summary>
        private IEnumerator MeasureThroughMap(PMSDKSkin skin, PMSDKCalibrationDisplay display, IPMSDKCalibrationCamera camera,
            PMSDKCameraIntrinsics intrinsics, PMSDKSurfaceGeometry geometry, Action<PMSDKGrayCaptureResult, PMSDKSurfaceReport, PMSDKSurfaceGeometry> onDone)
        {
            SetStatus("Measuring through the warp map (" + PMSDKContentGrayCode.FrameCount(patternResolution) + " frames)...");
            PMSDKGrayCaptureResult capture = null;
            IEnumerator sequence = PMSDKContentGrayCode.Capture(skin, display, camera, patternResolution, settleFrames, minContrast, sampleStep, true,
                delegate(PMSDKGrayCaptureResult r) { capture = r; });
            while (sequence.MoveNext()) {
                yield return sequence.Current;
            }
            PMSDKSurfaceReport report = new PMSDKSurfaceReport();
            if (capture == null || !capture.Success) {
                report.Message = capture != null ? capture.Message : "Gray-code capture failed.";
            } else {
                string error = "";
                DeriveRegionIfMissing(capture);
                if (geometry == null) {
                    SetStatus("Measuring the surface with depth...");
                    TryMeasureGeometry(camera, intrinsics, out geometry, out error);
                }
                if (geometry == null) {
                    report.Message = error;
                } else {
                    report = EvaluateSurface(capture, intrinsics, geometry);
                }
            }
            onDone(capture, report, geometry);
        }

        /// <summary>Surface report for camera -> shown content UV samples (region and decode-error filters applied).</summary>
        public PMSDKSurfaceReport EvaluateSurface(PMSDKGrayCaptureResult capture, PMSDKCameraIntrinsics intrinsics, PMSDKSurfaceGeometry geometry)
        {
            List<Vector2> pixels;
            List<Vector2> uvs;
            SelectSamples(capture, out pixels, out uvs);
            Vector2[] undistorted;
            string error;
            PMSDKSurfaceReport report = new PMSDKSurfaceReport();
            if (!PMSDKAutoAdjustNative.TryUndistort(pixels.ToArray(), intrinsics, out undistorted, out error)) {
                report.Message = error;
                return report;
            }
            List<Vector2> raw = new List<Vector2>(pixels.Count);
            List<Vector2> surface = new List<Vector2>(pixels.Count);
            List<Vector2> shownUv = new List<Vector2>(pixels.Count);
            List<int> planes = new List<int>(pixels.Count);
            for (int i = 0; i < pixels.Count; i++) {
                Vector2 mm;
                int plane;
                if (geometry.TryToSurface(pixels[i], undistorted[i], out mm, out plane)) {
                    raw.Add(pixels[i]);
                    surface.Add(mm);
                    shownUv.Add(uvs[i]);
                    planes.Add(plane);
                }
            }
            return PMSDKSurfaceMetrics.Evaluate(raw, surface, shownUv, planes, geometry, reportCellsU, reportCellsV, tolerance);
        }

        private bool TryMeasureGeometry(IPMSDKCalibrationCamera camera, PMSDKCameraIntrinsics intrinsics, out PMSDKSurfaceGeometry geometry, out string error)
        {
            geometry = null;
            if (intrinsics == null) {
                error = "Physical spacing needs the camera intrinsics.";
                return false;
            }
            ushort[] depth;
            string source;
            if (UsesProjectorCalibration(camera)) {
                depth = (ushort[])ProjectorCalibration.Depth.Clone();
                source = "projector triangulation";
            } else {
                IPMSDKDepthSource depthSource = camera as IPMSDKDepthSource;
                if (depthSource == null || !depthSource.HasDepth) {
                    error = "Physical spacing needs depth: calibrate the projector, or start the OAK-D with depth enabled.";
                    return false;
                }
                depth = depthSource.CaptureDepth();
                if (depth == null) {
                    error = "No depth frame." + PMSDKAutoAdjust.CameraError(camera);
                    return false;
                }
                source = "depth camera";
            }
            byte[] region = cameraRegion != null && cameraRegion.Length == camera.Width * camera.Height ? cameraRegion : null;
            if (region != null) {
                // Look for facets of this surface only, not the rest of the room.
                for (int i = 0; i < depth.Length; i++) {
                    if (region[i] <= 127) {
                        depth[i] = 0;
                    }
                }
            }
            List<PMSDKDetectedPlane> planes;
            if (!PMSDKAutoAdjustNative.TryDetectPlanes(depth, camera.Width, camera.Height, intrinsics, out planes, out error)) {
                error = "No flat surface found in the " + source + " depth (" + error + ").";
                return false;
            }
            // Triangulated depth is precise enough to place pixels at their measured points; OAK stereo is not.
            return PMSDKSurfaceGeometry.TryBuild(planes, intrinsics, camera.Width, camera.Height, depth, region, UsesProjectorCalibration(camera),
                out geometry, out error);
        }

        private bool UsesProjectorCalibration(IPMSDKCalibrationCamera camera)
        {
            return ProjectorCalibration != null && ProjectorCalibration.Depth != null && camera != null &&
                   ProjectorCalibration.CameraWidth == camera.Width && ProjectorCalibration.CameraHeight == camera.Height;
        }

        /// <summary>Region filter, then drops isolated decode errors: a sample must agree with at least two of its
        /// camera neighbours (or with every neighbour it has, at the region border).</summary>
        private void SelectSamples(PMSDKGrayCaptureResult capture, out List<Vector2> pixels, out List<Vector2> uvs)
        {
            List<Vector2> regionPixels = new List<Vector2>(capture.CameraPixels.Count);
            List<Vector2> regionUvs = new List<Vector2>(capture.CameraPixels.Count);
            for (int i = 0; i < capture.CameraPixels.Count; i++) {
                Vector2 pixel = capture.CameraPixels[i];
                if (cameraRegion != null) {
                    int index = (int)pixel.y * cameraRegionWidth + (int)pixel.x;
                    if (index < 0 || index >= cameraRegion.Length || cameraRegion[index] <= 127) {
                        continue;
                    }
                }
                regionPixels.Add(pixel);
                regionUvs.Add(capture.ContentUvs[i]);
            }
            int width = Mathf.Max(1, capture.CameraWidth);
            Dictionary<int, int> byPixel = new Dictionary<int, int>(regionPixels.Count);
            for (int i = 0; i < regionPixels.Count; i++) {
                byPixel[(int)regionPixels[i].y * width + (int)regionPixels[i].x] = i;
            }
            int step = DetectStep(regionPixels);
            List<float> steps = new List<float>();
            for (int i = 0; i < regionPixels.Count; i += 7) {
                int n;
                if (byPixel.TryGetValue(((int)regionPixels[i].y) * width + (int)regionPixels[i].x + step, out n)) {
                    steps.Add((regionUvs[i] - regionUvs[n]).magnitude);
                }
            }
            if (steps.Count < 10) {
                pixels = regionPixels;
                uvs = regionUvs;
                return;
            }
            steps.Sort();
            float limit = Mathf.Max(steps[steps.Count / 2] * NeighbourTolerance, 1e-4f);
            pixels = new List<Vector2>(regionPixels.Count);
            uvs = new List<Vector2>(regionPixels.Count);
            int[] dx = { step, -step, 0, 0 };
            int[] dy = { 0, 0, step, -step };
            for (int i = 0; i < regionPixels.Count; i++) {
                int x = (int)regionPixels[i].x;
                int y = (int)regionPixels[i].y;
                int available = 0;
                int agree = 0;
                for (int k = 0; k < 4; k++) {
                    int nx = x + dx[k];
                    int ny = y + dy[k];
                    int n;
                    if (nx < 0 || nx >= width || !byPixel.TryGetValue(ny * width + nx, out n)) {
                        continue;
                    }
                    available++;
                    if ((regionUvs[i] - regionUvs[n]).magnitude <= limit) {
                        agree++;
                    }
                }
                if (available > 0 && agree >= Mathf.Min(2, available)) {
                    pixels.Add(regionPixels[i]);
                    uvs.Add(regionUvs[i]);
                }
            }
        }

        private static int DetectStep(List<Vector2> pixels)
        {
            int step = int.MaxValue;
            for (int i = 1; i < pixels.Count && i < 2000; i++) {
                if (Mathf.Approximately(pixels[i].y, pixels[i - 1].y)) {
                    int d = Mathf.RoundToInt(pixels[i].x - pixels[i - 1].x);
                    if (d > 0) {
                        step = Mathf.Min(step, d);
                    }
                }
            }
            return step == int.MaxValue ? 1 : step;
        }

        /// <summary>"Correct" = the best-fit perspective image of the content in the camera (D-046).</summary>
        private bool TryPerspectiveTargets(Vector2[] camera, Vector2[] currentUv, out Vector2[] targetUv, out string error)
        {
            targetUv = null;
            int count = camera.Length;
            List<Vector2> fitCamera = new List<Vector2>();
            List<Vector2> fitUv = new List<Vector2>();
            int stride = Mathf.Max(1, count / Mathf.Max(16, fitSamples));
            for (int i = 0; i < count; i += stride) {
                fitCamera.Add(camera[i]);
                fitUv.Add(currentUv[i]);
            }
            PMSDKHomography cameraToUv = PMSDKHomography.FitNormalized(fitCamera, fitUv);
            if (!cameraToUv.Valid) {
                error = "Could not fit the target perspective to the samples.";
                return false;
            }
            targetUv = new Vector2[count];
            for (int i = 0; i < count; i++) {
                targetUv[i] = cameraToUv.Apply(camera[i]);
            }
            error = "";
            return true;
        }

        /// <summary>
        /// "Correct" = uniform millimetres along the unfolded surface (D-051): the content spans the surface's
        /// trimmed extent (or a rectangle of <see cref="ContentAspect"/> covering it, centred), u along the unfolded
        /// x axis (camera-horizontal on the root facet), v up. Samples on no facet are dropped.
        /// </summary>
        private bool TryPhysicalTargets(PMSDKSurfaceGeometry geometry, ref Vector2[] rawPixels, ref Vector2[] camera, ref Vector2[] currentUv,
            out Vector2[] targetUv, out Vector2[] offSurfaceUv, out string error)
        {
            targetUv = null;
            List<Vector2> rejectedUv = new List<Vector2>();
            offSurfaceUv = new Vector2[0];
            List<Vector2> keptRaw = new List<Vector2>(rawPixels.Length);
            List<Vector2> keptCamera = new List<Vector2>(rawPixels.Length);
            List<Vector2> keptUv = new List<Vector2>(rawPixels.Length);
            List<Vector2> surface = new List<Vector2>(rawPixels.Length);
            for (int i = 0; i < rawPixels.Length; i++) {
                Vector2 mm;
                int plane;
                if (geometry.TryToSurface(rawPixels[i], camera[i], out mm, out plane)) {
                    keptRaw.Add(rawPixels[i]);
                    keptCamera.Add(camera[i]);
                    keptUv.Add(currentUv[i]);
                    surface.Add(mm);
                } else {
                    rejectedUv.Add(currentUv[i]);
                }
            }
            offSurfaceUv = rejectedUv.ToArray();
            if (surface.Count < 16) {
                error = "Only " + surface.Count + " samples lie on a measured facet (is the surface inside the depth camera's range?).";
                return false;
            }
            List<float> xs = new List<float>(surface.Count);
            List<float> ys = new List<float>(surface.Count);
            foreach (Vector2 mm in surface) {
                xs.Add(mm.x);
                ys.Add(mm.y);
            }
            xs.Sort();
            ys.Sort();
            int low = Mathf.FloorToInt(ExtentTrim * (xs.Count - 1));
            int high = Mathf.CeilToInt((1f - ExtentTrim) * (xs.Count - 1));
            float x0 = xs[low];
            float x1 = xs[high];
            float y0 = ys[low];
            float y1 = ys[high];
            float width = Mathf.Max(1e-3f, x1 - x0);
            float height = Mathf.Max(1e-3f, y1 - y0);
            if (contentAspect > 0f) {
                // Cover the surface with a rectangle of the content's aspect, centred: square content cells stay square.
                if (width / height > contentAspect) {
                    float h = width / contentAspect;
                    y0 -= (h - height) * 0.5f;
                    height = h;
                } else {
                    float w = height * contentAspect;
                    x0 -= (w - width) * 0.5f;
                    width = w;
                }
            }
            targetUv = new Vector2[surface.Count];
            for (int i = 0; i < surface.Count; i++) {
                targetUv[i] = new Vector2((surface[i].x - x0) / width, (surface[i].y - y0) / height);
            }
            rawPixels = keptRaw.ToArray();
            camera = keptCamera.ToArray();
            currentUv = keptUv.ToArray();
            error = "";
            return true;
        }

        /// <summary>
        /// Marks map texels invalid where samples lie on another surface (e.g. the side face below a fold) and no
        /// surface sample does, so the gap filling does not spread content there and the shader leaves it dark.
        /// </summary>
        public static void BlankOffSurface(float[] rgba, int size, Rect rect, Vector2[] surfaceRaster, Vector2[] offSurfaceRaster)
        {
            if (offSurfaceRaster == null || offSurfaceRaster.Length == 0) {
                return;
            }
            int[] on = new int[size * size];
            int[] off = new int[size * size];
            Count(surfaceRaster, size, rect, on);
            Count(offSurfaceRaster, size, rect, off);
            for (int i = 0; i < on.Length; i++) {
                if (off[i] > on[i]) {
                    rgba[i * 4] = 0f;
                    rgba[i * 4 + 1] = 0f;
                    rgba[i * 4 + 2] = 0f;
                    rgba[i * 4 + 3] = 0f;
                }
            }
        }

        private static void Count(Vector2[] raster, int size, Rect rect, int[] counts)
        {
            foreach (Vector2 r in raster) {
                int x = Mathf.FloorToInt((r.x - rect.x) / rect.width * size);
                int y = Mathf.FloorToInt((r.y - rect.y) / rect.height * size);
                if (x >= 0 && y >= 0 && x < size && y < size) {
                    counts[y * size + x]++;
                }
            }
        }

        private static bool IsPolygonKind(PMSDKSkin skin)
        {
            return skin.Kind == PMSDKSkinShapeKind.Polygon || skin.Kind == PMSDKSkinShapeKind.Freeform;
        }

        /// <summary>
        /// Projector raster position of content UVs on <paramref name="skin"/>: the corner pin (+ correction) for quad
        /// kinds; for Polygon / Freeform skins the mesh UV is the outline's bounding box in raster space (D-055).
        /// </summary>
        public static Vector2[] ContentToRaster(PMSDKSkin skin, Vector2[] uvs)
        {
            if (!IsPolygonKind(skin)) {
                return PMSDKAutoAdjustNative.EvaluateWarpMany(skin.Vertices, skin.CorrectionColumns, skin.CorrectionRows, skin.GetCorrectionOffsets(), uvs);
            }
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector2 v in skin.Vertices) {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            Vector2 size = Vector2.Max(max - min, new Vector2(1e-6f, 1e-6f));
            Vector2[] raster = new Vector2[uvs.Length];
            for (int i = 0; i < uvs.Length; i++) {
                raster[i] = min + Vector2.Scale(uvs[i], size);
            }
            return raster;
        }

        /// <summary>
        /// Without a marked camera region, the skin's own capture defines it: the largest connected piece of camera
        /// pixels the skin lit (drops reflections and stray decodes). Kept until the run ends.
        /// </summary>
        private void DeriveRegionIfMissing(PMSDKGrayCaptureResult capture)
        {
            if (cameraRegion != null || capture == null || !capture.Success) {
                return;
            }
            int width = capture.CameraWidth;
            int height = capture.CameraHeight;
            byte[] region = new byte[width * height];
            int step = Mathf.Max(1, sampleStep);
            foreach (Vector2 pixel in capture.CameraPixels) {
                int x0 = (int)pixel.x;
                int y0 = (int)pixel.y;
                for (int y = y0; y < Mathf.Min(height, y0 + step); y++) {
                    for (int x = x0; x < Mathf.Min(width, x0 + step); x++) {
                        region[y * width + x] = 255;
                    }
                }
            }
            if (PMSDKSurfaceFinder.KeepLargestPiece(region, width, height) > 0) {
                cameraRegion = region;
                cameraRegionWidth = width;
                regionDerived = true;
            }
        }

        private void ClearDerivedRegion()
        {
            if (regionDerived) {
                cameraRegion = null;
                cameraRegionWidth = 0;
                regionDerived = false;
            }
        }

        /// <summary>True when this skin's correspondences no longer fit the projector calibration (rig moved).</summary>
        private bool IsCalibrationStale(PMSDKSkin skin, PMSDKGrayCaptureResult capture, PMSDKCameraIntrinsics intrinsics, IPMSDKCalibrationCamera camera,
            out string message)
        {
            message = "";
            if (!UsesProjectorCalibration(camera) || intrinsics == null) {
                return false;
            }
            List<Vector2> pixels;
            List<Vector2> uvs;
            SelectSamples(capture, out pixels, out uvs);
            if (pixels.Count < 100) {
                return false;
            }
            Vector2[] raster = ContentToRaster(skin, uvs.ToArray());
            float error = ProjectorCalibration.MedianEpipolarErrorPx(pixels, raster, intrinsics);
            if (error <= StaleCalibrationPx) {
                return false;
            }
            message = "The camera or projector moved since the projector calibration (epipolar error " + error.ToString("0.0") +
                      " px): calibrate the projector again.";
            return true;
        }

        private static Rect RasterRect(Vector2[] raster)
        {
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            foreach (Vector2 r in raster) {
                minX = Mathf.Min(minX, r.x);
                minY = Mathf.Min(minY, r.y);
                maxX = Mathf.Max(maxX, r.x);
                maxY = Mathf.Max(maxY, r.y);
            }
            float padX = Mathf.Max(1e-3f, (maxX - minX) * 0.02f);
            float padY = Mathf.Max(1e-3f, (maxY - minY) * 0.02f);
            return new Rect(minX - padX, minY - padY, (maxX - minX) + 2f * padX, (maxY - minY) + 2f * padY);
        }

        private bool TrySaveMap(PMSDKSkin skin, Rect rect, float[] rgba, out string fileName, out string error)
        {
            fileName = SanitizeId(skin.SkinId) + "_warp.pmwm";
            string stored = string.IsNullOrEmpty(OutputFolder) ? fileName : System.IO.Path.Combine(OutputFolder, fileName);
            try {
                PMSDKWarpMapFile.Save(PMSDKSkinStore.ResolveMaskPath(stored), mapSize, mapSize, rect, rgba);
            } catch (Exception ex) {
                error = "Could not save the warp map: " + ex.Message;
                return false;
            }
            fileName = stored;
            skin.WarpMapFile = fileName;
            skin.ReloadWarpMap();
            error = "";
            return true;
        }

        private void WriteReport(PMSDKSkin skin, string stage, PMSDKSurfaceReport report, PMSDKGrayCaptureResult capture, PMSDKSurfaceGeometry geometry,
            PMSDKCameraIntrinsics intrinsics)
        {
            if (string.IsNullOrEmpty(ReportFolder) || report == null) {
                return;
            }
            try {
                System.IO.Directory.CreateDirectory(ReportFolder);
                string stem = System.IO.Path.Combine(ReportFolder, SanitizeId(skin.SkinId) + "_" + stage);
                System.IO.File.WriteAllText(stem + ".txt", report.ToText());
                if (report.Valid && capture != null && capture.White != null) {
                    List<Vector2> pixels;
                    List<Vector2> uvs;
                    SelectSamples(capture, out pixels, out uvs);
                    PMSDKSurfaceDiagnostics.WritePng(stem + ".png", capture.White, capture.CameraWidth, capture.CameraHeight, geometry, intrinsics,
                        pixels, uvs, report);
                }
            } catch (Exception ex) {
                Debug.LogWarning("[PMSDK] Could not write the surface report: " + ex.Message);
            }
        }

        private string Validate(PMSDKSkin skin, IPMSDKCalibrationCamera camera)
        {
            return Validate(skin, camera, target);
        }

        private string Validate(PMSDKSkin skin, IPMSDKCalibrationCamera camera, PMSDKWarpMapTarget mode)
        {
            if (IsRunning) {
                return "A warp map is already being built.";
            }
            if (skin == null) {
                return "No skin selected.";
            }
            if (skin.Vertices == null || skin.Vertices.Count < (IsPolygonKind(skin) ? 3 : 4)) {
                return "The skin has no valid outline.";
            }
            if (camera == null) {
                return "No calibration camera.";
            }
            if (!PMSDKAutoAdjustNative.IsAvailable()) {
                return "ProjectionMappingSDK.dll has no Auto Adjust support (update the native plugin).";
            }
            if (mode == PMSDKWarpMapTarget.PhysicalSpacing && !(camera is IPMSDKCameraIntrinsicsSource)) {
                return "Physical spacing needs a camera with intrinsics (OAK-D).";
            }
            if (mode == PMSDKWarpMapTarget.PhysicalSpacing && ProjectorCalibration == null && !(camera is IPMSDKDepthSource)) {
                return "Physical spacing needs depth: calibrate the projector, or use a depth camera.";
            }
            return null;
        }

        private static string SanitizeId(string id)
        {
            string text = string.IsNullOrEmpty(id) ? "skin" : id;
            char[] chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++) {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }

        private void SetStatus(string text)
        {
            Status = text;
            StatusChanged?.Invoke(text);
        }
    }
}
