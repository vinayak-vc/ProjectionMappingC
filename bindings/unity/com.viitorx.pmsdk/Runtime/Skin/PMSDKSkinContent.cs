using UnityEngine;
using UnityEngine.Video;

namespace vxpmsdk.Components
{
    /// <summary>Content source kind for a <see cref="PMSDKSkin"/> (v1 set — Camera / VideoUrl /
    /// Spout / NDI / Webcam land in M-Sk3).</summary>
    public enum PMSDKSkinContentMode
    {
        /// <summary>No source — the skin renders whatever texture is already bound (default white).</summary>
        None = 0,
        /// <summary>Static <see cref="Texture"/> reference (Texture2D, Sprite atlas texture, ...).</summary>
        Texture = 1,
        /// <summary>External <see cref="UnityEngine.RenderTexture"/> the caller writes to
        /// (Unity camera, custom shader, video capture, ...).</summary>
        RenderTexture = 2,
        /// <summary>A <see cref="VideoClip"/> asset played on a per-skin <see cref="VideoPlayer"/>
        /// into an owned <see cref="UnityEngine.RenderTexture"/>.</summary>
        VideoClip = 3,
        /// <summary>Built-in <see cref="PMSDKTestPatterns"/> texture (calibration aid).</summary>
        TestPattern = 4,
    }

    /// <summary>
    /// Binds a content source to a <see cref="PMSDKSkin"/>. Exposes the resolved output as
    /// <see cref="CurrentTexture"/>; <see cref="PMSDKSkin"/> reads it each frame and pushes it
    /// into the material via MaterialPropertyBlock.
    ///
    /// Lifecycle: owns a per-skin <see cref="VideoPlayer"/> + <see cref="RenderTexture"/> only
    /// when the source is <see cref="PMSDKSkinContentMode.VideoClip"/>. In every other mode
    /// nothing is allocated. Switching modes tears down the previous owned resources cleanly.
    ///
    /// Playback controls (Play/Pause/Loop/TimeOffset/Rate/Mute) map to the underlying
    /// <see cref="VideoPlayer"/>. For non-video modes they are ignored.
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
        public PMSDKTestPatternType TestPatternType = PMSDKTestPatternType.Checkerboard;
        [Tooltip("Test-pattern texture resolution (square). Rebuilt when Kind or Size changes.")]
        public int TestPatternSize = 512;
        [Tooltip("Checkerboard grid density (used by the Checkerboard pattern only).")]
        public int TestPatternGridSize = 8;
        public Color TestPatternColorA = Color.white;
        public Color TestPatternColorB = Color.black;

        [Header("Playback (VideoClip only)")]
        public bool Play = true;
        public bool Loop = true;
        public bool Mute = false;
        [Range(0f, 0.25f)] public float RestartInterval = 0f; // 0 = never restart from top
        public double StartTime = 0.0;
        [Range(0.1f, 8f)] public float PlaybackRate = 1f;

        [Header("Video Target Size")]
        [Tooltip("Owned RT dimensions (per-skin). Ignored for non-video modes.")]
        public int VideoTargetWidth = 1280;
        public int VideoTargetHeight = 720;

        /// <summary>The texture <see cref="PMSDKSkin"/> should render.</summary>
        public Texture CurrentTexture { get; private set; }

        private VideoPlayer videoPlayer;
        private RenderTexture ownedVideoRT;
        private Texture2D ownedPatternTex;
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
            // Guard against inspector-driven bad values before OnEnable / Update pick them up.
            TestPatternSize = Mathf.Clamp(TestPatternSize, 16, 4096);
            VideoTargetWidth  = Mathf.Clamp(VideoTargetWidth, 16, 8192);
            VideoTargetHeight = Mathf.Clamp(VideoTargetHeight, 16, 8192);
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
                    ApplyVideoClipMode();
                    break;

                case PMSDKSkinContentMode.TestPattern:
                    ApplyTestPatternMode();
                    break;
            }
        }

        private void ApplyVideoClipMode()
        {
            if (VideoClip == null) { CurrentTexture = null; return; }

            // Ensure owned RT sized to VideoTargetWidth/Height.
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
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // audio is a v1.1 add
                videoPlayer.waitForFirstFrame = true;
                videoPlayer.skipOnDrop = true;
                videoPlayer.hideFlags = HideFlags.HideInInspector; // the owning skin exposes the controls
            }
            videoPlayer.clip = VideoClip;
            videoPlayer.targetTexture = ownedVideoRT;
            videoPlayer.isLooping = Loop;
            // Setting playbackSpeed on an un-prepared player is fine; it takes effect on Prepare.
            videoPlayer.playbackSpeed = PlaybackRate;
            SetMuted(videoPlayer, Mute);

            if (Play)
            {
                if (!videoPlayer.isPlaying) videoPlayer.Play();
                if (StartTime > 0.0 && Application.isPlaying && videoPlayer.isPrepared && videoPlayer.time < StartTime)
                    videoPlayer.time = StartTime;
            }
            else
            {
                if (videoPlayer.isPlaying) videoPlayer.Pause();
            }

            CurrentTexture = ownedVideoRT;
        }

        private static void SetMuted(VideoPlayer vp, bool mute)
        {
            // The mute API is per audio-track. Track 0 is the default; guard against no-audio clips.
            if (vp.audioTrackCount == 0) return;
            for (ushort i = 0; i < vp.audioTrackCount; i++) vp.SetDirectAudioMute(i, mute);
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
            ReleaseOwnedVideoRT();
            ReleaseOwnedPatternTex();
            if (videoPlayer != null)
            {
                if (Application.isEditor && !Application.isPlaying) DestroyImmediate(videoPlayer);
                else Destroy(videoPlayer);
                videoPlayer = null;
            }
            CurrentTexture = null;
        }

        private void ReleaseOwnedVideoRT()
        {
            if (ownedVideoRT == null) return;
            if (ownedVideoRT.IsCreated()) ownedVideoRT.Release();
            if (Application.isEditor && !Application.isPlaying) DestroyImmediate(ownedVideoRT);
            else Destroy(ownedVideoRT);
            ownedVideoRT = null;
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
