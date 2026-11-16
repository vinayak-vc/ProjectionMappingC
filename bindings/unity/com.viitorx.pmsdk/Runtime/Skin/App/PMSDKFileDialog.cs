using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>Media categories offered by <see cref="PMSDKFileDialog"/>.</summary>
    public enum PMSDKFileDialogKind
    {
        Video = 0,
        Image = 1,
        Any = 2,
        SkinSet = 3,
    }

    /// <summary>
    /// Native Windows "Open File" dialog (comdlg32) so artists can pick media that lives
    /// outside the Unity project from a standalone build. Windows only; on other
    /// platforms <see cref="TryPickFile"/> returns false and the UI falls back to the
    /// editable path field. Blocks the main thread while the dialog is open.
    /// </summary>
    public static class PMSDKFileDialog
    {
        public static bool IsSupported {
            get {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Show the dialog. Returns true and sets <paramref name="path"/> when the
        /// user picked a file; false on cancel, error or unsupported platform.</summary>
        public static bool TryPickFile(PMSDKFileDialogKind kind, string initialDirectory, out string path)
        {
            path = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            const int BufferChars = 4096;
            IntPtr buffer = Marshal.AllocHGlobal(BufferChars * 2);
            try {
                for (int i = 0; i < BufferChars; i++) {
                    Marshal.WriteInt16(buffer, i * 2, 0);
                }

                OpenFileName ofn = new OpenFileName();
                ofn.structSize = Marshal.SizeOf(typeof(OpenFileName));
                ofn.owner = GetActiveWindow();
                ofn.filter = BuildFilter(kind);
                ofn.filterIndex = 1;
                ofn.file = buffer;
                ofn.maxFile = BufferChars;
                ofn.initialDir = string.IsNullOrEmpty(initialDirectory) ? null : initialDirectory;
                ofn.title = BuildTitle(kind);
                ofn.flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnHideReadOnly;

                if (!GetOpenFileNameW(ref ofn)) {
                    return false;
                }
                string picked = Marshal.PtrToStringUni(buffer);
                if (string.IsNullOrEmpty(picked)) {
                    return false;
                }
                path = picked;
                return true;
            } catch (Exception ex) {
                Debug.LogWarning("PMSDKFileDialog: dialog failed - " + ex.Message);
                return false;
            } finally {
                Marshal.FreeHGlobal(buffer);
            }
#else
            return false;
#endif
        }

        private static string BuildTitle(PMSDKFileDialogKind kind)
        {
            switch (kind) {
                case PMSDKFileDialogKind.Video: return "Choose a video";
                case PMSDKFileDialogKind.Image: return "Choose an image";
                case PMSDKFileDialogKind.SkinSet: return "Choose a skin set";
                default: return "Choose a media file";
            }
        }

        private static string BuildFilter(PMSDKFileDialogKind kind)
        {
            switch (kind) {
                case PMSDKFileDialogKind.Video:
                    return "Video files\0*.mp4;*.mov;*.m4v;*.avi;*.wmv;*.webm;*.mkv;*.flv;*.mpg;*.mpeg;*.ts;*.mts;*.m2ts;*.3gp;*.ogv\0All files\0*.*\0\0";
                case PMSDKFileDialogKind.Image:
                    return "Image files\0*.png;*.jpg;*.jpeg;*.bmp;*.tga\0All files\0*.*\0\0";
                case PMSDKFileDialogKind.SkinSet:
                    return "Skin set (*.json)\0*.json\0All files\0*.*\0\0";
                default:
                    return "Media files\0*.mp4;*.mov;*.m4v;*.avi;*.wmv;*.webm;*.mkv;*.flv;*.mpg;*.mpeg;*.ts;*.mts;*.m2ts;*.3gp;*.ogv;*.png;*.jpg;*.jpeg;*.bmp;*.tga\0All files\0*.*\0\0";
            }
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const int OfnHideReadOnly = 0x00000004;
        private const int OfnNoChangeDir = 0x00000008;
        private const int OfnPathMustExist = 0x00000800;
        private const int OfnFileMustExist = 0x00001000;
        private const int OfnExplorer = 0x00080000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int structSize;
            public IntPtr owner;
            public IntPtr instance;
            public string filter;
            public string customFilter;
            public int maxCustFilter;
            public int filterIndex;
            public IntPtr file;
            public int maxFile;
            public string fileTitle;
            public int maxFileTitle;
            public string initialDir;
            public string title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public string defExt;
            public IntPtr custData;
            public IntPtr hook;
            public string templateName;
            public IntPtr reserved;
            public int reserved2;
            public int flagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileNameW(ref OpenFileName ofn);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();
#endif
    }
}
