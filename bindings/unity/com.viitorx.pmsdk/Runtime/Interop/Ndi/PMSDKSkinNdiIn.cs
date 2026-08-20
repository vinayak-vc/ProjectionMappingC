using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Per-skin NDI receiver (M-Sk3). Push a KlakNDI receiver's texture into a
    /// <see cref="PMSDKSkinContent"/> as its <see cref="PMSDKSkinContent.ExternalTexture"/>
    /// each frame. Sits on the same GameObject as the skin's content component.
    ///
    /// Only compiles when jp.keijiro.klak.ndi is installed (asmdef defineConstraints);
    /// NDI additionally requires the NDI runtime from ndi.video. For scene-wide (non-skin)
    /// NDI input, use <see cref="PMSDKNdiIn"/>.
    /// </summary>
    [RequireComponent(typeof(PMSDKSkinContent))]
    public class PMSDKSkinNdiIn : MonoBehaviour
    {
        [Tooltip("NDI source name to receive (empty = first available).")]
        public string SourceName = "";

        [Tooltip("If true, sets PMSDKSkinContent.Mode = ExternalTexture on enable.")]
        public bool ForceExternalTextureMode = true;

        private Klak.Ndi.NdiReceiver receiver;
        private PMSDKSkinContent content;
        private PMSDKSkinContentMode prevMode;
        private bool prevModeCaptured;

        private void OnEnable()
        {
            content = GetComponent<PMSDKSkinContent>();
            if (receiver == null) receiver = gameObject.GetComponent<Klak.Ndi.NdiReceiver>();
            if (receiver == null) receiver = gameObject.AddComponent<Klak.Ndi.NdiReceiver>();
            if (!string.IsNullOrEmpty(SourceName)) receiver.ndiName = SourceName;

            if (ForceExternalTextureMode && content != null)
            {
                if (!prevModeCaptured) { prevMode = content.Mode; prevModeCaptured = true; }
                content.Mode = PMSDKSkinContentMode.ExternalTexture;
            }
        }

        private void Update()
        {
            if (receiver == null || content == null) return;
            content.ExternalTexture = receiver.texture;
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
