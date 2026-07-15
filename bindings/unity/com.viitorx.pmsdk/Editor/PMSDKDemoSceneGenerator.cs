using UnityEngine;
using UnityEditor;
using vxpmsdk.Components;

namespace vxpmsdk.Editor
{
    public class PMSDKDemoSceneGenerator : UnityEditor.Editor
    {
        [MenuItem("Tools/Projection Mapping/Generate Demo Scene")]
        public static void GenerateDemoScene()
        {
            // 1. Create the Content Camera (renders to a RenderTexture)
            GameObject contentCamObj = new GameObject("Content Camera");
            Camera contentCam = contentCamObj.AddComponent<Camera>();
            contentCamObj.transform.position = new Vector3(0, 1, -5);

            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            rt.name = "Projection_RT";
            rt.Create();
            contentCam.targetTexture = rt;

            // 2. Create some 3D content for the Content Camera to see
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Bouncing Cube";
            cube.transform.position = new Vector3(0, 0, 0);
            cube.layer = LayerMask.NameToLayer("Water"); // Isolate Content
            cube.AddComponent<Rigidbody>().useGravity = false;
            
            contentCam.cullingMask = 1 << LayerMask.NameToLayer("Water");

            // 3. Create a Material that uses the RenderTexture
            Material rtMat = new Material(Shader.Find("Unlit/Texture"));
            rtMat.mainTexture = rt;

            // 4. Create the Projection Planes
            GameObject leftPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            leftPlane.name = "Left_Screen";
            leftPlane.layer = LayerMask.NameToLayer("TransparentFX"); // Isolate Screens
            leftPlane.transform.position = new Vector3(-5, 0, 0);
            leftPlane.transform.rotation = Quaternion.Euler(-90, 0, 0);
            leftPlane.GetComponent<MeshRenderer>().sharedMaterial = rtMat;

            GameObject rightPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            rightPlane.name = "Right_Screen";
            rightPlane.layer = LayerMask.NameToLayer("TransparentFX"); // Isolate Screens
            rightPlane.transform.position = new Vector3(5, 0, 0);
            rightPlane.transform.rotation = Quaternion.Euler(-90, 0, 0);
            rightPlane.GetComponent<MeshRenderer>().sharedMaterial = rtMat;

            // 5. Add Core SDK Components to Planes
            var leftWarp = leftPlane.AddComponent<PMSDKMeshWarp>();
            leftPlane.AddComponent<PMSDKTestPattern>();
            leftPlane.AddComponent<PMSDKCornerPin>();
            leftPlane.AddComponent<PMSDKEdgeBlend>();

            var rightWarp = rightPlane.AddComponent<PMSDKMeshWarp>();
            rightPlane.AddComponent<PMSDKTestPattern>();
            rightPlane.AddComponent<PMSDKCornerPin>();
            rightPlane.AddComponent<PMSDKEdgeBlend>();

            // 6. Create Projector Cameras
            GameObject leftProjObj = new GameObject("PMSDK_Projector_Left");
            leftProjObj.transform.position = leftPlane.transform.position + new Vector3(0, 0, -10);
            Camera leftCam = leftProjObj.AddComponent<Camera>();
            leftCam.targetDisplay = 1; // Display 2
            leftCam.cullingMask = (1 << LayerMask.NameToLayer("UI")) | (1 << LayerMask.NameToLayer("TransparentFX"));
            var leftProjector = leftProjObj.AddComponent<PMSDKProjector>();

            GameObject rightProjObj = new GameObject("PMSDK_Projector_Right");
            rightProjObj.transform.position = rightPlane.transform.position + new Vector3(0, 0, -10);
            Camera rightCam = rightProjObj.AddComponent<Camera>();
            rightCam.targetDisplay = 2; // Display 3
            rightCam.cullingMask = (1 << LayerMask.NameToLayer("UI")) | (1 << LayerMask.NameToLayer("TransparentFX"));
            var rightProjector = rightProjObj.AddComponent<PMSDKProjector>();

            // 7. Link Projectors
            leftWarp.Projector = leftProjector;
            rightWarp.Projector = rightProjector;

            // 8. Add UI Components AFTER linking projectors so they can find the cameras!
            leftPlane.AddComponent<PMSDKCornerPinUI>();
            rightPlane.AddComponent<PMSDKCornerPinUI>();

            // 9. Fix Skybox leaking by clearing to solid black for Projectors
            leftCam.clearFlags = CameraClearFlags.SolidColor;
            leftCam.backgroundColor = Color.black;
            rightCam.clearFlags = CameraClearFlags.SolidColor;
            rightCam.backgroundColor = Color.black;

            Selection.objects = new UnityEngine.Object[] { leftPlane, rightPlane };
            Debug.Log("Demo scene successfully generated! Press Play and you will see the interactive UI handles on the projector outputs.");
        }
    }
}
