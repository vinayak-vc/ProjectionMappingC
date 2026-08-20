using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode tests for the M-Sk4 polish drag-drop routing (<see cref="PMSDKSkinDragDrop"/>).
    /// Covers path-kind classification, image-file loading via LoadImage, video URL
    /// routing, JSON pass-through, and (Editor-only) asset-object routing.
    /// </summary>
    public class PMSDKSkinDragDropTests
    {
        private GameObject skinGO;
        private PMSDKSkinContent content;

        [SetUp]
        public void Setup()
        {
            skinGO = new GameObject("Skin", typeof(MeshFilter), typeof(MeshRenderer));
            var shader = Shader.Find("PMSDK/UnlitWarp") ?? Shader.Find("Unlit/Texture");
            skinGO.GetComponent<MeshRenderer>().sharedMaterial = new Material(shader);
            content = skinGO.AddComponent<PMSDKSkinContent>();
        }

        [TearDown]
        public void Teardown()
        {
            if (skinGO != null) Object.DestroyImmediate(skinGO);
        }

        // ---- KindForPath ----

        [Test]
        public void KindForPath_ClassifiesByExtension()
        {
            Assert.AreEqual(PMSDKSkinDropKind.Image, PMSDKSkinDragDrop.KindForPath("C:/x/y.PNG"));
            Assert.AreEqual(PMSDKSkinDropKind.Image, PMSDKSkinDragDrop.KindForPath("/tmp/a.jpg"));
            Assert.AreEqual(PMSDKSkinDropKind.Image, PMSDKSkinDragDrop.KindForPath("a.tga"));
            Assert.AreEqual(PMSDKSkinDropKind.Video, PMSDKSkinDragDrop.KindForPath("clip.mp4"));
            Assert.AreEqual(PMSDKSkinDropKind.Video, PMSDKSkinDragDrop.KindForPath("clip.MOV"));
            Assert.AreEqual(PMSDKSkinDropKind.Json,  PMSDKSkinDragDrop.KindForPath("set.json"));
            Assert.AreEqual(PMSDKSkinDropKind.Unknown, PMSDKSkinDragDrop.KindForPath("readme.txt"));
            Assert.AreEqual(PMSDKSkinDropKind.Unknown, PMSDKSkinDragDrop.KindForPath(null));
            Assert.AreEqual(PMSDKSkinDropKind.Unknown, PMSDKSkinDragDrop.KindForPath(""));
        }

        // ---- ApplyPath: image ----

        [Test]
        public void ApplyPath_Image_LoadsTextureAndSetsMode()
        {
            // Write a tiny valid PNG via Texture2D.EncodeToPNG, then apply-path back.
            var src = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            src.SetPixels(new[] {
                Color.red, Color.green, Color.blue, Color.white,
                Color.red, Color.green, Color.blue, Color.white,
                Color.red, Color.green, Color.blue, Color.white,
                Color.red, Color.green, Color.blue, Color.white,
            });
            src.Apply();
            var png = src.EncodeToPNG();
            var path = Path.Combine(Application.temporaryCachePath,
                "dragdrop_test_" + System.Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(path, png);

            try
            {
                bool ok = PMSDKSkinDragDrop.ApplyPath(content, path, out var jsonOut);
                Assert.IsTrue(ok);
                Assert.IsNull(jsonOut);
                Assert.AreEqual(PMSDKSkinContentMode.Texture, content.Mode);
                Assert.IsNotNull(content.StaticTexture);
                Assert.IsInstanceOf<Texture2D>(content.StaticTexture);
                Assert.AreEqual(4, content.StaticTexture.width);
                Assert.AreEqual(4, content.StaticTexture.height);
            }
            finally
            {
                Object.DestroyImmediate(src);
                if (content.StaticTexture != null) Object.DestroyImmediate(content.StaticTexture);
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Test]
        public void ApplyPath_ImageMissingFile_ReturnsFalse()
        {
            var missing = Path.Combine(Application.temporaryCachePath,
                "definitely_does_not_exist_" + System.Guid.NewGuid().ToString("N") + ".png");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("image path does not exist.*"));
            bool ok = PMSDKSkinDragDrop.ApplyPath(content, missing, out _);
            Assert.IsFalse(ok);
        }

        // ---- ApplyPath: video ----

        [Test]
        public void ApplyPath_Video_SetsUrlModeWithFileUri()
        {
            var path = Path.Combine(Application.temporaryCachePath, "clip.mp4");
            bool ok = PMSDKSkinDragDrop.ApplyPath(content, path, out var jsonOut);
            Assert.IsTrue(ok);
            Assert.IsNull(jsonOut);
            Assert.AreEqual(PMSDKSkinContentMode.VideoUrl, content.Mode);
            Assert.IsTrue(content.VideoUrl.StartsWith("file:"),
                "Video path should be converted to a file:// URL.");
        }

        // ---- ApplyPath: json ----

        [Test]
        public void ApplyPath_Json_SignalsPathToCaller()
        {
            var path = "C:/some/skinset.json";
            bool ok = PMSDKSkinDragDrop.ApplyPath(content, path, out var jsonOut);
            Assert.IsTrue(ok);
            Assert.AreEqual(path, jsonOut);
            // Content mode isn't changed for JSON — controller-level LoadFrom is the caller's job.
            Assert.AreEqual(PMSDKSkinContentMode.None, content.Mode);
        }

        // ---- ApplyPath: unknown extension ----

        [Test]
        public void ApplyPath_Unknown_ReturnsFalse()
        {
            bool ok = PMSDKSkinDragDrop.ApplyPath(content, "notes.txt", out _);
            Assert.IsFalse(ok);
        }

        // ---- ApplyObject (Editor-only) ----

#if UNITY_EDITOR
        [Test]
        public void ApplyObject_Texture_SetsTextureMode()
        {
            var tex = new Texture2D(2, 2);
            try
            {
                bool ok = PMSDKSkinDragDrop.ApplyObject(content, tex, out var skinSet);
                Assert.IsTrue(ok);
                Assert.IsNull(skinSet);
                Assert.AreEqual(PMSDKSkinContentMode.Texture, content.Mode);
                Assert.AreSame(tex, content.StaticTexture);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void ApplyObject_RenderTexture_SetsRTMode()
        {
            var rt = new RenderTexture(32, 32, 0);
            rt.Create();
            try
            {
                bool ok = PMSDKSkinDragDrop.ApplyObject(content, rt, out _);
                Assert.IsTrue(ok);
                Assert.AreEqual(PMSDKSkinContentMode.RenderTexture, content.Mode);
                Assert.AreSame(rt, content.ExternalRenderTexture);
            }
            finally { rt.Release(); Object.DestroyImmediate(rt); }
        }

        [Test]
        public void ApplyObject_SkinSet_SignalsSetOut()
        {
            var set = ScriptableObject.CreateInstance<PMSDKSkinSet>();
            try
            {
                bool ok = PMSDKSkinDragDrop.ApplyObject(content, set, out var skinSet);
                Assert.IsTrue(ok);
                Assert.AreSame(set, skinSet);
                // Content mode unchanged — the set is applied via the controller path.
                Assert.AreEqual(PMSDKSkinContentMode.None, content.Mode);
            }
            finally { Object.DestroyImmediate(set); }
        }

        [Test]
        public void ApplyObject_UnknownType_ReturnsFalse()
        {
            var go = new GameObject("Foo"); // GameObject isn't a valid drop target.
            try
            {
                bool ok = PMSDKSkinDragDrop.ApplyObject(content, go, out _);
                Assert.IsFalse(ok);
            }
            finally { Object.DestroyImmediate(go); }
        }
#endif
    }
}
