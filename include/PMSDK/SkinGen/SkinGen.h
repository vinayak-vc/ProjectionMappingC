#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Core/Status.h"
#include "PMSDK/Math/Vector2.h"

#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

/// Photo -> skin extraction (D-041). Takes a picture of an object (ideally a PNG with a
/// transparent background), finds the object's pixels and returns, per detected PART of the
/// object (e.g. statue body, trident): a pixel-accurate mask, simplified outline polygons
/// (with holes), and a bounding quad. Engine-agnostic: pure data in, pure data out. A host
/// turns a part into a skin as either "quad + mask" (corner pin) or "polygon".
namespace pmsdk::SkinGen {

/// How the object's pixels are found.
enum class SegmentationMode : std::int32_t {
    /// Alpha channel if the image has real transparency, else colour key if the border is a
    /// uniform colour, else GrabCut.
    Auto = 0,
    /// Pixels with alpha above Options::alphaThreshold (requires a 4-channel image).
    Alpha = 1,
    /// Pixels that differ from the border colour by more than Options::colorTolerance.
    ColorKey = 2,
    /// OpenCV GrabCut seeded with a rectangle inset by Options::grabCutMargin.
    GrabCut = 3,
    /// A caller-supplied 8-bit mask (non-zero = object).
    ProvidedMask = 4,
};

/// Tunables. Defaults work for a typical cut-out PNG.
struct Options {
    SegmentationMode mode = SegmentationMode::Auto;

    /// Alpha mode: pixels with alpha strictly greater than this are the object (0..254).
    std::uint8_t alphaThreshold = 8;
    /// ColorKey mode: max per-channel difference (0..255) from the border colour that still
    /// counts as background.
    float colorTolerance = 28.0f;
    /// GrabCut mode: fraction of each side left out of the initial rectangle (0..0.45).
    float grabCutMargin = 0.05f;
    int grabCutIterations = 5;

    /// Parts / specks smaller than this fraction of the object area are dropped.
    float minPartAreaFraction = 0.002f;
    /// Holes smaller than this fraction of their part area are filled in.
    float minHoleAreaFraction = 0.0005f;

    /// Split the object into parts: thick body vs thin appendages, then touching small
    /// appendages are merged (statue + trident -> 2 parts). False = one part per connected piece.
    bool splitParts = true;
    /// Width of a "thin" feature as a fraction of the object's smaller bounding-box side.
    float thinPartFraction = 0.05f;
    /// Parts larger than this fraction of the object stay separate; smaller touching parts merge.
    float appendageMaxFraction = 0.12f;
    /// Keep at most this many parts (largest first). 0 = unlimited.
    int maxParts = 16;

    /// Upper bound on the points of each simplified outline (1..4096). Hosts that cap skin
    /// polygons (e.g. 128) set it to that cap.
    int maxPolygonPoints = 128;

    /// Output coordinate system. false: origin top-left, y down (image space). true: origin
    /// bottom-left, y up (what most renderers and the Unity skin raster use).
    bool yUp = false;
};

/// An outline in normalised [0,1] image coordinates (see Options::yUp).
struct Contour {
    std::vector<Math::Vector2> points;
    /// Outer outlines have positive signed area (shoelace formula in the returned coordinate
    /// system, i.e. counter-clockwise when y is drawn upward); holes have negative signed area.
    bool isHole = false;
};

/// One detected part of the object.
struct Part {
    int id = 0;
    /// Part area / whole image area.
    float areaFraction = 0.0f;

    /// Axis-aligned bounds in normalised coordinates (min <= max per axis).
    Math::Vector2 boundsMin;
    Math::Vector2 boundsMax;
    /// Bounding quad, visual order: bottom-left, bottom-right, top-right, top-left.
    Math::Vector2 quad[4];

    /// Pixel rectangle of the cropped mask inside the SOURCE image (origin top-left, always).
    int maskX = 0;
    int maskY = 0;
    int maskWidth = 0;
    int maskHeight = 0;
    /// maskWidth * maskHeight bytes, rows top-to-bottom, 255 = part, 0 = outside.
    std::vector<std::uint8_t> mask;

    /// Largest outer outline, simplified to <= Options::maxPolygonPoints. No holes.
    std::vector<Math::Vector2> polygon;
    /// All outlines (outer pieces and holes), each simplified to <= maxPolygonPoints.
    std::vector<Contour> contours;
};

/// Everything extracted from one image.
struct Extraction {
    int imageWidth = 0;
    int imageHeight = 0;
    /// The mode actually used (resolves Auto).
    SegmentationMode usedMode = SegmentationMode::Auto;
    /// Largest part first.
    std::vector<Part> parts;
};

/// An 8-bit image owned by the caller/SDK. Channel order is R,G,B(,A); rows top-to-bottom.
struct ImageData {
    int width = 0;
    int height = 0;
    /// 1 (grey), 3 (RGB) or 4 (RGBA).
    int channels = 0;
    std::vector<std::uint8_t> pixels;
};

/// Decode an image file (png, jpg/jpeg/jfif, bmp, tga, tiff, webp ...). Keeps an alpha channel.
PMSDK_API Result<ImageData> LoadImage(const std::string& path);

/// Decode an in-memory encoded image (the bytes of a png/jpg file).
PMSDK_API Result<ImageData> DecodeImage(const std::uint8_t* bytes, std::size_t size);

/// Extract the parts of the object in `image`. `providedMask` (width*height bytes, may be
/// null) is used only by SegmentationMode::ProvidedMask.
PMSDK_API Result<Extraction> ExtractSkins(const ImageData& image, const Options& options,
                                          const std::uint8_t* providedMask = nullptr);

/// Convenience: LoadImage + ExtractSkins.
PMSDK_API Result<Extraction> ExtractSkinsFromFile(const std::string& path, const Options& options);

} // namespace pmsdk::SkinGen
