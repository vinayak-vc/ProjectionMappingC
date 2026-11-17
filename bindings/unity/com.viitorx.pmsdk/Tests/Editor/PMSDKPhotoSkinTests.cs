using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>EditMode tests for the photo-to-skin Unity side (placement, mask PNG, skin creation,
    /// JSON round trip, Background content) plus a native integration test that is skipped when the
    /// deployed ProjectionMappingSDK.dll does not contain the SkinGen module yet.</summary>
    public class PMSDKPhotoSkinTests
    {
        private GameObject projGO;
        private GameObject ctrlGO;
        private PMSDKSkinEditorController controller;
        private readonly List<string> createdFiles = new List<string>();

        [SetUp]
        public void Setup()
        {
            projGO = new GameObject("TestProjector", typeof(Camera));
            projGO.AddComponent<PMSDKProjector>();
            ctrlGO = new GameObject("SkinEditor");
            controller = ctrlGO.AddComponent<PMSDKSkinEditorController>();
            controller.Projector = projGO.GetComponent<PMSDKProjector>();
            controller.SkinLayerName = "TransparentFX";
            controller.enabled = false;
            controller.enabled = true;
        }

        [TearDown]
        public void Teardown()
        {
            foreach (PMSDKSkin s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None)) {
                Object.DestroyImmediate(s.gameObject);
            }
            if (ctrlGO != null) {
                Object.DestroyImmediate(ctrlGO);
            }
            if (projGO != null) {
                Object.DestroyImmediate(projGO);
            }
            foreach (string file in createdFiles) {
                if (File.Exists(file)) {
                    File.Delete(file);
                }
            }
        }

        // 4x2 silhouette: top row = left half, bottom row = right half.
        private static PMSDKPhotoExtraction SyntheticExtraction(bool withHole)
        {
            PMSDKPhotoPart part = new PMSDKPhotoPart();
            part.Id = 0;
            part.AreaFraction = 0.5f;
            part.BoundsMin = new Vector2(0.25f, 0.25f);
            part.BoundsMax = new Vector2(0.75f, 0.75f);
            part.Quad[0] = new Vector2(0.25f, 0.25f);
            part.Quad[1] = new Vector2(0.75f, 0.25f);
            part.Quad[2] = new Vector2(0.75f, 0.75f);
            part.Quad[3] = new Vector2(0.25f, 0.75f);
            part.Polygon = new List<Vector2> {
                new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.25f),
                new Vector2(0.75f, 0.75f), new Vector2(0.25f, 0.75f),
            };
            PMSDKPhotoContour outer = new PMSDKPhotoContour();
            outer.Points = new List<Vector2>(part.Polygon);
            part.Contours.Add(outer);
            if (withHole) {
                PMSDKPhotoContour hole = new PMSDKPhotoContour();
                hole.IsHole = true;
                // Clockwise, as the native module returns holes.
                hole.Points = new List<Vector2> {
                    new Vector2(0.4f, 0.4f), new Vector2(0.4f, 0.6f),
                    new Vector2(0.6f, 0.6f), new Vector2(0.6f, 0.4f),
                };
                part.Contours.Add(hole);
            }
            part.MaskX = 250;
            part.MaskY = 250;
            part.MaskWidth = 4;
            part.MaskHeight = 2;
            part.Mask = new byte[] { 255, 255, 0, 0, 0, 0, 255, 255 };

            PMSDKPhotoExtraction extraction = new PMSDKPhotoExtraction();
            extraction.ImageWidth = 1000;
            extraction.ImageHeight = 1000;
            extraction.UsedMode = "Alpha";
            extraction.Parts.Add(part);
            return extraction;
        }

        [Test]
        public void PlaceInRaster_KeepsAspectAndCentres()
        {
            Vector2 centre = PMSDKPhotoSkinBuilder.PlaceInRaster(new Vector2(0.5f, 0.5f), 1000, 1000, 0.9f);
            Assert.AreEqual(0.5f, centre.x, 1e-5f);
            Assert.AreEqual(0.5f, centre.y, 1e-5f);

            // Square photo in a 16:9 raster: limited by height, so it is narrower than it is tall.
            Vector2 bl = PMSDKPhotoSkinBuilder.PlaceInRaster(new Vector2(0f, 0f), 1000, 1000, 0.9f);
            Vector2 tr = PMSDKPhotoSkinBuilder.PlaceInRaster(new Vector2(1f, 1f), 1000, 1000, 0.9f);
            Assert.AreEqual(0.9f, tr.y - bl.y, 1e-5f);
            Assert.AreEqual(0.9f * (1f / PMSDKPhotoSkinBuilder.RasterAspect), tr.x - bl.x, 1e-5f);

            // A very wide photo is limited by width.
            Vector2 wl = PMSDKPhotoSkinBuilder.PlaceInRaster(new Vector2(0f, 0.5f), 4000, 1000, 0.9f);
            Vector2 wr = PMSDKPhotoSkinBuilder.PlaceInRaster(new Vector2(1f, 0.5f), 4000, 1000, 0.9f);
            Assert.AreEqual(0.9f, wr.x - wl.x, 1e-5f);
        }

        [Test]
        public void MaskPng_IsFlippedToUnityOrientation()
        {
            string path = Path.Combine(Application.temporaryCachePath, "pmsdk_test_mask.png");
            createdFiles.Add(path);
            PMSDKPhotoSkinBuilder.WriteMaskPng(path, SyntheticExtraction(false).Parts[0]);

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)));
            Assert.AreEqual(4, tex.width);
            Assert.AreEqual(2, tex.height);
            // Source top row (255,255,0,0) is the texture's TOP row (y = 1).
            Assert.Greater(tex.GetPixel(0, 1).r, 0.9f);
            Assert.Less(tex.GetPixel(3, 1).r, 0.1f);
            Assert.Less(tex.GetPixel(0, 0).r, 0.1f);
            Assert.Greater(tex.GetPixel(3, 0).r, 0.9f);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void QuadWithMask_CreatesRectangleSkinWithMaskFile()
        {
            List<PMSDKSkin> skins = PMSDKPhotoSkinBuilder.CreateSkins(controller, SyntheticExtraction(false),
                PMSDKPhotoSkinMode.QuadWithMask, "Statue");
            Assert.AreEqual(1, skins.Count);
            PMSDKSkin skin = skins[0];
            createdFiles.Add(PMSDKSkinStore.ResolveMaskPath(skin.AlphaMaskPath));

            Assert.AreEqual(PMSDKSkinShapeKind.Rectangle, skin.Kind);
            Assert.AreEqual(4, skin.Vertices.Count);
            Assert.IsFalse(string.IsNullOrEmpty(skin.AlphaMaskPath));
            Assert.IsFalse(Path.IsPathRooted(skin.AlphaMaskPath), "stored as a bare file name");
            Assert.IsTrue(File.Exists(PMSDKSkinStore.ResolveMaskPath(skin.AlphaMaskPath)));
            Assert.AreEqual("Statue", skin.DisplayName);
            // BL below TL, BL left of BR (skin vertex order BL, BR, TR, TL, y up).
            Assert.Less(skin.Vertices[0].y, skin.Vertices[3].y);
            Assert.Less(skin.Vertices[0].x, skin.Vertices[1].x);
        }

        [Test]
        public void MaskFile_SurvivesTheSkinSetJsonRoundTrip()
        {
            List<PMSDKSkin> skins = PMSDKPhotoSkinBuilder.CreateSkins(controller, SyntheticExtraction(false),
                PMSDKPhotoSkinMode.QuadWithMask, "Statue");
            createdFiles.Add(PMSDKSkinStore.ResolveMaskPath(skins[0].AlphaMaskPath));
            string expected = skins[0].AlphaMaskPath;

            PMSDKSkinSetJson.Set set = PMSDKSkinSetJson.Capture(controller);
            string json = PMSDKSkinSetJson.Serialize(set, false);
            PMSDKSkinSetJson.Set back = PMSDKSkinSetJson.Deserialize(json);
            Assert.AreEqual("customAlpha", back.skins[0].mask.kind);
            Assert.AreEqual(expected, back.skins[0].mask.maskFile);

            skins[0].AlphaMaskPath = null;
            PMSDKSkinSetJson.Apply(controller, back);
            controller.RefreshSkinList();
            Assert.AreEqual(expected, controller.Skins[0].AlphaMaskPath);
        }

        [Test]
        public void PolygonFillHoles_AddsBackgroundHoleSkinAboveTheShape()
        {
            List<PMSDKSkin> skins = PMSDKPhotoSkinBuilder.CreateSkins(controller, SyntheticExtraction(true),
                PMSDKPhotoSkinMode.PolygonFillHoles, "Ring");
            controller.RefreshSkinList();
            Assert.AreEqual(1, skins.Count);
            Assert.AreEqual(2, controller.Skins.Count, "shape + one hole skin");

            PMSDKSkin shape = skins[0];
            PMSDKSkin hole = null;
            foreach (PMSDKSkin s in controller.Skins) {
                if (s != shape) {
                    hole = s;
                }
            }
            Assert.IsNotNull(hole);
            Assert.AreEqual(PMSDKSkinContentMode.Background, hole.GetComponent<PMSDKSkinContent>().Mode);
            Assert.Greater(hole.ZOrder, shape.ZOrder);

            // Counter-clockwise (positive shoelace area) after the hole's clockwise points were reversed.
            double area = 0.0;
            for (int i = 0; i < hole.Vertices.Count; i++) {
                Vector2 a = hole.Vertices[i];
                Vector2 b = hole.Vertices[(i + 1) % hole.Vertices.Count];
                area += a.x * b.y - b.x * a.y;
            }
            Assert.Greater(area, 0.0);
        }

        [Test]
        public void PolygonOutline_CreatesOnlyTheOuterShape()
        {
            PMSDKPhotoSkinBuilder.CreateSkins(controller, SyntheticExtraction(true), PMSDKPhotoSkinMode.PolygonOutline, "Ring");
            controller.RefreshSkinList();
            Assert.AreEqual(1, controller.Skins.Count);
            Assert.AreEqual(PMSDKSkinShapeKind.Polygon, controller.Skins[0].Kind);
        }

        [Test]
        public void BackgroundContent_ProvidesAOnePixelTextureOfTheBackgroundColour()
        {
            GameObject go = new GameObject("BgContent");
            try {
                PMSDKSkinContent content = go.AddComponent<PMSDKSkinContent>();
                content.Mode = PMSDKSkinContentMode.Background;
                content.enabled = false;
                content.enabled = true;
                Assert.IsNotNull(content.CurrentTexture);
                Texture2D tex = (Texture2D)content.CurrentTexture;
                Color expected = PMSDKSkinStore.CurrentBackground;
                Color actual = tex.GetPixel(0, 0);
                Assert.AreEqual(expected.r, actual.r, 0.01f);
                Assert.AreEqual(expected.g, actual.g, 0.01f);
                Assert.AreEqual(expected.b, actual.b, 0.01f);
            } finally {
                Object.DestroyImmediate(go);
            }
        }

        // ---- native integration (skipped until the DLL with SkinGen is deployed) ----

        [Test]
        public void Native_StatueFixture_GivesTwoParts()
        {
            if (!PMSDKPhotoSkin.IsAvailable()) {
                Assert.Ignore("The deployed ProjectionMappingSDK.dll has no SkinGen module yet.");
            }
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PMSDKPhotoSkin).Assembly);
            string fixture = Path.GetFullPath(Path.Combine(package.resolvedPath, "..", "..", "..", "tests", "data", "skingen_bronze_statue.png"));
            if (!File.Exists(fixture)) {
                Assert.Ignore("Fixture not found: " + fixture);
            }
            PMSDKPhotoExtraction extraction;
            string error;
            Assert.IsTrue(PMSDKPhotoSkin.TryExtract(fixture, 128, out extraction, out error), error);
            Assert.AreEqual("Alpha", extraction.UsedMode);
            Assert.AreEqual(2, extraction.Parts.Count);
            Assert.Greater(extraction.Parts[0].AreaFraction, extraction.Parts[1].AreaFraction);
            Assert.AreEqual(extraction.Parts[0].MaskWidth * extraction.Parts[0].MaskHeight, extraction.Parts[0].Mask.Length);
            Assert.GreaterOrEqual(extraction.Parts[0].Polygon.Count, 3);
        }
    }
}
