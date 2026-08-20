using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode integration tests for <see cref="PMSDKSkin"/> and <see cref="PMSDKSkinContent"/>:
    /// component wiring, kind-driven mesh selection, MPB uniform push, content-source
    /// lifecycle. Runs without entering Play mode.
    /// </summary>
    public class PMSDKSkinTests
    {
        private GameObject go;

        [SetUp]
        public void Setup()
        {
            go = new GameObject("SkinUnderTest", typeof(MeshFilter), typeof(MeshRenderer));
            // Give the renderer a real material so MPB gets applied through it.
            var shader = Shader.Find("PMSDK/UnlitWarp") ?? Shader.Find("Unlit/Texture");
            go.GetComponent<MeshRenderer>().sharedMaterial = new Material(shader);
        }

        [TearDown]
        public void Teardown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        // ---- kind-driven mesh selection ----

        [Test]
        public void Rectangle_ProducesUnitQuadMesh()
        {
            var skin = go.AddComponent<PMSDKSkin>();
            skin.Kind = PMSDKSkinShapeKind.Rectangle;
            // Force OnEnable → OnEnable path by disabling+enabling.
            skin.enabled = false; skin.enabled = true;

            var mf = go.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf.sharedMesh);
            Assert.AreEqual(4, mf.sharedMesh.vertexCount);
            Assert.AreEqual(6, mf.sharedMesh.triangles.Length);
        }

        [Test]
        public void Polygon_ProducesTriangulatedMesh()
        {
            var skin = go.AddComponent<PMSDKSkin>();
            skin.Kind = PMSDKSkinShapeKind.Polygon;
            skin.Vertices = new List<Vector2>
            {
                new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.2f),
                new Vector2(0.8f, 0.8f), new Vector2(0.2f, 0.8f),
                new Vector2(0.1f, 0.5f), // pentagon (concave-ish notch)
            };
            skin.enabled = false; skin.enabled = true;

            var mf = go.GetComponent<MeshFilter>();
            Assert.IsNotNull(mf.sharedMesh);
            Assert.AreEqual(5, mf.sharedMesh.vertexCount);
            Assert.AreEqual((5 - 2) * 3, mf.sharedMesh.triangles.Length);
        }

        [Test]
        public void SwitchingKind_RebuildsMesh()
        {
            var skin = go.AddComponent<PMSDKSkin>();
            skin.Kind = PMSDKSkinShapeKind.Rectangle;
            skin.enabled = false; skin.enabled = true;
            int quadVerts = go.GetComponent<MeshFilter>().sharedMesh.vertexCount;

            // Switch to hexagon polygon.
            skin.Kind = PMSDKSkinShapeKind.Polygon;
            skin.Vertices = new List<Vector2>();
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.PI * 2f * i / 6f;
                skin.Vertices.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.25f, 0.5f + Mathf.Sin(a) * 0.25f));
            }
            skin.SendMessage("Update", SendMessageOptions.DontRequireReceiver);

            int hexVerts = go.GetComponent<MeshFilter>().sharedMesh.vertexCount;
            Assert.AreEqual(4, quadVerts);
            Assert.AreEqual(6, hexVerts);
        }

        // ---- MPB / uniforms ----

        [Test]
        public void Ellipse_SetsShapeMaskUniformToOne()
        {
            var skin = go.AddComponent<PMSDKSkin>();
            skin.Kind = PMSDKSkinShapeKind.Ellipse;
            skin.Opacity = 0.5f;
            skin.SoftMask = 0.03f;
            skin.enabled = false; skin.enabled = true;

            var mpb = new MaterialPropertyBlock();
            go.GetComponent<MeshRenderer>().GetPropertyBlock(mpb);
            Assert.AreEqual(1f, mpb.GetFloat("_ShapeMask"), 1e-4f);
            Assert.AreEqual(0.5f, mpb.GetFloat("_Opacity"), 1e-4f);
            Assert.AreEqual(0.03f, mpb.GetFloat("_MaskFeather"), 1e-4f);
        }

        [Test]
        public void Rectangle_LeavesShapeMaskAtZero()
        {
            var skin = go.AddComponent<PMSDKSkin>();
            skin.Kind = PMSDKSkinShapeKind.Rectangle;
            skin.enabled = false; skin.enabled = true;

            var mpb = new MaterialPropertyBlock();
            go.GetComponent<MeshRenderer>().GetPropertyBlock(mpb);
            Assert.AreEqual(0f, mpb.GetFloat("_ShapeMask"), 1e-4f);
        }

        // ---- z-order ----

        [Test]
        public void ZOrder_ShiftsLocalZ()
        {
            var skin = go.AddComponent<PMSDKSkin>();
            skin.Kind = PMSDKSkinShapeKind.Rectangle;
            skin.ZOrder = 0;
            skin.enabled = false; skin.enabled = true;
            float z0 = go.transform.localPosition.z;

            skin.ZOrder = 10;
            skin.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            float z10 = go.transform.localPosition.z;

            Assert.Less(z10, z0, "Higher zOrder should move the skin FORWARD (toward the camera at -Z).");
        }

        // ---- OnValidate clamping ----

        [Test]
        public void OnValidate_ClampsPolygonVertexCount()
        {
            var skin = go.AddComponent<PMSDKSkin>();
            skin.Kind = PMSDKSkinShapeKind.Polygon;
            skin.Vertices = new List<Vector2>();
            for (int i = 0; i < PMSDKSkinMeshBuilder.MaxPolygonVertices + 5; i++)
                skin.Vertices.Add(new Vector2(0.5f, 0.5f));
            // OnValidate is invoked via the "SendMessage" fallback for tests.
            var mi = typeof(PMSDKSkin).GetMethod(
                "OnValidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mi.Invoke(skin, null);

            Assert.AreEqual(PMSDKSkinMeshBuilder.MaxPolygonVertices, skin.Vertices.Count);
        }

        [Test]
        public void OnValidate_PadsRectangleTo4Vertices()
        {
            var skin = go.AddComponent<PMSDKSkin>();
            skin.Kind = PMSDKSkinShapeKind.Rectangle;
            skin.Vertices = new List<Vector2> { new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.3f) };
            var mi = typeof(PMSDKSkin).GetMethod(
                "OnValidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            mi.Invoke(skin, null);
            Assert.AreEqual(4, skin.Vertices.Count);
        }

        // ---- content lifecycle ----

        [Test]
        public void Content_NoneMode_ExposesNullTexture()
        {
            var content = go.AddComponent<PMSDKSkinContent>();
            content.Mode = PMSDKSkinContentMode.None;
            content.enabled = false; content.enabled = true;
            Assert.IsNull(content.CurrentTexture);
        }

        [Test]
        public void Content_TextureMode_ExposesAssignedTexture()
        {
            var tex = new Texture2D(2, 2);
            try
            {
                var content = go.AddComponent<PMSDKSkinContent>();
                content.Mode = PMSDKSkinContentMode.Texture;
                content.StaticTexture = tex;
                content.enabled = false; content.enabled = true;
                Assert.AreSame(tex, content.CurrentTexture);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void Content_TestPatternMode_AllocatesAndReleasesOwnedTexture()
        {
            var content = go.AddComponent<PMSDKSkinContent>();
            content.Mode = PMSDKSkinContentMode.TestPattern;
            content.TestPatternType = PMSDKTestPatternType.Checkerboard;
            content.TestPatternSize = 64;
            content.enabled = false; content.enabled = true;

            Assert.IsNotNull(content.CurrentTexture);
            Assert.AreEqual(64, content.CurrentTexture.width);

            // Switching to None releases the owned texture.
            content.Mode = PMSDKSkinContentMode.None;
            content.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            Assert.IsNull(content.CurrentTexture);
        }

        [Test]
        public void Content_SwitchingPatternTypeRebuildsTexture()
        {
            var content = go.AddComponent<PMSDKSkinContent>();
            content.Mode = PMSDKSkinContentMode.TestPattern;
            content.TestPatternType = PMSDKTestPatternType.White;
            content.TestPatternSize = 32;
            content.enabled = false; content.enabled = true;
            var texA = content.CurrentTexture;

            content.TestPatternType = PMSDKTestPatternType.Black;
            content.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            var texB = content.CurrentTexture;

            Assert.IsNotNull(texA);
            Assert.IsNotNull(texB);
            Assert.AreNotSame(texA, texB, "Changing pattern type must rebuild the owned texture.");
        }
    }
}
