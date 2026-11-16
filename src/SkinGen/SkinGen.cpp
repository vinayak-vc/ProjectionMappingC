#include "PMSDK/SkinGen/SkinGen.h"

#include <opencv2/imgcodecs.hpp>
#include <opencv2/imgproc.hpp>

#include <algorithm>
#include <cmath>
#include <filesystem>
#include <fstream>
#include <climits>
#include <cstdint>
#include <iterator>
#include <numeric>
#include <string>
#include <utility>
#include <vector>

namespace pmsdk::SkinGen {

namespace {

constexpr int kMaxImageSide = 16384;
constexpr int kAnalysisMaxSide = 640;
constexpr int kGrabCutMaxSide = 640;

Status Fail(ErrorCode code, std::string message) {
    return Status(code, std::move(message));
}

// ---------------------------------------------------------------------------------------
// Image <-> cv::Mat
// ---------------------------------------------------------------------------------------

Status ValidateImage(const ImageData& image) {
    if (image.width <= 0 || image.height <= 0 || image.width > kMaxImageSide || image.height > kMaxImageSide) {
        return Fail(ErrorCode::InvalidArgument, "image size must be 1.." + std::to_string(kMaxImageSide) + " pixels per side");
    }
    if (image.channels != 1 && image.channels != 3 && image.channels != 4) {
        return Fail(ErrorCode::InvalidArgument, "image must have 1, 3 or 4 channels");
    }
    const std::size_t expected = static_cast<std::size_t>(image.width) * static_cast<std::size_t>(image.height) *
                                 static_cast<std::size_t>(image.channels);
    if (image.pixels.size() != expected) {
        return Fail(ErrorCode::InvalidArgument, "pixel buffer size does not match width * height * channels");
    }
    return Status::Ok();
}

// Non-owning view over the caller's pixels.
cv::Mat View(const ImageData& image) {
    return cv::Mat(image.height, image.width, CV_8UC(image.channels), const_cast<std::uint8_t*>(image.pixels.data()));
}

// 8-bit RGB view of any supported image (alpha dropped, grey replicated).
cv::Mat ToRgb(const ImageData& image) {
    cv::Mat src = View(image);
    cv::Mat rgb;
    if (image.channels == 4) {
        cv::cvtColor(src, rgb, cv::COLOR_RGBA2RGB);
    } else if (image.channels == 1) {
        cv::cvtColor(src, rgb, cv::COLOR_GRAY2RGB);
    } else {
        rgb = src.clone();
    }
    return rgb;
}

Result<ImageData> FromDecoded(cv::Mat decoded, const std::uint8_t* bytes, std::size_t size) {
    if (decoded.empty()) {
        return Fail(ErrorCode::ParseError, "could not decode the image");
    }
    // Images without alpha are re-decoded as colour so that EXIF orientation (phone JPEGs) is applied.
    if (decoded.channels() != 4) {
        const std::vector<std::uint8_t> buffer(bytes, bytes + size);
        cv::Mat color = cv::imdecode(buffer, cv::IMREAD_COLOR);
        if (!color.empty()) {
            decoded = color;
        }
    }
    if (decoded.depth() == CV_16U) {
        cv::Mat eight;
        decoded.convertTo(eight, CV_8U, 1.0 / 257.0);
        decoded = eight;
    } else if (decoded.depth() != CV_8U) {
        cv::Mat eight;
        decoded.convertTo(eight, CV_8U);
        decoded = eight;
    }

    cv::Mat rgb;
    if (decoded.channels() == 4) {
        cv::cvtColor(decoded, rgb, cv::COLOR_BGRA2RGBA);
    } else if (decoded.channels() == 3) {
        cv::cvtColor(decoded, rgb, cv::COLOR_BGR2RGB);
    } else if (decoded.channels() == 1) {
        cv::cvtColor(decoded, rgb, cv::COLOR_GRAY2RGB);
    } else {
        return Fail(ErrorCode::ParseError, "unsupported channel count in the decoded image");
    }

    ImageData out;
    out.width = rgb.cols;
    out.height = rgb.rows;
    out.channels = rgb.channels();
    const std::size_t total = static_cast<std::size_t>(rgb.total()) * static_cast<std::size_t>(rgb.channels());
    out.pixels.assign(rgb.data, rgb.data + total);
    return out;
}

// ---------------------------------------------------------------------------------------
// Segmentation (all produce an 8-bit mask: 255 = object)
// ---------------------------------------------------------------------------------------

cv::Mat SegmentAlpha(const cv::Mat& rgba, std::uint8_t threshold) {
    cv::Mat alpha;
    cv::extractChannel(rgba, alpha, 3);
    cv::Mat mask;
    cv::threshold(alpha, mask, static_cast<double>(threshold), 255.0, cv::THRESH_BINARY);
    return mask;
}

bool HasRealTransparency(const cv::Mat& rgba, std::uint8_t threshold) {
    cv::Mat alpha;
    cv::extractChannel(rgba, alpha, 3);
    cv::Mat transparent;
    cv::threshold(alpha, transparent, static_cast<double>(threshold), 255.0, cv::THRESH_BINARY_INV);
    const double fraction = static_cast<double>(cv::countNonZero(transparent)) / static_cast<double>(alpha.total());
    return fraction >= 0.001 && fraction < 0.999;
}

// Median colour of a thin frame around the image.
cv::Vec3b BorderColor(const cv::Mat& rgb) {
    const int thickness = std::max(1, std::min(rgb.cols, rgb.rows) / 100);
    std::vector<int> r;
    std::vector<int> g;
    std::vector<int> b;
    for (int y = 0; y < rgb.rows; ++y) {
        const bool edgeRow = y < thickness || y >= rgb.rows - thickness;
        for (int x = 0; x < rgb.cols; ++x) {
            if (!edgeRow && x >= thickness && x < rgb.cols - thickness) {
                x = std::max(x, rgb.cols - thickness - 1);
                continue;
            }
            const cv::Vec3b p = rgb.at<cv::Vec3b>(y, x);
            r.push_back(p[0]);
            g.push_back(p[1]);
            b.push_back(p[2]);
        }
    }
    auto median = [](std::vector<int>& v) {
        std::nth_element(v.begin(), v.begin() + static_cast<std::ptrdiff_t>(v.size() / 2), v.end());
        return static_cast<std::uint8_t>(v[v.size() / 2]);
    };
    return cv::Vec3b(median(r), median(g), median(b));
}

// Per-pixel max-abs channel difference to a colour.
cv::Mat ColorDistance(const cv::Mat& rgb, const cv::Vec3b& color) {
    cv::Mat diff;
    cv::absdiff(rgb, cv::Scalar(color[0], color[1], color[2]), diff);
    std::vector<cv::Mat> planes;
    cv::split(diff, planes);
    cv::Mat result;
    cv::max(planes[0], planes[1], result);
    cv::max(result, planes[2], result);
    return result;
}

bool BorderIsUniform(const cv::Mat& rgb, float tolerance) {
    const cv::Vec3b color = BorderColor(rgb);
    const cv::Mat distance = ColorDistance(rgb, color);
    const int thickness = std::max(1, std::min(rgb.cols, rgb.rows) / 100);
    long long close = 0;
    long long total = 0;
    for (int y = 0; y < rgb.rows; ++y) {
        const bool edgeRow = y < thickness || y >= rgb.rows - thickness;
        for (int x = 0; x < rgb.cols; ++x) {
            if (!edgeRow && x >= thickness && x < rgb.cols - thickness) {
                x = std::max(x, rgb.cols - thickness - 1);
                continue;
            }
            ++total;
            if (distance.at<std::uint8_t>(y, x) <= tolerance) {
                ++close;
            }
        }
    }
    return total > 0 && static_cast<double>(close) / static_cast<double>(total) >= 0.9;
}

cv::Mat SegmentColorKey(const cv::Mat& rgb, float tolerance) {
    const cv::Vec3b color = BorderColor(rgb);
    cv::Mat distance = ColorDistance(rgb, color);
    cv::Mat mask;
    cv::threshold(distance, mask, static_cast<double>(tolerance), 255.0, cv::THRESH_BINARY);
    const cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(3, 3));
    cv::morphologyEx(mask, mask, cv::MORPH_CLOSE, kernel);
    return mask;
}

cv::Mat SegmentGrabCut(const cv::Mat& rgb, float margin, int iterations) {
    const double scale = std::min(1.0, static_cast<double>(kGrabCutMaxSide) / static_cast<double>(std::max(rgb.cols, rgb.rows)));
    cv::Mat small;
    if (scale < 1.0) {
        cv::resize(rgb, small, cv::Size(), scale, scale, cv::INTER_AREA);
    } else {
        small = rgb;
    }
    cv::Mat bgr;
    cv::cvtColor(small, bgr, cv::COLOR_RGB2BGR);

    const float m = std::clamp(margin, 0.0f, 0.45f);
    const int mx = std::max(1, static_cast<int>(std::lround(static_cast<float>(bgr.cols) * m)));
    const int my = std::max(1, static_cast<int>(std::lround(static_cast<float>(bgr.rows) * m)));
    const cv::Rect rect(mx, my, std::max(1, bgr.cols - 2 * mx), std::max(1, bgr.rows - 2 * my));

    cv::Mat gcMask(bgr.size(), CV_8UC1, cv::Scalar(cv::GC_BGD));
    cv::Mat bgModel;
    cv::Mat fgModel;
    cv::grabCut(bgr, gcMask, rect, bgModel, fgModel, std::max(1, iterations), cv::GC_INIT_WITH_RECT);

    cv::Mat foreground = (gcMask == cv::GC_FGD) | (gcMask == cv::GC_PR_FGD);
    if (scale < 1.0) {
        cv::Mat up;
        cv::resize(foreground, up, rgb.size(), 0.0, 0.0, cv::INTER_LINEAR);
        cv::threshold(up, foreground, 127.0, 255.0, cv::THRESH_BINARY);
    }
    return foreground;
}

// ---------------------------------------------------------------------------------------
// Parts
// ---------------------------------------------------------------------------------------

struct Region {
    cv::Mat mask;     // analysis-scale mask
    int area = 0;
};

int MinAreaFor(double total, float fraction, int floorValue) {
    return std::max(floorValue, static_cast<int>(std::lround(total * static_cast<double>(std::max(0.0f, fraction)))));
}

// Removes connected pieces smaller than minArea.
cv::Mat RemoveSpecks(const cv::Mat& mask, int minArea) {
    cv::Mat labels;
    cv::Mat stats;
    cv::Mat centroids;
    const int count = cv::connectedComponentsWithStats(mask, labels, stats, centroids, 8, CV_32S);
    std::vector<std::uint8_t> keep(static_cast<std::size_t>(count), 0);
    for (int i = 1; i < count; ++i) {
        keep[static_cast<std::size_t>(i)] = stats.at<int>(i, cv::CC_STAT_AREA) >= minArea ? 255 : 0;
    }
    cv::Mat out(mask.size(), CV_8UC1, cv::Scalar(0));
    for (int y = 0; y < labels.rows; ++y) {
        const int* l = labels.ptr<int>(y);
        std::uint8_t* o = out.ptr<std::uint8_t>(y);
        for (int x = 0; x < labels.cols; ++x) {
            o[x] = keep[static_cast<std::size_t>(l[x])];
        }
    }
    return out;
}

// One region per connected piece, at the mask's own scale.
std::vector<Region> ConnectedRegions(const cv::Mat& mask, int minArea) {
    cv::Mat labels;
    cv::Mat stats;
    cv::Mat centroids;
    const int count = cv::connectedComponentsWithStats(mask, labels, stats, centroids, 8, CV_32S);
    std::vector<Region> regions;
    for (int i = 1; i < count; ++i) {
        const int area = stats.at<int>(i, cv::CC_STAT_AREA);
        if (area < minArea) {
            continue;
        }
        Region r;
        r.mask = (labels == i);
        r.area = area;
        regions.push_back(std::move(r));
    }
    return regions;
}

bool RegionsTouch(const Region& a, const Region& b, const cv::Mat& kernel) {
    const cv::Rect ra = cv::boundingRect(a.mask);
    const cv::Rect rb = cv::boundingRect(b.mask);
    const cv::Rect inflated(ra.x - 4, ra.y - 4, ra.width + 8, ra.height + 8);
    const cv::Rect overlap = inflated & rb;
    if (overlap.area() <= 0) {
        return false;
    }
    cv::Mat grown;
    cv::dilate(a.mask(overlap), grown, kernel);
    cv::Mat hit;
    cv::bitwise_and(grown, b.mask(overlap), hit);
    return cv::countNonZero(hit) > 0;
}

// Thick body vs thin appendage split, then touching small regions merge.
std::vector<Region> SplitRegions(const cv::Mat& small, const Options& options) {
    const double total = static_cast<double>(cv::countNonZero(small));
    const int minArea = MinAreaFor(total, options.minPartAreaFraction, 4);
    const cv::Rect box = cv::boundingRect(small);
    const int diameter = std::max(3, static_cast<int>(std::lround(static_cast<double>(std::min(box.width, box.height)) *
                                                                  static_cast<double>(std::max(0.001f, options.thinPartFraction)))));
    const cv::Mat kernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(diameter, diameter));

