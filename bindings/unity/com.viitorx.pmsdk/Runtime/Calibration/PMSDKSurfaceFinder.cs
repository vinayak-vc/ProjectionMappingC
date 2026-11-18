using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>Outcome of an automatic surface search.</summary>
    public sealed class PMSDKSurfaceFinderResult
    {
        public bool Success;
        public string Message = "";
        /// <summary>Every plane the depth camera found (camera space).</summary>
        public List<PMSDKDetectedPlane> Planes = new List<PMSDKDetectedPlane>();
        /// <summary>Skins created, one per usable plane.</summary>
        public List<PMSDKSkin> Created = new List<PMSDKSkin>();
        /// <summary>Why planes were skipped (not lit by the projector, too oblique, ...).</summary>
        public List<string> Skipped = new List<string>();
    }

    /// <summary>
    /// Auto Adjust Phase 4 (D-047): finds flat surfaces automatically with a depth camera (OAK-D) and
    /// creates one Rectangle skin per surface the projector can reach.
    ///
    /// 1. A temporary full-raster skin shows Gray code: every lit camera pixel learns its projector
    ///    raster position (dense camera -> projector map, folds included).
    /// 2. Stereo depth (aligned to the colour camera) is split into planes (native RANSAC).
    /// 3. Per plane: the decoded pixels inside its outline give that plane's camera -> projector
    ///    homography (exact for a flat surface after lens undistortion); its quad corners become the
    ///    new skin's corners, and its measured size becomes the skin's facet size (SURFACE-METRIC).
    /// The caller records undo before starting. Skins are then refined with <see cref="PMSDKAutoAdjust"/>.
    /// </summary>
    public class PMSDKSurfaceFinder : MonoBehaviour
    {
        /// <summary>Id of the temporary full-screen skin used while mapping / calibrating; never saved or loaded.</summary>
        public const string ProbeId = "pmsdk-surface-probe";

        /// <summary>True for skins that exist only during a calibration run (they must not end up in a skin set).</summary>
        public static bool IsTemporarySkin(string skinId)
        {
            return skinId == ProbeId;
        }
        private const int MaxMaskSize = 1024;
        // Footprint islands smaller than this fraction of the largest are Gray-code bit errors, not surface.
        private const float MinIslandFraction = 0.02f;

        [SerializeField] private int patternResolution = 512;
        [SerializeField] private int settleFrames = 3;
        [SerializeField] private int minContrast = 25;
        [SerializeField] private int sampleStep = 2;
        [SerializeField] private int minLitSamples = 150;
        [SerializeField] private float maxViewAngleDeg = 75f;
        [SerializeField] private string namePrefix = "Surface";

        public bool IsRunning { get; private set; }

        public string Status { get; private set; } = "";

        public event Action<string> StatusChanged;

        public int SettleFrames
        {
            get { return settleFrames; }
            set { settleFrames = Mathf.Max(0, value); }
        }

        public int PatternResolution
        {
            get { return patternResolution; }
            set { patternResolution = Mathf.Clamp(value, 16, 2048); }
        }

        public int MinLitSamples
        {
            get { return minLitSamples; }
            set { minLitSamples = Mathf.Max(4, value); }
        }

        /// <summary><paramref name="camera"/> must also implement <see cref="IPMSDKDepthSource"/> and
        /// <see cref="IPMSDKCameraIntrinsicsSource"/> (e.g. <c>new PMSDKOakCalibrationCamera(enableDepth: true)</c>).</summary>
        public IEnumerator FindSurfaces(PMSDKSkinEditorController controller, IPMSDKCalibrationCamera camera,
            Action<PMSDKSurfaceFinderResult> onDone)
        {
            PMSDKSurfaceFinderResult result = new PMSDKSurfaceFinderResult();
            string problem = Validate(controller, camera);
            if (problem != null) {
                result.Message = problem;
                onDone?.Invoke(result);
                yield break;
            }
            IsRunning = true;
            IPMSDKDepthSource depthSource = camera as IPMSDKDepthSource;
            PMSDKSkin probe = null;
            PMSDKCalibrationDisplay display = null;
            bool cameraStarted = false;
            try {
                do {
                    SetStatus("Starting camera...");
                    cameraStarted = camera.Begin();
                    if (!cameraStarted) {
                        result.Message = "The camera did not start." + PMSDKAutoAdjust.CameraError(camera);
                        break;
                    }
                    if (!depthSource.HasDepth) {
                        result.Message = "The camera delivers no depth (start the OAK-D with depth enabled).";
                        break;
                    }
                    PMSDKCameraIntrinsics intrinsics;
                    if (!((IPMSDKCameraIntrinsicsSource)camera).TryGetIntrinsics(out intrinsics) || intrinsics == null) {
                        result.Message = "The camera has no intrinsics; plane sizes cannot be measured.";
                        break;
                    }

                    // 1. Dense camera -> projector map from a temporary full-raster skin.
                    controller.RefreshSkinList();
                    List<PMSDKSkin> existing = new List<PMSDKSkin>(controller.Skins);
                    probe = controller.SpawnSkinFromSnapshot(PMSDKSkinShapeKind.Rectangle, new List<Vector2> {
                        new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                    }, ProbeId, "Surface probe");
                    if (probe == null) {
                        result.Message = "Could not create the temporary full-screen skin.";
                        break;
                    }
                    probe.ZOrder = AboveAll(existing);
                    existing.Add(probe);
                    display = new PMSDKCalibrationDisplay(existing, probe.gameObject.layer);
                    display.HideAllExcept(probe);
                    SetStatus("Mapping camera to projector (" + PMSDKContentGrayCode.FrameCount(patternResolution) + " frames)...");
                    PMSDKGrayCaptureResult map = null;
                    IEnumerator sequence = PMSDKContentGrayCode.Capture(probe, display, camera, patternResolution, settleFrames, minContrast,
                        sampleStep, delegate(PMSDKGrayCaptureResult r) { map = r; });
                    while (sequence.MoveNext()) {
                        yield return sequence.Current;
                    }
                    display.Dispose();
                    display = null;
                    RemoveProbe(controller, ref probe);
                    if (map == null || !map.Success) {
                        result.Message = map != null ? map.Message : "Gray-code capture failed.";
                        break;
                    }

                    // 2. Planes from depth.
                    SetStatus("Measuring depth...");
                    ushort[] depth = depthSource.CaptureDepth();
                    if (depth == null) {
                        result.Message = "No depth frame." + PMSDKAutoAdjust.CameraError(camera);
                        break;
                    }
                    List<PMSDKDetectedPlane> planes;
                    string error;
                    if (!PMSDKAutoAdjustNative.TryDetectPlanes(depth, camera.Width, camera.Height, intrinsics, out planes, out error)) {
                        result.Message = "No flat surface found in the depth image (" + error + ").";
                        break;
                    }
                    result.Planes = planes;

                    // 3. One skin per plane the projector reaches.
                    PlaceSkins(controller, planes, map, intrinsics, result);
                    controller.RefreshSkinList();
                    result.Success = result.Created.Count > 0;
                    result.Message = "Found " + planes.Count + " flat surface(s); created " + result.Created.Count + " skin(s)" +
                                     (result.Skipped.Count > 0 ? "; skipped " + result.Skipped.Count + " (" + string.Join("; ", result.Skipped) + ")" : "") + ".";
                } while (false);
            } finally {
                if (display != null) {
                    display.Dispose();
                }
                RemoveProbe(controller, ref probe);
                if (cameraStarted) {
                    camera.End();
                }
                IsRunning = false;
            }
            SetStatus(result.Message);
            onDone?.Invoke(result);
        }

        /// <summary>
        /// Creates skins for <paramref name="planes"/> using the camera -> raster samples in
        /// <paramref name="map"/>. Public for tests and for hosts with their own capture.
        /// </summary>
        public void PlaceSkins(PMSDKSkinEditorController controller, List<PMSDKDetectedPlane> planes, PMSDKGrayCaptureResult map,
            PMSDKCameraIntrinsics intrinsics, PMSDKSurfaceFinderResult result)
        {
            Vector2[] undistortedSamples;
            string error;
            if (!PMSDKAutoAdjustNative.TryUndistort(map.CameraPixels.ToArray(), intrinsics, out undistortedSamples, out error)) {
                result.Skipped.Add("lens undistortion failed: " + error);
                return;
            }
            int number = 1;
            for (int p = 0; p < planes.Count; p++) {
                PMSDKDetectedPlane plane = planes[p];
                string label = "plane " + (p + 1);
                if (plane.ViewAngleDeg > maxViewAngleDeg) {
                    result.Skipped.Add(label + " seen too obliquely (" + plane.ViewAngleDeg.ToString("0") + " deg)");
                    continue;
                }
                List<Vector2> cameraPoints = new List<Vector2>();
                List<Vector2> rasterPoints = new List<Vector2>();
                for (int i = 0; i < map.CameraPixels.Count; i++) {
                    if (InsidePolygon(plane.Polygon, map.CameraPixels[i])) {
                        cameraPoints.Add(undistortedSamples[i]);
                        rasterPoints.Add(map.ContentUvs[i]);
                    }
                }
                if (cameraPoints.Count < minLitSamples) {
                    result.Skipped.Add(label + " not lit by the projector");
                    continue;
                }
                PMSDKHomography cameraToRaster = PMSDKHomography.FitNormalized(cameraPoints, rasterPoints);
                Vector2[] quad;
                if (!cameraToRaster.Valid || !PMSDKAutoAdjustNative.TryUndistort(plane.Quad, intrinsics, out quad, out error)) {
                    result.Skipped.Add(label + " could not be related to the projector");
                    continue;
                }
                List<Vector2> corners = new List<Vector2>();
                bool usable = true;
                foreach (Vector2 q in quad) {
                    Vector2 r = cameraToRaster.Apply(q);
                    usable &= !float.IsNaN(r.x) && !float.IsNaN(r.y) && r.x > -0.5f && r.x < 1.5f && r.y > -0.5f && r.y < 1.5f;
                    corners.Add(r);
                }
                if (!usable) {
                    result.Skipped.Add(label + " lies mostly outside the projector image");
                    continue;
                }
                PMSDKSkin skin = controller.SpawnSkinFromSnapshot(PMSDKSkinShapeKind.Rectangle, corners,
                    Guid.NewGuid().ToString("N").Substring(0, 8), namePrefix + " " + number);
                if (skin == null) {
                    result.Skipped.Add(label + ": skin creation failed");
                    continue;
                }
                // The quad's bottom edge is the skin's width direction; match it to the plane's longer side.
                float bottom = Vector2.Distance(plane.Quad[0], plane.Quad[1]);
                float left = Vector2.Distance(plane.Quad[0], plane.Quad[3]);
                float longer = Mathf.Max(plane.WidthMm, plane.HeightMm);
                float shorter = Mathf.Min(plane.WidthMm, plane.HeightMm);
                skin.FacetWidthMm = bottom >= left ? longer : shorter;
                skin.FacetHeightMm = bottom >= left ? shorter : longer;
                result.Created.Add(skin);
                number++;
            }
        }

        /// <summary>
        /// Projector-camera calibration (D-052): Gray code at <paramref name="patternResolution"/> through a temporary
        /// full-raster skin, then <see cref="PMSDKProjectorCalibration.TrySolve"/> (scale from the camera's depth when it
        /// has depth). The projector should light more than one flat surface. Saved to <paramref name="folder"/>.
        /// </summary>
        public IEnumerator CalibrateProjector(PMSDKSkinEditorController controller, IPMSDKCalibrationCamera camera, int calibrationResolution,
            string folder, Action<bool, string, PMSDKProjectorCalibration> onDone)
        {
            if (IsRunning || controller == null || camera == null || !(camera is IPMSDKCameraIntrinsicsSource)) {
                onDone?.Invoke(false, IsRunning ? "A surface search is already running." : "Projector calibration needs a camera with intrinsics.", null);
                yield break;
            }
            IsRunning = true;
            PMSDKSkin probe = null;
            PMSDKCalibrationDisplay display = null;
            bool cameraStarted = false;
            bool success = false;
            string message = "";
            PMSDKProjectorCalibration calibration = null;
            try {
                do {
                    SetStatus("Starting camera...");
                    cameraStarted = camera.Begin();
                    if (!cameraStarted) {
                        message = "The camera did not start." + PMSDKAutoAdjust.CameraError(camera);
                        break;
                    }
                    PMSDKCameraIntrinsics intrinsics;
                    if (!((IPMSDKCameraIntrinsicsSource)camera).TryGetIntrinsics(out intrinsics) || intrinsics == null) {
                        message = "The camera has no intrinsics.";
                        break;
                    }
                    controller.RefreshSkinList();
                    List<PMSDKSkin> existing = new List<PMSDKSkin>(controller.Skins);
                    probe = controller.SpawnSkinFromSnapshot(PMSDKSkinShapeKind.Rectangle, new List<Vector2> {
                        new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                    }, ProbeId, "Surface probe");
                    if (probe == null) {
                        message = "Could not create the temporary full-screen skin.";
                        break;
                    }
                    probe.ZOrder = AboveAll(existing);
                    existing.Add(probe);
                    display = new PMSDKCalibrationDisplay(existing, probe.gameObject.layer);
                    display.HideAllExcept(probe);
                    int resolution = Mathf.Clamp(calibrationResolution, 64, 2048);
                    SetStatus("Calibrating projector (" + PMSDKContentGrayCode.FrameCount(resolution) + " frames)...");
                    PMSDKGrayCaptureResult map = null;
                    IEnumerator sequence = PMSDKContentGrayCode.Capture(probe, display, camera, resolution, settleFrames, minContrast,
                        1, delegate(PMSDKGrayCaptureResult r) { map = r; });
                    while (sequence.MoveNext()) {
                        yield return sequence.Current;
                    }
                    display.Dispose();
                    display = null;
                    RemoveProbe(controller, ref probe);
                    if (map == null || !map.Success) {
                        message = map != null ? map.Message : "Gray-code capture failed.";
                        break;
                    }
                    IPMSDKDepthSource depthSource = camera as IPMSDKDepthSource;
                    ushort[] depth = depthSource != null && depthSource.HasDepth ? depthSource.CaptureDepth() : null;
                    SetStatus("Solving projector geometry from " + map.CameraPixels.Count + " samples...");
                    yield return null;
                    string error;
                    if (!PMSDKProjectorCalibration.TrySolve(map.CameraPixels, map.ContentUvs, intrinsics, camera.Width, camera.Height, depth,
                        out calibration, out error)) {
                        message = error;
                        break;
                    }
                    if (!string.IsNullOrEmpty(folder)) {
                        calibration.Save(folder);
                    }
                    int regions = UpdateCameraRegions(controller.Skins, map);
                    success = true;
                    message = "Projector calibrated: " + calibration.Describe() + "." +
                              (calibration.Metric ? "" : " No depth camera: sizes are relative.");
                    if (regions > 0) {
                        message += " Camera regions of " + regions + " marked skin(s) updated.";
                    }
                } while (false);
            } finally {
                if (display != null) {
                    display.Dispose();
                }
                RemoveProbe(controller, ref probe);
                if (cameraStarted) {
                    camera.End();
                }
                IsRunning = false;
            }
            SetStatus(message);
            onDone?.Invoke(success, message, calibration);
        }

        /// <summary>
        /// Maps a surface marked in the camera image (D-049): a hand-drawn mark today, an AI cutout later.
        /// Gray code through a temporary full-raster skin gives every lit camera pixel its projector raster
        /// position; the samples inside <paramref name="cameraMask"/> become one Rectangle skin around them,
        /// cut to their exact outline by an alpha mask (folds included, no plane assumption). Refine it
        /// afterwards with <see cref="PMSDKWarpMapBuilder"/>. The caller records undo before starting.
        /// </summary>
        /// <param name="cameraMask">Raw camera image size, row 0 = top, values above 127 = the surface.</param>
        public IEnumerator MapMarkedSurface(PMSDKSkinEditorController controller, IPMSDKCalibrationCamera camera, byte[] cameraMask,
            int maskWidth, int maskHeight, string skinName, Action<PMSDKSurfaceFinderResult> onDone)
        {
            PMSDKSurfaceFinderResult result = new PMSDKSurfaceFinderResult();
            string problem = ValidateMarked(controller, camera, cameraMask, maskWidth, maskHeight);
            if (problem != null) {
                result.Message = problem;
                onDone?.Invoke(result);
                yield break;
            }
            IsRunning = true;
            PMSDKSkin probe = null;
            PMSDKCalibrationDisplay display = null;
            bool cameraStarted = false;
            try {
                do {
                    SetStatus("Starting camera...");
                    cameraStarted = camera.Begin();
                    if (!cameraStarted) {
                        result.Message = "The camera did not start." + PMSDKAutoAdjust.CameraError(camera);
                        break;
                    }
                    if (camera.Width != maskWidth || camera.Height != maskHeight) {
                        result.Message = "The mark is " + maskWidth + " x " + maskHeight + " but the camera delivers " + camera.Width + " x " +
                                         camera.Height + " (mark a frame from this camera).";
                        break;
                    }

                    controller.RefreshSkinList();
                    List<PMSDKSkin> existing = new List<PMSDKSkin>(controller.Skins);
                    probe = controller.SpawnSkinFromSnapshot(PMSDKSkinShapeKind.Rectangle, new List<Vector2> {
                        new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                    }, ProbeId, "Surface probe");
                    if (probe == null) {
                        result.Message = "Could not create the temporary full-screen skin.";
                        break;
                    }
                    probe.ZOrder = AboveAll(existing);
                    existing.Add(probe);
                    display = new PMSDKCalibrationDisplay(existing, probe.gameObject.layer);
                    display.HideAllExcept(probe);
                    SetStatus("Mapping camera to projector (" + PMSDKContentGrayCode.FrameCount(patternResolution) + " frames)...");
                    PMSDKGrayCaptureResult map = null;
                    IEnumerator sequence = PMSDKContentGrayCode.Capture(probe, display, camera, patternResolution, settleFrames, minContrast,
                        1, delegate(PMSDKGrayCaptureResult r) { map = r; });
                    while (sequence.MoveNext()) {
                        yield return sequence.Current;
                    }
                    display.Dispose();
                    display = null;
                    RemoveProbe(controller, ref probe);
                    if (map == null || !map.Success) {
                        result.Message = map != null ? map.Message : "Gray-code capture failed.";
                        break;
                    }

                    string message;
                    PMSDKSkin skin = PlaceMarkedSkin(controller, cameraMask, maskWidth, map, skinName, out message);
                    controller.RefreshSkinList();
                    result.Message = message;
                    if (skin != null) {
                        result.Created.Add(skin);
                        result.Success = true;
                    }
                } while (false);
            } finally {
                if (display != null) {
                    display.Dispose();
                }
                RemoveProbe(controller, ref probe);
                if (cameraStarted) {
                    camera.End();
                }
                IsRunning = false;
            }
            SetStatus(result.Message);
            onDone?.Invoke(result);
        }

        /// <summary>
        /// Creates the masked Rectangle skin for the camera samples inside <paramref name="cameraMask"/>.
        /// The skin is the raster bounding box of those samples (so mesh UV is a plain scale of raster
        /// position) and its alpha mask is their raster footprint, gaps closed and interior filled.
        /// Public for tests and for hosts with their own capture.
        /// </summary>
        public PMSDKSkin PlaceMarkedSkin(PMSDKSkinEditorController controller, byte[] cameraMask, int maskWidth, PMSDKGrayCaptureResult map,
            string skinName, out string message)
        {
            List<Vector2> raster = new List<Vector2>();
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < map.CameraPixels.Count; i++) {
                Vector2 pixel = map.CameraPixels[i];
                int index = (int)pixel.y * maskWidth + (int)pixel.x;
                if (index < 0 || index >= cameraMask.Length || cameraMask[index] <= 127) {
                    continue;
                }
                Vector2 r = map.ContentUvs[i];
                raster.Add(r);
                min = Vector2.Min(min, r);
                max = Vector2.Max(max, r);
            }
            if (raster.Count < minLitSamples) {
                message = "Only " + raster.Count + " lit camera pixels inside the mark (need " + minLitSamples +
                          "); the projector does not reach the marked surface or the camera moved.";
                return null;
            }

            // Gray-code bit errors throw a few samples far from the surface: build the footprint once, keep its
            // main islands, then rebuild it tightly from the samples that survived.
            Footprint first = BuildFootprint(raster, min, max, false);
            List<Vector2> kept = KeepMainIslands(raster, first);
            if (kept.Count < minLitSamples) {
                message = "Only " + kept.Count + " consistent camera pixels inside the mark (need " + minLitSamples + ").";
                return null;
            }
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector2 r in kept) {
                min = Vector2.Min(min, r);
                max = Vector2.Max(max, r);
            }
            Footprint footprint = BuildFootprint(kept, min, max, true);
            min = footprint.Min;
            max = footprint.Max;
            byte[] mask = footprint.Mask;
            int width = footprint.Width;
            int height = footprint.Height;

            List<Vector2> corners = new List<Vector2> {
                new Vector2(min.x, min.y), new Vector2(max.x, min.y), new Vector2(max.x, max.y), new Vector2(min.x, max.y),
            };
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            PMSDKSkin skin = controller.SpawnSkinFromSnapshot(PMSDKSkinShapeKind.Rectangle, corners, id,
                string.IsNullOrWhiteSpace(skinName) ? namePrefix + " (marked)" : skinName);
            if (skin == null) {
                message = "Skin creation failed.";
                return null;
            }
            string fileName = id + ".png";
            PMSDKPhotoSkinBuilder.WriteMaskPng(PMSDKSkinStore.ResolveMaskPath(fileName), mask, width, height, false);
            skin.AlphaMaskPath = fileName;
            message = "Created \"" + skin.DisplayName + "\" from " + kept.Count + " lit camera pixels (" + (raster.Count - kept.Count) +
                      " stray dropped; raster " +
                      min.x.ToString("0.000") + ".." + max.x.ToString("0.000") + " x " + min.y.ToString("0.000") + ".." + max.y.ToString("0.000") +
                      ", mask " + width + " x " + height + ").";
            return skin;
        }

        private sealed class Footprint
        {
            public Vector2 Min;
            public Vector2 Max;
            public int Width;
            public int Height;
            /// <summary>Row 0 = bottom (raster y up), i.e. already in the skin's UV orientation.</summary>
            public byte[] Mask;

            public int IndexOf(Vector2 raster)
            {
                int x = Mathf.Clamp(Mathf.FloorToInt((raster.x - Min.x) / (Max.x - Min.x) * Width), 0, Width - 1);
                int y = Mathf.Clamp(Mathf.FloorToInt((raster.y - Min.y) / (Max.y - Min.y) * Height), 0, Height - 1);
                return y * Width + x;
            }
        }

        /// <summary>Raster footprint of <paramref name="raster"/>: mask texel = mean sample spacing (so the splat
        /// is connected), one texel of margin. <paramref name="close"/> closes gaps and fills the interior; island
        /// detection uses the raw splat, because closing would bridge strays lying a few texels off the surface.</summary>
        private static Footprint BuildFootprint(List<Vector2> raster, Vector2 min, Vector2 max, bool close)
        {
            Vector2 size = max - min;
            float spacing = Mathf.Sqrt(Mathf.Max(size.x * size.y, 1e-8f) / raster.Count);
            float texel = Mathf.Max(spacing, Mathf.Max(size.x, size.y) / MaxMaskSize);
            min -= new Vector2(texel, texel);
            max += new Vector2(texel, texel);
            min = Vector2.Max(min, Vector2.zero);
            max = Vector2.Min(max, Vector2.one);
            size = max - min;
            Footprint footprint = new Footprint();
            footprint.Min = min;
            footprint.Max = max;
            footprint.Width = Mathf.Clamp(Mathf.CeilToInt(size.x / texel), 4, MaxMaskSize);
            footprint.Height = Mathf.Clamp(Mathf.CeilToInt(size.y / texel), 4, MaxMaskSize);
            int width = footprint.Width;
            int height = footprint.Height;
            byte[] mask = new byte[width * height];
            for (int i = 0; i < raster.Count; i++) {
                float fx = (raster[i].x - min.x) / size.x * width - 0.5f;
                float fy = (raster[i].y - min.y) / size.y * height - 0.5f;
                int x0 = Mathf.FloorToInt(fx);
                int y0 = Mathf.FloorToInt(fy);
                for (int dy = 0; dy <= 1; dy++) {
                    for (int dx = 0; dx <= 1; dx++) {
                        int x = x0 + dx;
                        int y = y0 + dy;
                        if (x >= 0 && y >= 0 && x < width && y < height) {
                            mask[y * width + x] = 255;
                        }
                    }
                }
            }
            if (close) {
                mask = Erode(Dilate(mask, width, height), width, height);
                FillInterior(mask, width, height);
            }
            footprint.Mask = mask;
            return footprint;
        }

        /// <summary>Samples lying on footprint islands of at least <see cref="MinIslandFraction"/> of the largest one.</summary>
        private static List<Vector2> KeepMainIslands(List<Vector2> raster, Footprint footprint)
        {
            int[] labels = new int[footprint.Mask.Length];
            List<int> areas = new List<int> { 0 };
            Stack<int> pending = new Stack<int>();
            int width = footprint.Width;
            int height = footprint.Height;
            for (int start = 0; start < labels.Length; start++) {
                if (footprint.Mask[start] == 0 || labels[start] != 0) {
                    continue;
                }
                int label = areas.Count;
                int area = 0;
                pending.Push(start);
                labels[start] = label;
                while (pending.Count > 0) {
                    int index = pending.Pop();
                    area++;
                    int x = index % width;
                    int y = index / width;
                    for (int n = 0; n < 4; n++) {
                        int nx = x + (n == 0 ? -1 : n == 1 ? 1 : 0);
                        int ny = y + (n == 2 ? -1 : n == 3 ? 1 : 0);
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) {
                            continue;
                        }
                        int next = ny * width + nx;
                        if (footprint.Mask[next] != 0 && labels[next] == 0) {
                            labels[next] = label;
                            pending.Push(next);
                        }
                    }
                }
                areas.Add(area);
            }
            int largest = 0;
            foreach (int area in areas) {
                largest = Mathf.Max(largest, area);
            }
            int minArea = Mathf.Max(1, Mathf.CeilToInt(largest * MinIslandFraction));
            List<Vector2> kept = new List<Vector2>(raster.Count);
            foreach (Vector2 r in raster) {
                int label = labels[footprint.IndexOf(r)];
                if (label > 0 && areas[label] >= minArea) {
                    kept.Add(r);
                }
            }
            return kept;
        }

        private static byte[] Dilate(byte[] source, int width, int height)
        {
            byte[] result = new byte[source.Length];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    byte value = 0;
                    for (int dy = -1; dy <= 1 && value == 0; dy++) {
                        for (int dx = -1; dx <= 1; dx++) {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < width && ny < height && source[ny * width + nx] > 0) {
                                value = 255;
                                break;
                            }
                        }
                    }
                    result[y * width + x] = value;
                }
            }
            return result;
        }

        private static byte[] Erode(byte[] source, int width, int height)
        {
            byte[] result = new byte[source.Length];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    byte value = 255;
                    for (int dy = -1; dy <= 1 && value > 0; dy++) {
                        for (int dx = -1; dx <= 1; dx++) {
                            int nx = x + dx;
                            int ny = y + dy;
                            bool outside = nx < 0 || ny < 0 || nx >= width || ny >= height;
                            if (!outside && source[ny * width + nx] == 0) {
                                value = 0;
                                break;
                            }
                        }
                    }
                    result[y * width + x] = value;
                }
            }
            return result;
        }

        /// <summary>Sets every empty texel not connected to the border (a gap inside the footprint) to 255.</summary>
        private static void FillInterior(byte[] mask, int width, int height)
        {
            bool[] outside = new bool[mask.Length];
            Stack<int> pending = new Stack<int>();
            for (int x = 0; x < width; x++) {
                pending.Push(x);
                pending.Push((height - 1) * width + x);
            }
            for (int y = 0; y < height; y++) {
                pending.Push(y * width);
                pending.Push(y * width + width - 1);
            }
            while (pending.Count > 0) {
                int index = pending.Pop();
                if (outside[index] || mask[index] > 0) {
                    continue;
                }
                outside[index] = true;
                int x = index % width;
                int y = index / width;
                if (x > 0) {
                    pending.Push(index - 1);
                }
                if (x < width - 1) {
                    pending.Push(index + 1);
                }
                if (y > 0) {
                    pending.Push(index - width);
                }
                if (y < height - 1) {
                    pending.Push(index + width);
                }
            }
            for (int i = 0; i < mask.Length; i++) {
                if (!outside[i]) {
                    mask[i] = 255;
                }
            }
        }

        private string ValidateMarked(PMSDKSkinEditorController controller, IPMSDKCalibrationCamera camera, byte[] cameraMask, int maskWidth,
            int maskHeight)
        {
            if (IsRunning) {
                return "A surface search is already running.";
            }
            if (controller == null) {
                return "No skin editor controller.";
            }
            if (camera == null) {
                return "No calibration camera.";
            }
            if (cameraMask == null || maskWidth <= 0 || maskHeight <= 0 || cameraMask.Length != maskWidth * maskHeight) {
                return "The surface mark is empty or its size does not match its pixels.";
            }
            return null;
        }

        private static bool InsidePolygon(Vector2[] polygon, Vector2 point)
        {
            if (polygon == null || polygon.Length < 3) {
                return false;
            }
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++) {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// Re-derives the camera region (<see cref="PMSDKCameraMark.RegionFileFor"/>) of every skin that has one, from
        /// full-raster correspondences: the camera pixels whose projector position lies inside the skin's quad and
        /// alpha mask, largest connected piece only (drops reflections). Skins live in projector space, so this keeps
        /// their regions right after the camera has moved. Returns the number of regions written.
        /// </summary>
        public static int UpdateCameraRegions(IList<PMSDKSkin> skins, PMSDKGrayCaptureResult map)
        {
            int written = 0;
            if (skins == null || map == null || !map.Success) {
                return written;
            }
            int width = map.CameraWidth;
            int height = map.CameraHeight;
            foreach (PMSDKSkin skin in skins) {
                if (skin == null || skin.Vertices == null || skin.Vertices.Count < 4 ||
                    (skin.Kind != PMSDKSkinShapeKind.Rectangle && skin.Kind != PMSDKSkinShapeKind.Ellipse) ||
                    !System.IO.File.Exists(PMSDKSkinStore.ResolveMaskPath(PMSDKCameraMark.RegionFileFor(skin)))) {
                    continue;
                }
                PMSDKHomography rasterToUv = PMSDKHomography.FitNormalized(
                    new List<Vector2> { skin.Vertices[0], skin.Vertices[1], skin.Vertices[2], skin.Vertices[3] },
                    new List<Vector2> { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
                if (!rasterToUv.Valid) {
                    continue;
                }
                Texture2D alpha = LoadAlpha(skin);
                byte[] region = new byte[width * height];
                try {
                    for (int i = 0; i < map.CameraPixels.Count; i++) {
                        Vector2 uv = rasterToUv.Apply(map.ContentUvs[i]);
                        if (uv.x < 0f || uv.y < 0f || uv.x > 1f || uv.y > 1f) {
                            continue;
                        }
                        if (alpha != null && alpha.GetPixelBilinear(uv.x, uv.y).r < 0.5f) {
                            continue;
                        }
                        int x = (int)map.CameraPixels[i].x;
                        int y = (int)map.CameraPixels[i].y;
                        if (x >= 0 && y >= 0 && x < width && y < height) {
                            region[y * width + x] = 255;
                        }
                    }
                } finally {
                    if (alpha != null) {
                        if (Application.isPlaying) {
                            Destroy(alpha);
                        } else {
                            DestroyImmediate(alpha);
                        }
                    }
                }
                if (KeepLargestPiece(region, width, height) > 0) {
                    PMSDKCameraMark.SaveRegion(skin, region, width, height);
                    written++;
                }
            }
            return written;
        }

        private static Texture2D LoadAlpha(PMSDKSkin skin)
        {
            if (string.IsNullOrEmpty(skin.AlphaMaskPath)) {
                return null;
            }
            string path = PMSDKSkinStore.ResolveMaskPath(skin.AlphaMaskPath);
            if (!System.IO.File.Exists(path)) {
                return null;
            }
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!texture.LoadImage(System.IO.File.ReadAllBytes(path), false)) {
                DestroyImmediate(texture);
                return null;
            }
            return texture;
        }

        /// <summary>Clears every 4-connected piece except the largest; returns its size.</summary>
        public static int KeepLargestPiece(byte[] mask, int width, int height)
        {
            int[] labels = new int[mask.Length];
            int bestLabel = 0;
            int bestSize = 0;
            int next = 0;
            Stack<int> pending = new Stack<int>();
            for (int start = 0; start < mask.Length; start++) {
                if (mask[start] == 0 || labels[start] != 0) {
                    continue;
                }
                next++;
                int size = 0;
                labels[start] = next;
                pending.Push(start);
                while (pending.Count > 0) {
                    int index = pending.Pop();
                    size++;
                    int x = index % width;
                    int y = index / width;
                    if (x > 0 && mask[index - 1] != 0 && labels[index - 1] == 0) {
                        labels[index - 1] = next;
                        pending.Push(index - 1);
                    }
                    if (x < width - 1 && mask[index + 1] != 0 && labels[index + 1] == 0) {
                        labels[index + 1] = next;
                        pending.Push(index + 1);
                    }
                    if (y > 0 && mask[index - width] != 0 && labels[index - width] == 0) {
                        labels[index - width] = next;
                        pending.Push(index - width);
                    }
                    if (y < height - 1 && mask[index + width] != 0 && labels[index + width] == 0) {
                        labels[index + width] = next;
                        pending.Push(index + width);
                    }
                }
                if (size > bestSize) {
                    bestSize = size;
                    bestLabel = next;
                }
            }
            for (int i = 0; i < mask.Length; i++) {
                if (labels[i] != bestLabel) {
                    mask[i] = 0;
                }
            }
            return bestSize;
        }

        // One rank above every skin. PMSDKSkin turns ZOrder into a small z offset that must stay inside the
        // projector camera's clip range, so a huge rank would push the probe out of view.
        private static int AboveAll(List<PMSDKSkin> skins)
        {
            int top = 0;
            foreach (PMSDKSkin skin in skins) {
                if (skin != null) {
                    top = Mathf.Max(top, skin.ZOrder + 1);
                }
            }
            return top;
        }

        private static void RemoveProbe(PMSDKSkinEditorController controller, ref PMSDKSkin probe)
        {
            if (probe == null) {
                return;
            }
            if (Application.isPlaying) {
                Destroy(probe.gameObject);
            } else {
                DestroyImmediate(probe.gameObject);
            }
            probe = null;
            if (controller != null) {
                controller.RefreshSkinList();
            }
        }

        private string Validate(PMSDKSkinEditorController controller, IPMSDKCalibrationCamera camera)
        {
            if (IsRunning) {
                return "A surface search is already running.";
            }
            if (controller == null) {
                return "No skin editor controller.";
            }
            if (camera == null) {
                return "No calibration camera.";
            }
            if (!(camera is IPMSDKDepthSource) || !(camera is IPMSDKCameraIntrinsicsSource)) {
                return "Finding surfaces needs a depth camera with intrinsics (OAK-D with depth).";
            }
            if (!PMSDKAutoAdjustNative.IsAvailable()) {
                return "ProjectionMappingSDK.dll has no Auto Adjust support (update the native plugin).";
            }
            return null;
        }

        private void SetStatus(string text)
        {
            Status = text;
            StatusChanged?.Invoke(text);
        }
    }
}
