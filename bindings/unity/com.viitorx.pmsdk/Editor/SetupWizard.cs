using UnityEngine;
using UnityEditor;
using vxpmsdk.Components;

namespace vxpmsdk.Editor
{
    public class SetupWizard : EditorWindow
    {
        [MenuItem("Tools/Projection Mapping/Setup Wizard")]
        public static void ShowWindow()
        {
            GetWindow<SetupWizard>("PMSDK Setup Wizard");
        }

        private void OnGUI()
        {
            GUILayout.Label("Projection Mapping SDK Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            GUILayout.Label("Select a GameObject with a MeshFilter in the hierarchy.");
            
            if (GUILayout.Button("Setup Projector & Mesh Warp"))
            {
                SetupProjectorForSelected();
            }
        }

        private void SetupProjectorForSelected()
        {
            if (Selection.activeGameObject == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a GameObject in the hierarchy.", "OK");
                return;
            }

            GameObject target = Selection.activeGameObject;
            if (target.GetComponent<MeshFilter>() == null)
            {
                EditorUtility.DisplayDialog("Error", "Selected object must have a MeshFilter.", "OK");
                return;
            }

            // Create Projector
            GameObject projectorObj = new GameObject("PMSDK_Projector");
            projectorObj.transform.position = target.transform.position + new Vector3(0, 0, -5);
            projectorObj.transform.LookAt(target.transform);
            
            PMSDKProjector projectorComp = projectorObj.AddComponent<PMSDKProjector>();

            // Setup Mesh Warp
            PMSDKMeshWarp warpComp = target.GetComponent<PMSDKMeshWarp>();
            if (warpComp == null)
            {
                warpComp = target.AddComponent<PMSDKMeshWarp>();
            }
            warpComp.Projector = projectorComp;

            Selection.activeGameObject = projectorObj;
            Debug.Log("Successfully setup PMSDK Projector and linked to mesh warp.");
        }
    }
}
