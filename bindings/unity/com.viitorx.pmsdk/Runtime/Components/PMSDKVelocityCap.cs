using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Caps a Rigidbody's linear velocity at runtime.
    ///
    /// Rigidbody.maxLinearVelocity is a runtime-only PhysX property — it is NOT
    /// serialized with the scene, so setting it in the Editor silently resets to
    /// the PhysX default (1e16) on play. Demo scenes use this to keep a
    /// restitution-1 bouncing body stable: PhysX restitution overshoot otherwise
    /// adds a little energy on every impact and the bounce grows without bound.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PMSDKVelocityCap : MonoBehaviour
    {
        [Tooltip("Maximum linear speed (m/s). For a bounce from height h: v = sqrt(2*g*h).")]
        public float MaxLinearVelocity = 8.9f;

        private void Awake()
        {
            GetComponent<Rigidbody>().maxLinearVelocity = MaxLinearVelocity;
        }
    }
}
