#include "PMSDK/AutoAdjust/Facets.h"

#include <opencv2/calib3d.hpp>
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

#include <algorithm>
#include <cmath>
#include <exception>

namespace pmsdk::AutoAdjust {

namespace {

using Math::Vector2;

Vector2 ApplyH(const cv::Mat& h, const Vector2& p) {
    const double x = h.at<double>(0, 0) * p.x + h.at<double>(0, 1) * p.y + h.at<double>(0, 2);
    const double y = h.at<double>(1, 0) * p.x + h.at<double>(1, 1) * p.y + h.at<double>(1, 2);
    double w = h.at<double>(2, 0) * p.x + h.at<double>(2, 1) * p.y + h.at<double>(2, 2);
    if (std::abs(w) < 1e-12) {
        w = 1e-12;
    }
    return Vector2(static_cast<float>(x / w), static_cast<float>(y / w));
}

// Quad from a contour: simplify the convex hull with a growing tolerance until it has 4 corners;
// fall back to the minimum-area rectangle.
std::array<Vector2, 4> FitQuad(const std::vector<cv::Point>& contour, bool& fromOutline) {
    std::vector<cv::Point> hull;
    cv::convexHull(contour, hull);
    const double perimeter = cv::arcLength(hull, true);
    std::vector<cv::Point> approx;
    for (double epsilon = std::max(1.0, perimeter * 0.005); epsilon <= perimeter * 0.12; epsilon *= 1.15) {
        cv::approxPolyDP(hull, approx, epsilon, true);
        if (approx.size() <= 4) {
            break;
        }
    }
    std::array<Vector2, 4> corners{};
    if (approx.size() == 4) {
        for (int i = 0; i < 4; ++i) {
            corners[static_cast<std::size_t>(i)] =
                Vector2(static_cast<float>(approx[static_cast<std::size_t>(i)].x), static_cast<float>(approx[static_cast<std::size_t>(i)].y));
        }
        fromOutline = true;
    } else {
        const cv::RotatedRect box = cv::minAreaRect(contour);
        cv::Point2f points[4];
        box.points(points);
        for (int i = 0; i < 4; ++i) {
            corners[static_cast<std::size_t>(i)] = Vector2(points[i].x, points[i].y);
        }
        fromOutline = false;
    }
    return OrderQuad(corners);
}

} // namespace

std::array<Vector2, 4> FitQuadToOutline(const std::vector<Vector2>& outline, bool* fromOutline) {
    bool fitted = false;
    std::array<Vector2, 4> quad{};
    if (outline.size() >= 3) {
        std::vector<cv::Point> contour;
        contour.reserve(outline.size());
        for (const Vector2& p : outline) {
            contour.emplace_back(static_cast<int>(std::lround(p.x)), static_cast<int>(std::lround(p.y)));
        }
        quad = FitQuad(contour, fitted);
    }
    if (fromOutline != nullptr) {
        *fromOutline = fitted;
    }
    return quad;
}

std::array<Vector2, 4> OrderQuad(const std::array<Vector2, 4>& corners) {
    // Image coordinates (y down): top-left has the smallest x + y, bottom-right the largest;
    // top-right the largest x - y, bottom-left the smallest x - y.
    std::array<int, 4> used{0, 0, 0, 0};
    const auto pick = [&](auto score, bool largest) {
        int best = -1;
        float bestScore = 0.0f;
        for (int i = 0; i < 4; ++i) {
            if (used[static_cast<std::size_t>(i)] != 0) {
                continue;
            }
            const float s = score(corners[static_cast<std::size_t>(i)]);
            if (best < 0 || (largest ? s > bestScore : s < bestScore)) {
                best = i;
                bestScore = s;
            }
        }
        used[static_cast<std::size_t>(best)] = 1;
        return corners[static_cast<std::size_t>(best)];
    };
    const Vector2 topLeft = pick([](const Vector2& p) { return p.x + p.y; }, false);
    const Vector2 bottomRight = pick([](const Vector2& p) { return p.x + p.y; }, true);
    const Vector2 topRight = pick([](const Vector2& p) { return p.x - p.y; }, true);
    const Vector2 bottomLeft = pick([](const Vector2& p) { return p.x - p.y; }, false);
    return {bottomLeft, bottomRight, topRight, topLeft};
}

Result<std::vector<Facet>> SegmentFacets(const std::uint8_t* white, const std::uint8_t* black, int width, int height, int stride,
                                         const FacetOptions& options) {
    if (white == nullptr || black == nullptr || width < 16 || height < 16 || stride < width) {
        return Status(ErrorCode::InvalidArgument, "invalid white/black images");
    }
    if (!(options.minContrast >= 0.0f) || !(options.blurSigma >= 0.0f) || !(options.edgeThreshold > 0.0f) ||
        !(options.minAreaFraction >= 0.0f) || options.maxFacets < 1 || options.regrowPx < 0) {
        return Status(ErrorCode::InvalidArgument, "invalid facet options");
    }
    try {
        const cv::Mat whiteMat(height, width, CV_8UC1, const_cast<std::uint8_t*>(white), static_cast<std::size_t>(stride));
        const cv::Mat blackMat(height, width, CV_8UC1, const_cast<std::uint8_t*>(black), static_cast<std::size_t>(stride));
        cv::Mat diff;
        cv::subtract(whiteMat, blackMat, diff, cv::noArray(), CV_32F);
        cv::Mat lit = diff > options.minContrast;

        // Log brightness makes a facet step independent of the overall projector brightness.
        cv::Mat logImage;
        cv::max(diff, 0.0, logImage);
        logImage += 1.0;
        cv::log(logImage, logImage);
        if (options.blurSigma > 0.0f) {
            cv::GaussianBlur(logImage, logImage, cv::Size(0, 0), options.blurSigma);
        }
        // Border band = where the log brightness changes fast. Unlike thinned edge lines, the band
        // has no gaps where an inner fold meets the outer outline, so regions cannot leak.
        cv::Mat gx;
        cv::Mat gy;
        cv::Sobel(logImage, gx, CV_32F, 1, 0, 3, 0.125);
        cv::Sobel(logImage, gy, CV_32F, 0, 1, 3, 0.125);
        cv::Mat gradient;
        cv::magnitude(gx, gy, gradient);
        cv::Mat border = gradient > options.edgeThreshold;

        cv::Mat inner = lit.clone();
        inner.setTo(0, border);
        cv::Mat labels;
        cv::Mat stats;
        cv::Mat centroids;
        const int count = cv::connectedComponentsWithStats(inner, labels, stats, centroids, 4, CV_32S);
        const double minArea = std::max(16.0, static_cast<double>(options.minAreaFraction) * width * height);

        // Drop tiny components, then grow the kept ones back over the lit border band (each lit
        // pixel joins the region that reaches it first), so outlines sit on the real facet edges.
        std::vector<std::uint8_t> keep(static_cast<std::size_t>(count), 0);
        for (int label = 1; label < count; ++label) {
            keep[static_cast<std::size_t>(label)] = stats.at<int>(label, cv::CC_STAT_AREA) >= minArea ? 1 : 0;
        }
        for (int y = 0; y < height; ++y) {
            int* row = labels.ptr<int>(y);
            for (int x = 0; x < width; ++x) {
                if (row[x] > 0 && keep[static_cast<std::size_t>(row[x])] == 0) {
                    row[x] = 0;
                }
            }
        }
        for (int pass = 0; pass < options.regrowPx; ++pass) {
            cv::Mat next = labels.clone();
            bool changed = false;
            for (int y = 0; y < height; ++y) {
                const std::uint8_t* litRow = lit.ptr<std::uint8_t>(y);
                const int* current = labels.ptr<int>(y);
                int* out = next.ptr<int>(y);
                for (int x = 0; x < width; ++x) {
                    if (current[x] != 0 || litRow[x] == 0) {
                        continue;
                    }
                    int label = 0;
                    if (x > 0 && current[x - 1] > 0) {
                        label = current[x - 1];
                    } else if (x + 1 < width && current[x + 1] > 0) {
                        label = current[x + 1];
                    } else if (y > 0 && labels.ptr<int>(y - 1)[x] > 0) {
                        label = labels.ptr<int>(y - 1)[x];
                    } else if (y + 1 < height && labels.ptr<int>(y + 1)[x] > 0) {
                        label = labels.ptr<int>(y + 1)[x];
                    }
                    if (label > 0) {
                        out[x] = label;
                        changed = true;
                    }
                }
            }
            labels = next;
            if (!changed) {
                break;
            }
        }

        std::vector<Facet> facets;
        for (int label = 1; label < count; ++label) {
            if (keep[static_cast<std::size_t>(label)] == 0) {
                continue;
            }
            cv::Mat region = labels == label;
            std::vector<std::vector<cv::Point>> contours;
            cv::findContours(region, contours, cv::RETR_EXTERNAL, cv::CHAIN_APPROX_SIMPLE);
            if (contours.empty()) {
                continue;
            }
            std::size_t largest = 0;
            for (std::size_t c = 1; c < contours.size(); ++c) {
                if (cv::contourArea(contours[c]) > cv::contourArea(contours[largest])) {
                    largest = c;
                }
            }
            const std::vector<cv::Point>& contour = contours[largest];
            Facet facet;
            std::vector<cv::Point> simplified;
            cv::approxPolyDP(contour, simplified, options.polygonEpsilonPx, true);
            for (const cv::Point& p : simplified) {
                facet.polygon.emplace_back(static_cast<float>(p.x), static_cast<float>(p.y));
            }
            facet.quad = FitQuad(contour, facet.quadFromOutline);
            facet.areaPx = static_cast<float>(cv::countNonZero(region));
            facet.meanContrast = static_cast<float>(cv::mean(diff, region)[0]);
            facets.push_back(std::move(facet));
        }
        std::sort(facets.begin(), facets.end(), [](const Facet& a, const Facet& b) { return a.areaPx > b.areaPx; });
        if (static_cast<int>(facets.size()) > options.maxFacets) {
            facets.resize(static_cast<std::size_t>(options.maxFacets));
        }
        return facets;
    } catch (const std::exception& ex) {
        return Status(ErrorCode::OperationFailed, ex.what());
    }
}

int FindFacetContaining(const std::vector<Facet>& facets, const std::vector<Vector2>& points, const std::vector<std::uint8_t>& valid) {
    int best = -1;
    int bestCount = 0;
    for (std::size_t f = 0; f < facets.size(); ++f) {
        if (facets[f].polygon.size() < 3) {
            continue;
        }
        std::vector<cv::Point2f> outline;
        for (const Vector2& p : facets[f].polygon) {
            outline.emplace_back(p.x, p.y);
        }
        int inside = 0;
        for (std::size_t k = 0; k < points.size(); ++k) {
            if (k < valid.size() && valid[k] == 0) {
                continue;
            }
            if (cv::pointPolygonTest(outline, cv::Point2f(points[k].x, points[k].y), false) >= 0.0) {
                ++inside;
            }
        }
        if (inside > bestCount) {
            bestCount = inside;
            best = static_cast<int>(f);
        }
    }
    return best;
}

Result<std::array<Vector2, 4>> SolveCornersForTarget(const SkinWarp& current, const std::vector<Vector2>& uvs,
                                                     const std::vector<Vector2>& camera, const std::vector<std::uint8_t>& valid,
                                                     const std::array<Vector2, 4>& targetQuad) {
    if (uvs.size() != camera.size() || uvs.size() != valid.size()) {
        return Status(ErrorCode::InvalidArgument, "uvs, camera and valid must have the same length");
    }
    try {
        std::vector<cv::Point2f> cameraPoints;
        std::vector<cv::Point2f> projectorPoints;
        for (std::size_t k = 0; k < uvs.size(); ++k) {
            if (valid[k] == 0) {
                continue;
            }
            const Vector2 p = EvaluateWarp(current, uvs[k].x, uvs[k].y);
            cameraPoints.emplace_back(camera[k].x, camera[k].y);
            projectorPoints.emplace_back(p.x, p.y);
        }
        if (cameraPoints.size() < 4) {
            return Status(ErrorCode::NotFound, "need at least 4 valid corners to relate camera and projector");
        }
        const cv::Mat cameraToProjector = cv::findHomography(cameraPoints, projectorPoints, 0);
        if (cameraToProjector.empty()) {
            return Status(ErrorCode::OperationFailed, "could not relate camera and projector from the corners");
        }
        std::array<Vector2, 4> corners{};
        for (std::size_t i = 0; i < 4; ++i) {
            corners[i] = ApplyH(cameraToProjector, targetQuad[i]);
            if (!std::isfinite(corners[i].x) || !std::isfinite(corners[i].y)) {
                return Status(ErrorCode::OperationFailed, "target quad maps outside the projector");
            }
        }
        return corners;
    } catch (const std::exception& ex) {
        return Status(ErrorCode::OperationFailed, ex.what());
    }
}

float MetricScaleMmPerPx(const std::array<Vector2, 4>& quad, float widthMm, float heightMm) {
    if (!(widthMm > 0.0f) || !(heightMm > 0.0f)) {
        return 0.0f;
    }
    const auto length = [](const Vector2& a, const Vector2& b) {
        return std::hypot(b.x - a.x, b.y - a.y);
    };
    const float bottom = length(quad[0], quad[1]);
    const float right = length(quad[1], quad[2]);
    const float top = length(quad[3], quad[2]);
    const float left = length(quad[0], quad[3]);
    if (bottom < 1e-3f || right < 1e-3f || top < 1e-3f || left < 1e-3f) {
        return 0.0f;
    }
    return 0.25f * (widthMm / bottom + widthMm / top + heightMm / left + heightMm / right);
}

} // namespace pmsdk::AutoAdjust
