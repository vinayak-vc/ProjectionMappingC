using System;
using UnityEngine;

namespace vxpmsdk.Core
{
    public enum CurveType
    {
        Linear = 0,
        Power = 1,
        Smoothstep = 2
    }

    public class EdgeBlend
    {
        internal IntPtr Handle { get; private set; }

        internal EdgeBlend(IntPtr handle)
        {
            Handle = handle;
        }

        public float Size
        {
            get => NativeBindings.pmsdk_edgeblend_get_size(Handle);
            set => NativeBindings.pmsdk_edgeblend_set_size(Handle, value);
        }

        public float Gamma
        {
            get => NativeBindings.pmsdk_edgeblend_get_gamma(Handle);
            set => NativeBindings.pmsdk_edgeblend_set_gamma(Handle, value);
        }

        public CurveType Curve
        {
            get => (CurveType)NativeBindings.pmsdk_edgeblend_get_curve_type(Handle);
            set => NativeBindings.pmsdk_edgeblend_set_curve_type(Handle, (int)value);
        }
    }
}