    cv::Mat body;
    cv::morphologyEx(small, body, cv::MORPH_OPEN, kernel);

    std::vector<Region> regions;
    cv::Mat used(small.size(), CV_8UC1, cv::Scalar(0));
    for (Region& piece : ConnectedRegions(body, minArea)) {
        cv::Mat grown;
        cv::dilate(piece.mask, grown, kernel);
        cv::bitwise_and(grown, small, grown);
        cv::bitwise_or(used, grown, used);
        Region r;
        r.mask = grown;
        r.area = cv::countNonZero(grown);
        regions.push_back(std::move(r));
    }
    cv::Mat rest;
    cv::bitwise_and(small, ~used, rest);
    for (Region& piece : ConnectedRegions(rest, minArea)) {
        regions.push_back(std::move(piece));
    }

    // Merge small regions that touch each other (trident head + prongs + shaft -> one part).
    const double largeThreshold = total * static_cast<double>(options.appendageMaxFraction);
    std::vector<std::size_t> smallIndex;
    for (std::size_t i = 0; i < regions.size(); ++i) {
        if (static_cast<double>(regions[i].area) < largeThreshold) {
            smallIndex.push_back(i);
        }
    }
    std::vector<std::size_t> parent(regions.size());
    std::iota(parent.begin(), parent.end(), static_cast<std::size_t>(0));
    auto find = [&parent](std::size_t x) {
        while (parent[x] != x) {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }
        return x;
    };
    const cv::Mat touchKernel = cv::getStructuringElement(cv::MORPH_ELLIPSE, cv::Size(7, 7));
    for (std::size_t i = 0; i < smallIndex.size(); ++i) {
        for (std::size_t j = i + 1; j < smallIndex.size(); ++j) {
            const std::size_t a = smallIndex[i];
            const std::size_t b = smallIndex[j];
            if (find(a) != find(b) && RegionsTouch(regions[a], regions[b], touchKernel)) {
                parent[find(a)] = find(b);
            }
        }
    }
    std::vector<Region> merged;
    std::vector<int> slot(regions.size(), -1);
    for (std::size_t i = 0; i < regions.size(); ++i) {
        const std::size_t root = find(i);
        if (slot[root] < 0) {
            slot[root] = static_cast<int>(merged.size());
            Region r;
            r.mask = cv::Mat(small.size(), CV_8UC1, cv::Scalar(0));
            merged.push_back(std::move(r));
        }
        cv::bitwise_or(merged[static_cast<std::size_t>(slot[root])].mask, regions[i].mask,
                       merged[static_cast<std::size_t>(slot[root])].mask);
    }
    for (Region& r : merged) {
        r.area = cv::countNonZero(r.mask);
    }
    return merged;
}

