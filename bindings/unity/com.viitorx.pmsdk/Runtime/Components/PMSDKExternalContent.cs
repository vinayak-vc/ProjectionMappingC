using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Feeds an EXTERNAL texture (NDI/Spout receiver, video player, render texture
    /// from another system) into the projection rig in place of the internal content
    /// RenderTexture. Each warp surface keeps its own material (so per-projector
    /// slicing/tiling, blend, color still apply) — only the sampled texture changes.
    ///
    /// Assign <see cref="Source"/> directly, or let an adapter (PMSDKSpoutIO /
    /// PMSDKNdiIO, present when the matching Klak package is installed) drive it.
    /// Disable to restore the original content texture on every surface.
    /// </summary>
    [ExecuteAlways]
    public class PMSDKExternalContent : MonoBehaviour
    {
        [Tooltip("External texture to project. When null, surfaces keep their original content.")]
        public Texture Source;

        private readonly Dictionary<Material, Texture> originals = new Dictionary<Material, Texture>();
        private Texture applied;

        private void OnDisable()
        {
            RestoreAll();
        }

        private void Update()
        {
            if (Source == null)
            {
                if (applied != null) RestoreAll();
                return;
            }
            if (!ReferenceEquals(applied, Source) || originals.Count == 0)
            {
                Apply();
            }
        }

        private void Apply()
        {
            foreach (var warp in FindObjectsByType<PMSDKMeshWarp>(FindObjectsSortMode.None))
            {
                var mr = warp.GetComponent<MeshRenderer>();
                var mat = mr != null ? mr.sharedMaterial : null;
                if (mat == null) continue;
                if (!originals.ContainsKey(mat))
                {
                    originals[mat] = mat.mainTexture; // remember the internal content RT
                }
                mat.mainTexture = Source;
            }
            applied = Source;
        }

        private void RestoreAll()
        {
            foreach (var kv in originals)
            {
                if (kv.Key != null) kv.Key.mainTexture = kv.Value;
            }
            originals.Clear();
            applied = null;
        }
    }
}
