#pragma once

// PMSDK_API marks symbols that are part of the SDK's public binary interface.
// - Building the SDK as a DLL/shared library: define PMSDK_EXPORTS (done by CMake).
// - Consuming the SDK as a static library: define PMSDK_STATIC (propagated by CMake).
#if defined(PMSDK_STATIC)
    #define PMSDK_API
#elif defined(_WIN32)
    #if defined(PMSDK_EXPORTS)
        #define PMSDK_API __declspec(dllexport)
    #else
        #define PMSDK_API __declspec(dllimport)
    #endif
#else
    #define PMSDK_API __attribute__((visibility("default")))
#endif
