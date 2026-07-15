using UnityEngine;
using UnityEditor;
using System;
using vxpmsdk;

namespace vxpmsdk.Editor
{
    public class CalibrationWindow : EditorWindow
    {
        private IntPtr nativeDecoder = IntPtr.Zero;
        private int selectedCameraIndex = 0;
        private bool isCameraOpen = false;

        [MenuItem("Tools/Projection Mapping/GrayCode Calibration")]
        public static void ShowWindow()
        {
            GetWindow<CalibrationWindow>("Calibration");
        }

        private void OnGUI()
        {
            GUILayout.Label("GrayCode Calibration & Triangulation", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            selectedCameraIndex = EditorGUILayout.IntField("Camera Index", selectedCameraIndex);

            if (!isCameraOpen)
            {
                if (GUILayout.Button("Open Camera"))
                {
                    if (nativeDecoder == IntPtr.Zero)
                    {
                        nativeDecoder = NativeBindings.pmsdk_decoder_create(1920, 1080);
                    }
                    var status = NativeBindings.pmsdk_decoder_open_camera(nativeDecoder, selectedCameraIndex);
                    if (status == pmsdk_status_t.PMSDK_SUCCESS)
                    {
                        isCameraOpen = true;
                        Debug.Log("Opened camera successfully.");
                    }
                    else
                    {
                        Debug.LogError("Failed to open camera.");
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Close Camera"))
                {
                    NativeBindings.pmsdk_decoder_close_camera(nativeDecoder);
                    isCameraOpen = false;
                    Debug.Log("Closed camera.");
                }

                if (GUILayout.Button("Capture Frame"))
                {
                    NativeBindings.pmsdk_decoder_capture_frame(nativeDecoder);
                    Debug.Log("Captured frame via C++ OpenCV backend.");
                }
            }
        }

        private void OnDestroy()
        {
            if (nativeDecoder != IntPtr.Zero)
            {
                if (isCameraOpen)
                {
                    NativeBindings.pmsdk_decoder_close_camera(nativeDecoder);
                }
                NativeBindings.pmsdk_decoder_destroy(nativeDecoder);
                nativeDecoder = IntPtr.Zero;
            }
        }
    }
}
