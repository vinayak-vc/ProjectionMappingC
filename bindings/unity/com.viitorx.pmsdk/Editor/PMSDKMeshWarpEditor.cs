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

            // The native warp engine outputs mesh vertices in normalized projector
            // raster space: a [0,1]x[0,1] quad on the local XY plane (z=0), where
            // position = gridwarp(uv). The correct rig is therefore an ORTHOGRAPHIC
            // camera framing that unit quad exactly — not a perspective camera
            // looking at a 3D plane (the warped quad would be edge-on and invisible).

            // 1. Normalize the warp surface transform for raster space: identity
            // rotation, 16:9 horizontal stretch so the unit quad fills the raster.
            if (warp.transform.rotation != Quaternion.identity)
            {
                Undo.RecordObject(warp.transform, "Normalize Warp Surface Rotation");
                warp.transform.rotation = Quaternion.identity;
                Debug.Log($"{warp.gameObject.name}: rotation reset to identity (warped mesh lives in local XY raster space).");
            }
            if (warp.transform.localScale == Vector3.one)
            {
                Undo.RecordObject(warp.transform, "Scale Warp Surface To Aspect");
                warp.transform.localScale = new Vector3(16f / 9f, 1f, 1f);
            }

            // 2. Create an orthographic Projector camera framing the unit quad.
            if (warp.Projector == null)
            {
                GameObject projObj = new GameObject("PMSDK_Projector_" + warp.gameObject.name);

                Camera cam = projObj.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 0.5f * warp.transform.localScale.y;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 50f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                // Render the screen's own layer, not Default: screens live on an
                // isolated layer (e.g. TransparentFX) so projectors don't see the
                // raw 3D content behind them.
                cam.cullingMask = (1 << LayerMask.NameToLayer("UI")) | (1 << warp.gameObject.layer);

                // Center on the quad ([0,1]^2 in local space => center at (0.5, 0.5)),
                // pulled back along -Z so the quad is in front of the camera.
                Vector3 quadCenter = warp.transform.TransformPoint(new Vector3(0.5f, 0.5f, 0f));
                projObj.transform.position = quadCenter - warp.transform.forward * 5f;
                projObj.transform.rotation = warp.transform.rotation;

                PMSDKProjector proj = projObj.AddComponent<PMSDKProjector>();

                Undo.RegisterCreatedObjectUndo(projObj, "Create Projector Camera");

                warp.Projector = proj;
                EditorUtility.SetDirty(warp);
                Debug.Log($"Created new orthographic Projector Camera: {projObj.name}");
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

            // 3. Add dependencies. The test pattern is a calibration aid that swaps
            // the screen material while enabled, so it is added disabled by default.
            PMSDKTestPattern pattern = warp.gameObject.GetComponent<PMSDKTestPattern>();
            if (pattern == null)
            {
                pattern = Undo.AddComponent<PMSDKTestPattern>(warp.gameObject);
                pattern.enabled = false;
            }
            AddIfMissing<PMSDKCornerPin>(warp.gameObject);
            AddIfMissing<PMSDKEdgeBlend>(warp.gameObject);

            // 4. Ensure the scene has the runtime calibration + display services
            // (F2 keyboard calibration with persistence; Display.Activate in builds).
            if (Object.FindFirstObjectByType<PMSDKCalibrationManager>() == null)
            {
                GameObject services = GameObject.Find("PMSDK Runtime Services") ?? new GameObject("PMSDK Runtime Services");
                if (services.GetComponent<PMSDKDisplayActivator>() == null) services.AddComponent<PMSDKDisplayActivator>();
                services.AddComponent<PMSDKCalibrationManager>();
                Undo.RegisterCreatedObjectUndo(services, "Create PMSDK Runtime Services");
                Debug.Log("Added PMSDK Runtime Services (calibration manager + display activator).");
            }

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
