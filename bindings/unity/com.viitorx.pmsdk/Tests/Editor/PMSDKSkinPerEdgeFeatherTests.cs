using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// EditMode tests for M-Sk4 v1.2 polish: per-edge feather on Ellipse skins.
    /// Covers MaterialPropertyBlock uniforms and JSON round-trip.
    /// </summary>
    public class PMSDKSkinPerEdgeFeatherTests
    {
        private GameObject skinGO;
        private PMSDKSkin skin;

        [SetUp]
        public void Setup()
        {
            skinGO = new GameObject("SkinPerEdge", typeof(MeshFilter), typeof(MeshRenderer));
            var shader = Shader.Find("PMSDK/UnlitWarp") ?? Shader.Find("Unlit/Texture");
            skinGO.GetComponent<MeshRenderer>().sharedMaterial = new Material(shader);
            skin = skinGO.AddComponent<PMSDKSkin>();
            skin.Kind = PMSDKSkinShapeKind.Ellipse;
            skin.enabled = false; skin.enabled = true;
        }

        [TearDown]
        public void Teardown()
        {
            if (skinGO != null) Object.DestroyImmediate(skinGO);
        }

        // ---- MPB uniform ----

        [Test]
        public void Default_PushesUniformScalarAcrossFourChannels()
        {
            skin.PerEdgeFeatherEnabled = false;
            skin.SoftMask = 0.04f;
            skin.SendMessage("Update", SendMessageOptions.DontRequireReceiver);

            var mpb = new MaterialPropertyBlock();
            skinGO.GetComponent<MeshRenderer>().GetPropertyBlock(mpb);
            Vector4 f4 = mpb.GetVector("_MaskFeather4");
            Assert.That(f4.x, Is.EqualTo(0.04f).Within(1e-4f));
            Assert.That(f4.y, Is.EqualTo(0.04f).Within(1e-4f));
            Assert.That(f4.z, Is.EqualTo(0.04f).Within(1e-4f));
            Assert.That(f4.w, Is.EqualTo(0.04f).Within(1e-4f));
        }

        [Test]
        public void Enabled_PushesPerEdgeValues()
        {
            skin.PerEdgeFeatherEnabled = true;
            skin.EdgeFeather = new Vector4(0.10f, 0.20f, 0.30f, 0.40f);
            skin.SoftMask = 0.05f; // should be IGNORED when per-edge is enabled
            skin.SendMessage("Update", SendMessageOptions.DontRequireReceiver);

            var mpb = new MaterialPropertyBlock();
            skinGO.GetComponent<MeshRenderer>().GetPropertyBlock(mpb);
            Vector4 f4 = mpb.GetVector("_MaskFeather4");
            Assert.That(f4.x, Is.EqualTo(0.10f).Within(1e-4f));
            Assert.That(f4.y, Is.EqualTo(0.20f).Within(1e-4f));
            Assert.That(f4.z, Is.EqualTo(0.30f).Within(1e-4f));
            Assert.That(f4.w, Is.EqualTo(0.40f).Within(1e-4f));
        }

        [Test]
        public void Enabled_NegativeValuesClampToZero()
        {
            // Sanity: negative feather makes no physical sense. Push clamps to >= 0.
            skin.PerEdgeFeatherEnabled = true;
            skin.EdgeFeather = new Vector4(-0.1f, 0.2f, 0f, 0.5f);
            skin.SendMessage("Update", SendMessageOptions.DontRequireReceiver);

            var mpb = new MaterialPropertyBlock();
            skinGO.GetComponent<MeshRenderer>().GetPropertyBlock(mpb);
            Vector4 f4 = mpb.GetVector("_MaskFeather4");
            Assert.That(f4.x, Is.EqualTo(0f).Within(1e-4f));
        }

        // ---- JSON round-trip ----

        [Test]
        public void JsonSchema_SerializesPerEdgeFeatherWhenEnabled()
        {
            var projGO = new GameObject("Proj", typeof(Camera));
            projGO.AddComponent<PMSDKProjector>();
            var ctrlGO = new GameObject("Ctrl");
            var ctrl = ctrlGO.AddComponent<PMSDKSkinEditorController>();
            ctrl.Projector = projGO.GetComponent<PMSDKProjector>();
            ctrl.enabled = false; ctrl.enabled = true;
            try
            {
                ctrl.PickTool(PMSDKSkinEditorController.Tool.Ellipse);
                ctrl.AddDefaultShape();
                var s = ctrl.Selected;
                s.PerEdgeFeatherEnabled = true;
                s.EdgeFeather = new Vector4(0.01f, 0.02f, 0.03f, 0.04f);

                var set = PMSDKSkinSetJson.Capture(ctrl);
                Assert.IsNotNull(set.skins[0].mask.perEdgeFeather);
                Assert.AreEqual(4, set.skins[0].mask.perEdgeFeather.Count);
                Assert.That(set.skins[0].mask.perEdgeFeather[0], Is.EqualTo(0.01f).Within(1e-5f));
                Assert.That(set.skins[0].mask.perEdgeFeather[3], Is.EqualTo(0.04f).Within(1e-5f));
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
        public void JsonSchema_ScalarOnly_LeavesPerEdgeEmpty()
        {
            var projGO = new GameObject("Proj", typeof(Camera));
            projGO.AddComponent<PMSDKProjector>();
            var ctrlGO = new GameObject("Ctrl");
            var ctrl = ctrlGO.AddComponent<PMSDKSkinEditorController>();
            ctrl.Projector = projGO.GetComponent<PMSDKProjector>();
            ctrl.enabled = false; ctrl.enabled = true;
            try
            {
                ctrl.PickTool(PMSDKSkinEditorController.Tool.Ellipse);
                ctrl.AddDefaultShape();
                var s = ctrl.Selected;
                s.PerEdgeFeatherEnabled = false;
                s.SoftMask = 0.05f;

                var set = PMSDKSkinSetJson.Capture(ctrl);
                Assert.IsTrue(set.skins[0].mask.perEdgeFeather == null
                    || set.skins[0].mask.perEdgeFeather.Count == 0,
                    "Scalar-only skin should not populate perEdgeFeather.");
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
        public void JsonRoundTrip_RestoresPerEdgeState()
        {
            var projGO = new GameObject("Proj", typeof(Camera));
            projGO.AddComponent<PMSDKProjector>();
            var ctrlGO = new GameObject("Ctrl");
            var ctrl = ctrlGO.AddComponent<PMSDKSkinEditorController>();
            ctrl.Projector = projGO.GetComponent<PMSDKProjector>();
            ctrl.enabled = false; ctrl.enabled = true;
            try
            {
                ctrl.PickTool(PMSDKSkinEditorController.Tool.Ellipse);
                ctrl.AddDefaultShape();
                var s = ctrl.Selected;
                s.PerEdgeFeatherEnabled = true;
                s.EdgeFeather = new Vector4(0.11f, 0.22f, 0.33f, 0.44f);

                string json = PMSDKSkinSetJson.Serialize(ctrl);
                var reloaded = PMSDKSkinSetJson.Deserialize(json);

                // Wipe and re-apply.
                Object.DestroyImmediate(s.gameObject);
                ctrl.RefreshSkinList();
                PMSDKSkinSetJson.Apply(ctrl, reloaded);

                var reloadedSkin = ctrl.Selected;
                Assert.IsTrue(reloadedSkin.PerEdgeFeatherEnabled);
                Assert.That(reloadedSkin.EdgeFeather.x, Is.EqualTo(0.11f).Within(1e-4f));
                Assert.That(reloadedSkin.EdgeFeather.w, Is.EqualTo(0.44f).Within(1e-4f));
            }
            finally
            {
                foreach (var s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None))
                    Object.DestroyImmediate(s.gameObject);
                Object.DestroyImmediate(ctrlGO);
                Object.DestroyImmediate(projGO);
            }
        }
    }
}
