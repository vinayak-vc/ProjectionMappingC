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
        private const string ProbeId = "pmsdk-surface-probe";

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
                    probe.ZOrder = 100000;
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
