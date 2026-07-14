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
     * @brief Decodes the accumulated images into a list of pixel matches.
     * @param threshold Grayscale threshold to distinguish white from black (0-255).
     * @return A list of valid pixel matches between the camera and projector.
     */
    PMSDK_API std::vector<PointMatch> Decode(int threshold = 127) const;

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
