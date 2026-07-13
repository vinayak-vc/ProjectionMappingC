#include "PMSDK/Calibration/GrayCodeDecoder.h"
#include <opencv2/opencv.hpp>
#include <iostream>

namespace pmsdk::Calibration {

static int grayToBinary(int gray) {
    int binary = gray;
    while (gray >>= 1) {
        binary ^= gray;
    }
    return binary;
}

struct GrayCodeDecoder::Impl {
    int width;
    int height;
    int colBits;
    int rowBits;
    std::vector<cv::Mat> images;
    cv::VideoCapture capture;

    Impl(int w, int h) : width(w), height(h) {
        colBits = 0;
        int pw = 1;
        while (pw < width) {
            pw *= 2;
            colBits++;
        }
        
        rowBits = 0;
        int ph = 1;
        while (ph < height) {
            ph *= 2;
            rowBits++;
        }
    }
};

GrayCodeDecoder::GrayCodeDecoder(int projectorWidth, int projectorHeight)
    : m_impl(std::make_unique<Impl>(projectorWidth, projectorHeight)) {}

GrayCodeDecoder::~GrayCodeDecoder() {
    CloseCamera();
}

bool GrayCodeDecoder::OpenCamera(int cameraIndex) {
    if (m_impl->capture.isOpened()) {
        m_impl->capture.release();
    }
    return m_impl->capture.open(cameraIndex);
}

bool GrayCodeDecoder::CaptureFrame() {
    if (!m_impl->capture.isOpened()) {
        return false;
    }
    cv::Mat frame;
    if (!m_impl->capture.read(frame)) {
        return false;
    }
    
    cv::Mat gray;
    cv::cvtColor(frame, gray, cv::COLOR_BGR2GRAY);
    m_impl->images.push_back(gray);
    return true;
}

void GrayCodeDecoder::CloseCamera() {
    if (m_impl->capture.isOpened()) {
        m_impl->capture.release();
    }
}

bool GrayCodeDecoder::AddImage(const std::string& filepath) {
    cv::Mat img = cv::imread(filepath, cv::IMREAD_GRAYSCALE);
    if (img.empty()) {
        return false;
    }
    m_impl->images.push_back(img);
    return true;
}

std::vector<PointMatch> GrayCodeDecoder::Decode(int threshold) const {
    std::vector<PointMatch> matches;
    size_t requiredPatterns = m_impl->colBits + m_impl->rowBits;
    
    if (m_impl->images.size() != requiredPatterns) {
        return matches;
    }

    int camWidth = m_impl->images[0].cols;
    int camHeight = m_impl->images[0].rows;

    for (const auto& img : m_impl->images) {
        if (img.cols != camWidth || img.rows != camHeight) {
            return matches;
        }
    }

    for (int y = 0; y < camHeight; ++y) {
        for (int x = 0; x < camWidth; ++x) {
            int projXGray = 0;
            int projYGray = 0;
            
            for (int i = 0; i < m_impl->colBits; ++i) {
                uint8_t pixel = m_impl->images[i].at<uint8_t>(y, x);
                if (pixel >= threshold) {
                    projXGray |= (1 << (m_impl->colBits - 1 - i));
                }
            }
            
            for (int i = 0; i < m_impl->rowBits; ++i) {
                uint8_t pixel = m_impl->images[m_impl->colBits + i].at<uint8_t>(y, x);
                if (pixel >= threshold) {
                    projYGray |= (1 << (m_impl->rowBits - 1 - i));
                }
            }

            int projX = grayToBinary(projXGray);
            int projY = grayToBinary(projYGray);

            if (projX < m_impl->width && projY < m_impl->height) {
                matches.push_back({{static_cast<float>(x), static_cast<float>(y)}, 
                                   {static_cast<float>(projX), static_cast<float>(projY)}});
            }
        }
    }

    return matches;
}

std::vector<Math::Vector3> GrayCodeDecoder::Triangulate(
    const std::vector<PointMatch>& matches,
    const Intrinsics& camIntrinsics, const Extrinsics& camExtrinsics,
    const Intrinsics& projIntrinsics, const Extrinsics& projExtrinsics) 
{
    std::vector<Math::Vector3> points3d;
    if (matches.empty()) return points3d;

    cv::Mat K1 = cv::Mat::eye(3, 3, CV_64F);
    float fx, fy, cx, cy;
    camIntrinsics.GetCameraMatrix(fx, fy, cx, cy);
    K1.at<double>(0, 0) = fx; K1.at<double>(1, 1) = fy;
    K1.at<double>(0, 2) = cx; K1.at<double>(1, 2) = cy;

    cv::Mat R1, t1(3, 1, CV_64F);
    Math::Vector3 camRot = camExtrinsics.GetRotationVector();
    Math::Vector3 camTrans = camExtrinsics.GetTranslationVector();
    cv::Mat rvec1 = (cv::Mat_<double>(3, 1) << camRot.x, camRot.y, camRot.z);
    cv::Rodrigues(rvec1, R1);
    t1.at<double>(0, 0) = camTrans.x; t1.at<double>(1, 0) = camTrans.y; t1.at<double>(2, 0) = camTrans.z;

    cv::Mat P1(3, 4, CV_64F);
    R1.copyTo(P1(cv::Rect(0, 0, 3, 3)));
    t1.copyTo(P1(cv::Rect(3, 0, 1, 3)));
    P1 = K1 * P1;

    cv::Mat K2 = cv::Mat::eye(3, 3, CV_64F);
    projIntrinsics.GetCameraMatrix(fx, fy, cx, cy);
    K2.at<double>(0, 0) = fx; K2.at<double>(1, 1) = fy;
    K2.at<double>(0, 2) = cx; K2.at<double>(1, 2) = cy;

    cv::Mat R2, t2(3, 1, CV_64F);
    Math::Vector3 projRot = projExtrinsics.GetRotationVector();
    Math::Vector3 projTrans = projExtrinsics.GetTranslationVector();
    cv::Mat rvec2 = (cv::Mat_<double>(3, 1) << projRot.x, projRot.y, projRot.z);
    cv::Rodrigues(rvec2, R2);
    t2.at<double>(0, 0) = projTrans.x; t2.at<double>(1, 0) = projTrans.y; t2.at<double>(2, 0) = projTrans.z;

    cv::Mat P2(3, 4, CV_64F);
    R2.copyTo(P2(cv::Rect(0, 0, 3, 3)));
    t2.copyTo(P2(cv::Rect(3, 0, 1, 3)));
    P2 = K2 * P2;

    std::vector<cv::Point2f> pts1, pts2;
    for (const auto& match : matches) {
        pts1.push_back(cv::Point2f(match.cameraPoint.x, match.cameraPoint.y));
        pts2.push_back(cv::Point2f(match.projectorPoint.x, match.projectorPoint.y));
    }

    cv::Mat points4D;
    cv::triangulatePoints(P1, P2, pts1, pts2, points4D);

    for (int i = 0; i < points4D.cols; ++i) {
        double w = points4D.at<double>(3, i);
        if (std::abs(w) > 1e-6) {
            points3d.push_back({
                static_cast<float>(points4D.at<double>(0, i) / w),
                static_cast<float>(points4D.at<double>(1, i) / w),
                static_cast<float>(points4D.at<double>(2, i) / w)
            });
        }
    }

    return points3d;
}

} // namespace pmsdk::Calibration
