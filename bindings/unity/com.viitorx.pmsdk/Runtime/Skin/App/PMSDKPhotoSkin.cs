using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>One outline of a photo part, in y-up normalised photo coordinates.</summary>
    public sealed class PMSDKPhotoContour
    {
        public List<Vector2> Points = new List<Vector2>();
        public bool IsHole;
    }

    /// <summary>One detected part of the photographed object (managed copy of the native result).</summary>
    public sealed class PMSDKPhotoPart
    {
        public int Id;
        public float AreaFraction;
        public Vector2 BoundsMin;
        public Vector2 BoundsMax;
        /// <summary>Bounding quad in skin vertex order: bottom-left, bottom-right, top-right, top-left.</summary>
        public Vector2[] Quad = new Vector2[4];
        /// <summary>Largest outer outline, simplified; positive (counter-clockwise) area, y up.</summary>
        public List<Vector2> Polygon = new List<Vector2>();
        public List<PMSDKPhotoContour> Contours = new List<PMSDKPhotoContour>();
        public int MaskX;
        public int MaskY;
        public int MaskWidth;
        public int MaskHeight;
        /// <summary>MaskWidth * MaskHeight bytes, rows TOP-to-bottom (image order), 255 = part.</summary>
        public byte[] Mask = new byte[0];
    }

    public sealed class PMSDKPhotoExtraction
    {
        public int ImageWidth;
        public int ImageHeight;
        public string UsedMode = "";
        public List<PMSDKPhotoPart> Parts = new List<PMSDKPhotoPart>();
    }

    /// <summary>
    /// Managed wrapper over the native photo -> skin extraction (C++ SkinGen module). All the
    /// image processing happens in the SDK DLL; this only copies the results into plain C# data.
    /// </summary>
    public static class PMSDKPhotoSkin
    {
        private static readonly string[] ModeNames = { "Auto", "Alpha", "ColorKey", "GrabCut", "ProvidedMask" };

        /// <summary>True when the loaded ProjectionMappingSDK.dll contains the SkinGen exports.</summary>
        public static bool IsAvailable()
        {
            try {
                pmsdk_skingen_options_t options = new pmsdk_skingen_options_t();
                NativeSkinGen.pmsdk_skingen_options_init(ref options);
                return options.max_polygon_points > 0;
            } catch (DllNotFoundException) {
                return false;
            } catch (EntryPointNotFoundException) {
                return false;
            }
        }

        public static pmsdk_skingen_options_t DefaultOptions(int maxPolygonPoints)
        {
            pmsdk_skingen_options_t options = new pmsdk_skingen_options_t();
            NativeSkinGen.pmsdk_skingen_options_init(ref options);
            options.y_up = 1;
            options.max_polygon_points = Mathf.Clamp(maxPolygonPoints, 3, 4096);
            return options;
        }

        /// <summary>Extract the parts of the object in an image file. On failure returns false and a message.</summary>
        public static bool TryExtract(string path, int maxPolygonPoints, out PMSDKPhotoExtraction extraction, out string error)
        {
            extraction = null;
            error = "";
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) {
                error = "The image file was not found.";
                return false;
            }
            IntPtr handle = IntPtr.Zero;
            try {
                pmsdk_skingen_options_t options = DefaultOptions(maxPolygonPoints);
                handle = NativeSkinGen.pmsdk_skingen_extract_from_file(path, ref options);
                if (handle == IntPtr.Zero) {
                    IntPtr message = NativeSkinGen.pmsdk_skingen_get_last_error();
                    error = message == IntPtr.Zero ? "The image could not be processed." : Marshal.PtrToStringUTF8(message);
                    return false;
                }
                extraction = ReadResult(handle);
                return true;
            } catch (DllNotFoundException) {
                error = "ProjectionMappingSDK.dll was not found.";
                return false;
            } catch (EntryPointNotFoundException) {
                error = "This ProjectionMappingSDK.dll is too old for photo skins. Deploy the updated DLL.";
                return false;
            } finally {
                if (handle != IntPtr.Zero) {
                    NativeSkinGen.pmsdk_skingen_result_destroy(handle);
                }
            }
        }

        private static PMSDKPhotoExtraction ReadResult(IntPtr handle)
        {
            PMSDKPhotoExtraction result = new PMSDKPhotoExtraction();
            int w;
            int h;
            NativeSkinGen.pmsdk_skingen_get_image_size(handle, out w, out h);
            result.ImageWidth = w;
            result.ImageHeight = h;
            int mode = NativeSkinGen.pmsdk_skingen_get_used_mode(handle);
            result.UsedMode = mode >= 0 && mode < ModeNames.Length ? ModeNames[mode] : "Unknown";

            int count = (int)NativeSkinGen.pmsdk_skingen_get_part_count(handle);
            for (int i = 0; i < count; i++) {
                UIntPtr part = new UIntPtr((uint)i);
                PMSDKPhotoPart p = new PMSDKPhotoPart();
                p.Id = i;
                p.AreaFraction = NativeSkinGen.pmsdk_skingen_get_part_area_fraction(handle, part);

                float[] bounds = new float[4];
                NativeSkinGen.pmsdk_skingen_get_part_bounds(handle, part, bounds);
                p.BoundsMin = new Vector2(bounds[0], bounds[1]);
                p.BoundsMax = new Vector2(bounds[2], bounds[3]);

                pmsdk_vec2_t[] quad = new pmsdk_vec2_t[4];
                NativeSkinGen.pmsdk_skingen_get_part_quad(handle, part, quad);
                for (int q = 0; q < 4; q++) {
                    p.Quad[q] = new Vector2(quad[q].x, quad[q].y);
                }

                p.Polygon = ReadPoints(delegate (pmsdk_vec2_t[] buffer, UIntPtr capacity) {
                    return NativeSkinGen.pmsdk_skingen_copy_polygon(handle, part, buffer, capacity);
                });

                int contourCount = (int)NativeSkinGen.pmsdk_skingen_get_contour_count(handle, part);
                for (int c = 0; c < contourCount; c++) {
                    UIntPtr contour = new UIntPtr((uint)c);
                    PMSDKPhotoContour pc = new PMSDKPhotoContour();
                    pc.IsHole = NativeSkinGen.pmsdk_skingen_get_contour_is_hole(handle, part, contour) == 1;
                    pc.Points = ReadPoints(delegate (pmsdk_vec2_t[] buffer, UIntPtr capacity) {
                        return NativeSkinGen.pmsdk_skingen_copy_contour(handle, part, contour, buffer, capacity);
                    });
                    p.Contours.Add(pc);
                }

                int[] rect = new int[4];
                NativeSkinGen.pmsdk_skingen_get_mask_rect(handle, part, rect);
                p.MaskX = rect[0];
                p.MaskY = rect[1];
                p.MaskWidth = rect[2];
                p.MaskHeight = rect[3];
                int bytes = (int)NativeSkinGen.pmsdk_skingen_copy_mask(handle, part, null, UIntPtr.Zero);
                p.Mask = new byte[bytes];
                if (bytes > 0) {
                    NativeSkinGen.pmsdk_skingen_copy_mask(handle, part, p.Mask, new UIntPtr((uint)bytes));
                }
                result.Parts.Add(p);
            }
            return result;
        }

        private delegate UIntPtr CopyPointsCall(pmsdk_vec2_t[] buffer, UIntPtr capacity);

        private static List<Vector2> ReadPoints(CopyPointsCall call)
        {
            int count = (int)call(null, UIntPtr.Zero);
            List<Vector2> points = new List<Vector2>(count);
            if (count <= 0) {
                return points;
            }
            pmsdk_vec2_t[] buffer = new pmsdk_vec2_t[count];
            call(buffer, new UIntPtr((uint)count));
            for (int i = 0; i < count; i++) {
                points.Add(new Vector2(buffer[i].x, buffer[i].y));
            }
            return points;
        }
    }
}
