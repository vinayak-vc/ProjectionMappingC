using UnityEngine;
using UnityEngine.Video;

namespace vxpmsdk.Components
{
    /// <summary>Content source kind for a <see cref="PMSDKSkin"/>. v1 (M-Sk1) shipped
    /// None/Texture/RenderTexture/VideoClip/TestPattern; v1.1 (M-Sk3) adds Camera,
    /// VideoUrl, Webcam, ExternalTexture.</summary>
    public enum PMSDKSkinContentMode
    {
        /// <summary>No source — the skin renders whatever texture is already bound (default white).</summary>
        None = 0,
        /// <summary>Static <see cref="Texture"/> reference.</summary>
        Texture = 1,
        /// <summary>External <see cref="UnityEngine.RenderTexture"/> the caller writes to.</summary>
        RenderTexture = 2,
        /// <summary>A <see cref="VideoClip"/> asset played on a per-skin <see cref="VideoPlayer"/>.</summary>
        VideoClip = 3,
        /// <summary>Built-in <see cref="PMSDKTestPatterns"/> texture (calibration aid).</summary>
        TestPattern = 4,
        /// <summary>A URL streamed via <see cref="VideoPlayer.source"/> = <c>Url</c>. Requires
        /// a network-reachable path. Playback controls (Loop/Mute/etc) apply.</summary>
        VideoUrl = 5,
        /// <summary>A Unity <see cref="UnityEngine.Camera"/> in the scene renders into a
        /// per-skin owned <see cref="UnityEngine.RenderTexture"/>. The camera's original
        /// <c>targetTexture</c> is preserved and restored on mode switch / disable.</summary>
        Camera = 6,
        /// <summary>The system webcam via <see cref="WebCamTexture"/>. Empty
        /// <see cref="PMSDKSkinContent.WebcamDeviceName"/> = default device.</summary>
        Webcam = 7,
        /// <summary>An arbitrary <see cref="Texture"/> assigned by an ADAPTER — e.g.
        /// <c>PMSDKSkinSpoutIn</c> / <c>PMSDKSkinNdiIn</c> in the Interop asmdefs (available
        /// when Klak Spout / NDI packages are installed), or user code. No owned lifetime.</summary>
        ExternalTexture = 8,
        /// <summary>An image file (png/jpg) read from an absolute disk path at runtime.
        /// Works in standalone builds where the file is not a Unity asset.</summary>
        ImageFile = 9,
    }

