#pragma once

// PMOAK_API marks symbols that are part of PMSDKOakCamera.dll's public binary interface.
// PMSDKOakCamera is a small, optional native plugin that streams OAK-D colour frames for
// camera-based calibration (docs/decisions.md D-044). It is the only PMSDK binary that links
// depthai-core; ProjectionMappingSDK.dll stays camera-free.
// - Building the DLL: define PMOAK_EXPORTS (done by CMake).
// - Consuming it as a static library: define PMOAK_STATIC.
#if defined(PMOAK_STATIC)
    #define PMOAK_API
#elif defined(_WIN32)
    #if defined(PMOAK_EXPORTS)
        #define PMOAK_API __declspec(dllexport)
    #else
        #define PMOAK_API __declspec(dllimport)
    #endif
#else
    #define PMOAK_API __attribute__((visibility("default")))
#endif
