#pragma once

// HOLOTRACK_API marks symbols that are part of HoloTrackSDK's public binary interface.
// - Building the SDK as a DLL/shared library: define HOLOTRACK_EXPORTS (done by CMake).
// - Consuming the SDK as a static library: define HOLOTRACK_STATIC (propagated by CMake).
//
// HoloTrackSDK is an independent product from ProjectionMappingSDK (see docs/decisions.md
// D-029): it ships its own DLL and never links pmsdk. It reuses only the header-only
// pmsdk::Math value types, which introduce no runtime dependency.
#if defined(HOLOTRACK_STATIC)
    #define HOLOTRACK_API
#elif defined(_WIN32)
    #if defined(HOLOTRACK_EXPORTS)
        #define HOLOTRACK_API __declspec(dllexport)
    #else
        #define HOLOTRACK_API __declspec(dllimport)
    #endif
#else
    #define HOLOTRACK_API __attribute__((visibility("default")))
#endif