    /// <summary>
    /// Binds a content source to a <see cref="PMSDKSkin"/>. Exposes the resolved output as
    /// <see cref="CurrentTexture"/>; the skin reads it each frame and pushes it into the
    /// material via MaterialPropertyBlock.
    ///
    /// Lifecycle: owns per-skin resources only for modes that need them —
    /// <see cref="PMSDKSkinContentMode.VideoClip"/>/<see cref="PMSDKSkinContentMode.VideoUrl"/>
    /// own a <see cref="VideoPlayer"/> + <see cref="RenderTexture"/>;
    /// <see cref="PMSDKSkinContentMode.Camera"/> owns a target <see cref="RenderTexture"/>
    /// (and restores the camera's prior <c>targetTexture</c> on teardown);
    /// <see cref="PMSDKSkinContentMode.Webcam"/> owns a <see cref="WebCamTexture"/>;
    /// <see cref="PMSDKSkinContentMode.TestPattern"/> owns a <see cref="Texture2D"/>.
    /// Switching modes tears down previous owned resources cleanly.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PMSDKSkinContent : MonoBehaviour
    {
        public PMSDKSkinContentMode Mode = PMSDKSkinContentMode.None;

        [Header("Sources")]
        public Texture StaticTexture;
        public RenderTexture ExternalRenderTexture;
        public VideoClip VideoClip;
        [Tooltip("URL for VideoUrl mode. HTTP/HTTPS or local file:// path.")]
        public string VideoUrl;
        [Tooltip("Scene Camera to render into an owned RT for Camera mode.")]
        public Camera SourceCamera;
        [Tooltip("WebCam device name for Webcam mode. Empty = first available.")]
        public string WebcamDeviceName;
        [Tooltip("Arbitrary texture assigned by an adapter (Spout/NDI/etc) or user code.")]
        public Texture ExternalTexture;
        [Tooltip("Absolute disk path to a png/jpg for ImageFile mode.")]
        public string ImagePath;
        public PMSDKTestPatternType TestPatternType = PMSDKTestPatternType.Checkerboard;
        [Tooltip("Test-pattern texture resolution (square). Rebuilt when Kind or Size changes.")]
        public int TestPatternSize = 512;
        [Tooltip("Checkerboard grid density (used by the Checkerboard pattern only).")]
        public int TestPatternGridSize = 8;
        public Color TestPatternColorA = Color.white;
        public Color TestPatternColorB = Color.black;

        [Header("Playback (VideoClip / VideoUrl only)")]
        public bool Play = true;
        public bool Loop = true;
        public bool Mute = false;
        public double StartTime = 0.0;
        [Range(0.1f, 8f)] public float PlaybackRate = 1f;

        [Header("Owned RT size (VideoClip / VideoUrl / Camera)")]
        [Tooltip("Owned RT dimensions (per-skin). Ignored for non-render modes.")]
        public int VideoTargetWidth = 1280;
        public int VideoTargetHeight = 720;

        [Header("Webcam size / fps")]
        public int WebcamWidth = 1280;
        public int WebcamHeight = 720;
        public int WebcamFps = 30;

        /// <summary>The texture <see cref="PMSDKSkin"/> should render.</summary>
        public Texture CurrentTexture { get; private set; }

        // Owned resources (only allocated for the modes that need them).
        private VideoPlayer videoPlayer;
        private RenderTexture ownedVideoRT;
        private RenderTexture ownedCameraRT;
        private WebCamTexture ownedWebcam;
        private Texture2D ownedPatternTex;
        private Texture2D ownedImageTex;
        private string loadedImagePath;

        /// <summary>Raised (once per source) when a VideoUrl source cannot be played: the video
        /// player reported an error, or the file did not start within 10 s. UI can then offer a
        /// conversion. Arguments: the content and a short reason.</summary>
        public static event System.Action<PMSDKSkinContent, string> VideoProblem;

        private string reportedVideoSource;
        private string watchedVideoSource;
        private float watchedVideoStart;
        // Camera targetTexture restore snapshot — remembered on mode enter, replayed on exit.
        private Camera trackedCamera;
        private RenderTexture prevCameraTarget;

        private PMSDKTestPatternType lastPatternType = (PMSDKTestPatternType)(-1);
        private int lastPatternSize = -1;
        private int lastPatternGrid = -1;
        private Color lastPatternColorA;
        private Color lastPatternColorB;
        private PMSDKSkinContentMode lastMode = (PMSDKSkinContentMode)(-1);

        private void OnEnable() { ApplyMode(force: true); }

        private void OnDisable() { TeardownAll(); }

        private void OnValidate()
        {
            TestPatternSize = Mathf.Clamp(TestPatternSize, 16, 4096);
            VideoTargetWidth  = Mathf.Clamp(VideoTargetWidth, 16, 8192);
            VideoTargetHeight = Mathf.Clamp(VideoTargetHeight, 16, 8192);
            WebcamWidth = Mathf.Clamp(WebcamWidth, 16, 8192);
            WebcamHeight = Mathf.Clamp(WebcamHeight, 16, 8192);
            WebcamFps = Mathf.Clamp(WebcamFps, 1, 240);
            PlaybackRate = Mathf.Clamp(PlaybackRate, 0.1f, 8f);
        }

        private void Update() { ApplyMode(force: false); }

        // -------- mode application --------

        private void ApplyMode(bool force)
        {
            bool modeChanged = force || Mode != lastMode;
            if (modeChanged) { TeardownAll(); lastMode = Mode; }

            switch (Mode)
            {
                case PMSDKSkinContentMode.None:
                    CurrentTexture = null;
                    break;

                case PMSDKSkinContentMode.Texture:
                    CurrentTexture = StaticTexture;
                    break;

                case PMSDKSkinContentMode.RenderTexture:
                    CurrentTexture = ExternalRenderTexture;
                    break;

                case PMSDKSkinContentMode.VideoClip:
                    ApplyVideoPlayerMode(useUrl: false);
                    break;

                case PMSDKSkinContentMode.VideoUrl:
                    ApplyVideoPlayerMode(useUrl: true);
                    break;

                case PMSDKSkinContentMode.Camera:
                    ApplyCameraMode();
                    break;

                case PMSDKSkinContentMode.Webcam:
                    ApplyWebcamMode();
                    break;

                case PMSDKSkinContentMode.ExternalTexture:
                    // No owned resources — read the field each frame so adapters that
                    // update it (Spout/NDI, user code) show through.
                    CurrentTexture = ExternalTexture;
                    break;

                case PMSDKSkinContentMode.TestPattern:
                    ApplyTestPatternMode();
                    break;

                case PMSDKSkinContentMode.ImageFile:
                    ApplyImageFileMode();
                    break;
            }
        }

        private void ApplyImageFileMode()
        {
            if (string.IsNullOrEmpty(ImagePath)) {
                ReleaseOwnedImageTex();
                CurrentTexture = null;
                return;
            }
            if (ownedImageTex != null && loadedImagePath == ImagePath) {
                CurrentTexture = ownedImageTex;
                return;
            }

            ReleaseOwnedImageTex();
            loadedImagePath = ImagePath;
            if (!System.IO.File.Exists(ImagePath)) {
                Debug.LogWarning("PMSDKSkinContent: image file not found: " + ImagePath, this);
                CurrentTexture = null;
                return;
            }
            try {
                byte[] bytes = System.IO.File.ReadAllBytes(ImagePath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes, false)) {
                    Debug.LogWarning("PMSDKSkinContent: could not decode image: " + ImagePath, this);
                    DestroyTexture(tex);
                    CurrentTexture = null;
                    return;
                }
                tex.name = "PMSDKSkin_" + name + "_Image";
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                ownedImageTex = tex;
                CurrentTexture = ownedImageTex;
            } catch (System.Exception ex) {
                Debug.LogWarning("PMSDKSkinContent: image load failed (" + ex.Message + "): " + ImagePath, this);
                CurrentTexture = null;
            }
        }