// Full-resolution part masks, largest first, each pixel assigned to exactly one part.
std::vector<cv::Mat> BuildPartMasks(const cv::Mat& objectMask, const Options& options) {
    const double rawTotal = static_cast<double>(cv::countNonZero(objectMask));
    const int minArea = MinAreaFor(rawTotal, options.minPartAreaFraction, 16);
    const cv::Mat clean = RemoveSpecks(objectMask, minArea);
    if (cv::countNonZero(clean) == 0) {
        return {};
    }

    std::vector<cv::Mat> parts;
    if (!options.splitParts) {
        std::vector<Region> pieces = ConnectedRegions(clean, 1);
        std::sort(pieces.begin(), pieces.end(), [](const Region& a, const Region& b) { return a.area > b.area; });
        for (Region& p : pieces) {
            parts.push_back(std::move(p.mask));
        }
    } else {
        const double scale = std::min(1.0, static_cast<double>(kAnalysisMaxSide) / static_cast<double>(std::max(clean.cols, clean.rows)));
        cv::Mat small;
        if (scale < 1.0) {
            cv::resize(clean, small, cv::Size(), scale, scale, cv::INTER_AREA);
            cv::threshold(small, small, 127.0, 255.0, cv::THRESH_BINARY);
        } else {
            small = clean;
        }
        std::vector<Region> regions = SplitRegions(small, options);
        std::sort(regions.begin(), regions.end(), [](const Region& a, const Region& b) { return a.area > b.area; });
        if (options.maxParts > 0 && regions.size() > static_cast<std::size_t>(options.maxParts)) {
            regions.resize(static_cast<std::size_t>(options.maxParts));
        }
        if (regions.empty()) {
            return {};
        }

        // Small parts claim their pixels first so thin appendages keep their full width.
        const cv::Mat grow = cv::getStructuringElement(cv::MORPH_RECT, cv::Size(3, 3));
        cv::Mat assigned(clean.size(), CV_8UC1, cv::Scalar(0));
        std::vector<cv::Mat> byAscending;
        for (std::size_t i = regions.size(); i-- > 0;) {
            cv::Mat grown;
            cv::dilate(regions[i].mask, grown, grow);
            cv::Mat up;
            if (scale < 1.0) {
                cv::resize(grown, up, clean.size(), 0.0, 0.0, cv::INTER_NEAREST);
            } else {
                up = grown;
            }
            cv::Mat part;
            cv::bitwise_and(up, clean, part);
            cv::bitwise_and(part, ~assigned, part);
            cv::bitwise_or(assigned, part, assigned);
            byAscending.push_back(std::move(part));
        }
        // Pixels no region claimed (thin fringes lost by downscaling) go to the largest part.
        cv::Mat leftover;
        cv::bitwise_and(clean, ~assigned, leftover);
        cv::bitwise_or(byAscending.back(), leftover, byAscending.back());
        for (std::size_t i = byAscending.size(); i-- > 0;) {
            parts.push_back(std::move(byAscending[i]));
        }
    }

    // Drop empties and order by area, largest first.
    std::vector<std::pair<int, cv::Mat>> sized;
    for (cv::Mat& p : parts) {
        const int area = cv::countNonZero(p);
        if (area > 0) {
            sized.emplace_back(area, std::move(p));
        }
    }
    std::stable_sort(sized.begin(), sized.end(), [](const auto& a, const auto& b) { return a.first > b.first; });
    std::vector<cv::Mat> result;
    for (auto& s : sized) {
        result.push_back(std::move(s.second));
    }
    return result;
}

