using System;

namespace vxpmsdk.Core
{
    public class WarpNode : IDisposable
    {
        internal IntPtr Handle { get; private set; }
        private bool _disposed = false;

        public WarpNode()
        {
            Handle = NativeBindings.pmsdk_warpnode_create();
            if (Handle == IntPtr.Zero)
            {
                throw new OutOfMemoryException("Failed to allocate PMSDK warp node.");
            }
        }

        ~WarpNode()
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
                    NativeBindings.pmsdk_warpnode_destroy(Handle);
                    Handle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        private void CheckDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WarpNode));
        }

        public void AddChild(WarpNode child)
        {
            CheckDisposed();
            if (child == null) throw new ArgumentNullException(nameof(child));
            
            var status = NativeBindings.pmsdk_warpnode_add_child(Handle, child.Handle);
            PMSDKException.ThrowIfFailed(status, "Failed to add child warp node.");
        }

        public void ProcessMesh(Mesh inputMesh, Mesh outputMesh)
        {
            CheckDisposed();
            if (inputMesh == null) throw new ArgumentNullException(nameof(inputMesh));
            if (outputMesh == null) throw new ArgumentNullException(nameof(outputMesh));

            var status = NativeBindings.pmsdk_warpnode_process_mesh(Handle, inputMesh.Handle, outputMesh.Handle);
            PMSDKException.ThrowIfFailed(status, "Failed to process mesh through warp node.");
        }
    }
}
