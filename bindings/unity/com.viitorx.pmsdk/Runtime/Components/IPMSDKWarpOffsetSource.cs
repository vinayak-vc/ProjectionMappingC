using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// A component that adds a per-vertex raster offset after the native warp (corner pin). Read by
    /// <see cref="PMSDKMeshWarp"/> on the same GameObject. <see cref="PMSDKSkin"/> implements it
    /// with the Auto Adjust correction grid (D-044).
    /// </summary>
    public interface IPMSDKWarpOffsetSource
    {
        /// <summary>True when offsets should be applied this frame.</summary>
        bool HasWarpOffsets { get; }

        /// <summary>Offset in normalized raster units for a vertex with content UV <paramref name="uv"/>.</summary>
        Vector2 EvaluateWarpOffset(Vector2 uv);

        /// <summary>True when the material needs the per-vertex raster position in UV1 (warp map, D-046).</summary>
        bool NeedsRasterUV { get; }
    }
}
