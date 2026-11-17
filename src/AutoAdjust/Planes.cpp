#include "PMSDK/AutoAdjust/Planes.h"
#include "PMSDK/AutoAdjust/Facets.h"

#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

#include <algorithm>
#include <cmath>
#include <exception>
#include <random>

namespace pmsdk::AutoAdjust {

namespace {

struct CloudPoint {
    cv::Vec3d position;
    int gridX;
    int gridY;
};

struct PlaneFit {
    cv::Vec3d normal;
    double d = 0.0;
    cv::Vec3d centroid;
    cv::Vec3d axisU;
    cv::Vec3d axisV;
};

// Least-squares plane through the points (smallest-eigenvalue direction of the covariance).
bool FitPlane(const std::vector<CloudPoint>& cloud, const std::vector<int>& indices, PlaneFit& fit) {
    if (indices.size() < 3) {
        return false;
    }
    cv::Vec3d mean(0.0, 0.0, 0.0);
    for (int i : indices) {
        mean += cloud[static_cast<std::size_t>(i)].position;
    }
    mean *= 1.0 / static_cast<double>(indices.size());
    cv::Matx33d covariance = cv::Matx33d::zeros();
    for (int i : indices) {
        const cv::Vec3d q = cloud[static_cast<std::size_t>(i)].position - mean;
        covariance += q * q.t();
    }
    cv::Mat eigenValues;
    cv::Mat eigenVectors;
    if (!cv::eigen(cv::Mat(covariance), eigenValues, eigenVectors)) {
        return false;
    }
    // Rows sorted by descending eigenvalue: 0 and 1 span the plane, 2 is the normal.
    fit.axisU = cv::Vec3d(eigenVectors.at<double>(0, 0), eigenVectors.at<double>(0, 1), eigenVectors.at<double>(0, 2));
    fit.axisV = cv::Vec3d(eigenVectors.at<double>(1, 0), eigenVectors.at<double>(1, 1), eigenVectors.at<double>(1, 2));
    fit.normal = cv::Vec3d(eigenVectors.at<double>(2, 0), eigenVectors.at<double>(2, 1), eigenVectors.at<double>(2, 2));
    const double length = cv::norm(fit.normal);
    if (length < 1e-12) {
        return false;
    }
    fit.normal *= 1.0 / length;
    fit.centroid = mean;
    fit.d = -fit.normal.dot(mean);
    return true;
}

std::vector<int> Inliers(const std::vector<CloudPoint>& cloud, const std::vector<int>& pool, const cv::Vec3d& normal, double d,
                         double threshold) {
    std::vector<int> inliers;
    for (int i : pool) {
        if (std::abs(normal.dot(cloud[static_cast<std::size_t>(i)].position) + d) <= threshold) {
            inliers.push_back(i);
        }
    }
    return inliers;
}

float Percentile(std::vector<double> values, double fraction) {
    if (values.empty()) {
        return 0.0f;
    }
    const std::size_t index = static_cast<std::size_t>(std::clamp(fraction, 0.0, 1.0) * static_cast<double>(values.size() - 1));
    std::nth_element(values.begin(), values.begin() + static_cast<std::ptrdiff_t>(index), values.end());
    return static_cast<float>(values[index]);
}

} // namespace

Result<std::vector<Plane>> DetectPlanes(const std::uint16_t* depthMm, int width, int height, int stride,
                                        const CameraIntrinsics& intrinsics, const PlaneOptions& options) {
    if (depthMm == nullptr || width < 8 || height < 8 || stride < width) {
        return Status(ErrorCode::InvalidArgument, "invalid depth image");
    }
    const float fx = intrinsics.fx;
    const float fy = intrinsics.fy;
    const float cx = intrinsics.cx;
    const float cy = intrinsics.cy;
    if (!(fx > 0.0f) || !(fy > 0.0f)) {
        return Status(ErrorCode::InvalidArgument, "depth intrinsics need positive fx and fy");
    }
    if (options.step < 1 || options.iterations < 1 || options.maxPlanes < 1 || !(options.inlierThresholdMm > 0.0f) ||
        !(options.maxDepthMm > options.minDepthMm)) {
        return Status(ErrorCode::InvalidArgument, "invalid plane options");
    }
    try {
        const int gridW = (width + options.step - 1) / options.step;
        const int gridH = (height + options.step - 1) / options.step;
        std::vector<Math::Vector2> pixels;
        std::vector<float> depths;
        std::vector<int> gridIndex;
        for (int gy = 0; gy < gridH; ++gy) {
            const int y = gy * options.step;
            for (int gx = 0; gx < gridW; ++gx) {
                const int x = gx * options.step;
                const float z = depthMm[static_cast<std::size_t>(y) * static_cast<std::size_t>(stride) + static_cast<std::size_t>(x)];
                if (z < options.minDepthMm || z > options.maxDepthMm) {
                    continue;
                }
                pixels.emplace_back(static_cast<float>(x), static_cast<float>(y));
                depths.push_back(z);
                gridIndex.push_back(gy * gridW + gx);
            }
        }
        // Straight rays: remove lens distortion from the pixel positions (no-op without coefficients).
        Result<std::vector<Math::Vector2>> rays = UndistortPoints(pixels, intrinsics);
        if (!rays.ok()) {
            return rays.status();
        }
        std::vector<CloudPoint> cloud;
        cloud.reserve(pixels.size());
        for (std::size_t i = 0; i < pixels.size(); ++i) {
            const Math::Vector2 ray = rays.value()[i];
            const double z = depths[i];
            CloudPoint p;
            p.position = cv::Vec3d((ray.x - cx) * z / fx, (ray.y - cy) * z / fy, z);
            p.gridX = gridIndex[i] % gridW;
            p.gridY = gridIndex[i] / gridW;
            cloud.push_back(p);
        }
        if (cloud.size() < 30) {
            return Status(ErrorCode::NotFound, "too few valid depth pixels");
        }
        const std::size_t total = cloud.size();
        const std::size_t minInliers = std::max<std::size_t>(30, static_cast<std::size_t>(options.minInlierFraction * total));

        std::vector<int> pool(total);
        for (std::size_t i = 0; i < total; ++i) {
            pool[i] = static_cast<int>(i);
        }
        std::mt19937 rng(options.seed);
        std::vector<Plane> planes;
        int stalls = 0;

        while (static_cast<int>(planes.size()) < options.maxPlanes && pool.size() >= minInliers && stalls < 3) {
            std::uniform_int_distribution<std::size_t> pick(0, pool.size() - 1);
            std::vector<int> best;
            for (int iteration = 0; iteration < options.iterations; ++iteration) {
                const cv::Vec3d& a = cloud[static_cast<std::size_t>(pool[pick(rng)])].position;
                const cv::Vec3d& b = cloud[static_cast<std::size_t>(pool[pick(rng)])].position;
                const cv::Vec3d& c = cloud[static_cast<std::size_t>(pool[pick(rng)])].position;
                cv::Vec3d n = (b - a).cross(c - a);
                const double length = cv::norm(n);
                if (length < 1e-6) {
                    continue;
                }
                n *= 1.0 / length;
                const double d = -n.dot(a);
                std::vector<int> inliers = Inliers(cloud, pool, n, d, options.inlierThresholdMm);
                if (inliers.size() > best.size()) {
                    best.swap(inliers);
                }
            }
            if (best.size() < minInliers) {
                break;
            }
            PlaneFit fit;
            for (int refine = 0; refine < 2; ++refine) {
                if (!FitPlane(cloud, best, fit)) {
                    break;
                }
                best = Inliers(cloud, pool, fit.normal, fit.d, options.inlierThresholdMm);
            }
            if (!FitPlane(cloud, best, fit)) {
                break;
            }

            // Keep the largest image-connected piece: parallel facets elsewhere become their own planes.
            cv::Mat mask = cv::Mat::zeros(gridH, gridW, CV_8U);
            for (int i : best) {
                mask.at<std::uint8_t>(cloud[static_cast<std::size_t>(i)].gridY, cloud[static_cast<std::size_t>(i)].gridX) = 255;
            }
            cv::Mat closed;
            cv::morphologyEx(mask, closed, cv::MORPH_CLOSE, cv::getStructuringElement(cv::MORPH_RECT, cv::Size(3, 3)));
            cv::Mat labels;
            cv::Mat stats;
            cv::Mat centroids;
            const int count = cv::connectedComponentsWithStats(closed, labels, stats, centroids, 8, CV_32S);
            int largest = 0;
            for (int label = 1; label < count; ++label) {
                if (largest == 0 || stats.at<int>(label, cv::CC_STAT_AREA) > stats.at<int>(largest, cv::CC_STAT_AREA)) {
                    largest = label;
                }
            }
            std::vector<int> piece;
            for (int i : best) {
                const CloudPoint& p = cloud[static_cast<std::size_t>(i)];
                if (largest > 0 && labels.at<int>(p.gridY, p.gridX) == largest) {
                    piece.push_back(i);
                }
            }
            if (piece.size() < minInliers) {
                // The best plane is fragmented: drop its points so the search can move on.
                std::vector<int> keep;
                std::vector<std::uint8_t> drop(total, 0);
                for (int i : best) {
                    drop[static_cast<std::size_t>(i)] = 1;
                }
                for (int i : pool) {
                    if (drop[static_cast<std::size_t>(i)] == 0) {
                        keep.push_back(i);
                    }
                }
                pool.swap(keep);
                ++stalls;
                continue;
            }
            stalls = 0;
            FitPlane(cloud, piece, fit);

            Plane plane;
            cv::Vec3d normal = fit.normal;
            double d = fit.d;
            if (normal.dot(fit.centroid) > 0.0) {
                normal = -normal;
                d = -d;
            }
            plane.normal = Math::Vector3(static_cast<float>(normal[0]), static_cast<float>(normal[1]), static_cast<float>(normal[2]));
            plane.d = static_cast<float>(d);
            plane.inlierCount = static_cast<int>(piece.size());
            plane.viewAngleDeg = static_cast<float>(std::acos(std::clamp(-normal[2], -1.0, 1.0)) * 180.0 / CV_PI);
            double sumSq = 0.0;
            std::vector<double> along;
            std::vector<double> across;
            along.reserve(piece.size());
            across.reserve(piece.size());
            for (int i : piece) {
                const cv::Vec3d q = cloud[static_cast<std::size_t>(i)].position;
                const double distance = normal.dot(q) + d;
                sumSq += distance * distance;
                along.push_back(fit.axisU.dot(q - fit.centroid));
                across.push_back(fit.axisV.dot(q - fit.centroid));
            }
            plane.rmsMm = static_cast<float>(std::sqrt(sumSq / static_cast<double>(piece.size())));
            plane.widthMm = Percentile(along, 0.98) - Percentile(along, 0.02);
            plane.heightMm = Percentile(across, 0.98) - Percentile(across, 0.02);

            cv::Mat pieceMask = labels == largest;
            std::vector<std::vector<cv::Point>> contours;
            cv::findContours(pieceMask, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);
            if (!contours.empty()) {
                std::size_t biggest = 0;
                for (std::size_t c = 1; c < contours.size(); ++c) {
                    if (cv::contourArea(contours[c]) > cv::contourArea(contours[biggest])) {
                        biggest = c;
                    }
                }
                std::vector<cv::Point> simplified;
                const double epsilon = std::max(0.5, static_cast<double>(options.polygonEpsilonPx) / options.step);
                cv::approxPolyDP(contours[biggest], simplified, epsilon, true);
                const float half = 0.5f * static_cast<float>(options.step - 1);
                for (const cv::Point& p : simplified) {
                    plane.polygon.emplace_back(static_cast<float>(p.x * options.step) + half,
                                               static_cast<float>(p.y * options.step) + half);
                }
                bool fromOutline = false;
                plane.quad = FitQuadToOutline(plane.polygon, &fromOutline);
            }
            planes.push_back(std::move(plane));

            std::vector<std::uint8_t> used(total, 0);
            for (int i : piece) {
                used[static_cast<std::size_t>(i)] = 1;
            }
            std::vector<int> keep;
            keep.reserve(pool.size());
            for (int i : pool) {
                if (used[static_cast<std::size_t>(i)] == 0) {
                    keep.push_back(i);
                }
            }
            pool.swap(keep);
        }
        if (planes.empty()) {
            return Status(ErrorCode::NotFound, "no plane found in the depth image");
        }
        return planes;
    } catch (const std::exception& ex) {
        return Status(ErrorCode::OperationFailed, ex.what());
    }
}

} // namespace pmsdk::AutoAdjust
