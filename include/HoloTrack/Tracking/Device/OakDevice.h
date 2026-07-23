/**
 * @file OakDevice.h
 * @brief OAK-D (Luxonis DepthAI) detection source — on-device spatial person detection.
 *
 * Concrete @ref IDetectionSource that runs a DepthAI pipeline (RGB + stereo depth + a spatial
 * MobileNet-SSD person-detection network) on a background thread and hands the most recent
 * frame's spatial detections to the tracker. This is the ONLY hardware-touching piece; it is
 * compiled only when the SDK is built with DepthAI (HOLOTRACK_WITH_DEPTHAI / the vcpkg
 * `depthai` feature, D-032). Built without it, the class still exists but @ref IsSupported is
 * false and @ref Start fails cleanly, so the DLL's ABI is stable either way.
 *
 * PImpl keeps all DepthAI types out of this header.
 */
#pragma once

#include <cstddef>
#include <memory>
#include <string>

#include "HoloTrack/Core/Export.h"
#include "HoloTrack/Tracking/Detection.h"
#include "HoloTrack/Tracking/IDetectionSource.h"

namespace holotrack {

/**
 * @brief Which on-device detector produces the head position.
 *
 * A face NN localizes the head directly (best for a forward-facing viewer); person detection is
 * robust when the viewer turns away but only estimates the head from the body box. FaceThenPerson
 * runs both and prefers the face, falling back to the person box after a run of faceless frames.
 */
enum class DetectionMode {
    Person = 0,        /**< Person spatial detection only (@ref blobPath). */
    Face = 1,          /**< Face spatial detection only (@ref faceBlobPath); head = face centre. */
    FaceThenPerson = 2 /**< Face preferred, person fallback after @ref faceFallbackFrames faceless frames. */
};

/** @brief OAK device configuration. */
struct OakOptions {
    /** @brief Active detector (default FaceThenPerson). */
    DetectionMode detectionMode{DetectionMode::FaceThenPerson};

    /**
     * @brief Path to the compiled PERSON spatial-detection blob (e.g. MobileNet-SSD, 300x300).
     *
     * Required for @ref DetectionMode::Person and @ref DetectionMode::FaceThenPerson. The SDK does
     * not bundle a model — produce one with the Luxonis model zoo / blobconverter. @ref Start fails
     * if the blob(s) required by the active mode are empty/unreadable.
     */
    std::string blobPath;

    /**
     * @brief Path to the compiled FACE spatial-detection blob (e.g. face-detection-retail-0004).
     *
     * Required for @ref DetectionMode::Face and @ref DetectionMode::FaceThenPerson. A face
     * detection is emitted with its centre as a nose keypoint so the head estimator uses it
     * directly (no body-height lift).
     */
    std::string faceBlobPath;

    /** @brief FaceThenPerson: frames with no face before falling back to the person box. */
    int faceFallbackFrames{15};

    /** @brief VOC class id treated as "person" (MobileNet-SSD: 15). */
    int personLabel{15};

    /** @brief Minimum detection confidence to keep [0,1]. */
    float confidenceThreshold{0.5f};

    /** @brief Lower/upper stereo depth clamp in millimetres (bounds spatial estimation). */
    float depthLowerThresholdMm{300.0f};
    float depthUpperThresholdMm{8000.0f};
};

/** @brief OAK-D spatial-detection source. */
class OakDevice : public IDetectionSource {
public:
    HOLOTRACK_API OakDevice();
    HOLOTRACK_API explicit OakDevice(const OakOptions& options);
    HOLOTRACK_API ~OakDevice() override;

    OakDevice(const OakDevice&) = delete;
    OakDevice& operator=(const OakDevice&) = delete;

    /** @brief True if this build includes DepthAI support (otherwise the device is inert). */
    HOLOTRACK_API static bool IsSupported();

    HOLOTRACK_API bool Start() override;
    HOLOTRACK_API void Stop() override;
    HOLOTRACK_API bool IsRunning() const override;
    HOLOTRACK_API bool Poll(const Detection** outDetections, std::size_t* outCount,
                            double* outTimestampSeconds) override;

    /** @brief Last error message from a failed @ref Start / @ref Poll (never null). */
    HOLOTRACK_API const char* LastError() const;

private:
    struct Impl;
    std::unique_ptr<Impl> impl_;
};

} // namespace holotrack
