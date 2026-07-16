using System;
using UnityEngine;
using vxpmsdk;

namespace vxpmsdk.Components
{
    [ExecuteAlways]
    [RequireComponent(typeof(PMSDKMeshWarp))]
    public class PMSDKCornerPin : MonoBehaviour
    {
        public Vector2 TopLeft = new Vector2(0, 1);
        public Vector2 TopRight = new Vector2(1, 1);
        public Vector2 BottomLeft = new Vector2(0, 0);
        public Vector2 BottomRight = new Vector2(1, 0);

        private PMSDKMeshWarp warp;

        private void OnEnable()
        {
            warp = GetComponent<PMSDKMeshWarp>();
        }

        private void Update()
        {
            if (warp == null || warp.Projector == null || warp.Projector.NativeWarpNode == IntPtr.Zero)
                return;

            // A finer grid warp, if present and enabled, owns the deformation —
            // a warp node has a single deformation type, and the N x M grid is the
            // superset control.
            var grid = GetComponent<PMSDKGridWarp>();
            if (grid != null && grid.enabled)
                return;

            PMSDKProjector projector = warp.Projector;

            // Perspective (projective) corner pin — deformation type 3. A 2x2 grid
            // warp would only interpolate bilinearly and shear the texture along the
            // diagonal on a keystoned quad; a homography foreshortens correctly.
            NativeBindings.pmsdk_warpnode_set_deformation_type(projector.NativeWarpNode, 3);
            IntPtr pw = NativeBindings.pmsdk_warpnode_get_perspectivewarp(projector.NativeWarpNode);

            if (pw != IntPtr.Zero)
            {
                NativeBindings.pmsdk_perspectivewarp_set_corners(
                    pw,
                    new pmsdk_vec2_t { x = BottomLeft.x, y = BottomLeft.y },
                    new pmsdk_vec2_t { x = BottomRight.x, y = BottomRight.y },
                    new pmsdk_vec2_t { x = TopRight.x, y = TopRight.y },
                    new pmsdk_vec2_t { x = TopLeft.x, y = TopLeft.y });
            }
        }
    }
}
