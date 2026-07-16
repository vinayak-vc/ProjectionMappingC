#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"
#include "PMSDK/Math/Vector2.h"
#include "PMSDK/Math/Vector3.h"
#include <memory>

namespace pmsdk::Geometry {

/**
 * @brief Perspective (projective) corner-pin deformation.
 *
 * Maps the unit UV square to an arbitrary quad via a true homography, so a
 * mesh warped onto a non-parallelogram quad foreshortens correctly. This is
 * what a projector/media-server corner pin actually does; a 2x2 bilinear grid
 * warp only shears along the diagonal and is visibly wrong on a keystoned quad.
 *
 * Corner order matches the UV corners:
 *   bottomLeft  -> uv (0,0)
 *   bottomRight -> uv (1,0)
 *   topRight    -> uv (1,1)
 *   topLeft     -> uv (0,1)
 */
class PerspectiveWarp {
public:
    PMSDK_API PerspectiveWarp();
    PMSDK_API ~PerspectiveWarp();
    PerspectiveWarp(const PerspectiveWarp&) = delete;
    PerspectiveWarp& operator=(const PerspectiveWarp&) = delete;

    /** @brief Sets the four target corners (XY used; Z carried through unchanged). */
    PMSDK_API void SetCorners(const Math::Vector2& bottomLeft, const Math::Vector2& bottomRight,
                              const Math::Vector2& topRight, const Math::Vector2& topLeft);

    /** @brief Evaluates the homography at normalized UV (u, v) in [0,1]. */
    PMSDK_API Math::Vector2 Evaluate(float u, float v) const;

    /** @brief Applies the deformation to a vertex array using each vertex's UV. */
    PMSDK_API void ApplyDeformation(Vertex* vertices, size_t count) const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Geometry
