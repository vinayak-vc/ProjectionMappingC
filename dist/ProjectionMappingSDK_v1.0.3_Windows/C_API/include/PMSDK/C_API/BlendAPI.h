#pragma once
#include "PMSDK/C_API/Types.h"

#ifdef __cplusplus
extern "C" {
#endif

PMSDK_API pmsdk_blendconfig_t* pmsdk_blendconfig_create(void);
PMSDK_API void pmsdk_blendconfig_destroy(pmsdk_blendconfig_t* config);

PMSDK_API void pmsdk_blendconfig_set_black_level(pmsdk_blendconfig_t* config, float level);
PMSDK_API float pmsdk_blendconfig_get_black_level(const pmsdk_blendconfig_t* config);

PMSDK_API pmsdk_edgeblend_t* pmsdk_blendconfig_get_left_edge(pmsdk_blendconfig_t* config);
PMSDK_API pmsdk_edgeblend_t* pmsdk_blendconfig_get_right_edge(pmsdk_blendconfig_t* config);
PMSDK_API pmsdk_edgeblend_t* pmsdk_blendconfig_get_top_edge(pmsdk_blendconfig_t* config);
PMSDK_API pmsdk_edgeblend_t* pmsdk_blendconfig_get_bottom_edge(pmsdk_blendconfig_t* config);

PMSDK_API float pmsdk_blendconfig_evaluate(const pmsdk_blendconfig_t* config, float u, float v);

PMSDK_API void pmsdk_edgeblend_set_size(pmsdk_edgeblend_t* edge, float size);
PMSDK_API float pmsdk_edgeblend_get_size(const pmsdk_edgeblend_t* edge);

PMSDK_API void pmsdk_edgeblend_set_gamma(pmsdk_edgeblend_t* edge, float gamma);
PMSDK_API float pmsdk_edgeblend_get_gamma(const pmsdk_edgeblend_t* edge);

PMSDK_API void pmsdk_edgeblend_set_curve_type(pmsdk_edgeblend_t* edge, pmsdk_curve_type_t type);
PMSDK_API pmsdk_curve_type_t pmsdk_edgeblend_get_curve_type(const pmsdk_edgeblend_t* edge);

#ifdef __cplusplus
}
#endif
