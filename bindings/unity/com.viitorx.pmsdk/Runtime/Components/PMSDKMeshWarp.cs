using System;
using UnityEngine;
using vxpmsdk;

namespace vxpmsdk.Components
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteAlways]
    public class PMSDKMeshWarp : MonoBehaviour
    {
        public PMSDKProjector Projector;
        
        private MeshFilter meshFilter;
        private Mesh originalMesh;
        private Mesh warpedMesh;

        private IntPtr nativeInputMesh = IntPtr.Zero;
        private IntPtr nativeOutputMesh = IntPtr.Zero;

        private pmsdk_vertex_t[] vertexBuffer;
        private uint[] indexBuffer;

        private void OnEnable()
        {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter.sharedMesh != null)
            {
                // Create a runtime copy so we don't permanently distort the original asset
                originalMesh = meshFilter.sharedMesh;
                warpedMesh = Instantiate(originalMesh);
                meshFilter.sharedMesh = warpedMesh;

                InitializeNativeMeshes();
            }
            else
            {
                Debug.LogWarning("PMSDKMeshWarp requires a Mesh to deform! Please assign a Mesh to the MeshFilter.", this);
            }
        }

        private void OnDisable()
        {
            if (nativeInputMesh != IntPtr.Zero)
            {
                NativeBindings.pmsdk_mesh_destroy(nativeInputMesh);
                nativeInputMesh = IntPtr.Zero;
            }
            if (nativeOutputMesh != IntPtr.Zero)
            {
                NativeBindings.pmsdk_mesh_destroy(nativeOutputMesh);
                nativeOutputMesh = IntPtr.Zero;
            }
            
            if (meshFilter != null && originalMesh != null)
            {
                meshFilter.sharedMesh = originalMesh;
            }
            if (warpedMesh != null)
            {
                if (Application.isEditor)
                    DestroyImmediate(warpedMesh);
                else
                    Destroy(warpedMesh);
            }
        }

        private void InitializeNativeMeshes()
        {
            nativeInputMesh = NativeBindings.pmsdk_mesh_create();
            nativeOutputMesh = NativeBindings.pmsdk_mesh_create();

            Vector3[] verts = warpedMesh.vertices;
            Vector3[] normals = warpedMesh.normals;
            Vector2[] uvs = warpedMesh.uv;
            Color[] colors = warpedMesh.colors;

            vertexBuffer = new pmsdk_vertex_t[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                vertexBuffer[i] = new pmsdk_vertex_t
                {
                    position = new pmsdk_vec3_t { x = verts[i].x, y = verts[i].y, z = verts[i].z },
                    normal = new pmsdk_vec3_t { x = normals[i].x, y = normals[i].y, z = normals[i].z },
                    uv = new pmsdk_vec2_t { x = uvs.Length > i ? uvs[i].x : 0, y = uvs.Length > i ? uvs[i].y : 0 },
                    color = new pmsdk_vec4_t { x = colors.Length > i ? colors[i].r : 1, y = colors.Length > i ? colors[i].g : 1, z = colors.Length > i ? colors[i].b : 1, w = colors.Length > i ? colors[i].a : 1 }
                };
            }

            indexBuffer = (uint[])(object)warpedMesh.triangles;

            NativeBindings.pmsdk_mesh_set_vertices(nativeInputMesh, vertexBuffer, (UIntPtr)vertexBuffer.Length);
            NativeBindings.pmsdk_mesh_set_indices(nativeInputMesh, indexBuffer, (UIntPtr)indexBuffer.Length);
        }

        private void Update()
        {
            if (Projector == null)
            {
                Debug.LogWarning("PMSDKMeshWarp is missing a linked Projector! Please assign a PMSDKProjector to this component.", this);
                return;
            }
            
            if (Projector.NativeWarpNode == IntPtr.Zero || nativeInputMesh == IntPtr.Zero)
                return;

            // Process warp using native C++ multithreaded backend
            pmsdk_status_t status = NativeBindings.pmsdk_warpnode_process_mesh(
                Projector.NativeWarpNode, nativeInputMesh, nativeOutputMesh);

            if (status == pmsdk_status_t.PMSDK_SUCCESS)
            {
                // If EdgeBlend is attached to this object, apply it to the mesh
                PMSDKEdgeBlend edgeBlend = GetComponent<PMSDKEdgeBlend>();
                if (edgeBlend != null && edgeBlend.NativeBlendConfig != IntPtr.Zero)
                {
                    NativeBindings.pmsdk_blendconfig_apply_to_mesh(edgeBlend.NativeBlendConfig, nativeOutputMesh);
                }

                // Pull data back and apply to Unity mesh
                NativeBindings.pmsdk_mesh_get_vertices(nativeOutputMesh, vertexBuffer, (UIntPtr)vertexBuffer.Length);

                Vector3[] unityVerts = new Vector3[vertexBuffer.Length];
                Color[] unityColors = new Color[vertexBuffer.Length];
                
                for (int i = 0; i < vertexBuffer.Length; i++)
                {
                    unityVerts[i] = new Vector3(vertexBuffer[i].position.x, vertexBuffer[i].position.y, vertexBuffer[i].position.z);
                    unityColors[i] = new Color(vertexBuffer[i].color.x, vertexBuffer[i].color.y, vertexBuffer[i].color.z, vertexBuffer[i].color.w);
                }
                warpedMesh.vertices = unityVerts;
                warpedMesh.colors = unityColors;
                warpedMesh.RecalculateBounds();
            }
        }
    }
}
