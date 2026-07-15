using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Activates all connected physical displays at startup so projector cameras
    /// (Camera.targetDisplay 1, 2, ...) actually output through the GPU ports.
    ///
    /// Unity standalone builds only render to the primary display unless each
    /// additional display is explicitly activated — without this component the
    /// projectors receive no signal and stay black in builds.
    ///
    /// Note: has no effect inside the Editor (Display.displays always reports a
    /// single display there); use the Game view's "Display 1/2/3" dropdown to
    /// preview projector outputs in the Editor instead.
    /// </summary>
    public class PMSDKDisplayActivator : MonoBehaviour
    {
        private void Start()
        {
            // Display 0 (primary) is always active; activate the rest.
            for (int i = 1; i < Display.displays.Length; i++)
            {
                Display.displays[i].Activate();
                Debug.Log($"[PMSDK] Activated display {i + 1} ({Display.displays[i].systemWidth}x{Display.displays[i].systemHeight}).");
            }

            if (Display.displays.Length == 1 && !Application.isEditor)
            {
                Debug.LogWarning("[PMSDK] Only one display detected — projector cameras targeting Display 2+ will not be visible.");
            }
        }
    }
}