// ---------------------------------------------------------------------------------------
// Outlines
// ---------------------------------------------------------------------------------------

std::vector<cv::Point> SimplifyToLimit(const std::vector<cv::Point>& contour, int limit) {
    const double perimeter = cv::arcLength(contour, true);
    const double minEpsilon = std::max(1.0, perimeter * 0.001);
    std::vector<cv::Point> approx;
    cv::approxPolyDP(contour, approx, minEpsilon, true);
    if (static_cast<int>(approx.size()) <= limit) {
        return approx;
    }
    double lo = minEpsilon;
    double hi = std::max(minEpsilon * 2.0, perimeter * 0.1);
    std::vector<cv::Point> best;
    cv::approxPolyDP(contour, best, hi, true);
    for (int i = 0; i < 24; ++i) {
        const double mid = 0.5 * (lo + hi);
        std::vector<cv::Point> candidate;
        cv::approxPolyDP(contour, candidate, mid, true);
        if (static_cast<int>(candidate.size()) <= limit) {
            hi = mid;
            best = std::move(candidate);
        } else {
            lo = mid;
        }
    }
    return best;
}

double SignedArea(const std::vector<Math::Vector2>& pts) {
    double sum = 0.0;
    for (std::size_t i = 0; i < pts.size(); ++i) {
        const Math::Vector2& a = pts[i];
        const Math::Vector2& b = pts[(i + 1) % pts.size()];
        sum += static_cast<double>(a.x) * static_cast<double>(b.y) - static_cast<double>(b.x) * static_cast<double>(a.y);
    }
    return 0.5 * sum;
}


