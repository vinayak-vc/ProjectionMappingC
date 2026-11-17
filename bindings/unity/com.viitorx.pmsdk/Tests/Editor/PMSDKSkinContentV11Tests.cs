using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode tests for the v1.1 content sources added in M-Sk3:
    ///   - Camera mode (owned RT + camera targetTexture swap + restore)
    ///   - VideoUrl mode (VideoPlayer.source = Url)
    ///   - Webcam mode (WebCamTexture alloc, no Play() in EditMode)
    ///   - ExternalTexture mode (arbitrary texture pass-through)
    /// Plus the JSON schema extensions and Bindings.Apply for the new modes.
    /// </summary>
    public class PMSDKSkinContentV11Tests
    {
        private GameObject skinGO;
        private PMSDKSkinContent content;

        [SetUp]
        public void Setup()
        {
            skinGO = new GameObject("SkinContentTest", typeof(MeshFilter), typeof(MeshRenderer));
            var shader = Shader.Find("PMSDK/UnlitWarp") ?? Shader.Find("Unlit/Texture");
            skinGO.GetComponent<MeshRenderer>().sharedMaterial = new Material(shader);
            content = skinGO.AddComponent<PMSDKSkinContent>();
        }

        [TearDown]
        public void Teardown()
        {
            if (skinGO != null) Object.DestroyImmediate(skinGO);
        }

        // ---- Camera mode ----

        [Test]
        public void CameraMode_AllocatesRTAndSwapsTargetTexture()
        {
            var camGO = new GameObject("SourceCam", typeof(Camera));
            var cam = camGO.GetComponent<Camera>();
            var priorTarget = new RenderTexture(64, 64, 0);
            priorTarget.Create();
            cam.targetTexture = priorTarget;

            try
            {
                content.Mode = PMSDKSkinContentMode.Camera;
                content.SourceCamera = cam;
                content.VideoTargetWidth = 256;
                content.VideoTargetHeight = 128;
                content.enabled = false; content.enabled = true;

                Assert.IsNotNull(content.CurrentTexture);
                var rt = content.CurrentTexture as RenderTexture;
                Assert.IsNotNull(rt);
                Assert.AreEqual(256, rt.width);
                Assert.AreEqual(128, rt.height);
                Assert.AreSame(rt, cam.targetTexture,
                    "Camera's targetTexture should be swapped to the skin's owned RT.");
            }
            finally
            {
                content.Mode = PMSDKSkinContentMode.None;
                content.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                // The prior target should be restored on mode switch.
                Assert.AreSame(priorTarget, cam.targetTexture,
                    "Camera's original targetTexture should be restored on teardown.");
                // Unbind BEFORE releasing priorTarget so Unity doesn't log the
                // "Releasing render texture that is set as Camera.targetTexture!" warning
                // (the RT is still referenced by cam if we release first).
                cam.targetTexture = null;
                if (priorTarget != null) { priorTarget.Release(); Object.DestroyImmediate(priorTarget); }
                Object.DestroyImmediate(camGO);
            }
        }

        [Test]
        public void CameraMode_NullCamera_ExposesNullTexture()
        {
            content.Mode = PMSDKSkinContentMode.Camera;
            content.SourceCamera = null;
            content.enabled = false; content.enabled = true;
            Assert.IsNull(content.CurrentTexture);
        }

        // ---- VideoUrl mode ----

        [Test]
        public void VideoUrlMode_EmptyUrl_ExposesNullTexture()
        {
            content.Mode = PMSDKSkinContentMode.VideoUrl;
            content.VideoUrl = "";
            content.enabled = false; content.enabled = true;
            Assert.IsNull(content.CurrentTexture);
        }

        [Test]
        public void VideoUrlMode_SetsPlayerToUrlSource()
        {
            content.Mode = PMSDKSkinContentMode.VideoUrl;
            content.VideoUrl = "file:///nonexistent-but-nonempty.mp4";
            content.VideoTargetWidth = 320;
            content.VideoTargetHeight = 240;
            content.Play = false; // don't actually try to play the phony url
            content.enabled = false; content.enabled = true;

            Assert.IsNotNull(content.CurrentTexture);
            var vp = skinGO.GetComponent<UnityEngine.Video.VideoPlayer>();
            Assert.IsNotNull(vp);
            Assert.AreEqual(UnityEngine.Video.VideoSource.Url, vp.source);
            Assert.AreEqual(content.VideoUrl, vp.url);
        }

        // ---- Webcam mode ----

        [Test]
        public void WebcamMode_InEditMode_AllocatesTextureWithoutPlaying()
        {
            content.Mode = PMSDKSkinContentMode.Webcam;
            content.WebcamDeviceName = ""; // default device
            content.WebcamWidth = 320;
            content.WebcamHeight = 240;
            content.enabled = false; content.enabled = true;

            Assert.IsNotNull(content.CurrentTexture);
            var wct = content.CurrentTexture as WebCamTexture;
            Assert.IsNotNull(wct);
            // In EditMode we intentionally skip Play() so we don't leave the device open
            // across domain reloads. isPlaying should be false.
            Assert.IsFalse(wct.isPlaying);
        }

        // ---- ExternalTexture mode ----

        [Test]
        public void ExternalTextureMode_ExposesAssignedTexture()
        {
            var tex = new Texture2D(4, 4);
            try
            {
                content.Mode = PMSDKSkinContentMode.ExternalTexture;
                content.ExternalTexture = tex;
                content.enabled = false; content.enabled = true;
                Assert.AreSame(tex, content.CurrentTexture);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void ExternalTextureMode_TracksFieldChangesEachFrame()
        {
            var a = new Texture2D(2, 2);
            var b = new Texture2D(4, 4);
            try
            {
                content.Mode = PMSDKSkinContentMode.ExternalTexture;
                content.ExternalTexture = a;
                content.enabled = false; content.enabled = true;
                Assert.AreSame(a, content.CurrentTexture);

                // Simulate an adapter (Spout/NDI) swapping the texture — CurrentTexture
                // should pick up the change on the next Update.
                content.ExternalTexture = b;
                content.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                Assert.AreSame(b, content.CurrentTexture);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        // ---- JSON schema extensions ----

        [Test]
        public void JsonSchema_SerializesVideoUrl()
        {
            var projGO = new GameObject("Proj", typeof(Camera));
            projGO.AddComponent<PMSDKProjector>();
            var ctrlGO = new GameObject("Ctrl");
            var ctrl = ctrlGO.AddComponent<PMSDKSkinEditorController>();
            ctrl.Projector = projGO.GetComponent<PMSDKProjector>();
            ctrl.enabled = false; ctrl.enabled = true;
            try
            {
                ctrl.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
                ctrl.AddDefaultShape();
                var c = ctrl.Selected.GetComponent<PMSDKSkinContent>();
                c.Mode = PMSDKSkinContentMode.VideoUrl;
                c.VideoUrl = "https://example.com/video.mp4";
                c.Play = false;

                var set = PMSDKSkinSetJson.Capture(ctrl);
                Assert.AreEqual("VideoUrl", set.skins[0].defaultContent.mode);
                Assert.AreEqual("https://example.com/video.mp4", set.skins[0].defaultContent.url);
            }
            finally
            {
                foreach (var s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None))
                    Object.DestroyImmediate(s.gameObject);
                Object.DestroyImmediate(ctrlGO);
                Object.DestroyImmediate(projGO);
            }
        }

        [Test]
        public void JsonSchema_SerializesWebcamDeviceName()
        {
            var projGO = new GameObject("Proj", typeof(Camera));
            projGO.AddComponent<PMSDKProjector>();
            var ctrlGO = new GameObject("Ctrl");
            var ctrl = ctrlGO.AddComponent<PMSDKSkinEditorController>();
            ctrl.Projector = projGO.GetComponent<PMSDKProjector>();
            ctrl.enabled = false; ctrl.enabled = true;
            try
            {
                ctrl.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
                ctrl.AddDefaultShape();
                var c = ctrl.Selected.GetComponent<PMSDKSkinContent>();
                c.Mode = PMSDKSkinContentMode.Webcam;
                c.WebcamDeviceName = "Logitech C270";

                var set = PMSDKSkinSetJson.Capture(ctrl);
                Assert.AreEqual("Webcam", set.skins[0].defaultContent.mode);
                Assert.AreEqual("Logitech C270", set.skins[0].defaultContent.webcamDeviceName);
            }
            finally
            {
                foreach (var s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None))
                    Object.DestroyImmediate(s.gameObject);
                Object.DestroyImmediate(ctrlGO);
                Object.DestroyImmediate(projGO);
            }
        }

        // ---- Bindings for new modes ----

        [Test]
        public void Bindings_Apply_VideoUrlMode()
        {
            var binding = new PMSDKSkinBindings.Binding
            {
                SkinId = "x",
                Mode = PMSDKSkinContentMode.VideoUrl,
                VideoUrl = "http://example.com/x.mp4",
                Loop = false,
                PlaybackRate = 2f,
            };
            PMSDKSkinBindings.Apply(content, binding);
            Assert.AreEqual(PMSDKSkinContentMode.VideoUrl, content.Mode);
            Assert.AreEqual("http://example.com/x.mp4", content.VideoUrl);
            Assert.IsFalse(content.Loop);
            Assert.AreEqual(2f, content.PlaybackRate, 1e-5f);
        }

        [Test]
        public void Bindings_Apply_CameraMode()
        {
            var camGO = new GameObject("Cam", typeof(Camera));
            try
            {
                var binding = new PMSDKSkinBindings.Binding
                {
                    SkinId = "x",
                    Mode = PMSDKSkinContentMode.Camera,
                    SourceCamera = camGO.GetComponent<Camera>(),
                };
                PMSDKSkinBindings.Apply(content, binding);
                Assert.AreEqual(PMSDKSkinContentMode.Camera, content.Mode);
                Assert.AreSame(camGO.GetComponent<Camera>(), content.SourceCamera);
            }
            finally { Object.DestroyImmediate(camGO); }
        }

        [Test]
        public void Bindings_Apply_WebcamMode()
        {
            var binding = new PMSDKSkinBindings.Binding
            {
                SkinId = "x",
                Mode = PMSDKSkinContentMode.Webcam,
                WebcamDeviceName = "Some Device",
            };
            PMSDKSkinBindings.Apply(content, binding);
            Assert.AreEqual(PMSDKSkinContentMode.Webcam, content.Mode);
            Assert.AreEqual("Some Device", content.WebcamDeviceName);
        }

        [Test]
        public void Bindings_Apply_ExternalTextureMode()
        {
            var tex = new Texture2D(2, 2);
            try
            {
                var binding = new PMSDKSkinBindings.Binding
                {
                    SkinId = "x",
                    Mode = PMSDKSkinContentMode.ExternalTexture,
                    ExternalTexture = tex,
                };
                PMSDKSkinBindings.Apply(content, binding);
                Assert.AreEqual(PMSDKSkinContentMode.ExternalTexture, content.Mode);
                Assert.AreSame(tex, content.ExternalTexture);
            }
            finally { Object.DestroyImmediate(tex); }
        }
    }
}
