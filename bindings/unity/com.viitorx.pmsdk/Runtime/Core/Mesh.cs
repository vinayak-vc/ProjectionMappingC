using System;
using UnityEngine;

namespace vxpmsdk.Core
{
    public class PMSDKException : Exception
    {
        public pmsdk_status_t Status { get; }

        public PMSDKException(pmsdk_status_t status, string message) : base($"{message} (Status: {status})")
        {
            Status = status;
        }

        public static void ThrowIfFailed(pmsdk_status_t status, string message = "PMSDK operation failed")
        {
            if (status != pmsdk_status_t.PMSDK_SUCCESS)
            {
                throw new PMSDKException(status, message);
            }
        }
    }

    public class Mesh : IDisposable
    {
        internal IntPtr Handle { get; private set; }
        private bool _disposed = false;

        public Mesh()
        {
            Handle = NativeBindings.pmsdk_mesh_create();
            if (Handle == IntPtr.Zero)
            {
                throw new OutOfMemoryException("Failed to allocate PMSDK mesh.");
            }
        }

        ~Mesh()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (Handle != IntPtr.Zero)
                {
                    NativeBindings.pmsdk_mesh_destroy(Handle);
                    Handle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        private void CheckDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Mesh));
        }

        public void SetVertices(pmsdk_vertex_t[] vertices)
        {
            CheckDisposed();
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            var status = NativeBindings.pmsdk_mesh_set_vertices(Handle, vertices, (UIntPtr)vertices.Length);
            PMSDKException.ThrowIfFailed(status, "Failed to set vertices on mesh.");
        }

        public void SetIndices(uint[] indices)
        {
            CheckDisposed();
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            var status = NativeBindings.pmsdk_mesh_set_indices(Handle, indices, (UIntPtr)indices.Length);
            PMSDKException.ThrowIfFailed(status, "Failed to set indices on mesh.");
        }

        public int VertexCount
        {
            get
            {
                CheckDisposed();
                return (int)NativeBindings.pmsdk_mesh_get_vertex_count(Handle);
            }
        }

        public int IndexCount
        {
            get
            {
                CheckDisposed();
                return (int)NativeBindings.pmsdk_mesh_get_index_count(Handle);
            }
        }

        public void RecalculateNormals()
        {
            CheckDisposed();
            var status = NativeBindings.pmsdk_mesh_recalculate_normals(Handle);
            PMSDKException.ThrowIfFailed(status, "Failed to recalculate normals.");
        }

        public void Clear()
        {
            CheckDisposed();
            var status = NativeBindings.pmsdk_mesh_clear(Handle);
            PMSDKException.ThrowIfFailed(status, "Failed to clear mesh.");
        }
    }
}
