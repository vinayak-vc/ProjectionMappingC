#include "PMSDK/C_API/CalibrationAPI.h"
#include "PMSDK/Calibration/Calibrator.h"
#include "PMSDK/Calibration/GrayCode.h"
#include "PMSDK/Calibration/GrayCodeDecoder.h"
#include <algorithm>
#include <cstring>
#include <string>

using namespace pmsdk;
using namespace pmsdk::Calibration;

// Define opaque structs mapping to our C++ classes
struct pmsdk_calibrator_t {
    Calibrator obj;
};

struct pmsdk_graycode_t {
    GrayCode obj;
};

extern "C" {

// -----------------------------------------------------------------------------
// GrayCode
// -----------------------------------------------------------------------------

PMSDK_API pmsdk_graycode_t* pmsdk_graycode_create(int width, int height) {
    try {
        if (width <= 0 || height <= 0) {
            return nullptr;
        }
        pmsdk_graycode_t* handle = new pmsdk_graycode_t{{width, height}};
        return handle;
    } catch (...) {
        return nullptr;
    }
}

PMSDK_API void pmsdk_graycode_destroy(pmsdk_graycode_t* handle) {
    if (handle) {
        delete handle;
    }
}

PMSDK_API size_t pmsdk_graycode_get_pattern_count(const pmsdk_graycode_t* handle) {
    try {
        if (!handle) {
            return 0;
        }
        return handle->obj.GetPatternCount();
    } catch (...) {
        return 0;
    }
}

PMSDK_API pmsdk_status_t pmsdk_graycode_generate_pattern(const pmsdk_graycode_t* handle, size_t index, uint8_t* outPixels) {
    try {
        if (!handle || !outPixels) {
            return PMSDK_ERROR_INVALID_ARGUMENT;
        }
        if (index >= handle->obj.GetPatternCount()) {
            return PMSDK_ERROR_INVALID_ARGUMENT;
        }

        std::vector<uint8_t> pixels = handle->obj.GeneratePattern(index);
        std::memcpy(outPixels, pixels.data(), pixels.size());
        
        return PMSDK_SUCCESS;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

PMSDK_API size_t pmsdk_graycode_get_robust_pattern_count(const pmsdk_graycode_t* handle) {
    try {
        if (!handle) {
            return 0;
        }
        return handle->obj.GetRobustPatternCount();
    } catch (...) {
        return 0;
    }
}

PMSDK_API pmsdk_status_t pmsdk_graycode_generate_robust_pattern(const pmsdk_graycode_t* handle, size_t index, uint8_t* outPixels) {
    try {
        if (!handle || !outPixels) {
            return PMSDK_ERROR_INVALID_ARGUMENT;
        }
        if (index >= handle->obj.GetRobustPatternCount()) {
            return PMSDK_ERROR_INVALID_ARGUMENT;
        }

        std::vector<uint8_t> pixels = handle->obj.GenerateRobustPattern(index);
        std::memcpy(outPixels, pixels.data(), pixels.size());

        return PMSDK_SUCCESS;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

// -----------------------------------------------------------------------------
// Calibrator
// -----------------------------------------------------------------------------

PMSDK_API pmsdk_calibrator_t* pmsdk_calibrator_create(void) {
    try {
        pmsdk_calibrator_t* handle = new pmsdk_calibrator_t();
        return handle;
    } catch (...) {
        return nullptr;
    }
}

PMSDK_API void pmsdk_calibrator_destroy(pmsdk_calibrator_t* handle) {
    if (handle) {
        delete handle;
    }
}

PMSDK_API pmsdk_status_t pmsdk_calibrator_add_observation(pmsdk_calibrator_t* handle, 
                                                          const pmsdk_vec3_t* objectPoints, 
                                                          const pmsdk_vec2_t* imagePoints, 
                                                          size_t pointCount) {
    try {
        if (!handle || !objectPoints || !imagePoints || pointCount == 0) {
            return PMSDK_ERROR_INVALID_ARGUMENT;
        }

        std::vector<Math::Vector3> objPts(pointCount);
        std::vector<Math::Vector2> imgPts(pointCount);
        for (size_t i = 0; i < pointCount; ++i) {
            objPts[i] = {objectPoints[i].x, objectPoints[i].y, objectPoints[i].z};
            imgPts[i] = {imagePoints[i].x, imagePoints[i].y};
        }

        handle->obj.AddObservation(objPts, imgPts);
        return PMSDK_SUCCESS;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

PMSDK_API pmsdk_status_t pmsdk_calibrator_calibrate(pmsdk_calibrator_t* handle, 
                                                     int imageWidth, 
                                                     int imageHeight,
                                                     float* outIntrinsics,
                                                     float* outDistortion,
                                                     double* outRmsError) {
    try {
        if (!handle || !outIntrinsics || !outDistortion || !outRmsError) {
            return PMSDK_ERROR_INVALID_ARGUMENT;
        }

        Intrinsics intr;
        std::vector<Extrinsics> extr;
        double rms = 0.0;
        
        bool success = handle->obj.Calibrate(imageWidth, imageHeight, intr, extr, rms);
        if (!success) {
            return PMSDK_ERROR_UNKNOWN;
        }

        float fx, fy, cx, cy;
        intr.GetCameraMatrix(fx, fy, cx, cy);
        outIntrinsics[0] = fx;
        outIntrinsics[1] = fy;
        outIntrinsics[2] = cx;
        outIntrinsics[3] = cy;

        auto dist = intr.GetDistortionCoefficients();
        for (size_t i = 0; i < 5; ++i) {
            if (i < dist.size()) {
                outDistortion[i] = dist[i];
            } else {
                outDistortion[i] = 0.0f;
            }
        }

        *outRmsError = rms;
        return PMSDK_SUCCESS;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

// -----------------------------------------------------------------------------
// Decoder
// -----------------------------------------------------------------------------

struct pmsdk_decoder_t {
    GrayCodeDecoder obj;
    pmsdk_decoder_t(int w, int h) : obj(w, h) {}
};

PMSDK_API pmsdk_decoder_t* pmsdk_decoder_create(int projectorWidth, int projectorHeight) {
    try {
        if (projectorWidth <= 0 || projectorHeight <= 0) return nullptr;
        return new pmsdk_decoder_t(projectorWidth, projectorHeight);
    } catch (...) {
        return nullptr;
    }
}

PMSDK_API void pmsdk_decoder_destroy(pmsdk_decoder_t* handle) {
    if (handle) {
        delete handle;
    }
}

PMSDK_API pmsdk_status_t pmsdk_decoder_open_camera(pmsdk_decoder_t* handle, int cameraIndex) {
    try {
        if (!handle) return PMSDK_ERROR_INVALID_ARGUMENT;
        if (handle->obj.OpenCamera(cameraIndex)) {
            return PMSDK_SUCCESS;
        }
        return PMSDK_ERROR_UNKNOWN;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

PMSDK_API pmsdk_status_t pmsdk_decoder_capture_frame(pmsdk_decoder_t* handle) {
    try {
        if (!handle) return PMSDK_ERROR_INVALID_ARGUMENT;
        if (handle->obj.CaptureFrame()) {
            return PMSDK_SUCCESS;
        }
        return PMSDK_ERROR_UNKNOWN;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

PMSDK_API void pmsdk_decoder_close_camera(pmsdk_decoder_t* handle) {
    if (handle) {
        handle->obj.CloseCamera();
    }
}

PMSDK_API pmsdk_status_t pmsdk_decoder_capture_frame_flushed(pmsdk_decoder_t* handle, int flushFrames) {
    try {
        if (!handle || flushFrames < 0) return PMSDK_ERROR_INVALID_ARGUMENT;
        if (handle->obj.CaptureFrameFlushed(flushFrames)) {
            return PMSDK_SUCCESS;
        }
        return PMSDK_ERROR_UNKNOWN;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

PMSDK_API pmsdk_status_t pmsdk_decoder_add_image(pmsdk_decoder_t* handle, const char* filepath) {
    try {
        if (!handle || !filepath) return PMSDK_ERROR_INVALID_ARGUMENT;
        if (handle->obj.AddImage(std::string(filepath))) {
            return PMSDK_SUCCESS;
        }
        return PMSDK_ERROR_UNKNOWN;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

PMSDK_API pmsdk_status_t pmsdk_decoder_add_image_memory(pmsdk_decoder_t* handle, const uint8_t* pixels, int width, int height) {
    try {
        if (!handle || !pixels || width <= 0 || height <= 0) return PMSDK_ERROR_INVALID_ARGUMENT;
        if (handle->obj.AddImageFromMemory(pixels, width, height)) {
            return PMSDK_SUCCESS;
        }
        return PMSDK_ERROR_UNKNOWN;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

PMSDK_API pmsdk_status_t pmsdk_decoder_get_last_frame(const pmsdk_decoder_t* handle, uint8_t* outPixels, int* inOutWidth, int* inOutHeight) {
    try {
        if (!handle || !inOutWidth || !inOutHeight) return PMSDK_ERROR_INVALID_ARGUMENT;

        std::vector<uint8_t> pixels;
        int w = 0, h = 0;
        if (!handle->obj.GetLastFrame(pixels, w, h)) {
            return PMSDK_ERROR_INVALID_ARGUMENT; // no frame available
        }

        if (!outPixels) {
            // Dimension query.
            *inOutWidth = w;
            *inOutHeight = h;
            return PMSDK_SUCCESS;
        }

        if (*inOutWidth != w || *inOutHeight != h) {
            // Caller's buffer was sized for different dimensions.
            *inOutWidth = w;
            *inOutHeight = h;
            return PMSDK_ERROR_INVALID_ARGUMENT;
        }

        std::memcpy(outPixels, pixels.data(), pixels.size());
        return PMSDK_SUCCESS;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

PMSDK_API size_t pmsdk_decoder_get_image_count(const pmsdk_decoder_t* handle) {
    try {
        if (!handle) return 0;
        return handle->obj.GetImageCount();
    } catch (...) {
        return 0;
    }
}

PMSDK_API void pmsdk_decoder_clear_images(pmsdk_decoder_t* handle) {
    if (handle) {
        handle->obj.ClearImages();
    }
}

PMSDK_API pmsdk_status_t pmsdk_decoder_decode_robust(
    const pmsdk_decoder_t* handle,
    int minContrast,
    pmsdk_vec2_t* outCameraPoints,
    pmsdk_vec2_t* outProjectorPoints,
    size_t* outCount,
    size_t maxPoints)
{
    try {
        if (!handle || !outCameraPoints || !outProjectorPoints || !outCount) {
            return PMSDK_ERROR_INVALID_ARGUMENT;
        }

        auto matches = handle->obj.DecodeRobust(minContrast);

        size_t count = std::min<size_t>(matches.size(), maxPoints);
        *outCount = matches.size(); // total found, even if truncated

        for (size_t i = 0; i < count; ++i) {
            outCameraPoints[i] = {matches[i].cameraPoint.x, matches[i].cameraPoint.y};
            outProjectorPoints[i] = {matches[i].projectorPoint.x, matches[i].projectorPoint.y};
        }

        return PMSDK_SUCCESS;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

PMSDK_API pmsdk_status_t pmsdk_decoder_decode_and_triangulate(
    const pmsdk_decoder_t* handle,
    int threshold,
    const float* camIntrinsics, const float* camExtrinsics,
    const float* projIntrinsics, const float* projExtrinsics,
    pmsdk_vec3_t* outPoints, size_t* outCount, size_t maxPoints) 
{
    try {
        if (!handle || !camIntrinsics || !camExtrinsics || !projIntrinsics || !projExtrinsics || !outPoints || !outCount) {
            return PMSDK_ERROR_INVALID_ARGUMENT;
        }

        Intrinsics cI, pI;
        cI.SetCameraMatrix(camIntrinsics[0], camIntrinsics[1], camIntrinsics[2], camIntrinsics[3]);
        pI.SetCameraMatrix(projIntrinsics[0], projIntrinsics[1], projIntrinsics[2], projIntrinsics[3]);

        Extrinsics cE, pE;
        cE.SetRotationVector({camExtrinsics[0], camExtrinsics[1], camExtrinsics[2]});
        cE.SetTranslationVector({camExtrinsics[3], camExtrinsics[4], camExtrinsics[5]});
        pE.SetRotationVector({projExtrinsics[0], projExtrinsics[1], projExtrinsics[2]});
        pE.SetTranslationVector({projExtrinsics[3], projExtrinsics[4], projExtrinsics[5]});

        auto matches = handle->obj.Decode(threshold);
        auto points3d = GrayCodeDecoder::Triangulate(matches, cI, cE, pI, pE);

        size_t count = std::min<size_t>(points3d.size(), maxPoints);
        *outCount = points3d.size(); // Tell the caller how many points there actually are

        for (size_t i = 0; i < count; ++i) {
            outPoints[i] = {points3d[i].x, points3d[i].y, points3d[i].z};
        }

        return PMSDK_SUCCESS;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

} // extern "C"
