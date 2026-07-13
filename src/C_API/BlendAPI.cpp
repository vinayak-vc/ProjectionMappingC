#include "PMSDK/C_API/BlendAPI.h"
#include "PMSDK/Blend/BlendConfig.h"

using namespace pmsdk::Blend;

extern "C" {

PMSDK_API pmsdk_blendconfig_t* pmsdk_blendconfig_create(void) {
    return reinterpret_cast<pmsdk_blendconfig_t*>(new BlendConfig());
}

PMSDK_API void pmsdk_blendconfig_destroy(pmsdk_blendconfig_t* config) {
    delete reinterpret_cast<BlendConfig*>(config);
}

PMSDK_API void pmsdk_blendconfig_set_black_level(pmsdk_blendconfig_t* config, float level) {
    if (config) reinterpret_cast<BlendConfig*>(config)->SetBlackLevel(level);
}

PMSDK_API float pmsdk_blendconfig_get_black_level(const pmsdk_blendconfig_t* config) {
    if (config) return reinterpret_cast<const BlendConfig*>(config)->GetBlackLevel();
    return 0.0f;
}

PMSDK_API pmsdk_edgeblend_t* pmsdk_blendconfig_get_left_edge(pmsdk_blendconfig_t* config) {
    if (!config) return nullptr;
    return reinterpret_cast<pmsdk_edgeblend_t*>(&(reinterpret_cast<BlendConfig*>(config)->GetLeftEdge()));
}

PMSDK_API pmsdk_edgeblend_t* pmsdk_blendconfig_get_right_edge(pmsdk_blendconfig_t* config) {
    if (!config) return nullptr;
    return reinterpret_cast<pmsdk_edgeblend_t*>(&(reinterpret_cast<BlendConfig*>(config)->GetRightEdge()));
}

PMSDK_API pmsdk_edgeblend_t* pmsdk_blendconfig_get_top_edge(pmsdk_blendconfig_t* config) {
    if (!config) return nullptr;
    return reinterpret_cast<pmsdk_edgeblend_t*>(&(reinterpret_cast<BlendConfig*>(config)->GetTopEdge()));
}

PMSDK_API pmsdk_edgeblend_t* pmsdk_blendconfig_get_bottom_edge(pmsdk_blendconfig_t* config) {
    if (!config) return nullptr;
    return reinterpret_cast<pmsdk_edgeblend_t*>(&(reinterpret_cast<BlendConfig*>(config)->GetBottomEdge()));
}

PMSDK_API float pmsdk_blendconfig_evaluate(const pmsdk_blendconfig_t* config, float u, float v) {
    if (config) return reinterpret_cast<const BlendConfig*>(config)->Evaluate(u, v);
    return 1.0f;
}

PMSDK_API void pmsdk_edgeblend_set_size(pmsdk_edgeblend_t* edge, float size) {
    if (edge) reinterpret_cast<EdgeBlend*>(edge)->SetSize(size);
}

PMSDK_API float pmsdk_edgeblend_get_size(const pmsdk_edgeblend_t* edge) {
    if (edge) return reinterpret_cast<const EdgeBlend*>(edge)->GetSize();
    return 0.0f;
}

PMSDK_API void pmsdk_edgeblend_set_gamma(pmsdk_edgeblend_t* edge, float gamma) {
    if (edge) reinterpret_cast<EdgeBlend*>(edge)->SetGamma(gamma);
}

PMSDK_API float pmsdk_edgeblend_get_gamma(const pmsdk_edgeblend_t* edge) {
    if (edge) return reinterpret_cast<const EdgeBlend*>(edge)->GetGamma();
    return 1.0f;
}

PMSDK_API void pmsdk_edgeblend_set_curve_type(pmsdk_edgeblend_t* edge, pmsdk_curve_type_t type) {
    if (edge) reinterpret_cast<EdgeBlend*>(edge)->SetCurveType(static_cast<CurveType>(type));
}

PMSDK_API pmsdk_curve_type_t pmsdk_edgeblend_get_curve_type(const pmsdk_edgeblend_t* edge) {
    if (edge) return static_cast<pmsdk_curve_type_t>(reinterpret_cast<const EdgeBlend*>(edge)->GetCurveType());
    return PMSDK_CURVE_POWER;
}

}
