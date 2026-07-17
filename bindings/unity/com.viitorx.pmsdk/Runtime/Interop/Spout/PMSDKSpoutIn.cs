using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Bridges a KlakSpout receiver into the projection rig: the received texture
    /// becomes the projected content on every warp surface (via PMSDKExternalContent),
    /// so Resolume/TouchDesigner/madMapper output can feed this rig over Spout.
    ///
    /// This assembly only compiles when jp.keijiro.klak.spout is installed
    /// (asmdef defineConstraints) — no hard dependency. For OUTPUT (sending a
    /// projector's view to other apps), add a Klak SpoutSender to the projector
    /// camera directly; no bridge needed.
    /// </summary>
    [RequireComponent(typeof(PMSDKExternalContent))]
    public class PMSDKSpoutIn : MonoBehaviour
    {
        [Tooltip("Spout sender name to receive (empty = first available).")]
        public string SourceName = "";

        private Klak.Spout.SpoutReceiver receiver;
        private PMSDKExternalContent content;

        private void OnEnable()
        {
            content = GetComponent<PMSDKExternalContent>();
            receiver = gameObject.GetComponent<Klak.Spout.SpoutReceiver>();
            if (receiver == null) receiver = gameObject.AddComponent<Klak.Spout.SpoutReceiver>();
            if (!string.IsNullOrEmpty(SourceName)) receiver.sourceName = SourceName;
        }

        private void Update()
        {
            if (receiver != null && content != null)
            {
                content.Source = receiver.receivedTexture;
            }
        }

        private void OnDisable()
        {
            if (content != null) content.Source = null;
        }
    }
}
