using System;
using UnityEngine;
using vxpmsdk;

namespace vxpmsdk.Components
{
    /// <summary>
    /// N x M bilinear grid warp — the advanced counterpart to the 4-corner
    /// PMSDKCornerPin, for mapping onto curved or irregular surfaces where four
    /// corners are not enough. Control points are normalized raster coordinates
    /// (0..1), row-major, y increasing upward (matching the corner-pin convention).
    ///
    /// Disabled by default: when enabled it takes over the warp node (deformation
    /// type Grid) and the corner pin yields to it. Edited via the calibration
    /// manager's grid mode (G), or driven programmatically.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(PMSDKMeshWarp))]
    public class PMSDKGridWarp : MonoBehaviour
    {
        [Min(2)] public int Columns = 3;
        [Min(2)] public int Rows = 3;

        // Row-major control points (length Columns*Rows). Serialized so grid edits
        // persist with the scene and survive domain reloads.
        [SerializeField, HideInInspector] private Vector2[] points;

        private PMSDKMeshWarp warp;
        private pmsdk_vec3_t[] buffer;

        public int PointCount => Columns * Rows;

        private void OnEnable()
        {
            warp = GetComponent<PMSDKMeshWarp>();
            EnsureGrid(false);
        }

        private void OnValidate()
        {
            EnsureGrid(false);
        }

        /// <summary>Index into the row-major control-point array.</summary>
        public int Index(int col, int row) => row * Columns + col;

        public Vector2 GetPoint(int col, int row) => points[Index(col, row)];

        public void SetPoint(int col, int row, Vector2 normalized)
        {
            normalized.x = Mathf.Clamp01(normalized.x);
            normalized.y = Mathf.Clamp01(normalized.y);
            points[Index(col, row)] = normalized;
        }

        public Vector2 GetPointByIndex(int i) => points[i];
        public void SetPointByIndex(int i, Vector2 v)
        {
            v.x = Mathf.Clamp01(v.x);
            v.y = Mathf.Clamp01(v.y);
            points[i] = v;
        }

        /// <summary>Reset to a regular Columns x Rows lattice spanning the unit square.</summary>
        public void ResetGrid()
        {
            points = new Vector2[Columns * Rows];
            for (int r = 0; r < Rows; r++)
            {
                float v = Rows > 1 ? (float)r / (Rows - 1) : 0f;
                for (int c = 0; c < Columns; c++)
                {
                    float u = Columns > 1 ? (float)c / (Columns - 1) : 0f;
                    points[Index(c, r)] = new Vector2(u, v);
                }
            }
        }

        /// <summary>Change subdivision, resampling existing control points onto the new lattice.</summary>
        public void Resize(int columns, int rows)
        {
            columns = Mathf.Max(2, columns);
            rows = Mathf.Max(2, rows);
            var resampled = new Vector2[columns * rows];
            for (int r = 0; r < rows; r++)
            {
                float v = rows > 1 ? (float)r / (rows - 1) : 0f;
                for (int c = 0; c < columns; c++)
                {
                    float u = columns > 1 ? (float)c / (columns - 1) : 0f;
                    resampled[r * columns + c] = SampleCurrent(u, v);
                }
            }
            Columns = columns;
            Rows = rows;
            points = resampled;
        }

        private Vector2 SampleCurrent(float u, float v)
        {
            if (points == null || points.Length != Columns * Rows)
            {
                return new Vector2(u, v);
            }
            float su = u * (Columns - 1);
            float sv = v * (Rows - 1);
            int c0 = Mathf.Clamp((int)Mathf.Floor(su), 0, Columns - 2);
            int r0 = Mathf.Clamp((int)Mathf.Floor(sv), 0, Rows - 2);
            float tu = su - c0, tv = sv - r0;
            Vector2 p00 = points[Index(c0, r0)];
            Vector2 p10 = points[Index(c0 + 1, r0)];
            Vector2 p01 = points[Index(c0, r0 + 1)];
            Vector2 p11 = points[Index(c0 + 1, r0 + 1)];
            return Vector2.Lerp(Vector2.Lerp(p00, p10, tu), Vector2.Lerp(p01, p11, tu), tv);
        }

        private void EnsureGrid(bool force)
        {
            if (Columns < 2) Columns = 2;
            if (Rows < 2) Rows = 2;
            if (force || points == null || points.Length != Columns * Rows)
            {
                ResetGrid();
            }
        }

        private void Update()
        {
            if (warp == null || warp.Projector == null || warp.Projector.NativeWarpNode == IntPtr.Zero)
                return;

            EnsureGrid(false);
            if (buffer == null || buffer.Length != points.Length)
                buffer = new pmsdk_vec3_t[points.Length];

            for (int i = 0; i < points.Length; i++)
                buffer[i] = new pmsdk_vec3_t { x = points[i].x, y = points[i].y, z = 0 };

            // Deformation type Grid (2).
            NativeBindings.pmsdk_warpnode_set_deformation_type(warp.Projector.NativeWarpNode, 2);
            IntPtr gridwarp = NativeBindings.pmsdk_warpnode_get_gridwarp(warp.Projector.NativeWarpNode);
            if (gridwarp != IntPtr.Zero)
            {
                NativeBindings.pmsdk_gridwarp_set_control_points(gridwarp, Columns, Rows, buffer);
            }
        }
    }
}
