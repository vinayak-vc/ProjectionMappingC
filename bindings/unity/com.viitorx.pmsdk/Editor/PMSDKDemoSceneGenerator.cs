using UnityEngine;
using UnityEditor;
using vxpmsdk.Components;

namespace vxpmsdk.Editor
{
    public class PMSDKDemoSceneGenerator : UnityEditor.Editor
    {
        // All generated objects (RenderTexture, materials, physics material) must be
        // saved as assets. In-memory objects referenced by the scene become missing
        // references after a scene reload, which silently breaks the RT->screen chain.
        private const string kAssetFolder = "Assets/PMSDKDemo";

        // The native warp engine outputs meshes in normalized projector raster space
        // ([0,1]x[0,1] on local XY, position = gridwarp(uv)), so warp surfaces are
        // unit quads framed by orthographic cameras — placed far from the 3D content
        // world so neither camera group sees the other.
        private const float kWarpSpaceX = 100f;
        private const float kAspect = 16f / 9f;
        // Each projector shows a horizontal slice of the content RT with an overlap
        // band in the middle; the edge-blend ramps fade the overlap so the two
        // projections sum to even brightness.
        private const float kOverlap = 0.10f;

        [MenuItem("Tools/Projection Mapping/Generate Demo Scene")]
        public static void GenerateDemoScene()
        {
            EnsureAssetFolder();

            // 1. Content Camera renders the 3D game world (Water layer) into the RT.
            GameObject contentCamObj = new GameObject("Content Camera");
            Camera contentCam = contentCamObj.AddComponent<Camera>();
            contentCamObj.transform.position = new Vector3(0, 1, -5);

            RenderTexture rt = CreateOrLoadAsset(() => new RenderTexture(1920, 1080, 24), kAssetFolder + "/Projection_RT.renderTexture");
            contentCam.targetTexture = rt;
            contentCam.cullingMask = 1 << LayerMask.NameToLayer("Water");

            // 2. 3D content: a cube that actually bounces (gravity + bouncy physics
            //    material + a floor to bounce off), isolated on the Water layer.
            PhysicsMaterial bouncy = CreateOrLoadAsset(() =>
            {
                var mat = new PhysicsMaterial("PMSDK_Bouncy");
                mat.bounciness = 1f;
                mat.bounceCombine = PhysicsMaterialCombine.Maximum;
                mat.dynamicFriction = 0f;
                mat.staticFriction = 0f;
                return mat;
            }, kAssetFolder + "/PMSDK_Bouncy.asset"); // .asset: CreateAsset rejects the .physicsMaterial extension

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Bouncing Cube";
            cube.transform.position = new Vector3(0, 3, 0);
            cube.layer = LayerMask.NameToLayer("Water");
            cube.GetComponent<BoxCollider>().sharedMaterial = bouncy;
            Rigidbody rb = cube.AddComponent<Rigidbody>();
            rb.useGravity = true;
            // A restitution-1 box tumbles chaotically off corner impacts and wanders
            // off the floor within seconds. Lock it to a clean vertical bounce, and
            // cap velocity so PhysX restitution overshoot can't add energy each hit.
            // (The cap must be applied by a runtime component: maxLinearVelocity is
            // not serialized and resets on play mode entry.)
            rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            cube.AddComponent<PMSDKVelocityCap>().MaxLinearVelocity = 8.9f;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Content Floor";
            floor.transform.position = new Vector3(0, -1.5f, 0);
            floor.layer = LayerMask.NameToLayer("Water");
            floor.GetComponent<MeshCollider>().sharedMaterial = bouncy;

            // 3. Split-slice screen materials (PMSDK/UnlitWarp honors the vertex
            //    colors written by the edge-blend stage; Unlit/Texture does not).
            float slice = 0.5f + kOverlap * 0.5f;
            Material leftMat = CreateScreenMaterial("PMSDK_Screen_Left", rt, new Vector2(slice, 1f), new Vector2(0f, 0f));
            Material rightMat = CreateScreenMaterial("PMSDK_Screen_Right", rt, new Vector2(slice, 1f), new Vector2(1f - slice, 0f));

            // 4. Warp surfaces + orthographic projector cameras.
            float blendFraction = kOverlap / slice; // overlap band as fraction of each raster
            GameObject leftScreen = CreateWarpSurface("Left_Screen", new Vector3(kWarpSpaceX, 0, 0), leftMat);
            GameObject rightScreen = CreateWarpSurface("Right_Screen", new Vector3(kWarpSpaceX + 10f, 0, 0), rightMat);

            var leftWarp = AddSdkComponents(leftScreen, rightEdgeBlend: blendFraction, leftEdgeBlend: 0f);
            var rightWarp = AddSdkComponents(rightScreen, rightEdgeBlend: 0f, leftEdgeBlend: blendFraction);

            Camera leftCam = CreateProjectorCamera("PMSDK_Projector_Left", leftScreen, 1, out var leftProjector);
            Camera rightCam = CreateProjectorCamera("PMSDK_Projector_Right", rightScreen, 2, out var rightProjector);

            // 5. Link Projectors.
            leftWarp.Projector = leftProjector;
            rightWarp.Projector = rightProjector;

            // 6. Runtime services:
            //    - DisplayActivator: without Display.Activate() standalone builds never
            //      light up Display 2/3 and the physical projectors stay black.
            //    - CalibrationManager: on-site keyboard calibration + persistence
            //      (F2, see docs/calibration-ux-design.md). Replaces PMSDKCornerPinUI.
            GameObject services = new GameObject("PMSDK Runtime Services");
            services.AddComponent<PMSDKDisplayActivator>();
            services.AddComponent<PMSDKCalibrationManager>();

            Selection.objects = new UnityEngine.Object[] { leftScreen, rightScreen };
            Debug.Log("Demo scene generated. In the Editor use the Game view Display 2/3 dropdown to preview each projector; in builds the DisplayActivator lights the physical outputs.");
        }

