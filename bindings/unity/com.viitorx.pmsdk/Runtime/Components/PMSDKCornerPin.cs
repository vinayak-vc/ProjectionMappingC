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
        private pmsdk_vec3_t[] pointsBuffer = new pmsdk_vec3_t[4];

        private void OnEnable()
        {
            warp = GetComponent<PMSDKMeshWarp>();
        }

        private void Update()
        {
            if (warp == null || warp.Projector == null || warp.Projector.NativeWarpNode == IntPtr.Zero)
                return;

            PMSDKProjector projector = warp.Projector;

            // Set warp node deformation type to Grid (2)
            NativeBindings.pmsdk_warpnode_set_deformation_type(projector.NativeWarpNode, 2);
            IntPtr gridwarp = NativeBindings.pmsdk_warpnode_get_gridwarp(projector.NativeWarpNode);

            if (gridwarp != IntPtr.Zero)
            {
                // Fill 2x2 grid (BottomLeft, BottomRight, TopLeft, TopRight)
                // Grid warp expects coordinates in row-major order starting from y=0
                pointsBuffer[0] = new pmsdk_vec3_t { x = BottomLeft.x, y = BottomLeft.y, z = 0 };
                pointsBuffer[1] = new pmsdk_vec3_t { x = BottomRight.x, y = BottomRight.y, z = 0 };
                pointsBuffer[2] = new pmsdk_vec3_t { x = TopLeft.x, y = TopLeft.y, z = 0 };
                pointsBuffer[3] = new pmsdk_vec3_t { x = TopRight.x, y = TopRight.y, z = 0 };

                NativeBindings.pmsdk_gridwarp_set_control_points(gridwarp, 2, 2, pointsBuffer);
            }
        }
    }
}
