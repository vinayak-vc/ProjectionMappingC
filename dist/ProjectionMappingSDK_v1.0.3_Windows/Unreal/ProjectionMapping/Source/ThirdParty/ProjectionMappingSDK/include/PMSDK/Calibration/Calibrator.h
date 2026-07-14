#pragma once
#include "PMSDK/Core/Export.h"
#include "PMSDK/Math/Vector2.h"
#include "PMSDK/Math/Vector3.h"
#include "PMSDK/Calibration/Intrinsics.h"
#include "PMSDK/Calibration/Extrinsics.h"
#include <vector>
#include <memory>

namespace pmsdk::Calibration {

class Calibrator {
public:
    PMSDK_API Calibrator();
    PMSDK_API ~Calibrator();

    // Adds an observation from a single view/pattern
    PMSDK_API void AddObservation(const std::vector<Math::Vector3>& objectPoints,
                                  const std::vector<Math::Vector2>& imagePoints);

    // Runs cv::calibrateCamera internally
    // Returns true if successful, RMS reprojection error is written to outRmsError.
    PMSDK_API bool Calibrate(int imageWidth, int imageHeight,
                             Intrinsics& outIntrinsics,
                             std::vector<Extrinsics>& outExtrinsics,
                             double& outRmsError);

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Calibration