        private static Material CreateScreenMaterial(string name, RenderTexture rt, Vector2 tiling, Vector2 offset)
        {
            Material mat = CreateOrLoadAsset(() =>
            {
                var m = new Material(Shader.Find("PMSDK/UnlitWarp"));
                m.name = name;
                return m;
            }, kAssetFolder + "/" + name + ".mat");
            mat.mainTexture = rt;
            mat.mainTextureScale = tiling;
            mat.mainTextureOffset = offset;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateWarpSurface(string name, Vector3 position, Material material)
        {
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.name = name;
            surface.layer = LayerMask.NameToLayer("TransparentFX"); // Isolate from content
            surface.transform.position = position;
            // Identity rotation: after native processing the mesh is a [0,1]^2 quad
            // on the local XY plane. The 16:9 X-scale stretches it to raster aspect.
            surface.transform.rotation = Quaternion.identity;
            surface.transform.localScale = new Vector3(kAspect, 1f, 1f);
            surface.GetComponent<MeshRenderer>().sharedMaterial = material;
            // The MeshCollider still holds the source plane; it serves no purpose on
            // a raster-space quad and would fight the warped vertices.
            var collider = surface.GetComponent<MeshCollider>();
            if (collider != null) DestroyImmediate(collider);
            return surface;
        }

        private static PMSDKMeshWarp AddSdkComponents(GameObject surface, float rightEdgeBlend, float leftEdgeBlend)
        {
            var warp = surface.AddComponent<PMSDKMeshWarp>();
            // Test pattern is a calibration aid: added disabled so the demo shows
            // content by default. Enabling it swaps the screen material; disabling
            // restores the original.
            var pattern = surface.AddComponent<PMSDKTestPattern>();
            pattern.enabled = false;
            surface.AddComponent<PMSDKCornerPin>();
            var blend = surface.AddComponent<PMSDKEdgeBlend>();
            blend.LeftEdge = leftEdgeBlend;
            blend.RightEdge = rightEdgeBlend;
            blend.Gamma = 2.2f;
            return warp;
        }

        private static Camera CreateProjectorCamera(string name, GameObject surface, int targetDisplay, out PMSDKProjector projector)
        {
            GameObject projObj = new GameObject(name);
            Camera cam = projObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.5f * surface.transform.localScale.y;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            cam.targetDisplay = targetDisplay;
            cam.cullingMask = (1 << LayerMask.NameToLayer("UI")) | (1 << surface.layer);
            // Solid black clear: Skybox clear flags would blast the skybox into the
            // physical room and ruin edge blending.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            // Frame the unit quad: center (0.5, 0.5) in surface-local space, camera
            // pulled back along -Z.
            Vector3 quadCenter = surface.transform.TransformPoint(new Vector3(0.5f, 0.5f, 0f));
            projObj.transform.position = quadCenter - surface.transform.forward * 5f;
            projObj.transform.rotation = surface.transform.rotation;

            projector = projObj.AddComponent<PMSDKProjector>();
            return cam;
        }

        private static void EnsureAssetFolder()
        {
            if (!AssetDatabase.IsValidFolder(kAssetFolder))
            {
                AssetDatabase.CreateFolder("Assets", "PMSDKDemo");
            }
        }

        private static T CreateOrLoadAsset<T>(System.Func<T> factory, string path) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }
            T created = factory();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
    }
}
