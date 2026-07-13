using System;
using UnityEngine;

namespace vxpmsdk.Core
{
    public class BlendConfig : IDisposable
    {
        internal IntPtr Handle { get; private set; }
        private bool _disposed = false;

        public EdgeBlend LeftEdge { get; private set; }
        public EdgeBlend RightEdge { get; private set; }
        public EdgeBlend TopEdge { get; private set; }
        public EdgeBlend BottomEdge { get; private set; }

        public BlendConfig()
        {
            Handle = NativeBindings.pmsdk_blendconfig_create();
            if (Handle == IntPtr.Zero)
            {
                throw new OutOfMemoryException("Failed to allocate PMSDK blend config.");
            }

            LeftEdge = new EdgeBlend(NativeBindings.pmsdk_blendconfig_get_left_edge(Handle));
            RightEdge = new EdgeBlend(NativeBindings.pmsdk_blendconfig_get_right_edge(Handle));
            TopEdge = new EdgeBlend(NativeBindings.pmsdk_blendconfig_get_top_edge(Handle));
            BottomEdge = new EdgeBlend(NativeBindings.pmsdk_blendconfig_get_bottom_edge(Handle));
        }

        ~BlendConfig()
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
                    NativeBindings.pmsdk_blendconfig_destroy(Handle);
                    Handle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        private void CheckDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BlendConfig));
        }

        public float BlackLevel
        {
            get
            {
                CheckDisposed();
                return NativeBindings.pmsdk_blendconfig_get_black_level(Handle);
            }
            set
            {
                CheckDisposed();
                NativeBindings.pmsdk_blendconfig_set_black_level(Handle, value);
            }
        }

        public float Evaluate(float u, float v)
        {
            CheckDisposed();
            return NativeBindings.pmsdk_blendconfig_evaluate(Handle, u, v);
        }
    }
}
