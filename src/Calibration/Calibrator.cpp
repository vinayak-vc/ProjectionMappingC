#include "PMSDK/Calibration/Calibrator.h"

// Conditionally include OpenCV. If it's not found (e.g., vcpkg failed), provide a stub.
#if __has_include(<opencv2/calib3d.hpp>)
#define PMSDK_HAS_OPENCV 1
#include <opencv2/core.hpp>
#include <opencv2/calib3d.hpp>
#else
#define PMSDK_HAS_OPENCV 0
#endif

namespace pmsdk::Calibration {

struct Calibrator::Impl {
    std::vector<std::vector<Math::Vector3>> objectPointsList;
    std::vector<std::vector<Math::Vector2>> imagePointsList;
};

Calibrator::Calibrator() : m_impl(std::make_unique<Impl>()) {}
Calibrator::~Calibrator() = default;

void Calibrator::AddObservation(const std::vector<Math::Vector3>& objectPoints,
                                const std::vector<Math::Vector2>& imagePoints) {
    if (objectPoints.size() != imagePoints.size() || objectPoints.empty()) {
        return;
    }
    m_impl->objectPointsList.push_back(objectPoints);
    m_impl->imagePointsList.push_back(imagePoints);
}

bool Calibrator::Calibrate(int imageWidth, int imageHeight,
                           Intrinsics& outIntrinsics,
                           std::vector<Extrinsics>& outExtrinsics,
                           double& outRmsError) {
#if PMSDK_HAS_OPENCV
    if (m_impl->objectPointsList.empty()) {
        return false;
    }

    std::vector<std::vector<cv::Point3f>> cvObjectPoints(m_impl->objectPointsList.size());
    std::vector<std::vector<cv::Point2f>> cvImagePoints(m_impl->imagePointsList.size());

    for (size_t i = 0; i < m_impl->objectPointsList.size(); ++i) {
        const auto& objPts = m_impl->objectPointsList[i];
        const auto& imgPts = m_impl->imagePointsList[i];
        
        cvObjectPoints[i].reserve(objPts.size());
        cvImagePoints[i].reserve(imgPts.size());
        
        for (size_t j = 0; j < objPts.size(); ++j) {
            cvObjectPoints[i].push_back(cv::Point3f(objPts[j].x, objPts[j].y, objPts[j].z));
            cvImagePoints[i].push_back(cv::Point2f(imgPts[j].x, imgPts[j].y));
        }
    }

    cv::Mat cameraMatrix, distCoeffs;
    std::vector<cv::Mat> rvecs, tvecs;
    cv::Size imageSize(imageWidth, imageHeight);

    outRmsError = cv::calibrateCamera(cvObjectPoints, cvImagePoints, imageSize,
                                      cameraMatrix, distCoeffs, rvecs, tvecs);

    outIntrinsics.SetCameraMatrix(
        (float)cameraMatrix.at<double>(0, 0),
        (float)cameraMatrix.at<double>(1, 1),
        (float)cameraMatrix.at<double>(0, 2),
        (float)cameraMatrix.at<double>(1, 2)
    );

    std::vector<float> distCoeffsVec;
    for (int i = 0; i < distCoeffs.cols * distCoeffs.rows; ++i) {
        distCoeffsVec.push_back((float)distCoeffs.at<double>(i));
    }
    outIntrinsics.SetDistortionCoefficients(distCoeffsVec);

    outExtrinsics.resize(rvecs.size());
    for (size_t i = 0; i < rvecs.size(); ++i) {
        Math::Vector3 r((float)rvecs[i].at<double>(0), (float)rvecs[i].at<double>(1), (float)rvecs[i].at<double>(2));
        Math::Vector3 t((float)tvecs[i].at<double>(0), (float)tvecs[i].at<double>(1), (float)tvecs[i].at<double>(2));
        outExtrinsics[i].SetRotationVector(r);
        outExtrinsics[i].SetTranslationVector(t);
    }

    return true;
#else
    // Stub implementation if OpenCV is not available
    (void)imageWidth;
    (void)imageHeight;
    outIntrinsics.SetCameraMatrix(1.0f, 1.0f, 0.0f, 0.0f);
    outExtrinsics.clear();
    outRmsError = -1.0;
    return false;
#endif
}

} // namespace pmsdk::Calibration