struct OutlineSet {
    std::vector<Contour> contours;
    std::vector<Math::Vector2> polygon;
};

// Traces the outlines of `crop` (a part's cropped mask) and fills holes smaller than the
// configured fraction directly in `crop`. Coordinates are normalised to the source image.
OutlineSet BuildOutlines(cv::Mat& crop, int offsetX, int offsetY, int imageW, int imageH, const Options& options) {
    cv::Mat padded;
    cv::copyMakeBorder(crop, padded, 1, 1, 1, 1, cv::BORDER_CONSTANT, cv::Scalar(0));

    std::vector<std::vector<cv::Point>> raw;
    std::vector<cv::Vec4i> hierarchy;
    cv::findContours(padded.clone(), raw, hierarchy, cv::RETR_CCOMP, cv::CHAIN_APPROX_NONE);

    const double partArea = static_cast<double>(cv::countNonZero(padded));
    const double holeLimit = partArea * static_cast<double>(std::max(0.0f, options.minHoleAreaFraction));
    const double pieceLimit = partArea * 0.001;

    struct Candidate {
        std::size_t index;
        double area;
        bool isHole;
    };
    std::vector<Candidate> kept;
    for (std::size_t i = 0; i < raw.size(); ++i) {
        const bool isHole = hierarchy[i][3] != -1;
        const double area = std::abs(cv::contourArea(raw[i]));
        if (isHole && area < holeLimit) {
            cv::drawContours(padded, raw, static_cast<int>(i), cv::Scalar(255), cv::FILLED);
            continue;
        }
        if (raw[i].size() < 3) {
            continue;
        }
        kept.push_back({i, area, isHole});
    }
    std::stable_sort(kept.begin(), kept.end(), [](const Candidate& a, const Candidate& b) {
        if (a.isHole != b.isHole) {
            return !a.isHole;
        }
        return a.area > b.area;
    });
    crop = padded(cv::Rect(1, 1, crop.cols, crop.rows)).clone();

    const int limit = std::clamp(options.maxPolygonPoints, 3, 4096);
    OutlineSet set;
    bool first = true;
    for (const Candidate& c : kept) {
        if (!c.isHole && !first && c.area < pieceLimit) {
            continue;
        }
        std::vector<cv::Point> simple = SimplifyToLimit(raw[c.index], limit);
        if (simple.size() < 3) {
            continue;
        }
        Contour contour;
        contour.isHole = c.isHole;
        contour.points.reserve(simple.size());
        for (const cv::Point& p : simple) {
            const float x = (static_cast<float>(p.x - 1 + offsetX) + 0.5f) / static_cast<float>(imageW);
            float y = (static_cast<float>(p.y - 1 + offsetY) + 0.5f) / static_cast<float>(imageH);
            if (options.yUp) {
                y = 1.0f - y;
            }
            contour.points.emplace_back(x, y);
        }
        const double signedArea = SignedArea(contour.points);
        if ((!c.isHole && signedArea < 0.0) || (c.isHole && signedArea > 0.0)) {
            std::reverse(contour.points.begin(), contour.points.end());
        }
        if (!c.isHole && first) {
            set.polygon = contour.points;
            first = false;
        }
        set.contours.push_back(std::move(contour));
    }
    return set;
}

