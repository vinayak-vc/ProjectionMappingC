using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Bridges a KlakNDI receiver into the projection rig: the received texture
    /// becomes the projected content on every warp surface (via PMSDKExternalContent),
    /// so any NDI source on the network can feed this rig.
    ///
    /// This assembly only compiles when jp.keijiro.klak.ndi is installed (asmdef
    /// defineConstraints) — no hard dependency; NDI additionally requires the NDI
    /// runtime from ndi.video. For OUTPUT, add a Klak NdiSender to the projector
    /// camera directly; no bridge needed.
    /// </summary>
    [RequireComponent(typeof(PMSDKExternalContent))]
    public class PMSDKNdiIn : MonoBehaviour
    {
        [Tooltip("NDI source name to receive (empty = first available).")]
        public string SourceName = "";

        private Klak.Ndi.NdiReceiver receiver;
        private PMSDKExternalContent content;

        private void OnEnable()
        {
            content = GetComponent<PMSDKExternalContent>();
            receiver = gameObject.GetComponent<Klak.Ndi.NdiReceiver>();
            if (receiver == null) receiver = gameObject.AddComponent<Klak.Ndi.NdiReceiver>();
            if (!string.IsNullOrEmpty(SourceName)) receiver.ndiName = SourceName;
        }

        private void Update()
        {
            if (receiver != null && content != null)
            {
                content.Source = receiver.texture;
            }
        }

        private void OnDisable()
        {
            if (content != null) content.Source = null;
        }
    }
}
