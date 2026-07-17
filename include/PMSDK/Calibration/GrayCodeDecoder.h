#pragma once
#include "PMSDK/Core/Export.h"
#include "PMSDK/Math/Vector2.h"
#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Calibration/Intrinsics.h"
#include "PMSDK/Calibration/Extrinsics.h"
#include <string>
#include <vector>
#include <memory>

namespace pmsdk::Calibration {

/**
 * @brief Represents a single match between a camera pixel and a projector pixel.
 */
struct PointMatch {
    Math::Vector2 cameraPoint;
    Math::Vector2 projectorPoint;
};

class GrayCodeDecoder {
public:
    /**
     * @brief Creates a decoder for the specified projector resolution.
     */
    PMSDK_API GrayCodeDecoder(int projectorWidth, int projectorHeight);
    PMSDK_API ~GrayCodeDecoder();

    /**
     * @brief Opens a physical camera for direct SDK capture (Option B).
     * @param cameraIndex The OS index of the camera (e.g., 0 for default webcam).
     * @return true if the camera was successfully opened.
     */
    PMSDK_API bool OpenCamera(int cameraIndex);

    /**
     * @brief Captures a frame from the opened camera and adds it to the decoder sequence.
     * @return true if successfully captured and added.
     */
    PMSDK_API bool CaptureFrame();

    /**
     * @brief Captures a frame after discarding a number of buffered frames.
     *
     * cv::VideoCapture buffers frames internally: after the projected pattern
     * changes, a plain read() often returns a frame showing the PREVIOUS
     * pattern, corrupting the decode. Flushing a few frames first guarantees
     * the capture reflects what is currently on the wall.
     *
     * @param flushFrames Frames to grab and discard before the real capture (2-3 typical).
     * @return true if successfully captured and added.
     */
    PMSDK_API bool CaptureFrameFlushed(int flushFrames);

    /**
     * @brief Closes the camera if it is open.
     */
    PMSDK_API void CloseCamera();

    /**
     * @brief Adds an image by its file path.
     * @param filepath Absolute or relative path to the image file.
     * @return true if the image was successfully loaded.
     */
    PMSDK_API bool AddImage(const std::string& filepath);

    /**
     * @brief Adds an 8-bit grayscale image supplied by the host (row-major, top-left origin).
     * @return true if the image was accepted.
     */
    PMSDK_API bool AddImageFromMemory(const uint8_t* pixels, int width, int height);

    /**
     * @brief Copies the most recently added image (grayscale) to the caller.
     * @param outPixels Receives width*height bytes.
     * @param outWidth Receives the image width.
     * @param outHeight Receives the image height.
     * @return true if an image was available.
     */
    PMSDK_API bool GetLastFrame(std::vector<uint8_t>& outPixels, int& outWidth, int& outHeight) const;

    /** @brief Number of images currently accumulated. */
    PMSDK_API size_t GetImageCount() const;

    /** @brief Discards all accumulated images (camera stays open). */
    PMSDK_API void ClearImages();

    /**
     * @brief Decodes the accumulated images into a list of pixel matches.
     * @param threshold Grayscale threshold to distinguish white from black (0-255).
     * @return A list of valid pixel matches between the camera and projector.
     */
    PMSDK_API std::vector<PointMatch> Decode(int threshold = 127) const;

    /**
     * @brief Robust decode over a robust capture sequence (see GrayCode::GenerateRobustPattern):
     * images must be [white, black, p0, p0inv, p1, p1inv, ...].
     *
     * Each bit is decided by comparing the pattern capture against its inverse
     * capture per pixel (no global threshold), and pixels whose white-black
     * contrast is below minContrast are rejected as unlit/shadowed — this is
     * what keeps ambient light, surface albedo and other projectors from
     * producing garbage matches.
     *
     * @param minContrast Minimum white-black difference (0-255) for a pixel to count as lit.
     * @return Valid pixel matches between the camera and projector.
     */
    PMSDK_API std::vector<PointMatch> DecodeRobust(int minContrast = 30) const;

    /**
     * @brief Utility method to triangulate the decoded 2D matches into 3D points.
     * @param matches The matches obtained from Decode().
     * @param camIntrinsics The physical camera's intrinsics.
     * @param camExtrinsics The physical camera's extrinsics (relative to some world origin).
     * @param projIntrinsics The projector's intrinsics.
     * @param projExtrinsics The projector's extrinsics.
     * @return A list of 3D points in the world coordinate system.
     */
    PMSDK_API static std::vector<Math::Vector3> Triangulate(
        const std::vector<PointMatch>& matches,
        const Intrinsics& camIntrinsics, const Extrinsics& camExtrinsics,
        const Intrinsics& projIntrinsics, const Extrinsics& projExtrinsics);

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Calibration
