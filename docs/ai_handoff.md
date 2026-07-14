# AI Agent Handoff Document

## Current State (2026-07-13)

**Last Completed Milestone**: Milestone 18 (Performance optimization)
**Current Task**: Ready for Milestone 19 (Plugin SDK).

## Current State
- The Core, Math, Geometry, Warp, Blend, Serialization, and Calibration modules are implemented.
- We have fully decoupled OpenCV behind a strict PImpl interface to prevent ABI spillage.
- Milestone 18 (Performance Optimization) is complete. The Warp, Mesh, and Math components now heavily utilize `std::execution::par_unseq` for multithreaded SIMD vectorization.
- The `MSVCWorkaround.cpp` was introduced as a C++ file wrapped in `extern "C"` to provide missing static linker symbols on older MSVC environments without conflicting with GHA MSVC CI headers.

## Next Recommended Task
- **Milestone 19: Plugin SDK**: Create plugin bindings for UE5 or wrap the C API in C# for advanced Unity rendering hooks.

## Project Structure Notes
- The DLL is completely self-contained.
- Unity wrapper expects `ProjectionMappingSDK.dll` in its plugins directory.
- Unreal wrapper uses the standard ThirdParty plugin structure to load the DLL.

## Commands
Build C++ core: `cmake --preset vs2022` then `cmake --build build/vs2022 --config Release`