Part BuildPart(const cv::Mat& fullMask, int id, int imageW, int imageH, const Options& options) {
    const cv::Rect box = cv::boundingRect(fullMask);
    cv::Mat crop = fullMask(box).clone();
    OutlineSet outlines = BuildOutlines(crop, box.x, box.y, imageW, imageH, options);

    Part part;
    part.id = id;
    part.maskX = box.x;
    part.maskY = box.y;
    part.maskWidth = box.width;
    part.maskHeight = box.height;
    part.mask.assign(crop.data, crop.data + static_cast<std::size_t>(crop.total()));
    part.areaFraction = static_cast<float>(cv::countNonZero(crop)) / (static_cast<float>(imageW) * static_cast<float>(imageH));
    part.polygon = std::move(outlines.polygon);
    part.contours = std::move(outlines.contours);

    const float w = static_cast<float>(imageW);
    const float h = static_cast<float>(imageH);
    const float x0 = static_cast<float>(box.x) / w;
    const float x1 = static_cast<float>(box.x + box.width) / w;
    const float y0 = static_cast<float>(box.y) / h;
    const float y1 = static_cast<float>(box.y + box.height) / h;
    const float yMin = options.yUp ? 1.0f - y1 : y0;
    const float yMax = options.yUp ? 1.0f - y0 : y1;
    part.boundsMin = Math::Vector2(x0, yMin);
    part.boundsMax = Math::Vector2(x1, yMax);

    const float bottom = options.yUp ? yMin : yMax;
    const float top = options.yUp ? yMax : yMin;
    part.quad[0] = Math::Vector2(x0, bottom);
    part.quad[1] = Math::Vector2(x1, bottom);
    part.quad[2] = Math::Vector2(x1, top);
    part.quad[3] = Math::Vector2(x0, top);
    return part;
}

