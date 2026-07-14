using System;

namespace vxpmsdk.Core
{
    public class Projector : IDisposable
    {
        internal IntPtr Handle { get; private set; }
        private bool _disposed = false;

        public Projector()
        {
            Handle = NativeBindings.pmsdk_projector_create();
            if (Handle == IntPtr.Zero)
            {
                throw new OutOfMemoryException("Failed to allocate PMSDK projector.");
            }
        }

        ~Projector()
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
                    NativeBindings.pmsdk_projector_destroy(Handle);
                    Handle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        private void CheckDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Projector));
        }

        public void SetAspectRatio(float ratio)
        {
            CheckDisposed();
            var status = NativeBindings.pmsdk_projector_set_aspect_ratio(Handle, ratio);
            PMSDKException.ThrowIfFailed(status, "Failed to set projector aspect ratio.");
        }

        public void SetThrowRatio(float ratio)
        {
            CheckDisposed();
            var status = NativeBindings.pmsdk_projector_set_throw_ratio(Handle, ratio);
            PMSDKException.ThrowIfFailed(status, "Failed to set projector throw ratio.");
        }
    }
}
