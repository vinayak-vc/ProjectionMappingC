# AI Handoff — ProjectionMappingSDK

Updated: 2026-07-10 (Milestone 10)

## Current state

Milestones 1–10 are complete! The SDK now has a fully stable **C ABI** alongside its modern C++ interface, meaning `ProjectionMappingSDK.dll` can be used by Python (ctypes), Unity (C# P/Invoke), and Godot.

- **Core module**: ErrorCode, Status, Logger, Config, Context.
- **Math module**: Vector, Matrix, Quaternion, Ray, Plane, BoundingBox, Transform.
- **Geometry module**: `Vertex`, `Mesh`, `DynamicMesh`, `MeshBuilder`, `MeshSubdivision`, `MeshOptimizer`, `Intersection`, `BVH`, `KDTree`, `BezierCurve`, `Spline`, `UVMapping`, `BezierPatch`, `GridWarp`.
- **Warp module**: `Projector`, `DeformationField`, `WarpNode`, `Sampler`.
- **Blend module**: `EdgeBlend`, `BlendConfig`, `MaskGenerator`.
- **Serialization module**: `GeometrySerializer`, `WarpSerializer`, `BlendSerializer`.
- **Calibration module**: `Intrinsics`, `Extrinsics`, `Calibrator`, `GrayCode` (Powered privately by OpenCV).
- **C-API (M10)**: Opaque handles `pmsdk_mesh_t`, `pmsdk_projector_t`, `pmsdk_warpnode_t` exported cleanly as unmangled `extern "C"` functions.

112 unit tests are currently running and passing under strict `/W4 /WX` on MSVC.

## How to build here

Windows dev box (VS 2026 at `C:\Program Files\Microsoft Visual Studio\18\Community`):

```bat
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat"
cmake --preset debug && cmake --build --preset debug && ctest --preset debug
```

To verify C API exports:
```bat
dumpbin /exports build\debug\bin\ProjectionMappingSDKd.dll | findstr pmsdk_
```

## Key files

- [include/PMSDK/PMSDK.h](../include/PMSDK/PMSDK.h) — umbrella C++ header
- [include/PMSDK/PMSDK_C.h](../include/PMSDK/PMSDK_C.h) — umbrella C header
- [include/PMSDK/C_API/](../include/PMSDK/C_API/) — C-API declarations
- [src/C_API/](../src/C_API/) — C-API wrappers

## Conventions locked in (see decisions.md D-001…D-017)

- Namespace `pmsdk` (internal: `pmsdk::detail`).
- Export pattern: classes NOT dllexported; each public method carries `PMSDK_API` (avoids MSVC C4251 warnings).
- **C-API Architecture (M10)**: Pure C-linkage, opaque structs, no exceptions. Internal `std::shared_ptr` ownership is handled by allocating a thin wrapper struct internally.
- `pmsdk_apply_compiler_options()` on every target; builds must stay clean under `/W4 /WX`.

## Next recommended task — Milestone 11 (Unit Tests)

The system works and tests cover the happy path (112 tests). The next step is a deep unit test sweep to hit 90%+ code coverage, particularly testing edge cases in `DeformationField`, edge blending bounds, and malformed JSON payloads in serialization.

## Modified files (Milestone 10)

New: `include/PMSDK/C_API/Types.h`, `GeometryAPI.h`, `WarpAPI.h`, `PMSDK_C.h`
New: `src/C_API/GeometryAPI.cpp`, `WarpAPI.cpp`
New: `tests/C_API/GeometryAPITests.cpp`
Modified: `src/CMakeLists.txt`, `tests/CMakeLists.txt`, `include/PMSDK/PMSDK.h`
