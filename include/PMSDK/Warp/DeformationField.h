#pragma once

#include "PMSDK/Core/Export.h"
#include "PMSDK/Geometry/Mesh.h"
#include "PMSDK/Geometry/BezierPatch.h"
#include "PMSDK/Geometry/GridWarp.h"
#include <memory>
#include <variant>

namespace pmsdk::Warp {

/**
 * @brief Specifies the type of deformation applied.
 */
enum class DeformationType {
    None,   /**< No deformation applied. */
    Bezier, /**< Cubic Bezier Patch deformation. */
    Grid    /**< Bilinear Grid Warp deformation. */
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
     * @brief Applies the active deformation to a base mesh.
     * 
     * The base mesh's UV coordinates (or normalized X/Y) are used as 
     * parameters to evaluate the deformation surface.
     * 
     * @param baseMesh The source mesh.
     * @return A newly allocated mesh with the applied deformation.
     */
    PMSDK_API std::unique_ptr<Geometry::Mesh> ApplyDeformation(const Geometry::Mesh& baseMesh) const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace pmsdk::Warp
