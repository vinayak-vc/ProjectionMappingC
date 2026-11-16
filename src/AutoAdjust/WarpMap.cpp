#include "PMSDK/AutoAdjust/WarpMap.h"

#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

#include <algorithm>
#include <cmath>
#include <exception>

namespace pmsdk::AutoAdjust {

namespace {

// Pull-push hole filling on a weighted 2-channel image: levels are weighted averages, holes take
// the coarser level's value. Weights saturate at 1 so dense regions are not over-trusted.
void PullPush(cv::Mat& value, cv::Mat& weight) {
    if (value.rows <= 1 && value.cols <= 1) {
        return;
    }
    const int coarseRows = std::max(1, (value.rows + 1) / 2);
    const int coarseCols = std::max(1, (value.cols + 1) / 2);
    cv::Mat coarseValue = cv::Mat::zeros(coarseRows, coarseCols, CV_32FC2);
    cv::Mat coarseWeight = cv::Mat::zeros(coarseRows, coarseCols, CV_32F);
    for (int y = 0; y < value.rows; ++y) {
        for (int x = 0; x < value.cols; ++x) {
            const float w = weight.at<float>(y, x);
            if (w <= 0.0f) {
                continue;
            }
            coarseValue.at<cv::Vec2f>(y / 2, x / 2) += value.at<cv::Vec2f>(y, x) * w;
            coarseWeight.at<float>(y / 2, x / 2) += w;
        }
    }
    for (int y = 0; y < coarseRows; ++y) {
        for (int x = 0; x < coarseCols; ++x) {
            const float w = coarseWeight.at<float>(y, x);
            if (w > 0.0f) {
                coarseValue.at<cv::Vec2f>(y, x) /= w;
                coarseWeight.at<float>(y, x) = std::min(1.0f, w);
            }
        }
    }
    if (coarseRows < value.rows || coarseCols < value.cols) {
        PullPush(coarseValue, coarseWeight);
    }
    for (int y = 0; y < value.rows; ++y) {
        for (int x = 0; x < value.cols; ++x) {
            const float w = weight.at<float>(y, x);
            if (w >= 1.0f) {
                continue;
            }
            // Bilinear upsample of the coarse level at this pixel's centre.
            const float cy = std::clamp((y + 0.5f) * 0.5f - 0.5f, 0.0f, static_cast<float>(coarseRows - 1));
            const float cx = std::clamp((x + 0.5f) * 0.5f - 0.5f, 0.0f, static_cast<float>(coarseCols - 1));
            const int y0 = static_cast<int>(cy);
            const int x0 = static_cast<int>(cx);
            const int y1 = std::min(y0 + 1, coarseRows - 1);
            const int x1 = std::min(x0 + 1, coarseCols - 1);
            const float ty = cy - y0;
            const float tx = cx - x0;
            cv::Vec2f coarse = coarseValue.at<cv::Vec2f>(y0, x0) * ((1 - tx) * (1 - ty)) +
                               coarseValue.at<cv::Vec2f>(y0, x1) * (tx * (1 - ty)) +
                               coarseValue.at<cv::Vec2f>(y1, x0) * ((1 - tx) * ty) +
                               coarseValue.at<cv::Vec2f>(y1, x1) * (tx * ty);
            const float cw = coarseWeight.at<float>(y0, x0) * ((1 - tx) * (1 - ty)) + coarseWeight.at<float>(y0, x1) * (tx * (1 - ty)) +
                             coarseWeight.at<float>(y1, x0) * ((1 - tx) * ty) + coarseWeight.at<float>(y1, x1) * (tx * ty);
            if (cw <= 0.0f) {
                continue;
            }
            value.at<cv::Vec2f>(y, x) = value.at<cv::Vec2f>(y, x) * w + coarse * (1.0f - w);
            weight.at<float>(y, x) = w + (1.0f - w) * std::min(1.0f, cw);
        }
    }
}

} // namespace

Result<WarpMap> BuildWarpMap(const std::vector<WarpMapSample>& samples, const WarpMapRect& rect, const WarpMapOptions& options) {
    if (options.width < 2 || options.height < 2 || options.width > 4096 || options.height > 4096) {
        return Status(ErrorCode::InvalidArgument, "warp map size must be 2..4096");
    }
    if (!(rect.width > 0.0f) || !(rect.height > 0.0f)) {
        return Status(ErrorCode::InvalidArgument, "warp map rect must have a positive size");
    }
    if (!(options.maxFillDistancePx >= 0.0f)) {
        return Status(ErrorCode::InvalidArgument, "maxFillDistancePx must be >= 0");
    }
    try {
        const int w = options.width;
        const int h = options.height;
        cv::Mat sum = cv::Mat::zeros(h, w, CV_32FC2);
        cv::Mat weight = cv::Mat::zeros(h, w, CV_32F);
        int used = 0;
        for (const WarpMapSample& s : samples) {
            if (!std::isfinite(s.raster.x) || !std::isfinite(s.raster.y) || !std::isfinite(s.uv.x) || !std::isfinite(s.uv.y)) {
                continue;
            }
            // Map pixel centres sit at integer coordinates.
            const float fx = (s.raster.x - rect.x) / rect.width * w - 0.5f;
            const float fy = (s.raster.y - rect.y) / rect.height * h - 0.5f;
            if (fx < -0.5f || fy < -0.5f || fx > w - 0.5f || fy > h - 0.5f) {
                continue;
            }
            const int x0 = static_cast<int>(std::floor(fx));
            const int y0 = static_cast<int>(std::floor(fy));
            const float tx = fx - x0;
            const float ty = fy - y0;
            const int xs[2] = {x0, x0 + 1};
            const int ys[2] = {y0, y0 + 1};
            const float wx[2] = {1.0f - tx, tx};
            const float wy[2] = {1.0f - ty, ty};
            for (int j = 0; j < 2; ++j) {
                for (int i = 0; i < 2; ++i) {
                    if (xs[i] < 0 || ys[j] < 0 || xs[i] >= w || ys[j] >= h) {
                        continue;
                    }
                    const float k = wx[i] * wy[j];
                    sum.at<cv::Vec2f>(ys[j], xs[i]) += cv::Vec2f(s.uv.x, s.uv.y) * k;
                    weight.at<float>(ys[j], xs[i]) += k;
                }
            }
            ++used;
        }
        if (used == 0) {
            return Status(ErrorCode::NotFound, "no samples fall inside the warp map rect");
        }

        cv::Mat value = cv::Mat::zeros(h, w, CV_32FC2);
        cv::Mat sampled = cv::Mat::zeros(h, w, CV_8U);
        for (int y = 0; y < h; ++y) {
            for (int x = 0; x < w; ++x) {
                const float k = weight.at<float>(y, x);
                if (k > 1e-6f) {
                    value.at<cv::Vec2f>(y, x) = sum.at<cv::Vec2f>(y, x) / k;
                    weight.at<float>(y, x) = std::min(1.0f, k);
                    sampled.at<std::uint8_t>(y, x) = 255;
                } else {
                    weight.at<float>(y, x) = 0.0f;
                }
            }
        }
        PullPush(value, weight);

        // Distance (map pixels) to the nearest sampled pixel limits how far holes are filled.
        cv::Mat notSampled = sampled == 0;
        cv::Mat distance;
        cv::distanceTransform(notSampled, distance, cv::DIST_L2, cv::DIST_MASK_PRECISE);

        WarpMap map;
        map.width = w;
        map.height = h;
        map.rect = rect;
        map.rgba.assign(static_cast<std::size_t>(w) * static_cast<std::size_t>(h) * 4, 0.0f);
        int validCount = 0;
        for (int y = 0; y < h; ++y) {
            for (int x = 0; x < w; ++x) {
                const bool ok = weight.at<float>(y, x) > 0.0f && distance.at<float>(y, x) <= options.maxFillDistancePx;
                if (!ok) {
                    continue;
                }
                const std::size_t i = (static_cast<std::size_t>(y) * static_cast<std::size_t>(w) + static_cast<std::size_t>(x)) * 4;
                const cv::Vec2f uv = value.at<cv::Vec2f>(y, x);
                map.rgba[i] = uv[0];
                map.rgba[i + 1] = uv[1];
                map.rgba[i + 3] = 1.0f;
                ++validCount;
            }
        }
        map.coverage = static_cast<float>(validCount) / static_cast<float>(w * h);
        return map;
    } catch (const std::exception& ex) {
        return Status(ErrorCode::OperationFailed, ex.what());
    }
}

} // namespace pmsdk::AutoAdjust
