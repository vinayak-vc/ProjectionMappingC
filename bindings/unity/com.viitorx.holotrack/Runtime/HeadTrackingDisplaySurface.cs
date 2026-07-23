using UnityEngine;

namespace vxholotrack
{
    /// <summary>
    /// Defines the physical display surface (the fixed "window") for off-axis rendering. The quad
    /// is centred on this transform, spanning local X by <see cref="width"/> and local Y by
    /// <see cref="height"/>, facing local +Z. Place and size it to match the real projection
    /// surface in world units.
    /// </summary>
    [AddComponentMenu("ViitorX/HoloTrack/Display Surface")]
    public sealed class HeadTrackingDisplaySurface : MonoBehaviour
    {
        [Tooltip("Surface width in world units (local X).")]
        [SerializeField] private float width = 2.0f;

        [Tooltip("Surface height in world units (local Y).")]
        [SerializeField] private float height = 1.2f;

        /// <summary>Surface width in world units.</summary>
        public float Width => width;

        /// <summary>Surface height in world units.</summary>
        public float Height => height;

        /// <summary>World-space corners used by the off-axis solve.</summary>
        public void GetCorners(out Vector3 bottomLeft, out Vector3 bottomRight, out Vector3 topLeft)
        {
            Vector3 r = transform.right * (width * 0.5f);
            Vector3 u = transform.up * (height * 0.5f);
            Vector3 c = transform.position;
            bottomLeft = c - r - u;
            bottomRight = c + r - u;
            topLeft = c - r + u;
        }

        private void OnDrawGizmos()
        {
            GetCorners(out Vector3 bl, out Vector3 br, out Vector3 tl);
            Vector3 tr = br + (tl - bl);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }
    }
}
