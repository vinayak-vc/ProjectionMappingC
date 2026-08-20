using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Per-skin Spout receiver (M-Sk3). Push a KlakSpout receiver's texture into a
    /// <see cref="PMSDKSkinContent"/> as its <see cref="PMSDKSkinContent.ExternalTexture"/>,
    /// each frame. Sits on the same GameObject as the skin's content component.
    ///
    /// Only compiles when jp.keijiro.klak.spout is installed (asmdef defineConstraints) —
    /// no hard dependency on the package. For scene-wide (non-skin) Spout input, use
    /// <see cref="PMSDKSpoutIn"/>.
    /// </summary>
    [RequireComponent(typeof(PMSDKSkinContent))]
    public class PMSDKSkinSpoutIn : MonoBehaviour
    {
        [Tooltip("Spout sender name to receive (empty = first available).")]
        public string SourceName = "";

        [Tooltip("If true, sets PMSDKSkinContent.Mode = ExternalTexture on enable. Set false " +
                 "when the content component's Mode is already ExternalTexture and you want " +
                 "the adapter to only stream frames.")]
        public bool ForceExternalTextureMode = true;

        private Klak.Spout.SpoutReceiver receiver;
        private PMSDKSkinContent content;
        private PMSDKSkinContentMode prevMode;
        private bool prevModeCaptured;

        private void OnEnable()
        {
            content = GetComponent<PMSDKSkinContent>();
            if (receiver == null) receiver = gameObject.GetComponent<Klak.Spout.SpoutReceiver>();
            if (receiver == null) receiver = gameObject.AddComponent<Klak.Spout.SpoutReceiver>();
            if (!string.IsNullOrEmpty(SourceName)) receiver.sourceName = SourceName;

            if (ForceExternalTextureMode && content != null)
            {
                if (!prevModeCaptured) { prevMode = content.Mode; prevModeCaptured = true; }
                content.Mode = PMSDKSkinContentMode.ExternalTexture;
            }
        }

        private void Update()
        {
            if (receiver == null || content == null) return;
            content.ExternalTexture = receiver.receivedTexture;
        }

        private void OnDisable()
        {
            if (content != null)
            {
                content.ExternalTexture = null;
                if (ForceExternalTextureMode && prevModeCaptured)
                {
                    content.Mode = prevMode;
                    prevModeCaptured = false;
                }
            }
        }
    }
}
