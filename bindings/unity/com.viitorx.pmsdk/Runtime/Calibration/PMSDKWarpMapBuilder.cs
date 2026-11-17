using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
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
        /// <see cref="PMSDKAutoAdjust"/> sits on the same GameObject).</summary>
        public bool Verified;
        public PMSDKAutoAdjustScore Verification;
    }

    /// <summary>
    /// Auto Adjust Phase 3 (D-046): dense warp map for curved / non-planar skins. Projects Gray code
    /// through the skin's current warp, decodes camera pixel -> content UV, defines "correct" as the
    /// best-fit perspective image of the content in the (undistorted) camera, and stores, per projector
    /// raster position, the content UV that must be shown there. The map is applied in
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

        /// <summary>Folder for warp map files; null = <see cref="PMSDKSkinStore.MasksFolder"/> (stored as a bare
        /// name). A custom folder is stored as an absolute path in the skin.</summary>
        public string OutputFolder { get; set; }

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
                        sampleStep, delegate(PMSDKGrayCaptureResult r) { capture = r; });
                    while (sequence.MoveNext()) {
                        yield return sequence.Current;
                    }
                    if (capture == null || !capture.Success) {
                        result.Message = capture != null ? capture.Message : "Gray-code capture failed.";
                        break;
                    }

                    string error;
                    if (!TryComputeMap(skin, capture, intrinsics, result, out error)) {
                        result.Message = error;
                        break;
                    }
                    result.Success = true;
                    result.Message = "Warp map built from " + result.Samples + " samples (" + (result.Coverage * 100f).ToString("0") + " % of the map).";
                } while (false);
            } finally {
                display.Dispose();
                if (cameraStarted) {
                    camera.End();
                }
                IsRunning = false;
            }

            PMSDKAutoAdjust measurer = verify && result.Success ? GetComponent<PMSDKAutoAdjust>() : null;
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
        /// Turns camera -> content correspondences into the skin's warp map file. Public for tests and
        /// for hosts that capture correspondences another way.
        /// </summary>
        public bool TryComputeMap(PMSDKSkin skin, PMSDKGrayCaptureResult capture, PMSDKCameraIntrinsics intrinsics,
            PMSDKWarpMapResult result, out string error)
        {
            int count = capture.CameraPixels.Count;
            if (count < 16) {
                error = "Too few decoded samples (" + count + ").";
                return false;
            }
            Vector2[] camera;
            if (!PMSDKAutoAdjustNative.TryUndistort(capture.CameraPixels.ToArray(), intrinsics, out camera, out error)) {
                return false;
            }
            Vector2[] currentUv = capture.ContentUvs.ToArray();

            // "Correct" = the best-fit perspective image of the content in the camera: fit camera -> UV.
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

            // Where each sample is on the projector now (corner pin + correction, without the map).
            Vector2[] raster = PMSDKAutoAdjustNative.EvaluateWarpMany(skin.Vertices, skin.CorrectionColumns, skin.CorrectionRows,
                skin.GetCorrectionOffsets(), currentUv);
            Vector2[] targetUv = new Vector2[count];
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            for (int i = 0; i < count; i++) {
                targetUv[i] = cameraToUv.Apply(camera[i]);
                minX = Mathf.Min(minX, raster[i].x);
                minY = Mathf.Min(minY, raster[i].y);
                maxX = Mathf.Max(maxX, raster[i].x);
                maxY = Mathf.Max(maxY, raster[i].y);
            }
            float padX = Mathf.Max(1e-3f, (maxX - minX) * 0.02f);
            float padY = Mathf.Max(1e-3f, (maxY - minY) * 0.02f);
            Rect rect = new Rect(minX - padX, minY - padY, (maxX - minX) + 2f * padX, (maxY - minY) + 2f * padY);

            float[] rgba;
            float coverage;
            if (!PMSDKAutoAdjustNative.TryBuildWarpMap(raster, targetUv, rect, mapSize, mapSize, maxFillDistancePx, out rgba, out coverage, out error)) {
                return false;
            }
            string fileName = SanitizeId(skin.SkinId) + "_warp.pmwm";
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
            result.Samples = count;
            result.Coverage = coverage;
            result.File = fileName;
            error = "";
            return true;
        }

        private string Validate(PMSDKSkin skin, IPMSDKCalibrationCamera camera)
        {
            if (IsRunning) {
                return "A warp map is already being built.";
            }
            if (skin == null) {
                return "No skin selected.";
            }
            if (skin.Kind != PMSDKSkinShapeKind.Rectangle && skin.Kind != PMSDKSkinShapeKind.Ellipse) {
                return "Warp maps work on Rectangle and Ellipse skins (this one is " + skin.Kind + ").";
            }
            if (camera == null) {
                return "No calibration camera.";
            }
            if (!PMSDKAutoAdjustNative.IsAvailable()) {
                return "ProjectionMappingSDK.dll has no Auto Adjust support (update the native plugin).";
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
