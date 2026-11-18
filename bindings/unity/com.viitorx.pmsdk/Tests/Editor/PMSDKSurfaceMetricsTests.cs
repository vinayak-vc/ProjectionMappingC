using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>
    /// Surface-aware grid validation and the physical-spacing warp map (D-051) on exact synthetic geometry: a
    /// pinhole (or distorted) camera at the origin looking at one tilted plane or two facets folded about a
    /// vertical line, and a pinhole projector beside it. Ground truth is the unfolded surface position of every
    /// camera pixel, so physical errors are known exactly. Simulation only - no hardware involved.
    /// </summary>
    public class PMSDKSurfaceMetricsTests
    {
        private const int CamW = 320;
        private const int CamH = 240;
        private const float Focal = 260f;
        private const float DepthMm = 2000f;
        private const int Step = 2;

        private GameObject projGO;
        private GameObject ctrlGO;
        private PMSDKSkinEditorController controller;
        private string tempFolder;

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
            tempFolder = Path.Combine(Application.temporaryCachePath, "PMSDKSurfaceMetricsTests");
            Directory.CreateDirectory(tempFolder);
        }

        [TearDown]
        public void Teardown()
        {
            foreach (PMSDKSkin s in Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None)) {
                Object.DestroyImmediate(s.gameObject);
            }
            Object.DestroyImmediate(ctrlGO);
            Object.DestroyImmediate(projGO);
            if (Directory.Exists(tempFolder)) {
                Directory.Delete(tempFolder, true);
            }
        }

        // ------------------------------------------------------------------ geometry

        [Test]
        public void UnfoldingKeepsDistancesAlongTheSurface()
        {
            Scene scene = new Scene(40f, null);
            PMSDKSurfaceGeometry geometry = scene.Geometry();
            Assert.AreEqual(2, geometry.Planes.Count);
            Assert.IsTrue(geometry.AreNeighbours(0, 1), "the facets share the fold");

            // One pixel on each facet: unfolded distance must equal the distance walked along the surface.
            Vector2 left = new Vector2(80f, 60f);
            Vector2 right = new Vector2(250f, 190f);
            Vector2 a;
            Vector2 b;
            int planeA;
            int planeB;
            Assert.IsTrue(geometry.TryToSurface(left, left, out a, out planeA));
            Assert.IsTrue(geometry.TryToSurface(right, right, out b, out planeB));
            Assert.AreNotEqual(planeA, planeB);
            Vector2 truthA = scene.TruthSurface(left);
            Vector2 truthB = scene.TruthSurface(right);
            Assert.AreEqual((truthA - truthB).magnitude, (a - b).magnitude, 0.5f, "geodesic distance across the fold");
            Assert.Greater(b.x, a.x, "unfolded x runs with the camera's x");
            Assert.Greater(a.y, b.y, "unfolded y points up as the camera sees it");
        }

        [Test]
        public void GeometryRefusesWhenTheDepthCoversTooLittleOfTheSurface()
        {
            // Hardware case (2026-11-17): stereo depth found one plane covering a fraction of the marked band.
            PMSDKDetectedPlane plane = new PMSDKDetectedPlane {
                Normal = new Vector3(0f, 0f, -1f), D = DepthMm,
                Polygon = new[] { new Vector2(0f, 0f), new Vector2(60f, 0f), new Vector2(60f, CamH - 1f), new Vector2(0f, CamH - 1f) },
            };
            byte[] region = new byte[CamW * CamH];
            for (int i = 0; i < region.Length; i++) {
                region[i] = 255;
            }
            PMSDKCameraIntrinsics intrinsics = new PMSDKCameraIntrinsics { Fx = Focal, Fy = Focal, Cx = CamW * 0.5f, Cy = CamH * 0.5f };
            PMSDKSurfaceGeometry geometry;
            string error;
            Assert.IsFalse(PMSDKSurfaceGeometry.TryBuild(new List<PMSDKDetectedPlane> { plane }, intrinsics, CamW, CamH, null, region, out geometry, out error));
            StringAssert.Contains("cannot resolve", error);
            Assert.IsNull(geometry);
        }

        [Test]
        public void MeasuredPointsRemoveTheSidewaysErrorOfABumpySurface()
        {
            // Hardware case (D-052): the POP band bulges +-5 mm; seen at ~40 deg the ray-plane point slides sideways.
            float tilt = 40f * Mathf.Deg2Rad;
            Vector3 normal = new Vector3(Mathf.Sin(tilt), 0f, -Mathf.Cos(tilt));
            Vector3 along = new Vector3(Mathf.Cos(tilt), 0f, Mathf.Sin(tilt));
            Vector3 anchor = new Vector3(0f, 0f, DepthMm);
            System.Func<Vector3, float> bump = delegate(Vector3 onPlane) {
                float s = Vector3.Dot(onPlane - anchor, along);
                return 6f * Mathf.Exp(-((s - 150f) * (s - 150f) + onPlane.y * onPlane.y) / (2f * 250f * 250f));
            };
            ushort[] depth = new ushort[CamW * CamH];
            Vector3[] truth = new Vector3[CamW * CamH];
            for (int y = 0; y < CamH; y++) {
                for (int x = 0; x < CamW; x++) {
                    Vector3 ray = new Vector3((x - CamW * 0.5f) / Focal, (y - CamH * 0.5f) / Focal, 1f);
                    // Surface = plane + bump along the (camera-facing) normal; fixed-point on the bump height.
                    float h = 0f;
                    Vector3 p = Vector3.zero;
                    for (int k = 0; k < 8; k++) {
                        float d = -Vector3.Dot(normal, anchor) - h;
                        p = ray * (-d / Vector3.Dot(normal, ray));
                        h = bump(p - normal * Vector3.Dot(normal, p - anchor));
                    }
                    depth[y * CamW + x] = (ushort)Mathf.RoundToInt(p.z);
                    truth[y * CamW + x] = p;
                }
            }
            PMSDKDetectedPlane plane = new PMSDKDetectedPlane {
                Normal = normal, D = -Vector3.Dot(normal, anchor),
                Polygon = new[] { new Vector2(0f, 0f), new Vector2(CamW - 1f, 0f), new Vector2(CamW - 1f, CamH - 1f), new Vector2(0f, CamH - 1f) },
            };
            PMSDKCameraIntrinsics intrinsics = new PMSDKCameraIntrinsics { Fx = Focal, Fy = Focal, Cx = CamW * 0.5f, Cy = CamH * 0.5f };
            List<PMSDKDetectedPlane> planes = new List<PMSDKDetectedPlane> { plane };
            PMSDKSurfaceGeometry planar;
            PMSDKSurfaceGeometry measured;
            string error;
            Assert.IsTrue(PMSDKSurfaceGeometry.TryBuild(planes, intrinsics, CamW, CamH, depth, null, false, out planar, out error), error);
            Assert.IsTrue(PMSDKSurfaceGeometry.TryBuild(planes, intrinsics, CamW, CamH, depth, null, true, out measured, out error), error);

            // Distances along the surface between pixel pairs straddling the bump (truth: in-plane positions of the real points).
            System.Random random = new System.Random(5);
            float planarWorst = 0f;
            float measuredWorst = 0f;
            for (int k = 0; k < 400; k++) {
                Vector2 a = new Vector2(random.Next(20, CamW - 20), random.Next(20, CamH - 20));
                Vector2 b = new Vector2(random.Next(20, CamW - 20), random.Next(20, CamH - 20));
                Vector3 ta = truth[(int)a.y * CamW + (int)a.x];
                Vector3 tb = truth[(int)b.y * CamW + (int)b.x];
                Vector3 inPlane = Vector3.ProjectOnPlane(ta - tb, normal);
                float expected = inPlane.magnitude;
                Vector2 pa;
                Vector2 pb;
                int ignored;
                planar.TryToSurface(a, a, out pa, out ignored);
                planar.TryToSurface(b, b, out pb, out ignored);
                planarWorst = Mathf.Max(planarWorst, Mathf.Abs((pa - pb).magnitude - expected));
                measured.TryToSurface(a, a, out pa, out ignored);
                measured.TryToSurface(b, b, out pb, out ignored);
                measuredWorst = Mathf.Max(measuredWorst, Mathf.Abs((pa - pb).magnitude - expected));
            }
            TestContext.WriteLine("worst distance error: ideal plane " + planarWorst + " mm, measured points " + measuredWorst + " mm");
            Assert.Greater(planarWorst, 3f, "the bump must be visible to the plane-only geometry");
            Assert.Less(measuredWorst, 1.5f, "measured points keep distances along the surface");

            // A patch 150 mm behind the surface (another surface seen through the outline) is not part of it.
            for (int y = 100; y < 130; y++) {
                for (int x = 100; x < 130; x++) {
                    depth[y * CamW + x] = (ushort)(depth[y * CamW + x] + 150);
                }
            }
            PMSDKSurfaceGeometry withPatch;
            Assert.IsTrue(PMSDKSurfaceGeometry.TryBuild(planes, intrinsics, CamW, CamH, depth, null, true, out withPatch, out error), error);
            Vector2 mmPatch;
            int planePatch;
            Assert.IsFalse(withPatch.TryToSurface(new Vector2(115f, 115f), new Vector2(115f, 115f), out mmPatch, out planePatch), "off-surface point rejected");
            Assert.IsTrue(withPatch.TryToSurface(new Vector2(200f, 60f), new Vector2(200f, 60f), out mmPatch, out planePatch), "surface point kept");
        }

        [Test]
        public void DepthSpikesDoNotMoveOrDropMeasuredPoints()
        {
            // Hardware case (2026-11-18): triangulated depth accurate to ~1 mm, but ~2 % of pixels are decode spikes
            // hundreds of mm off. A plain box mean spread them into speckle (moved points) and black dots (rejected).
            float tilt = 40f * Mathf.Deg2Rad;
            Vector3 normal = new Vector3(Mathf.Sin(tilt), 0f, -Mathf.Cos(tilt));
            Vector3 anchor = new Vector3(0f, 0f, DepthMm);
            ushort[] clean = new ushort[CamW * CamH];
            for (int y = 0; y < CamH; y++) {
                for (int x = 0; x < CamW; x++) {
                    Vector3 ray = new Vector3((x - CamW * 0.5f) / Focal, (y - CamH * 0.5f) / Focal, 1f);
                    Vector3 p = ray * (Vector3.Dot(normal, anchor) / Vector3.Dot(normal, ray));
                    clean[y * CamW + x] = (ushort)Mathf.RoundToInt(p.z);
                }
            }
            ushort[] spiky = (ushort[])clean.Clone();
            System.Random random = new System.Random(11);
            for (int i = 0; i < spiky.Length; i++) {
                if (random.NextDouble() < 0.02) {
                    int offset = random.Next(300, 1500) * (random.Next(2) == 0 ? -1 : 1);
                    spiky[i] = (ushort)Mathf.Clamp(spiky[i] + offset, 1, 65535);
                }
            }
            PMSDKDetectedPlane plane = new PMSDKDetectedPlane {
                Normal = normal, D = -Vector3.Dot(normal, anchor),
                Polygon = new[] { new Vector2(0f, 0f), new Vector2(CamW - 1f, 0f), new Vector2(CamW - 1f, CamH - 1f), new Vector2(0f, CamH - 1f) },
            };
            PMSDKCameraIntrinsics intrinsics = new PMSDKCameraIntrinsics { Fx = Focal, Fy = Focal, Cx = CamW * 0.5f, Cy = CamH * 0.5f };
            List<PMSDKDetectedPlane> planes = new List<PMSDKDetectedPlane> { plane };
            PMSDKSurfaceGeometry reference;
            PMSDKSurfaceGeometry noisy;
            string error;
            Assert.IsTrue(PMSDKSurfaceGeometry.TryBuild(planes, intrinsics, CamW, CamH, clean, null, true, out reference, out error), error);
            Assert.IsTrue(PMSDKSurfaceGeometry.TryBuild(planes, intrinsics, CamW, CamH, spiky, null, true, out noisy, out error), error);

            int total = 0;
            int dropped = 0;
            float worst = 0f;
            for (int y = 6; y < CamH - 6; y += 3) {
                for (int x = 6; x < CamW - 6; x += 3) {
                    Vector2 pixel = new Vector2(x, y);
                    Vector2 expected;
                    Vector2 measured;
                    int ignored;
                    if (!reference.TryToSurface(pixel, pixel, out expected, out ignored)) {
                        continue;
                    }
                    total++;
                    if (!noisy.TryToSurface(pixel, pixel, out measured, out ignored)) {
                        dropped++;
                        continue;
                    }
                    worst = Mathf.Max(worst, (measured - expected).magnitude);
                }
            }
            TestContext.WriteLine("spiky depth: worst shift " + worst + " mm, dropped " + dropped + " of " + total);
            Assert.Greater(total, 5000);
            Assert.Less(worst, 2f, "spikes must not move surface points");
            Assert.Less(dropped, total / 200, "spikes must not reject surface points");
        }

        [Test]
        public void IsolatedOffSurfaceTexelIsNotBlanked()
        {
            int size = 16;
            float[] rgba = new float[size * size * 4];
            for (int i = 0; i < size * size; i++) {
                rgba[i * 4 + 3] = 1f;
            }
            Rect rect = new Rect(0f, 0f, 1f, 1f);
            List<Vector2> surface = new List<Vector2>();
            for (int y = 0; y < 32; y++) {
                for (int x = 0; x < 32; x++) {
                    Vector2 r = new Vector2((x + 0.5f) / 32f, (y + 0.5f) / 32f);
                    // Texel (8, 8) holds only stray off-surface samples.
                    if (Mathf.FloorToInt(r.x * size) != 8 || Mathf.FloorToInt(r.y * size) != 8) {
                        surface.Add(r);
                    }
                }
            }
            Vector2[] stray = { new Vector2(8.3f / size, 8.3f / size), new Vector2(8.7f / size, 8.6f / size) };
            PMSDKWarpMapBuilder.BlankOffSurface(rgba, size, rect, surface.ToArray(), stray);
            Assert.AreEqual(1f, rgba[(8 * size + 8) * 4 + 3], "a lone stray texel inside the surface stays valid");
        }

        [Test]
        public void DespeckleFixesWrongTexelsAndHolesButKeepsBlankedFaces()
        {
            // Hardware case (2026-11-18): texels 0.1-0.2 UV off their neighbours (coloured specks) and lone invalid
            // texels (black dots) inside a polygon skin's map.
            int size = 32;
            float[] rgba = new float[size * size * 4];
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    int i = (y * size + x) * 4;
                    // ~0.008 UV per texel, like a 256-texel map of a skin with a 16-cell grid.
                    rgba[i] = 0.3f + x / 128f;
                    rgba[i + 1] = 0.3f + y / 128f;
                    rgba[i + 3] = 1f;
                }
            }
            float[] truth = (float[])rgba.Clone();
            // 2 x 2 cluster of wrong texels.
            for (int y = 10; y < 12; y++) {
                for (int x = 10; x < 12; x++) {
                    rgba[(y * size + x) * 4] += 0.2f;
                    rgba[(y * size + x) * 4 + 1] -= 0.15f;
                }
            }
            // Lone hole.
            rgba[(20 * size + 20) * 4 + 3] = 0f;
            // Blanked side face: rows 26..28, full width.
            for (int y = 26; y <= 28; y++) {
                for (int x = 0; x < size; x++) {
                    int i = (y * size + x) * 4;
                    rgba[i] = 0f;
                    rgba[i + 1] = 0f;
                    rgba[i + 3] = 0f;
                }
            }
            PMSDKWarpMapBuilder.DespeckleMap(rgba, size, 0.02f);
            for (int y = 10; y < 12; y++) {
                for (int x = 10; x < 12; x++) {
                    int i = (y * size + x) * 4;
                    Assert.AreEqual(truth[i], rgba[i], 0.02f, "cluster u");
                    Assert.AreEqual(truth[i + 1], rgba[i + 1], 0.02f, "cluster v");
                }
            }
            int hole = (20 * size + 20) * 4;
            Assert.AreEqual(1f, rgba[hole + 3], "hole filled");
            Assert.AreEqual(truth[hole], rgba[hole], 0.02f);
            Assert.AreEqual(truth[hole + 1], rgba[hole + 1], 0.02f);
            for (int x = 0; x < size; x++) {
                Assert.AreEqual(0f, rgba[(27 * size + x) * 4 + 3], "blanked side face stays dark");
            }
            int smooth = (5 * size + 25) * 4;
            Assert.AreEqual(truth[smooth], rgba[smooth], 1e-6f, "correct texels untouched");
            Assert.AreEqual(truth[smooth + 1], rgba[smooth + 1], 1e-6f);
        }

        [Test]
        public void EnclosedHolesAreFilledButEdgeConnectedBlanksStayDark()
        {
            int size = 32;
            float[] rgba = new float[size * size * 4];
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    int i = (y * size + x) * 4;
                    rgba[i] = 0.3f + x / 128f;
                    rgba[i + 1] = 0.3f + y / 128f;
                    rgba[i + 3] = 1f;
                }
            }
            float[] truth = (float[])rgba.Clone();
            // 5 x 4 hole inside the surface (off-surface samples near a fold).
            for (int y = 12; y < 16; y++) {
                for (int x = 10; x < 15; x++) {
                    rgba[(y * size + x) * 4 + 3] = 0f;
                }
            }
            // Side face reaching the map border.
            for (int y = 24; y < size; y++) {
                for (int x = 20; x < 26; x++) {
                    rgba[(y * size + x) * 4 + 3] = 0f;
                }
            }
            int filled = PMSDKWarpMapBuilder.FillEnclosedHoles(rgba, size);
            Assert.AreEqual(20, filled);
            int centre = (14 * size + 12) * 4;
            Assert.AreEqual(1f, rgba[centre + 3], "hole filled");
            Assert.AreEqual(truth[centre], rgba[centre], 0.01f);
            Assert.AreEqual(truth[centre + 1], rgba[centre + 1], 0.01f);
            Assert.AreEqual(0f, rgba[(28 * size + 22) * 4 + 3], "edge-connected side face stays dark");
        }

        // ------------------------------------------------------------------ metrics

        [Test]
        public void FlatTiltedPlane_UniformGridPasses_CameraPerspectiveGridFails()
        {
            Scene scene = new Scene(0f, null, 35f);
            PMSDKSurfaceGeometry geometry = scene.Geometry();
            Assert.AreEqual(1, geometry.Planes.Count);

            PMSDKSurfaceReport good = scene.Evaluate(geometry, scene.UniformUv);
            TestContext.WriteLine(good.ToText());
            Assert.IsTrue(good.Valid, good.Message);
            Assert.IsTrue(good.Pass, good.ToText());
            Assert.Less(good.RmsMm, 0.2f);

            // A grid that looks regular in the camera image is foreshortened on a tilted plane.
            PMSDKSurfaceReport perspective = scene.Evaluate(geometry, scene.ImageUv);
            TestContext.WriteLine(perspective.ToText());
            Assert.IsTrue(perspective.Valid, perspective.Message);
            Assert.IsFalse(perspective.Pass, "camera-regular grid on a tilted plane is not physically uniform");
            Assert.Greater(perspective.RmsMm, 5f);
            Assert.IsTrue(float.IsNaN(perspective.Faces[0].MaxCellDimensionErrorMm) || perspective.Faces[0].MaxCellDimensionErrorMm > 1f);
        }

        [Test]
        public void FoldedFacets_UniformGridPasses_CameraPerspectiveGridShowsFacetSpacingError()
        {
            Scene scene = new Scene(40f, null);
            PMSDKSurfaceGeometry geometry = scene.Geometry();

            PMSDKSurfaceReport good = scene.Evaluate(geometry, scene.UniformUv);
            TestContext.WriteLine(good.ToText());
            Assert.IsTrue(good.Pass, good.ToText());
            Assert.AreEqual(2, good.Faces.Count);
            Assert.AreEqual(1, good.Transitions.Count, "one fold");
            Assert.Less(good.Transitions[0].JumpMm, 1f);
            foreach (PMSDKSurfaceFaceReport face in good.Faces) {
                Assert.Less(Mathf.Abs(face.HorizontalSpacingErrorPercent), 0.5f);
                Assert.Less(face.StraightnessP95Mm, 0.5f);
            }

            PMSDKSurfaceReport perspective = scene.Evaluate(geometry, scene.ImageUv);
            TestContext.WriteLine(perspective.ToText());
            Assert.IsFalse(perspective.Pass);
            float spread = Mathf.Abs(perspective.Faces[0].HorizontalSpacingErrorPercent - perspective.Faces[1].HorizontalSpacingErrorPercent);
            Assert.Greater(spread, 5f, "the facet turned away from the camera gets a different physical cell width");
            StringAssert.Contains("Facet Transition", perspective.ToText());
        }

        [Test]
        public void LensDistortionIsRemovedBeforeMeasuring()
        {
            RequireNative();
            float[] barrel = { -0.25f, 0.05f, 0f, 0f, 0f };
            Scene scene = new Scene(40f, barrel);
            PMSDKSurfaceGeometry geometry = scene.Geometry();
            PMSDKSurfaceReport withLens = scene.Evaluate(geometry, scene.UniformUv);
            TestContext.WriteLine(withLens.ToText());
            Assert.IsTrue(withLens.Pass, "a physically uniform grid passes when the lens model is applied");

            // Same raw pixels, lens model ignored: straight surface lines look bent, the report must fail.
            Scene noLens = scene.WithoutLensModel();
            PMSDKSurfaceReport ignored = noLens.Evaluate(noLens.Geometry(), scene.UniformUv);
            TestContext.WriteLine(ignored.ToText());
            Assert.IsFalse(ignored.Pass);
            Assert.Greater(ignored.RmsMm, withLens.RmsMm * 5f);
        }

        // ------------------------------------------------------------------ warp map + closed loop

        [Test]
        public void PhysicalSpacingWarpMapPassesWhereTheCameraPerspectiveMapFails()
        {
            RequireNative();
            Scene scene = new Scene(40f, null);
            PMSDKSurfaceGeometry geometry = scene.Geometry();
            PMSDKSkin skin = FullRasterSkin();
            PMSDKWarpMapBuilder builder = NewBuilder();
            try {
                PMSDKGrayCaptureResult capture = scene.CaptureRaster();

                builder.Target = PMSDKWarpMapTarget.CameraPerspective;
                PMSDKWarpMapResult perspectiveResult = new PMSDKWarpMapResult();
                PMSDKWarpMapBuilder.MapSamples perspective;
                string error;
                Assert.IsTrue(builder.TryComputeMap(skin, capture, scene.Intrinsics, null, perspectiveResult, out perspective, out error), error);
                PMSDKSurfaceReport before = builder.EvaluateSurface(scene.CaptureShown(perspective, null), scene.Intrinsics, geometry);
                TestContext.WriteLine("BEFORE (camera perspective)\n" + before.ToText());

                builder.Target = PMSDKWarpMapTarget.PhysicalSpacing;
                PMSDKWarpMapResult physicalResult = new PMSDKWarpMapResult();
                PMSDKWarpMapBuilder.MapSamples physical;
                Assert.IsTrue(builder.TryComputeMap(skin, capture, scene.Intrinsics, geometry, physicalResult, out physical, out error), error);
                PMSDKSurfaceReport after = builder.EvaluateSurface(scene.CaptureShown(physical, null), scene.Intrinsics, geometry);
                TestContext.WriteLine("AFTER (physical spacing)\n" + after.ToText());

                Assert.IsTrue(before.Valid && after.Valid);
                Assert.IsFalse(before.Pass);
                Assert.IsTrue(after.Pass, after.ToText());
                Assert.Less(after.RmsMm, before.RmsMm * 0.25f);
            } finally {
                Object.DestroyImmediate(builder.gameObject);
            }
        }

        [Test]
        public void PhysicalSpacingWarpMapWorksOnAPolygonSkin()
        {
            // D-055: a hand-drawn Polygon skin (mesh UV = bounding box of its raster outline) gets a physical map too.
            RequireNative();
            Scene scene = new Scene(40f, null);
            PMSDKSurfaceGeometry geometry = scene.Geometry();
            List<Vector2> outline = new List<Vector2> {
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.08f), new Vector2(0.9f, 0.6f), new Vector2(0.5f, 0.95f), new Vector2(0.08f, 0.7f),
            };
            PMSDKSkin skin = controller.SpawnSkinFromSnapshot(PMSDKSkinShapeKind.Polygon, outline, "polygon-test", "Polygon test");
            controller.RefreshSkinList();
            Vector2[] corners = PMSDKWarpMapBuilder.ContentToRaster(skin, new[] { Vector2.zero, Vector2.one });
            Assert.Less(Vector2.Distance(corners[0], new Vector2(0.05f, 0.05f)), 1e-4f, "UV 0,0 = bounding box min");
            Assert.Less(Vector2.Distance(corners[1], new Vector2(0.95f, 0.95f)), 1e-4f, "UV 1,1 = bounding box max");

            // Content capture through the polygon: raster -> polygon UV.
            PMSDKGrayCaptureResult raster = scene.CaptureRaster();
            PMSDKGrayCaptureResult capture = new PMSDKGrayCaptureResult { CameraWidth = raster.CameraWidth, CameraHeight = raster.CameraHeight, Success = true };
            for (int i = 0; i < raster.CameraPixels.Count; i++) {
                Vector2 r = raster.ContentUvs[i];
                Vector2 uv = new Vector2((r.x - 0.05f) / 0.9f, (r.y - 0.05f) / 0.9f);
                if (uv.x >= 0f && uv.y >= 0f && uv.x <= 1f && uv.y <= 1f) {
                    capture.CameraPixels.Add(raster.CameraPixels[i]);
                    capture.ContentUvs.Add(uv);
                }
            }
            PMSDKWarpMapBuilder builder = NewBuilder();
            try {
                builder.Target = PMSDKWarpMapTarget.PhysicalSpacing;
                PMSDKWarpMapResult result = new PMSDKWarpMapResult();
                PMSDKWarpMapBuilder.MapSamples samples;
                string error;
                Assert.IsTrue(builder.TryComputeMap(skin, capture, scene.Intrinsics, geometry, result, out samples, out error), error);
                PMSDKSurfaceReport report = builder.EvaluateSurface(scene.CaptureShown(samples, null), scene.Intrinsics, geometry);
                TestContext.WriteLine(report.ToText());
                Assert.IsTrue(report.Pass, report.ToText());
                Assert.IsFalse(string.IsNullOrEmpty(skin.WarpMapFile));
            } finally {
                Object.DestroyImmediate(builder.gameObject);
            }
        }

        [Test]
        public void ClosedLoopCorrectionReducesASystematicDisplayError()
        {
            RequireNative();
            Scene scene = new Scene(40f, null);
            PMSDKSurfaceGeometry geometry = scene.Geometry();
            PMSDKSkin skin = FullRasterSkin();
            PMSDKWarpMapBuilder builder = NewBuilder();
            try {
                builder.Target = PMSDKWarpMapTarget.PhysicalSpacing;
                PMSDKGrayCaptureResult capture = scene.CaptureRaster();
                PMSDKWarpMapResult result = new PMSDKWarpMapResult();
                PMSDKWarpMapBuilder.MapSamples samples;
                string error;
                Assert.IsTrue(builder.TryComputeMap(skin, capture, scene.Intrinsics, geometry, result, out samples, out error), error);

                // A display that does not show exactly what the map says (smooth, non-affine bias in v).
                System.Func<Vector2, Vector2> bias = delegate(Vector2 uv) {
                    return new Vector2(0f, 0.03f * Mathf.Sin(Mathf.PI * uv.x));
                };
                PMSDKGrayCaptureResult shown = scene.CaptureShown(samples, bias);
                PMSDKSurfaceReport first = builder.EvaluateSurface(shown, scene.Intrinsics, geometry);
                Assert.IsFalse(first.Pass, "the bias must be visible: " + first.ToText());

                float previous = first.RmsMm;
                for (int pass = 0; pass < 3; pass++) {
                    Vector2[] corrected = PMSDKWarpMapBuilder.CorrectTargets(samples, shown);
                    float[] rgba;
                    float coverage;
                    Assert.IsTrue(PMSDKAutoAdjustNative.TryBuildWarpMap(samples.Raster, corrected, samples.Rect, builder.MapSize, builder.MapSize, 12f,
                        out rgba, out coverage, out error), error);
                    samples = new PMSDKWarpMapBuilder.MapSamples {
                        RawPixels = samples.RawPixels, Raster = samples.Raster, Target = samples.Target, CameraWidth = samples.CameraWidth,
                        Rect = samples.Rect, Rgba = rgba,
                    };
                    shown = scene.CaptureShown(samples, bias);
                    PMSDKSurfaceReport next = builder.EvaluateSurface(shown, scene.Intrinsics, geometry);
                    TestContext.WriteLine("pass " + (pass + 1) + ": RMS " + next.RmsMm + " mm");
                    Assert.LessOrEqual(next.RmsMm, previous * 1.05f, "corrections must not oscillate");
                    previous = next.RmsMm;
                }
                Assert.Less(previous, first.RmsMm * 0.3f, "closed loop removes most of the display bias");
            } finally {
                Object.DestroyImmediate(builder.gameObject);
            }
        }

        [Test]
        public void OffSurfaceSamplesBlankTheMapButNotTheSurface()
        {
            // Side face below a fold: its samples must leave the map dark there instead of being gap-filled with content.
            int size = 16;
            float[] rgba = new float[size * size * 4];
            for (int i = 0; i < size * size; i++) {
                rgba[i * 4] = 0.5f;
                rgba[i * 4 + 1] = 0.5f;
                rgba[i * 4 + 3] = 1f;
            }
            Rect rect = new Rect(0f, 0f, 1f, 1f);
            List<Vector2> surface = new List<Vector2>();
            List<Vector2> offSurface = new List<Vector2>();
            for (int y = 0; y < 64; y++) {
                for (int x = 0; x < 64; x++) {
                    Vector2 r = new Vector2((x + 0.5f) / 64f, (y + 0.5f) / 64f);
                    if (r.y > 0.25f) {
                        surface.Add(r);
                    } else {
                        offSurface.Add(r);
                    }
                }
            }
            // One texel (x 0.5..0.5625, y 0.1875..0.25) with both: 16 off-surface samples, 20 surface samples - stays.
            for (int k = 0; k < 20; k++) {
                surface.Add(new Vector2(0.505f + 0.0025f * k, 0.19f + 0.0025f * (k % 20)));
            }
            PMSDKWarpMapBuilder.BlankOffSurface(rgba, size, rect, surface.ToArray(), offSurface.ToArray());
            Vector2 uv;
            Assert.IsFalse(PMSDKWarpMapBuilder.TrySampleMap(rgba, size, rect, new Vector2(0.3f, 0.1f), out uv), "side face is dark");
            Assert.IsTrue(PMSDKWarpMapBuilder.TrySampleMap(rgba, size, rect, new Vector2(0.3f, 0.6f), out uv), "surface keeps content");
            int texel = Mathf.FloorToInt(0.2f * size) * size + Mathf.FloorToInt(0.55f * size);
            Assert.AreEqual(1f, rgba[texel * 4 + 3], "a texel where the surface dominates stays valid");
        }

        // ------------------------------------------------------------------ calibration grid

        [Test]
        public void CalibrationGridHasEqualCellsAndLabels()
        {
            int width;
            int height;
            Color32[] pixels = PMSDKCalibrationGrid.Render(8, 4, 64, out width, out height);
            Assert.AreEqual(512, width);
            Assert.AreEqual(256, height);
            // Every vertical grid line is white at mid-height; mid-cell is not.
            for (int c = 0; c <= 8; c++) {
                int x = Mathf.Min(c * 64, width - 1);
                Assert.AreEqual(255, pixels[(height / 2 + 20) * width + x].r, "line " + c);
            }
            Assert.Less(pixels[(height / 2 + 20) * width + 32 + 10].r, 100);
            // Each cell carries a label (yellow pixels near its top-left corner).
            int labelled = 0;
            for (int r = 0; r < 4; r++) {
                for (int c = 0; c < 8; c++) {
                    bool found = false;
                    for (int y = (r + 1) * 64 - 20; y < (r + 1) * 64 && !found; y++) {
                        for (int x = c * 64; x < c * 64 + 40 && !found; x++) {
                            Color32 p = pixels[y * width + x];
                            found = p.r > 200 && p.g > 150 && p.b < 120;
                        }
                    }
                    labelled += found ? 1 : 0;
                }
            }
            Assert.AreEqual(32, labelled);
        }

        // ------------------------------------------------------------------ helpers

        private static void RequireNative()
        {
            if (!PMSDKAutoAdjustNative.IsAvailable()) {
                Assert.Ignore("ProjectionMappingSDK.dll without Auto Adjust exports is loaded.");
            }
        }

        private PMSDKSkin FullRasterSkin()
        {
            PMSDKSkin skin = controller.SpawnSkinFromSnapshot(PMSDKSkinShapeKind.Rectangle, new List<Vector2> {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f),
            }, "surface-test", "Surface test");
            controller.RefreshSkinList();
            return skin;
        }

        private PMSDKWarpMapBuilder NewBuilder()
        {
            PMSDKWarpMapBuilder builder = new GameObject("Builder").AddComponent<PMSDKWarpMapBuilder>();
            builder.OutputFolder = tempFolder;
            builder.MapSize = 128;
            builder.SetReportGrid(8, 8);
            return builder;
        }

        /// <summary>
        /// Camera at the origin (x right, y down, z forward). One plane tilted by <c>tiltDeg</c> about the vertical
        /// axis, or two facets: the left one facing the camera at z = 2000 mm, the right one hinged on the vertical
        /// line through the optical axis and turned away by <c>foldDeg</c>.
        /// </summary>
        private sealed class Scene
        {
            public readonly PMSDKCameraIntrinsics Intrinsics;
            private readonly float foldDeg;
            private readonly float tiltDeg;
            private readonly bool single;
            private readonly Vector2[] rawPixels;
            private readonly Vector2[] undistorted;
            private readonly List<PMSDKDetectedPlane> planes = new List<PMSDKDetectedPlane>();
            private readonly ushort[] depth;

            // Pinhole projector beside the camera, axes parallel to the camera's; raster normalised, y up.
            private static readonly Vector3 ProjectorPosition = new Vector3(250f, -150f, 0f);
            private const float ProjectorFocal = 0.42f;

            public Scene(float foldDeg, float[] distortion, float tiltDeg = 0f)
            {
                this.foldDeg = foldDeg;
                this.tiltDeg = tiltDeg;
                single = foldDeg == 0f;
                Intrinsics = new PMSDKCameraIntrinsics { Fx = Focal, Fy = Focal, Cx = CamW * 0.5f, Cy = CamH * 0.5f, Distortion = distortion ?? new float[0] };
                List<Vector2> raw = new List<Vector2>();
                for (int y = 0; y < CamH; y += Step) {
                    for (int x = 0; x < CamW; x += Step) {
                        raw.Add(new Vector2(x, y));
                    }
                }
                rawPixels = raw.ToArray();
                Vector2[] all = new Vector2[CamW * CamH];
                for (int i = 0; i < all.Length; i++) {
                    all[i] = new Vector2(i % CamW, i / CamW);
                }
                Vector2[] allUndistorted = Undistort(all);
                undistorted = new Vector2[rawPixels.Length];
                for (int i = 0; i < rawPixels.Length; i++) {
                    undistorted[i] = allUndistorted[(int)rawPixels[i].y * CamW + (int)rawPixels[i].x];
                }

                depth = new ushort[CamW * CamH];
                List<Vector2> leftOutline = new List<Vector2>();
                List<Vector2> rightOutline = new List<Vector2>();
                for (int i = 0; i < all.Length; i++) {
                    Vector3 p = Point(allUndistorted[i]);
                    depth[i] = (ushort)Mathf.RoundToInt(p.z);
                }
                // Outlines in raw pixels, kept 4 px away from the fold so the depth decides there.
                int fold = FoldColumn(allUndistorted);
                if (single) {
                    planes.Add(MakePlane(0, new[] { new Vector2(2f, 2f), new Vector2(CamW - 3f, 2f), new Vector2(CamW - 3f, CamH - 3f), new Vector2(2f, CamH - 3f) }));
                } else {
                    planes.Add(MakePlane(0, new[] { new Vector2(2f, 2f), new Vector2(fold - 4f, 2f), new Vector2(fold - 4f, CamH - 3f), new Vector2(2f, CamH - 3f) }));
                    planes.Add(MakePlane(1, new[] { new Vector2(fold + 4f, 2f), new Vector2(CamW - 3f, 2f), new Vector2(CamW - 3f, CamH - 3f), new Vector2(fold + 4f, CamH - 3f) }));
                }
            }

            private Scene(Scene source, PMSDKCameraIntrinsics intrinsics)
            {
                foldDeg = source.foldDeg;
                tiltDeg = source.tiltDeg;
                single = source.single;
                Intrinsics = intrinsics;
                rawPixels = source.rawPixels;
                undistorted = source.undistorted;
                planes = source.planes;
                depth = source.depth;
            }

            /// <summary>Same captures, but the measuring code is told the lens has no distortion.</summary>
            public Scene WithoutLensModel()
            {
                return new Scene(this, new PMSDKCameraIntrinsics { Fx = Intrinsics.Fx, Fy = Intrinsics.Fy, Cx = Intrinsics.Cx, Cy = Intrinsics.Cy });
            }

            public PMSDKSurfaceGeometry Geometry()
            {
                PMSDKSurfaceGeometry geometry;
                string error;
                Assert.IsTrue(PMSDKSurfaceGeometry.TryBuild(planes, Intrinsics, CamW, CamH, depth, null, out geometry, out error), error);
                return geometry;
            }

            /// <summary>Content UV of a physically uniform grid: unfolded surface mm, scaled into 0..1.</summary>
            public Vector2 UniformUv(int i)
            {
                Vector2 mm = TruthSurfaceOfIndex(i);
                return new Vector2((mm.x + 2500f) / 5000f, (mm.y + 1500f) / 3000f);
            }

            /// <summary>Content UV that is regular in the (undistorted) camera image.</summary>
            public Vector2 ImageUv(int i)
            {
                Vector2 u = undistorted[i];
                return new Vector2(u.x / CamW, 1f - u.y / CamH);
            }

            public PMSDKSurfaceReport Evaluate(PMSDKSurfaceGeometry geometry, System.Func<int, Vector2> uvOf)
            {
                Vector2[] measuredUndistorted = UndistortWith(rawPixels, Intrinsics);
                List<Vector2> raw = new List<Vector2>();
                List<Vector2> surface = new List<Vector2>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> facets = new List<int>();
                for (int i = 0; i < rawPixels.Length; i++) {
                    Vector2 mm;
                    int plane;
                    if (geometry.TryToSurface(rawPixels[i], measuredUndistorted[i], out mm, out plane)) {
                        raw.Add(rawPixels[i]);
                        surface.Add(mm);
                        uvs.Add(uvOf(i));
                        facets.Add(plane);
                    }
                }
                return PMSDKSurfaceMetrics.Evaluate(raw, surface, uvs, facets, geometry, 8, 8, PMSDKSurfaceTolerance.Default());
            }

            /// <summary>Gray-code result of a full-raster skin: camera pixel -> projector raster (= content UV).</summary>
            public PMSDKGrayCaptureResult CaptureRaster()
            {
                PMSDKGrayCaptureResult capture = new PMSDKGrayCaptureResult { CameraWidth = CamW, CameraHeight = CamH, Success = true };
                for (int i = 0; i < rawPixels.Length; i++) {
                    capture.CameraPixels.Add(rawPixels[i]);
                    capture.ContentUvs.Add(Raster(Point(undistorted[i])));
                }
                return capture;
            }

            /// <summary>Content shown at every camera pixel through a built map (plus an optional display bias).</summary>
            public PMSDKGrayCaptureResult CaptureShown(PMSDKWarpMapBuilder.MapSamples map, System.Func<Vector2, Vector2> bias)
            {
                int size = Mathf.RoundToInt(Mathf.Sqrt(map.Rgba.Length / 4f));
                PMSDKGrayCaptureResult capture = new PMSDKGrayCaptureResult { CameraWidth = CamW, CameraHeight = CamH, Success = true };
                for (int i = 0; i < rawPixels.Length; i++) {
                    Vector2 uv;
                    if (PMSDKWarpMapBuilder.TrySampleMap(map.Rgba, size, map.Rect, Raster(Point(undistorted[i])), out uv)) {
                        capture.CameraPixels.Add(rawPixels[i]);
                        capture.ContentUvs.Add(bias != null ? uv + bias(uv) : uv);
                    }
                }
                return capture;
            }

            public Vector2 TruthSurface(Vector2 undistortedPixel)
            {
                return Unfold(Point(undistortedPixel));
            }

            private Vector2 TruthSurfaceOfIndex(int i)
            {
                return Unfold(Point(undistorted[i]));
            }

            /// <summary>Exact unfolded position: x along the surface from the fold (or the plane's centre line), y up.</summary>
            private Vector2 Unfold(Vector3 p)
            {
                if (single) {
                    float t = tiltDeg * Mathf.Deg2Rad;
                    Vector3 along = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t));
                    return new Vector2(Vector3.Dot(p - new Vector3(0f, 0f, DepthMm), along), -p.y);
                }
                if (p.x <= 0f) {
                    return new Vector2(p.x, -p.y);
                }
                float f = foldDeg * Mathf.Deg2Rad;
                Vector3 alongRight = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
                return new Vector2(Vector3.Dot(p - new Vector3(0f, 0f, DepthMm), alongRight), -p.y);
            }

            private Vector3 Point(Vector2 undistortedPixel)
            {
                Vector3 ray = new Vector3((undistortedPixel.x - CamW * 0.5f) / Focal, (undistortedPixel.y - CamH * 0.5f) / Focal, 1f);
                if (single) {
                    return Intersect(ray, TiltedNormal(tiltDeg), DepthMm);
                }
                Vector3 left = Intersect(ray, new Vector3(0f, 0f, -1f), DepthMm);
                if (left.x <= 0f) {
                    return left;
                }
                return Intersect(ray, TiltedNormal(foldDeg), DepthMm);
            }

            /// <summary>Normal of a plane through (0, 0, depth) turned away by <paramref name="degrees"/> about the y axis.</summary>
            private static Vector3 TiltedNormal(float degrees)
            {
                float t = degrees * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(t), 0f, -Mathf.Cos(t));
            }

            private static Vector3 Intersect(Vector3 ray, Vector3 normal, float depthAtAxis)
            {
                // normal . X + d = 0 through (0, 0, depthAtAxis).
                float d = -normal.z * depthAtAxis;
                float t = -d / Vector3.Dot(normal, ray);
                return ray * t;
            }

            private PMSDKDetectedPlane MakePlane(int index, Vector2[] outline)
            {
                Vector3 normal = single ? TiltedNormal(tiltDeg) : (index == 0 ? new Vector3(0f, 0f, -1f) : TiltedNormal(foldDeg));
                return new PMSDKDetectedPlane { Normal = normal, D = -normal.z * DepthMm, Polygon = outline, Quad = outline };
            }

            private static int FoldColumn(Vector2[] allUndistorted)
            {
                // Optical axis column (the fold line projects there); with distortion take the raw column whose
                // undistorted x is closest to the centre on the middle row.
                int best = CamW / 2;
                float bestDistance = float.MaxValue;
                int row = CamH / 2;
                for (int x = 0; x < CamW; x++) {
                    float d = Mathf.Abs(allUndistorted[row * CamW + x].x - CamW * 0.5f);
                    if (d < bestDistance) {
                        bestDistance = d;
                        best = x;
                    }
                }
                return best;
            }

            private static Vector2 Raster(Vector3 p)
            {
                Vector3 q = p - ProjectorPosition;
                return new Vector2(0.5f + ProjectorFocal * q.x / q.z, 0.5f - ProjectorFocal * q.y / q.z);
            }

            private Vector2[] Undistort(Vector2[] points)
            {
                return UndistortWith(points, Intrinsics);
            }

            private static Vector2[] UndistortWith(Vector2[] points, PMSDKCameraIntrinsics intrinsics)
            {
                if (intrinsics.Distortion == null || intrinsics.Distortion.Length == 0) {
                    return points;
                }
                Vector2[] output;
                string error;
                Assert.IsTrue(PMSDKAutoAdjustNative.TryUndistort(points, intrinsics, out output, out error), error);
                return output;
            }
        }
    }
}
