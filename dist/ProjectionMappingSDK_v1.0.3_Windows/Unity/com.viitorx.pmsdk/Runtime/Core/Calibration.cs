using System;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace vxpmsdk
{
    public class Intrinsics
    {
        public float fx, fy, cx, cy;
        public float k1, k2, p1, p2, k3;
    }

    public class GrayCode : IDisposable
    {
        private IntPtr handle;
        private int width;
        private int height;

        public GrayCode(int width, int height)
        {
            this.width = width;
            this.height = height;
            handle = NativeBindings.pmsdk_graycode_create(width, height);
            if (handle == IntPtr.Zero)
            {
                throw new Exception("Failed to create GrayCode generator");
            }
        }

        public int GetPatternCount()
        {
            return (int)NativeBindings.pmsdk_graycode_get_pattern_count(handle);
        }

        public void GeneratePattern(int index, Texture2D outTexture)
        {
            if (outTexture.width != width || outTexture.height != height || outTexture.format != TextureFormat.R8)
            {
                throw new ArgumentException($"Texture must be R8 format and match resolution {width}x{height}");
            }

            byte[] pixels = new byte[width * height];
            var status = NativeBindings.pmsdk_graycode_generate_pattern(handle, (UIntPtr)index, pixels);
            if (status != pmsdk_status_t.PMSDK_SUCCESS)
            {
                throw new Exception($"Failed to generate pattern: {status}");
            }

            outTexture.SetPixelData(pixels, 0);
            outTexture.Apply();
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                NativeBindings.pmsdk_graycode_destroy(handle);
                handle = IntPtr.Zero;
            }
        }
    }

    public class Calibrator : IDisposable
    {
        private IntPtr handle;

        public Calibrator()
        {
            handle = NativeBindings.pmsdk_calibrator_create();
            if (handle == IntPtr.Zero)
            {
                throw new Exception("Failed to create Calibrator");
            }
        }

        public void AddObservation(Vector3[] objectPoints, Vector2[] imagePoints)
        {
            if (objectPoints.Length != imagePoints.Length || objectPoints.Length == 0)
                throw new ArgumentException("Points arrays must be equal in length and non-empty");

            var nativeObj = new pmsdk_vec3_t[objectPoints.Length];
            var nativeImg = new pmsdk_vec2_t[imagePoints.Length];

            for (int i = 0; i < objectPoints.Length; i++)
            {
                nativeObj[i] = new pmsdk_vec3_t { x = objectPoints[i].x, y = objectPoints[i].y, z = objectPoints[i].z };
                nativeImg[i] = new pmsdk_vec2_t { x = imagePoints[i].x, y = imagePoints[i].y };
            }

            var status = NativeBindings.pmsdk_calibrator_add_observation(handle, nativeObj, nativeImg, (UIntPtr)objectPoints.Length);
            if (status != pmsdk_status_t.PMSDK_SUCCESS)
            {
                throw new Exception($"Failed to add observation: {status}");
            }
        }

        public double Calibrate(int imageWidth, int imageHeight, out Intrinsics intrinsics)
        {
            float[] outIntrinsics = new float[4];
            float[] outDistortion = new float[5];
            double rmsError = 0;

            var status = NativeBindings.pmsdk_calibrator_calibrate(handle, imageWidth, imageHeight, outIntrinsics, outDistortion, out rmsError);
            if (status != pmsdk_status_t.PMSDK_SUCCESS)
            {
                throw new Exception($"Failed to calibrate: {status}");
            }

            intrinsics = new Intrinsics
            {
                fx = outIntrinsics[0],
                fy = outIntrinsics[1],
                cx = outIntrinsics[2],
                cy = outIntrinsics[3],
                k1 = outDistortion[0],
                k2 = outDistortion[1],
                p1 = outDistortion[2],
                p2 = outDistortion[3],
                k3 = outDistortion[4]
            };

            return rmsError;
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                NativeBindings.pmsdk_calibrator_destroy(handle);
                handle = IntPtr.Zero;
            }
        }
    }
    public class Extrinsics
    {
        public float rx, ry, rz; // rotation vector
        public float tx, ty, tz; // translation vector
    }

    public class Decoder : IDisposable
    {
        private IntPtr handle;

        public Decoder(int projectorWidth, int projectorHeight)
        {
            handle = NativeBindings.pmsdk_decoder_create(projectorWidth, projectorHeight);
            if (handle == IntPtr.Zero)
            {
                throw new Exception("Failed to create Decoder");
            }
        }

        public void OpenCamera(int cameraIndex = 0)
        {
            var status = NativeBindings.pmsdk_decoder_open_camera(handle, cameraIndex);
            if (status != pmsdk_status_t.PMSDK_SUCCESS)
            {
                throw new Exception($"Failed to open camera index {cameraIndex}: {status}");
            }
        }

        public void CaptureFrame()
        {
            var status = NativeBindings.pmsdk_decoder_capture_frame(handle);
            if (status != pmsdk_status_t.PMSDK_SUCCESS)
            {
                throw new Exception($"Failed to capture frame: {status}");
            }
        }

        public void CloseCamera()
        {
            NativeBindings.pmsdk_decoder_close_camera(handle);
        }

        public void AddImage(string filepath)
        {
            var status = NativeBindings.pmsdk_decoder_add_image(handle, filepath);
            if (status != pmsdk_status_t.PMSDK_SUCCESS)
            {
                throw new Exception($"Failed to add image: {status}");
            }
        }

        public Vector3[] DecodeAndTriangulate(int threshold, Intrinsics camIntrinsics, Extrinsics camExtrinsics, Intrinsics projIntrinsics, Extrinsics projExtrinsics)
        {
            float[] cIntr = new float[] { camIntrinsics.fx, camIntrinsics.fy, camIntrinsics.cx, camIntrinsics.cy };
            float[] pIntr = new float[] { projIntrinsics.fx, projIntrinsics.fy, projIntrinsics.cx, projIntrinsics.cy };

            float[] cExtr = new float[] { camExtrinsics.rx, camExtrinsics.ry, camExtrinsics.rz, camExtrinsics.tx, camExtrinsics.ty, camExtrinsics.tz };
            float[] pExtr = new float[] { projExtrinsics.rx, projExtrinsics.ry, projExtrinsics.rz, projExtrinsics.tx, projExtrinsics.ty, projExtrinsics.tz };

            // Allocate buffer for out points (assuming max 1920x1080)
            int maxPoints = 2000000;
            pmsdk_vec3_t[] outPoints = new pmsdk_vec3_t[maxPoints];

            var status = NativeBindings.pmsdk_decoder_decode_and_triangulate(
                handle, threshold,
                cIntr, cExtr, pIntr, pExtr,
                outPoints, out UIntPtr actualCount, (UIntPtr)maxPoints);

            if (status != pmsdk_status_t.PMSDK_SUCCESS)
            {
                throw new Exception($"Failed to decode and triangulate: {status}");
            }

            int count = (int)actualCount;
            Vector3[] result = new Vector3[count];
            for (int i = 0; i < count; ++i)
            {
                result[i] = new Vector3(outPoints[i].x, outPoints[i].y, outPoints[i].z);
            }

            return result;
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                NativeBindings.pmsdk_decoder_destroy(handle);
                handle = IntPtr.Zero;
            }
        }
    }
}