Result<std::vector<std::uint8_t>> ReadFileBytes(const std::string& path) {
    try {
        const std::u8string utf8(reinterpret_cast<const char8_t*>(path.data()), path.size());
        const std::filesystem::path fsPath(utf8);
        std::ifstream file(fsPath, std::ios::binary);
        if (!file) {
            return Fail(ErrorCode::NotFound, "cannot open file: " + path);
        }
        std::vector<std::uint8_t> bytes((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
        if (bytes.empty()) {
            return Fail(ErrorCode::IoError, "file is empty: " + path);
        }
        return bytes;
    } catch (const std::exception& ex) {
        return Fail(ErrorCode::IoError, std::string("cannot read file: ") + ex.what());
    }
}

} // namespace

// ---------------------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------------------

Result<ImageData> DecodeImage(const std::uint8_t* bytes, std::size_t size) {
    if (bytes == nullptr || size == 0 || size > static_cast<std::size_t>(INT_MAX)) {
        return Fail(ErrorCode::InvalidArgument, "no image bytes supplied");
    }
    try {
        const cv::Mat buffer(1, static_cast<int>(size), CV_8UC1, const_cast<std::uint8_t*>(bytes));
        cv::Mat decoded = cv::imdecode(buffer, cv::IMREAD_UNCHANGED);
        Result<ImageData> image = FromDecoded(std::move(decoded), bytes, size);
        if (image.ok() && (image.value().width > kMaxImageSide || image.value().height > kMaxImageSide)) {
            return Fail(ErrorCode::InvalidArgument, "image is too large");
        }
        return image;
    } catch (const cv::Exception& ex) {
        return Fail(ErrorCode::ParseError, std::string("image decode failed: ") + ex.what());
    } catch (const std::bad_alloc&) {
        return Fail(ErrorCode::OutOfMemory, "out of memory while decoding the image");
    }
}

Result<ImageData> LoadImage(const std::string& path) {
    Result<std::vector<std::uint8_t>> bytes = ReadFileBytes(path);
    if (!bytes.ok()) {
        return bytes.status();
    }
    const std::vector<std::uint8_t>& data = bytes.value();
    return DecodeImage(data.data(), data.size());
}

Result<Extraction> ExtractSkins(const ImageData& image, const Options& options, const std::uint8_t* providedMask) {
    const Status valid = ValidateImage(image);
    if (!valid.ok()) {
        return valid;
    }
    if (options.maxPolygonPoints < 3 || options.maxPolygonPoints > 4096) {
        return Fail(ErrorCode::InvalidArgument, "maxPolygonPoints must be 3..4096");
    }

    try {
        SegmentationMode used = options.mode;
        cv::Mat mask;

        if (used == SegmentationMode::Auto) {
            if (image.channels == 4 && HasRealTransparency(View(image), options.alphaThreshold)) {
                used = SegmentationMode::Alpha;
            } else if (BorderIsUniform(ToRgb(image), options.colorTolerance)) {
                used = SegmentationMode::ColorKey;
            } else {
                used = SegmentationMode::GrabCut;
            }
        }

        switch (used) {
        case SegmentationMode::Alpha:
            if (image.channels != 4) {
                return Fail(ErrorCode::InvalidArgument, "Alpha mode needs an image with an alpha channel");
            }
            mask = SegmentAlpha(View(image), options.alphaThreshold);
            break;
        case SegmentationMode::ColorKey:
            mask = SegmentColorKey(ToRgb(image), options.colorTolerance);
            break;
        case SegmentationMode::GrabCut:
            mask = SegmentGrabCut(ToRgb(image), options.grabCutMargin, options.grabCutIterations);
            break;
        case SegmentationMode::ProvidedMask: {
            if (providedMask == nullptr) {
                return Fail(ErrorCode::InvalidArgument, "ProvidedMask mode needs a mask buffer");
            }
            const cv::Mat given(image.height, image.width, CV_8UC1, const_cast<std::uint8_t*>(providedMask));
            cv::threshold(given, mask, 0.0, 255.0, cv::THRESH_BINARY);
            break;
        }
        case SegmentationMode::Auto:
            return Fail(ErrorCode::Unknown, "unresolved segmentation mode");
        }

        if (cv::countNonZero(mask) == 0) {
            return Fail(ErrorCode::OperationFailed, "no object pixels were found in the image");
        }

        std::vector<cv::Mat> partMasks = BuildPartMasks(mask, options);
        if (options.maxParts > 0 && partMasks.size() > static_cast<std::size_t>(options.maxParts)) {
            partMasks.resize(static_cast<std::size_t>(options.maxParts));
        }
        if (partMasks.empty()) {
            return Fail(ErrorCode::OperationFailed, "the object is too small after removing specks");
        }

        Extraction result;
        result.imageWidth = image.width;
        result.imageHeight = image.height;
        result.usedMode = used;
        for (std::size_t i = 0; i < partMasks.size(); ++i) {
            result.parts.push_back(BuildPart(partMasks[i], static_cast<int>(i), image.width, image.height, options));
        }
        return result;
    } catch (const cv::Exception& ex) {
        return Fail(ErrorCode::OperationFailed, std::string("image processing failed: ") + ex.what());
    } catch (const std::bad_alloc&) {
        return Fail(ErrorCode::OutOfMemory, "out of memory while processing the image");
    }
}

Result<Extraction> ExtractSkinsFromFile(const std::string& path, const Options& options) {
    Result<ImageData> image = LoadImage(path);
    if (!image.ok()) {
        return image.status();
    }
    return ExtractSkins(image.value(), options, nullptr);
}

} // namespace pmsdk::SkinGen
