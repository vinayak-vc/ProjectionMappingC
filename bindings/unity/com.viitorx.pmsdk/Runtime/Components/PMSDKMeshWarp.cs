using System;
using UnityEngine;
using vxpmsdk;

namespace vxpmsdk.Components
{
    [RequireComponent(typeof(MeshFilter))]
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

            Vector3[] unityVerts = originalMesh.vertices;
            Vector3[] unityNormals = originalMesh.normals;
            Vector2[] unityUvs = originalMesh.uv;
            int[] unityIndices = originalMesh.triangles;

            vertexBuffer = new pmsdk_vertex_t[unityVerts.Length];
            for (int i = 0; i < unityVerts.Length; i++)
            {
                vertexBuffer[i].position = new pmsdk_vec3_t { x = unityVerts[i].x, y = unityVerts[i].y, z = unityVerts[i].z };
                if (unityNormals.Length > i)
                    vertexBuffer[i].normal = new pmsdk_vec3_t { x = unityNormals[i].x, y = unityNormals[i].y, z = unityNormals[i].z };
                if (unityUvs.Length > i)
                    vertexBuffer[i].uv = new pmsdk_vec2_t { x = unityUvs[i].x, y = unityUvs[i].y };
                vertexBuffer[i].color = new pmsdk_vec4_t { x = 1, y = 1, z = 1, w = 1 };
            }

            indexBuffer = new uint[unityIndices.Length];
            for (int i = 0; i < unityIndices.Length; i++)
            {
                indexBuffer[i] = (uint)unityIndices[i];
            }

            NativeBindings.pmsdk_mesh_set_vertices(nativeInputMesh, vertexBuffer, (UIntPtr)vertexBuffer.Length);
            NativeBindings.pmsdk_mesh_set_indices(nativeInputMesh, indexBuffer, (UIntPtr)indexBuffer.Length);
        }

        private void Update()
        {
            if (Projector == null || Projector.NativeWarpNode == IntPtr.Zero || nativeInputMesh == IntPtr.Zero)
                return;

            // Process warp using native C++ multithreaded backend
            pmsdk_status_t status = NativeBindings.pmsdk_warpnode_process_mesh(
                Projector.NativeWarpNode, nativeInputMesh, nativeOutputMesh);

            if (status == pmsdk_status_t.PMSDK_SUCCESS)
            {
                // Pull data back and apply to Unity mesh
                NativeBindings.pmsdk_mesh_get_vertices(nativeOutputMesh, vertexBuffer, (UIntPtr)vertexBuffer.Length);

                Vector3[] unityVerts = new Vector3[vertexBuffer.Length];
                for (int i = 0; i < vertexBuffer.Length; i++)
                {
                    unityVerts[i] = new Vector3(vertexBuffer[i].position.x, vertexBuffer[i].position.y, vertexBuffer[i].position.z);
                }
                warpedMesh.vertices = unityVerts;
                warpedMesh.RecalculateBounds();
            }
        }
    }
}
