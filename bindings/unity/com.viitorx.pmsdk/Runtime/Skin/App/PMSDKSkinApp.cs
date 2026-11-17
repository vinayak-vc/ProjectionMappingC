using UnityEngine;
using UnityEngine.SceneManagement;

namespace vxpmsdk.Components
{
    public enum PMSDKSkinAppMode
    {
        Player = 0,
        Editor = 1,
    }

    /// <summary>
    /// Scene switching for the single-EXE skin app: the Player scene (play the saved skin
    /// set, swap media) and the Editor scene (author skins). Both scenes must be in the
    /// build; the build menu item adds them with Player first.
    /// </summary>
    public static class PMSDKSkinApp
    {
        public const string PlayerSceneName = "SkinPlayback";
        public const string EditorSceneName = "SkinEditor";

        public static string SceneNameFor(PMSDKSkinAppMode mode)
        {
            return mode == PMSDKSkinAppMode.Editor ? EditorSceneName : PlayerSceneName;
        }

        /// <summary>Load the scene for <paramref name="mode"/>. Returns false (and logs) when
        /// the scene is not part of the build.</summary>
        public static bool Switch(PMSDKSkinAppMode mode)
        {
            string sceneName = SceneNameFor(mode);
            if (!Application.CanStreamedLevelBeLoaded(sceneName)) {
                Debug.LogError("PMSDKSkinApp: scene '" + sceneName + "' is not in the build settings.");
                return false;
            }
            SceneManager.LoadScene(sceneName);
            return true;
        }
    }
}
