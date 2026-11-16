#include "PMSDK/AutoAdjust/AutoAdjust.h"

#include <opencv2/calib3d.hpp>
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

#include <algorithm>
#include <cmath>
#include <exception>
#include <limits>
#include <string>

namespace pmsdk::AutoAdjust {

namespace {

using Math::Vector2;

// Unit square -> quad homography (Heckbert 1989), identical to Geometry::PerspectiveWarp but in
// double precision and copyable so it can be evaluated cheaply many times.
struct QuadMap {
    double a = 1.0;
    double b = 0.0;
    double c = 0.0;
    double d = 0.0;
    double e = 1.0;
    double f = 0.0;
    double g = 0.0;
    double h = 0.0;

    explicit QuadMap(const std::array<Vector2, 4>& q) {
        const double x0 = q[0].x;
        const double y0 = q[0].y;
        const double x1 = q[1].x;
        const double y1 = q[1].y;
        const double x2 = q[2].x;
        const double y2 = q[2].y;
        const double x3 = q[3].x;
        const double y3 = q[3].y;
        const double dx1 = x1 - x2;
        const double dx2 = x3 - x2;
        const double dx3 = x0 - x1 + x2 - x3;
        const double dy1 = y1 - y2;
        const double dy2 = y3 - y2;
        const double dy3 = y0 - y1 + y2 - y3;
        if (std::abs(dx3) < 1e-9 && std::abs(dy3) < 1e-9) {
            a = x1 - x0;
            b = x3 - x0;
            c = x0;
            d = y1 - y0;
            e = y3 - y0;
            f = y0;
            g = 0.0;
            h = 0.0;
        } else {
            double denom = dx1 * dy2 - dx2 * dy1;
            if (std::abs(denom) < 1e-12) {
                denom = 1e-12;
            }
            g = (dx3 * dy2 - dx2 * dy3) / denom;
            h = (dx1 * dy3 - dx3 * dy1) / denom;
            a = x1 - x0 + g * x1;
            b = x3 - x0 + h * x3;
            c = x0;
            d = y1 - y0 + g * y1;
            e = y3 - y0 + h * y3;
            f = y0;
        }
    }

