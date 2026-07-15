using UnityEngine;
using UnityEditor;
using vxpmsdk.Components;

namespace vxpmsdk.Editor
{
    [CustomEditor(typeof(PMSDKMeshWarp))]
    public class PMSDKMeshWarpEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PMSDKMeshWarp warp = (PMSDKMeshWarp)target;

            GUILayout.Space(20);
            EditorGUILayout.HelpBox("Use the button below to automatically configure all dependencies for this projection screen.", MessageType.Info);

            if (GUILayout.Button("Auto-Configure Full SDK Dependencies", GUILayout.Height(30)))
            {
                AutoConfigure(warp);
            }
        }

        private void AutoConfigure(PMSDKMeshWarp warp)
        {
            Undo.RecordObject(warp.gameObject, "Auto-Configure SDK");

            // 1. Create a Projector if missing
            if (warp.Projector == null)
            {
                GameObject projObj = new GameObject("PMSDK_Projector_" + warp.gameObject.name);
                projObj.transform.position = warp.transform.position + new Vector3(0, 0, -10);
                
                Camera cam = projObj.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = (1 << LayerMask.NameToLayer("UI")) | (1 << LayerMask.NameToLayer("Default"));
                
                PMSDKProjector proj = projObj.AddComponent<PMSDKProjector>();
                
                Undo.RegisterCreatedObjectUndo(projObj, "Create Projector Camera");
                
                warp.Projector = proj;
                EditorUtility.SetDirty(warp);
                Debug.Log($"Created new Projector Camera: {projObj.name}");
            }

            // 2. Ensure the object has a Mesh assigned!
            MeshFilter meshFilter = warp.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh == null)
            {
                // Create a temporary primitive to steal its mesh
                GameObject tempPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Mesh defaultPlaneMesh = tempPlane.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(tempPlane);
                
                Undo.RecordObject(meshFilter, "Assign Default Plane Mesh");
                meshFilter.sharedMesh = defaultPlaneMesh;
                Debug.Log($"Assigned default Unity Plane mesh to {warp.gameObject.name}");
                
                // We should re-enable the warp component so it picks up the new mesh
                warp.enabled = false;
                warp.enabled = true;
            }

            // 3. Add dependencies
            AddIfMissing<PMSDKTestPattern>(warp.gameObject);
            AddIfMissing<PMSDKCornerPin>(warp.gameObject);
            AddIfMissing<PMSDKEdgeBlend>(warp.gameObject);
            AddIfMissing<PMSDKCornerPinUI>(warp.gameObject);

            Debug.Log("Successfully added all required Projection Mapping components!");
        }

        private void AddIfMissing<T>(GameObject obj) where T : Component
        {
            if (obj.GetComponent<T>() == null)
            {
                Undo.AddComponent<T>(obj);
            }
        }
    }
}
