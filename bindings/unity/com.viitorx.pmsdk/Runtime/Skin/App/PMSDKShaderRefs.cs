using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Holds serialized references to the shaders the skin system looks up by name at
    /// runtime (<c>Shader.Find</c>), so a standalone build does not strip them. Put one on
    /// any object in every scene that is built into the skin app.
    /// </summary>
    [DisallowMultipleComponent]
    public class PMSDKShaderRefs : MonoBehaviour
    {
        [SerializeField] private Shader unlitWarp;
        [SerializeField] private Shader skinEditorOverlay;
        [SerializeField] private Shader internalColored;

        public Shader UnlitWarp {
            get { return unlitWarp; }
            set { unlitWarp = value; }
        }

        public Shader SkinEditorOverlay {
            get { return skinEditorOverlay; }
            set { skinEditorOverlay = value; }
        }

        public Shader InternalColored {
            get { return internalColored; }
            set { internalColored = value; }
        }
    }
}
