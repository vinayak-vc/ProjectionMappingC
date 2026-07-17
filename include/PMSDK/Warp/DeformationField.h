#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"
#include "PMSDK/Geometry/BezierPatch.h"
#include "PMSDK/Geometry/GridWarp.h"
#include "PMSDK/Geometry/PerspectiveWarp.h"
#include <memory>
#include <variant>

namespace pmsdk::Warp {

/**
 * @brief Specifies the type of deformation applied.
 */
enum class DeformationType {
    None,        /**< No deformation applied. */
    Bezier,      /**< Cubic Bezier Patch deformation. */
    Grid,        /**< Bilinear Grid Warp deformation. */
    Perspective  /**< Projective (homography) corner-pin deformation. */
};

/**
 * @brief Encapsulates an arbitrary 2D deformation field (Bezier or Grid).
 * 
 * Used within WarpNodes to apply non-linear distortions to meshes, 
 * typically to map them onto curved physical surfaces.
 */
class DeformationField {
public:
    /** @brief Constructs a new, empty DeformationField. */
    PMSDK_API DeformationField();
    
    /** @brief Destructor. */
    PMSDK_API ~DeformationField();

    /**
     * @brief Sets the active deformation mode.
     * @param type The deformation type (None, Bezier, Grid).
     */
    PMSDK_API void SetType(DeformationType type);
    
    /** @return The current deformation type. */
    PMSDK_API DeformationType GetType() const;

    /**
     * @brief Gets the underlying BezierPatch instance.
     * @return A pointer to the BezierPatch (valid if type is Bezier).
     */
    PMSDK_API Geometry::BezierPatch* GetBezierPatch();
    
    /**
     * @brief Gets the underlying GridWarp instance.
     * @return A pointer to the GridWarp (valid if type is Grid).
     */
    PMSDK_API Geometry::GridWarp* GetGridWarp();

    /**
     * @brief Gets the underlying PerspectiveWarp instance.
     * @return A pointer to the PerspectiveWarp (valid if type is Perspective).
     */
    PMSDK_API Geometry::PerspectiveWarp* GetPerspectiveWarp();

    /**
     * @brief Applies the active deformation to a base mesh.
     * 
     * The base mesh's UV coordinates (or normalized X/Y) are used as 
     * parameters to evaluate the deformation surface.
     * 
     * @param baseMesh The source mesh.
     * @return A newly allocated mesh with the applied deformation.
     */
    PMSDK_API std::unique_ptr<Geometry::Mesh> ApplyDeformation(const Geometry::Mesh& baseMesh) const;

    /**
     * @brief Applies the active deformation to a mesh in-place.
     * 
     * Avoids memory allocations by directly modifying the vertices of the provided mesh.
     * The mesh's existing UV coordinates are used to evaluate the deformation surface.
     * 
     * @param mesh The mesh to deform.
     */
    PMSDK_API void ApplyDeformationInPlace(Geometry::Mesh& mesh) const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Warp