        private void ReleaseOwnedImageTex()
        {
            if (ownedImageTex != null) {
                DestroyTexture(ownedImageTex);
                ownedImageTex = null;
            }
            loadedImagePath = null;
        }

        private static void DestroyTexture(Texture2D tex)
        {
            if (Application.isEditor && !Application.isPlaying) {
                DestroyImmediate(tex);
            } else {
                Destroy(tex);
            }
        }

        private void ApplyVideoPlayerMode(bool useUrl)
        {
            bool hasSource = useUrl
                ? !string.IsNullOrEmpty(VideoUrl)
                : VideoClip != null;
            if (!hasSource) { CurrentTexture = null; return; }

            if (ownedVideoRT == null || ownedVideoRT.width != VideoTargetWidth || ownedVideoRT.height != VideoTargetHeight)
            {
                if (ownedVideoRT != null) ReleaseOwnedVideoRT();
                ownedVideoRT = new RenderTexture(VideoTargetWidth, VideoTargetHeight, 0, RenderTextureFormat.ARGB32)
                {
                    name = $"PMSDKSkin_{name}_VideoRT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                ownedVideoRT.Create();
            }

            if (videoPlayer == null)
            {
                videoPlayer = gameObject.AddComponent<VideoPlayer>();
                videoPlayer.playOnAwake = false;
                videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
                videoPlayer.waitForFirstFrame = true;
                videoPlayer.skipOnDrop = true;
                videoPlayer.hideFlags = HideFlags.HideInInspector;
                videoPlayer.errorReceived += OnVideoPlayerError;
            }
            if (useUrl)
            {
                videoPlayer.source = VideoSource.Url;
                if (videoPlayer.url != VideoUrl) videoPlayer.url = VideoUrl;
                videoPlayer.clip = null;
            }
            else
            {
                videoPlayer.source = VideoSource.VideoClip;
                videoPlayer.clip = VideoClip;
                videoPlayer.url = null;
            }
            videoPlayer.targetTexture = ownedVideoRT;
            videoPlayer.isLooping = Loop;
            videoPlayer.playbackSpeed = PlaybackRate;
            SetMuted(videoPlayer, Mute);

            if (Play)
            {
                if (!videoPlayer.isPlaying) videoPlayer.Play();
                if (StartTime > 0.0 && Application.isPlaying && videoPlayer.isPrepared && videoPlayer.time < StartTime)
                    videoPlayer.time = StartTime;
            }
            else if (videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
            }

            if (useUrl) WatchVideoStart();
            CurrentTexture = ownedVideoRT;
        }

        private void OnVideoPlayerError(VideoPlayer source, string message)
        {
            ReportVideoProblem(string.IsNullOrEmpty(message) ? "The video player reported an error." : message);
        }

        private void ReportVideoProblem(string message)
        {
            string current = Mode == PMSDKSkinContentMode.VideoUrl ? VideoUrl : null;
            if (string.IsNullOrEmpty(current) || reportedVideoSource == current) return;
            reportedVideoSource = current;
            Debug.LogWarning("PMSDKSkinContent: video cannot be played (" + message + "): " + current, this);
            System.Action<PMSDKSkinContent, string> handler = VideoProblem;
            if (handler != null) handler(this, message);
        }

        // A file the platform cannot decode sometimes never errors, it just never prepares.
        private void WatchVideoStart()
        {
            if (!Application.isPlaying || videoPlayer == null) return;
            if (watchedVideoSource != VideoUrl)
            {
                watchedVideoSource = VideoUrl;
                watchedVideoStart = Time.realtimeSinceStartup;
                reportedVideoSource = null;
                return;
            }
            if (Play && !videoPlayer.isPrepared && reportedVideoSource != VideoUrl
                && Time.realtimeSinceStartup - watchedVideoStart > 10f
                && System.IO.File.Exists(VideoUrl))
            {
                ReportVideoProblem("The video did not start within 10 seconds.");
            }
        }

        private static void SetMuted(VideoPlayer vp, bool mute)
        {
            if (vp.audioTrackCount == 0) return;
            for (ushort i = 0; i < vp.audioTrackCount; i++) vp.SetDirectAudioMute(i, mute);
        }

        private void ApplyCameraMode()
        {
            if (SourceCamera == null) { CurrentTexture = null; return; }

            // Rebuild owned RT if its size changed OR the tracked camera changed.
            if (ownedCameraRT == null
                || ownedCameraRT.width != VideoTargetWidth
                || ownedCameraRT.height != VideoTargetHeight
                || trackedCamera != SourceCamera)
            {
                RestoreTrackedCamera();
                if (ownedCameraRT != null) ReleaseOwnedCameraRT();

                ownedCameraRT = new RenderTexture(VideoTargetWidth, VideoTargetHeight, 24, RenderTextureFormat.ARGB32)
                {
                    name = $"PMSDKSkin_{name}_CameraRT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                ownedCameraRT.Create();

                trackedCamera = SourceCamera;
                prevCameraTarget = trackedCamera.targetTexture;
                trackedCamera.targetTexture = ownedCameraRT;
            }

            CurrentTexture = ownedCameraRT;
        }

        private void ApplyWebcamMode()
        {
            if (ownedWebcam == null
                || ownedWebcam.deviceName != (WebcamDeviceName ?? "")
                || ownedWebcam.requestedWidth != WebcamWidth
                || ownedWebcam.requestedHeight != WebcamHeight)
            {
                ReleaseOwnedWebcam();

                if (string.IsNullOrEmpty(WebcamDeviceName))
                    ownedWebcam = new WebCamTexture(WebcamWidth, WebcamHeight, WebcamFps);
                else
                    ownedWebcam = new WebCamTexture(WebcamDeviceName, WebcamWidth, WebcamHeight, WebcamFps);
                ownedWebcam.name = $"PMSDKSkin_{name}_Webcam";
                ownedWebcam.wrapMode = TextureWrapMode.Clamp;

                // Only start playback in play mode — WebCamTexture.Play in edit mode can
                // leave the device open across domain reloads.
                if (Application.isPlaying) ownedWebcam.Play();
            }
            CurrentTexture = ownedWebcam;
        }

        private void ApplyTestPatternMode()
        {
            bool needsRebuild = ownedPatternTex == null
                || TestPatternType != lastPatternType
                || TestPatternSize != lastPatternSize
                || TestPatternGridSize != lastPatternGrid
                || TestPatternColorA != lastPatternColorA
                || TestPatternColorB != lastPatternColorB;

            if (needsRebuild)
            {
                if (ownedPatternTex != null) ReleaseOwnedPatternTex();
                int sz = Mathf.Max(16, TestPatternSize);
                ownedPatternTex = new Texture2D(sz, sz, TextureFormat.RGBA32, mipChain: false)
                {
                    name = $"PMSDKSkin_{name}_Pattern_{TestPatternType}",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                var pixels = new Color[sz * sz];
                PMSDKTestPatterns.Generate(
                    TestPatternType, pixels, sz,
                    Mathf.Max(1, TestPatternGridSize),
                    TestPatternColorA, TestPatternColorB,
                    default);
                ownedPatternTex.SetPixels(pixels);
                ownedPatternTex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

                lastPatternType  = TestPatternType;
                lastPatternSize  = sz;
                lastPatternGrid  = TestPatternGridSize;
                lastPatternColorA = TestPatternColorA;
                lastPatternColorB = TestPatternColorB;
            }
            CurrentTexture = ownedPatternTex;
        }

        // -------- teardown --------

        private void TeardownAll()
        {
            // Order matters: restore the camera's targetTexture BEFORE releasing our
            // owned RT — otherwise Unity logs "Releasing render texture that is set as
            // Camera.targetTexture!" when the RT is still referenced.
            RestoreTrackedCamera();
            ReleaseOwnedVideoRT();
            ReleaseOwnedCameraRT();
            ReleaseOwnedWebcam();
            ReleaseOwnedPatternTex();
            ReleaseOwnedImageTex();
            watchedVideoSource = null;
            // Do NOT destroy the VideoPlayer here — TeardownAll runs from OnDisable and
            // during mode switches, and Unity blocks "Destroy while GameObject is being
            // activated or deactivated." Leave the component attached and detach its
            // clip/url so the next VideoClip / VideoUrl mode re-uses it. Final destroy
            // happens in OnDestroy below.
            if (videoPlayer != null)
            {
                if (videoPlayer.isPlaying) videoPlayer.Stop();
                videoPlayer.clip = null;
                videoPlayer.url = null;
                videoPlayer.targetTexture = null;
            }
            CurrentTexture = null;
        }

        // Unity auto-destroys attached components when the GameObject is destroyed; a
        // manual DestroyImmediate here would double-destroy and log "Destroying object
        // multiple times." All owned RTs/textures/webcam are released by TeardownAll in
        // OnDisable, which runs first.

        private void ReleaseOwnedVideoRT()
        {
            if (ownedVideoRT == null) return;
            if (ownedVideoRT.IsCreated()) ownedVideoRT.Release();
            if (Application.isEditor && !Application.isPlaying) DestroyImmediate(ownedVideoRT);
            else Destroy(ownedVideoRT);
            ownedVideoRT = null;
        }

        private void ReleaseOwnedCameraRT()
        {
            if (ownedCameraRT == null) return;
            if (ownedCameraRT.IsCreated()) ownedCameraRT.Release();
            if (Application.isEditor && !Application.isPlaying) DestroyImmediate(ownedCameraRT);
            else Destroy(ownedCameraRT);
            ownedCameraRT = null;
        }

        private void RestoreTrackedCamera()
        {
            if (trackedCamera == null) return;
            // Only restore if the camera's current target is still OUR owned RT —
            // otherwise the user (or another system) reassigned it and we shouldn't overwrite.
            if (trackedCamera.targetTexture == ownedCameraRT)
                trackedCamera.targetTexture = prevCameraTarget;
            trackedCamera = null;
            prevCameraTarget = null;
        }

        private void ReleaseOwnedWebcam()
        {
            if (ownedWebcam == null) return;
            if (ownedWebcam.isPlaying) ownedWebcam.Stop();
            if (Application.isEditor && !Application.isPlaying) DestroyImmediate(ownedWebcam);
            else Destroy(ownedWebcam);
            ownedWebcam = null;
        }

        private void ReleaseOwnedPatternTex()
        {
            if (ownedPatternTex == null) return;
            if (Application.isEditor && !Application.isPlaying) DestroyImmediate(ownedPatternTex);
            else Destroy(ownedPatternTex);
            ownedPatternTex = null;
            lastPatternType = (PMSDKTestPatternType)(-1);
            lastPatternSize = -1;
            lastPatternGrid = -1;
        }
    }
}
