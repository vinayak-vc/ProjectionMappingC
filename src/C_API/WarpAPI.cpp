#include "PMSDK/C_API/WarpAPI.h"
#include "PMSDK/Warp/Projector.h"
#include "PMSDK/Warp/WarpNode.h"
#include "PMSDK/Geometry/Mesh.h"
#include <memory>

using namespace pmsdk;
using namespace pmsdk::Warp;
using namespace pmsdk::Geometry;

struct C_Projector {
    std::shared_ptr<Projector> ptr;
};

struct C_WarpNode {
    std::shared_ptr<WarpNode> ptr;
};

extern "C" {

pmsdk_projector_t* pmsdk_projector_create(void) {
    try {
        auto* wrapper = new C_Projector();
        wrapper->ptr = std::make_shared<Projector>();
        return reinterpret_cast<pmsdk_projector_t*>(wrapper);
    } catch (...) {
        return nullptr;
    }
}

void pmsdk_projector_destroy(pmsdk_projector_t* projector) {
    if (projector) {
        delete reinterpret_cast<C_Projector*>(projector);
    }
}

pmsdk_status_t pmsdk_projector_set_aspect_ratio(pmsdk_projector_t* projector, float aspect) {
    if (!projector) return PMSDK_ERROR_INVALID_ARGUMENT;
    reinterpret_cast<C_Projector*>(projector)->ptr->SetAspectRatio(aspect);
    return PMSDK_SUCCESS;
}

pmsdk_status_t pmsdk_projector_set_throw_ratio(pmsdk_projector_t* projector, float ratio) {
    if (!projector) return PMSDK_ERROR_INVALID_ARGUMENT;
    reinterpret_cast<C_Projector*>(projector)->ptr->SetThrowRatio(ratio);
    return PMSDK_SUCCESS;
}


pmsdk_warpnode_t* pmsdk_warpnode_create(void) {
    try {
        auto* wrapper = new C_WarpNode();
        wrapper->ptr = std::make_shared<WarpNode>();
        return reinterpret_cast<pmsdk_warpnode_t*>(wrapper);
    } catch (...) {
        return nullptr;
    }
}

void pmsdk_warpnode_destroy(pmsdk_warpnode_t* node) {
    if (node) {
        delete reinterpret_cast<C_WarpNode*>(node);
    }
}

pmsdk_status_t pmsdk_warpnode_add_child(pmsdk_warpnode_t* parent, pmsdk_warpnode_t* child) {
    if (!parent || !child) return PMSDK_ERROR_INVALID_ARGUMENT;
    try {
        auto* parentWrapper = reinterpret_cast<C_WarpNode*>(parent);
        auto* childWrapper = reinterpret_cast<C_WarpNode*>(child);
        parentWrapper->ptr->AddChild(childWrapper->ptr);
        return PMSDK_SUCCESS;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

pmsdk_status_t pmsdk_warpnode_process_mesh(pmsdk_warpnode_t* node, const pmsdk_mesh_t* input_mesh, pmsdk_mesh_t* output_mesh) {
    if (!node || !input_mesh || !output_mesh) return PMSDK_ERROR_INVALID_ARGUMENT;
    try {
        auto* nodeWrapper = reinterpret_cast<C_WarpNode*>(node);
        const auto* cpp_input = reinterpret_cast<const Mesh*>(input_mesh);
        auto* cpp_output = reinterpret_cast<Mesh*>(output_mesh);
        
        auto processed = nodeWrapper->ptr->ProcessMesh(*cpp_input);
        if (processed) {
            cpp_output->SetVertices(processed->GetVertices());
            cpp_output->SetIndices(processed->GetIndices());
        }
        return PMSDK_SUCCESS;
    } catch (...) {
        return PMSDK_ERROR_UNKNOWN;
    }
}

} // extern "C"