    Vector2 Map(double u, double v) const {
        const double x = a * u + b * v + c;
        const double y = d * u + e * v + f;
        double w = g * u + h * v + 1.0;
        if (std::abs(w) < 1e-12) {
            w = 1e-12;
        }
        return Vector2(static_cast<float>(x / w), static_cast<float>(y / w));
    }
};

Vector2 ApplyHomography(const cv::Mat& h, const Vector2& p) {
    const double x = h.at<double>(0, 0) * p.x + h.at<double>(0, 1) * p.y + h.at<double>(0, 2);
    const double y = h.at<double>(1, 0) * p.x + h.at<double>(1, 1) * p.y + h.at<double>(1, 2);
    double w = h.at<double>(2, 0) * p.x + h.at<double>(2, 1) * p.y + h.at<double>(2, 2);
    if (std::abs(w) < 1e-12) {
        w = 1e-12;
    }
    return Vector2(static_cast<float>(x / w), static_cast<float>(y / w));
}

std::vector<cv::Point2f> ToCv(const std::vector<Vector2>& points) {
    std::vector<cv::Point2f> out;
    out.reserve(points.size());
    for (const Vector2& p : points) {
        out.emplace_back(p.x, p.y);
    }
    return out;
}

bool IsFinite(const Vector2& p) {
    return std::isfinite(p.x) && std::isfinite(p.y);
}

// Mean grey level of a (2r+1)^2 patch around p; -1 when p is outside the image.
double SamplePatch(const cv::Mat& image, const Vector2& p, int radius) {
    const int cx = static_cast<int>(std::lround(p.x));
    const int cy = static_cast<int>(std::lround(p.y));
    if (cx - radius < 0 || cy - radius < 0 || cx + radius >= image.cols || cy + radius >= image.rows) {
        return -1.0;
    }
    double sum = 0.0;
    int count = 0;
    for (int y = cy - radius; y <= cy + radius; ++y) {
        const std::uint8_t* row = image.ptr<std::uint8_t>(y);
        for (int x = cx - radius; x <= cx + radius; ++x) {
            sum += row[x];
            ++count;
        }
    }
    return count > 0 ? sum / count : -1.0;
}

struct ValidSet {
    std::vector<Vector2> uv;
    std::vector<Vector2> camera;
    std::vector<int> index;
};

Result<ValidSet> CollectValid(const std::vector<Vector2>& uvs, const std::vector<Vector2>& camera,
                              const std::vector<std::uint8_t>& valid) {
    if (uvs.size() != camera.size() || uvs.size() != valid.size()) {
        return Status(ErrorCode::InvalidArgument, "uvs, camera and valid must have the same length");
    }
    ValidSet set;
    for (std::size_t k = 0; k < uvs.size(); ++k) {
        if (valid[k] != 0 && IsFinite(uvs[k]) && IsFinite(camera[k])) {
            set.uv.push_back(uvs[k]);
            set.camera.push_back(camera[k]);
            set.index.push_back(static_cast<int>(k));
        }
    }
    return set;
}

// Content UV -> camera pixel homography that defines "correct".
Result<cv::Mat> TargetHomography(const ValidSet& set, const RefineOptions& options) {
    if (options.useTargetQuad) {
        const std::vector<cv::Point2f> unit = {cv::Point2f(0.0f, 0.0f), cv::Point2f(1.0f, 0.0f), cv::Point2f(1.0f, 1.0f),
                                               cv::Point2f(0.0f, 1.0f)};
        std::vector<cv::Point2f> quad;
        for (const Vector2& p : options.targetQuad) {
            quad.emplace_back(p.x, p.y);
        }
        cv::Mat h = cv::getPerspectiveTransform(unit, quad);
        if (h.empty()) {
            return Status(ErrorCode::InvalidArgument, "target quad is degenerate");
        }
        h.convertTo(h, CV_64F);
        return h;
    }
    if (set.uv.size() < 4) {
        return Status(ErrorCode::NotFound, "need at least 4 valid corners to fit the target grid");
    }
    cv::Mat h = cv::findHomography(ToCv(set.uv), ToCv(set.camera), 0);
    if (h.empty()) {
        return Status(ErrorCode::OperationFailed, "could not fit a homography to the detected corners");
    }
    return h;
}

Score ComputeScore(const ValidSet& set, const cv::Mat& target) {
    Score score;
    score.validCount = static_cast<int>(set.uv.size());
    double sumSq = 0.0;
    double maxErr = 0.0;
    for (std::size_t k = 0; k < set.uv.size(); ++k) {
        const Vector2 ideal = ApplyHomography(target, set.uv[k]);
        const double dx = static_cast<double>(ideal.x) - set.camera[k].x;
        const double dy = static_cast<double>(ideal.y) - set.camera[k].y;
        const double err = std::sqrt(dx * dx + dy * dy);
        sumSq += err * err;
        maxErr = std::max(maxErr, err);
    }
    if (!set.uv.empty()) {
        score.rmsPx = static_cast<float>(std::sqrt(sumSq / static_cast<double>(set.uv.size())));
    }
    score.maxPx = static_cast<float>(maxErr);
    return score;
}

// Bilinear weights of (u, v) on a columns x rows grid: up to 4 (node, weight) pairs.
int BilinearWeights(int columns, int rows, double u, double v, int* nodes, double* weights) {
    u = std::clamp(u, 0.0, 1.0);
    v = std::clamp(v, 0.0, 1.0);
    const double fx = u * (columns - 1);
    const double fy = v * (rows - 1);
    const int x0 = std::min(static_cast<int>(std::floor(fx)), columns - 2);
    const int y0 = std::min(static_cast<int>(std::floor(fy)), rows - 2);
    const double tx = fx - x0;
    const double ty = fy - y0;
    nodes[0] = y0 * columns + x0;
    nodes[1] = y0 * columns + x0 + 1;
    nodes[2] = (y0 + 1) * columns + x0;
    nodes[3] = (y0 + 1) * columns + x0 + 1;
    weights[0] = (1.0 - tx) * (1.0 - ty);
    weights[1] = tx * (1.0 - ty);
    weights[2] = (1.0 - tx) * ty;
    weights[3] = tx * ty;
    return 4;
}

// Least-squares grid of offsets that reproduces the scattered increments at the given UVs, with a
// second-difference smoothness prior (extrapolates linearly beyond the data) and optional pinned
// corner nodes.
Result<CorrectionGrid> FitGrid(const std::vector<Vector2>& uvs, const std::vector<Vector2>& increments,
                               const RefineOptions& options) {
    const int columns = options.gridColumns;
    const int rows = options.gridRows;
    const int n = columns * rows;
    cv::Mat normal = cv::Mat::zeros(n, n, CV_64F);
    cv::Mat rhs = cv::Mat::zeros(n, 2, CV_64F);

    for (std::size_t k = 0; k < uvs.size(); ++k) {
        int nodes[4];
        double weights[4];
        BilinearWeights(columns, rows, uvs[k].x, uvs[k].y, nodes, weights);
        for (int a = 0; a < 4; ++a) {
            for (int b = 0; b < 4; ++b) {
                normal.at<double>(nodes[a], nodes[b]) += weights[a] * weights[b];
            }
            rhs.at<double>(nodes[a], 0) += weights[a] * increments[k].x;
            rhs.at<double>(nodes[a], 1) += weights[a] * increments[k].y;
        }
    }

    const double dataScale = std::max(1.0, static_cast<double>(uvs.size()) / static_cast<double>(n));
    const double lambda = static_cast<double>(options.smoothness) * dataScale;
    if (lambda > 0.0) {
        for (int r = 0; r < rows; ++r) {
            for (int c = 0; c < columns; ++c) {
                if (c >= 1 && c + 1 < columns) {
                    const int idx[3] = {r * columns + c - 1, r * columns + c, r * columns + c + 1};
                    const double coef[3] = {1.0, -2.0, 1.0};
                    for (int a = 0; a < 3; ++a) {
                        for (int b = 0; b < 3; ++b) {
                            normal.at<double>(idx[a], idx[b]) += lambda * coef[a] * coef[b];
                        }
                    }
                }
                if (r >= 1 && r + 1 < rows) {
                    const int idx[3] = {(r - 1) * columns + c, r * columns + c, (r + 1) * columns + c};
                    const double coef[3] = {1.0, -2.0, 1.0};
                    for (int a = 0; a < 3; ++a) {
                        for (int b = 0; b < 3; ++b) {
                            normal.at<double>(idx[a], idx[b]) += lambda * coef[a] * coef[b];
                        }
                    }
                }
            }
        }
    }
    if (options.pinCorners) {
        const double pinWeight = 1000.0 * dataScale;
        const int corners[4] = {0, columns - 1, (rows - 1) * columns, rows * columns - 1};
        for (int corner : corners) {
            normal.at<double>(corner, corner) += pinWeight;
        }
    }
    for (int i = 0; i < n; ++i) {
        normal.at<double>(i, i) += 1e-9;
    }

    cv::Mat solution;
    if (!cv::solve(normal, rhs, solution, cv::DECOMP_CHOLESKY)) {
        if (!cv::solve(normal, rhs, solution, cv::DECOMP_SVD)) {
            return Status(ErrorCode::OperationFailed, "correction grid solve failed");
        }
    }
    CorrectionGrid grid;
    grid.columns = columns;
    grid.rows = rows;
    grid.offsets.resize(static_cast<std::size_t>(n));
    for (int i = 0; i < n; ++i) {
        grid.offsets[static_cast<std::size_t>(i)] =
            Vector2(static_cast<float>(solution.at<double>(i, 0)), static_cast<float>(solution.at<double>(i, 1)));
    }
    return grid;
}

Status ValidateOptions(const RefineOptions& options) {
    if (!(options.damping > 0.0f && options.damping <= 1.0f)) {
        return Status(ErrorCode::InvalidArgument, "damping must be in (0, 1]");
    }
    if (options.gridColumns < 2 || options.gridColumns > 33 || options.gridRows < 2 || options.gridRows > 33) {
        return Status(ErrorCode::InvalidArgument, "grid columns and rows must be 2..33");
    }
    if (!(options.smoothness >= 0.0f) || !(options.maxStep > 0.0f)) {
        return Status(ErrorCode::InvalidArgument, "smoothness must be >= 0 and maxStep > 0");
    }
    return Status::Ok();
}

} // namespace

Status ValidateChecker(const CheckerSpec& spec) {
    if (spec.squaresX < 3 || spec.squaresY < 3 || spec.squaresX > 64 || spec.squaresY > 64) {
        return Status(ErrorCode::InvalidArgument, "checker needs 3..64 squares on each axis");
    }
    if (((spec.squaresX + spec.squaresY) % 2) == 0) {
        return Status(ErrorCode::InvalidArgument,
                      "squaresX + squaresY must be odd so the board orientation is unambiguous");
    }
    if (!(spec.margin >= 0.0f && spec.margin < 0.4f)) {
        return Status(ErrorCode::InvalidArgument, "checker margin must be in [0, 0.4)");
    }
    return Status::Ok();
}

int CheckerCornerCount(const CheckerSpec& spec) {
    if (!ValidateChecker(spec).ok()) {
        return 0;
    }
    return (spec.squaresX - 1) * (spec.squaresY - 1);
}

Result<std::vector<Vector2>> CheckerCornerUVs(const CheckerSpec& spec) {
    const Status status = ValidateChecker(spec);
    if (!status.ok()) {
        return status;
    }
    const double span = 1.0 - 2.0 * spec.margin;
    std::vector<Vector2> uvs;
    uvs.reserve(static_cast<std::size_t>(CheckerCornerCount(spec)));
    for (int j = 0; j < spec.squaresY - 1; ++j) {
        for (int i = 0; i < spec.squaresX - 1; ++i) {
            const double u = spec.margin + (i + 1) * span / spec.squaresX;
            const double fromTop = spec.margin + (j + 1) * span / spec.squaresY;
            uvs.emplace_back(static_cast<float>(u), static_cast<float>(1.0 - fromTop));
        }
    }
    return uvs;
}

Result<std::vector<std::uint8_t>> RenderChecker(const CheckerSpec& spec, int width, int height, bool bottomRowFirst) {
    const Status status = ValidateChecker(spec);
    if (!status.ok()) {
        return status;
    }
    if (width < 8 || height < 8 || width > 16384 || height > 16384) {
        return Status(ErrorCode::InvalidArgument, "checker image size must be 8..16384 pixels");
    }
    const double span = 1.0 - 2.0 * spec.margin;
    std::vector<std::uint8_t> pixels(static_cast<std::size_t>(width) * static_cast<std::size_t>(height), 255);
    for (int rowTop = 0; rowTop < height; ++rowTop) {
        const double t = (rowTop + 0.5) / height;
        const int outRow = bottomRowFirst ? (height - 1 - rowTop) : rowTop;
        std::uint8_t* row = pixels.data() + static_cast<std::size_t>(outRow) * static_cast<std::size_t>(width);
        if (t < spec.margin || t > 1.0 - spec.margin) {
            continue;
        }
        const int iy = std::clamp(static_cast<int>(std::floor((t - spec.margin) / span * spec.squaresY)), 0,
                                  spec.squaresY - 1);
        for (int x = 0; x < width; ++x) {
            const double u = (x + 0.5) / width;
            if (u < spec.margin || u > 1.0 - spec.margin) {
                continue;
            }
            const int ix = std::clamp(static_cast<int>(std::floor((u - spec.margin) / span * spec.squaresX)), 0,
                                      spec.squaresX - 1);
            if (((ix + iy) % 2) == 0) {
                row[x] = 0;
            }
        }
    }
    return pixels;
}

Result<Detection> DetectChecker(const std::uint8_t* gray, int width, int height, int stride, const CheckerSpec& spec,
                                const std::vector<Vector2>* predicted, const DetectOptions& options) {
    const Status specStatus = ValidateChecker(spec);
    if (!specStatus.ok()) {
        return specStatus;
    }
    if (gray == nullptr || width < 16 || height < 16 || stride < width) {
        return Status(ErrorCode::InvalidArgument, "invalid camera image");
    }
    const int cw = spec.squaresX - 1;
    const int ch = spec.squaresY - 1;
    const int count = cw * ch;
    if (predicted != nullptr && static_cast<int>(predicted->size()) != count) {
        return Status(ErrorCode::InvalidArgument, "predicted corner count does not match the checker");
    }

    try {
        const cv::Mat image(height, width, CV_8UC1, const_cast<std::uint8_t*>(gray), static_cast<std::size_t>(stride));
        Detection detection;
        detection.corners.assign(static_cast<std::size_t>(count), Vector2(0.0f, 0.0f));
        detection.valid.assign(static_cast<std::size_t>(count), 0);

        // --- Whole-board path ---
        std::vector<cv::Point2f> found;
        const bool boardFound = cv::findChessboardCornersSB(
            image, cv::Size(cw, ch), found, cv::CALIB_CB_EXHAUSTIVE | cv::CALIB_CB_ACCURACY | cv::CALIB_CB_NORMALIZE_IMAGE);
        if (boardFound && static_cast<int>(found.size()) == count) {
            // OpenCV orders rows of cw corners but may start at either end of the board. Pick the
            // ordering that is not mirrored (camera sees the front of the surface) and whose
            // top-left square is dark.
            double bestScore = -std::numeric_limits<double>::infinity();
            int bestCandidate = -1;
            for (int candidate = 0; candidate < 4; ++candidate) {
                const bool flipI = (candidate & 1) != 0;
                const bool flipJ = (candidate & 2) != 0;
                const auto at = [&](int i, int j) {
                    const int si = flipI ? (cw - 1 - i) : i;
                    const int sj = flipJ ? (ch - 1 - j) : j;
                    const cv::Point2f& p = found[static_cast<std::size_t>(sj * cw + si)];
                    return Vector2(p.x, p.y);
                };
                const Vector2 c00 = at(0, 0);
                const Vector2 alongI = at(1, 0) - c00;
                const Vector2 alongJ = at(0, 1) - c00;
                const double cross = static_cast<double>(alongI.x) * alongJ.y - static_cast<double>(alongI.y) * alongJ.x;
                if (cross <= 0.0) {
                    continue;
                }
                const double spacing = std::min(std::hypot(alongI.x, alongI.y), std::hypot(alongJ.x, alongJ.y));
                const int radius = std::max(1, static_cast<int>(spacing * 0.15));
                const double dark = SamplePatch(image, c00 - alongI * 0.5f - alongJ * 0.5f, radius);
                const double brightA = SamplePatch(image, c00 + alongI * 0.5f - alongJ * 0.5f, radius);
                const double brightB = SamplePatch(image, c00 - alongI * 0.5f + alongJ * 0.5f, radius);
                double score = 0.0;
                int terms = 0;
                if (dark >= 0.0 && brightA >= 0.0) {
                    score += brightA - dark;
                    ++terms;
                }
                if (dark >= 0.0 && brightB >= 0.0) {
                    score += brightB - dark;
                    ++terms;
                }
                if (terms == 0) {
                    continue;
                }
                score /= terms;
                if (score > bestScore) {
                    bestScore = score;
                    bestCandidate = candidate;
                }
            }
            if (bestCandidate >= 0 && bestScore > 0.0) {
                const bool flipI = (bestCandidate & 1) != 0;
                const bool flipJ = (bestCandidate & 2) != 0;
                for (int j = 0; j < ch; ++j) {
                    for (int i = 0; i < cw; ++i) {
                        const int si = flipI ? (cw - 1 - i) : i;
                        const int sj = flipJ ? (ch - 1 - j) : j;
                        const cv::Point2f& p = found[static_cast<std::size_t>(sj * cw + si)];
                        detection.corners[static_cast<std::size_t>(j * cw + i)] = Vector2(p.x, p.y);
                        detection.valid[static_cast<std::size_t>(j * cw + i)] = 1;
                    }
                }
                detection.validCount = count;
                detection.wholeBoard = true;
                return detection;
            }
        }

        // --- Per-corner path around predictions (partial occlusion) ---
        if (predicted == nullptr) {
            return Status(ErrorCode::NotFound, "checkerboard not found in the camera image");
        }
        const std::vector<Vector2>& pred = *predicted;
        for (int j = 0; j < ch; ++j) {
            for (int i = 0; i < cw; ++i) {
                const int k = j * cw + i;
                const Vector2 p = pred[static_cast<std::size_t>(k)];
                if (!IsFinite(p)) {
                    continue;
                }
                const Vector2 alongI = (i + 1 < cw) ? pred[static_cast<std::size_t>(k + 1)] - p
                                                    : p - pred[static_cast<std::size_t>(k - 1)];
                const Vector2 alongJ = (j + 1 < ch) ? pred[static_cast<std::size_t>(k + cw)] - p
                                                    : p - pred[static_cast<std::size_t>(k - cw)];
                const double spacing = std::min(std::hypot(alongI.x, alongI.y), std::hypot(alongJ.x, alongJ.y));
                const int half = std::clamp(static_cast<int>(spacing * 0.3), 3, 25);
                if (p.x < half + 1 || p.y < half + 1 || p.x > width - half - 2 || p.y > height - half - 2) {
                    continue;
                }
                std::vector<cv::Point2f> refined = {cv::Point2f(p.x, p.y)};
                cv::cornerSubPix(image, refined, cv::Size(half, half), cv::Size(-1, -1),
                                 cv::TermCriteria(cv::TermCriteria::EPS + cv::TermCriteria::COUNT, 40, 0.01));
                const Vector2 q(refined[0].x, refined[0].y);
                const Vector2 moved = q - p;
                if (!IsFinite(q) || std::hypot(moved.x, moved.y) > half) {
                    continue;
                }
                // Saddle check: the squares up-left/down-right share a colour, up-right/down-left the
                // other; up-left (square (i, j)) is dark when i + j is even.
                const int radius = std::max(1, static_cast<int>(spacing * 0.12));
                const double ul = SamplePatch(image, q - alongI * 0.35f - alongJ * 0.35f, radius);
                const double dr = SamplePatch(image, q + alongI * 0.35f + alongJ * 0.35f, radius);
                const double ur = SamplePatch(image, q + alongI * 0.35f - alongJ * 0.35f, radius);
                const double dl = SamplePatch(image, q - alongI * 0.35f + alongJ * 0.35f, radius);
                if (ul < 0.0 || dr < 0.0 || ur < 0.0 || dl < 0.0) {
                    continue;
                }
                const double darkPair = ((i + j) % 2 == 0) ? (ul + dr) * 0.5 : (ur + dl) * 0.5;
                const double brightPair = ((i + j) % 2 == 0) ? (ur + dl) * 0.5 : (ul + dr) * 0.5;
                const double pairSpread = std::max(std::abs(ul - dr), std::abs(ur - dl));
                if (brightPair - darkPair < options.minContrast || pairSpread > (brightPair - darkPair)) {
                    continue;
                }
                detection.corners[static_cast<std::size_t>(k)] = q;
                detection.valid[static_cast<std::size_t>(k)] = 1;
                ++detection.validCount;
            }
        }
        if (detection.validCount < 4) {
            return Status(ErrorCode::NotFound, "checkerboard not found in the camera image (too few corners near the prediction)");
        }
        return detection;
    } catch (const std::exception& ex) {
        return Status(ErrorCode::OperationFailed, ex.what());
    }
}

Result<std::vector<Vector2>> UndistortPoints(const std::vector<Vector2>& points, const CameraIntrinsics& intrinsics) {
    if (!(intrinsics.fx > 0.0f) || !(intrinsics.fy > 0.0f)) {
        return Status(ErrorCode::InvalidArgument, "intrinsics need positive fx and fy");
    }
    bool anyDistortion = false;
    for (float k : intrinsics.distortion) {
        if (k != 0.0f) {
            anyDistortion = true;
            break;
        }
    }
    if (!anyDistortion || points.empty()) {
        return points;
    }
    try {
        const cv::Matx33d k(intrinsics.fx, 0.0, intrinsics.cx, 0.0, intrinsics.fy, intrinsics.cy, 0.0, 0.0, 1.0);
        const std::vector<cv::Point2f> src = ToCv(points);
        std::vector<cv::Point2f> dst;
        if (intrinsics.model == LensModel::Fisheye) {
            std::vector<double> d(4, 0.0);
            for (std::size_t i = 0; i < 4 && i < intrinsics.distortion.size(); ++i) {
                d[i] = intrinsics.distortion[i];
            }
            cv::fisheye::undistortPoints(src, dst, k, d, cv::noArray(), k);
        } else {
            // OpenCV accepts 4, 5, 8, 12 or 14 coefficients: pad with zeros to the next valid count.
            const std::size_t given = std::min<std::size_t>(intrinsics.distortion.size(), 14);
            std::size_t size = 4;
            const std::size_t sizes[5] = {4, 5, 8, 12, 14};
            for (std::size_t s : sizes) {
                if (s >= given) {
                    size = s;
                    break;
                }
            }
            std::vector<double> d(size, 0.0);
            for (std::size_t i = 0; i < given; ++i) {
                d[i] = intrinsics.distortion[i];
            }
            cv::undistortPoints(src, dst, k, d, cv::noArray(), k);
        }
        std::vector<Vector2> out;
        out.reserve(dst.size());
        for (const cv::Point2f& p : dst) {
            out.emplace_back(p.x, p.y);
        }
        return out;
    } catch (const std::exception& ex) {
        return Status(ErrorCode::OperationFailed, ex.what());
    }
}

Vector2 EvaluateCorrection(const CorrectionGrid& grid, float u, float v) {
    if (grid.columns < 2 || grid.rows < 2 ||
        grid.offsets.size() != static_cast<std::size_t>(grid.columns) * static_cast<std::size_t>(grid.rows)) {
        return Vector2(0.0f, 0.0f);
    }
    int nodes[4];
    double weights[4];
    BilinearWeights(grid.columns, grid.rows, u, v, nodes, weights);
    double x = 0.0;
    double y = 0.0;
    for (int a = 0; a < 4; ++a) {
        x += weights[a] * grid.offsets[static_cast<std::size_t>(nodes[a])].x;
        y += weights[a] * grid.offsets[static_cast<std::size_t>(nodes[a])].y;
    }
    return Vector2(static_cast<float>(x), static_cast<float>(y));
}

CorrectionGrid ResampleCorrection(const CorrectionGrid& grid, int columns, int rows) {
    CorrectionGrid out;
    if (columns < 2 || rows < 2) {
        return out;
    }
    out.columns = columns;
    out.rows = rows;
    out.offsets.resize(static_cast<std::size_t>(columns) * static_cast<std::size_t>(rows));
    for (int r = 0; r < rows; ++r) {
        for (int c = 0; c < columns; ++c) {
            const float u = static_cast<float>(c) / static_cast<float>(columns - 1);
            const float v = static_cast<float>(r) / static_cast<float>(rows - 1);
            out.offsets[static_cast<std::size_t>(r * columns + c)] = EvaluateCorrection(grid, u, v);
        }
    }
    return out;
}

Vector2 EvaluateWarp(const SkinWarp& warp, float u, float v) {
    const QuadMap map(warp.corners);
    return map.Map(u, v) + EvaluateCorrection(warp.correction, u, v);
}

Result<Score> Measure(const std::vector<Vector2>& uvs, const std::vector<Vector2>& camera,
                      const std::vector<std::uint8_t>& valid, const RefineOptions& options) {
    try {
        Result<ValidSet> set = CollectValid(uvs, camera, valid);
        if (!set.ok()) {
            return set.status();
        }
        Result<cv::Mat> target = TargetHomography(set.value(), options);
        if (!target.ok()) {
            return target.status();
        }
        return ComputeScore(set.value(), target.value());
    } catch (const std::exception& ex) {
        return Status(ErrorCode::OperationFailed, ex.what());
    }
}

Result<RefineResult> Refine(const SkinWarp& current, const std::vector<Vector2>& uvs, const std::vector<Vector2>& camera,
                            const std::vector<std::uint8_t>& valid, const RefineOptions& options) {
    const Status optionStatus = ValidateOptions(options);
    if (!optionStatus.ok()) {
        return optionStatus;
    }
    try {
        Result<ValidSet> collected = CollectValid(uvs, camera, valid);
        if (!collected.ok()) {
            return collected.status();
        }
        const ValidSet& set = collected.value();
        if (set.uv.size() < 8) {
            return Status(ErrorCode::NotFound, "need at least 8 valid corners to refine");
        }
        Result<cv::Mat> target = TargetHomography(set, options);
        if (!target.ok()) {
            return target.status();
        }

        RefineResult result;
        result.before = ComputeScore(set, target.value());

        // Current projector positions of the corners, and the camera -> projector map they imply.
        const QuadMap quad(current.corners);
        std::vector<Vector2> projector;
        projector.reserve(set.uv.size());
        for (const Vector2& uv : set.uv) {
            projector.push_back(quad.Map(uv.x, uv.y) + EvaluateCorrection(current.correction, uv.x, uv.y));
        }
        const cv::Mat cameraToProjector = cv::findHomography(ToCv(set.camera), ToCv(projector), 0);
        if (cameraToProjector.empty()) {
            return Status(ErrorCode::OperationFailed, "could not relate camera and projector from the corners");
        }

        // Newton-like step: move each corner's projector position by the projector-space image of
        // its camera-space error, damped.
        std::vector<Vector2> increments;
        increments.reserve(set.uv.size());
        for (std::size_t k = 0; k < set.uv.size(); ++k) {
            const Vector2 ideal = ApplyHomography(target.value(), set.uv[k]);
            const Vector2 step =
                ApplyHomography(cameraToProjector, ideal) - ApplyHomography(cameraToProjector, set.camera[k]);
            increments.push_back(step * options.damping);
        }

        Result<CorrectionGrid> fitted = FitGrid(set.uv, increments, options);
        if (!fitted.ok()) {
            return fitted.status();
        }
        CorrectionGrid next = ResampleCorrection(current.correction, options.gridColumns, options.gridRows);
        const CorrectionGrid& delta = fitted.value();
        for (std::size_t i = 0; i < next.offsets.size(); ++i) {
            Vector2 change = delta.offsets[i];
            const float length = std::hypot(change.x, change.y);
            if (length > options.maxStep) {
                change = change * (options.maxStep / length);
            }
            next.offsets[i] += change;
        }
        result.correction = std::move(next);
        return result;
    } catch (const std::exception& ex) {
        return Status(ErrorCode::OperationFailed, ex.what());
    }
}

} // namespace pmsdk::AutoAdjust
