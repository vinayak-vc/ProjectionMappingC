# AI Handoff — ProjectionMappingSDK

Updated: 2026-07-10 (Milestone 9)

## Current state

Milestones 1–9 are complete! Library `pmsdk` (output `ProjectionMappingSDK`) now contains:
- **Core module**: ErrorCode, Status, Logger, Config, Context.
- **Math module**: Vector, Matrix, Quaternion, Ray, Plane, BoundingBox, Transform.
- **Geometry module**: `Vertex`, `Mesh`, `DynamicMesh`, `MeshBuilder`, `MeshSubdivision`, `MeshOptimizer`, `Intersection`, `BVH`, `KDTree`, `BezierCurve`, `Spline`, `UVMapping`, `BezierPatch`, `GridWarp`.
- **Warp module**: `Projector`, `DeformationField`, `WarpNode`, `Sampler`.
- **Blend module**: `EdgeBlend`, `BlendConfig`, `MaskGenerator`.
- **Serialization module**: `GeometrySerializer`, `WarpSerializer`, `BlendSerializer`.
- **Calibration module (M9)**: `Intrinsics`, `Extrinsics`, `Calibrator`, `GrayCode`. OpenCV is successfully integrated privately.

109 unit tests are currently running and passing under strict `/W4 /WX` on MSVC.

## How to build here

Windows dev box (VS 2026 at `C:\Program Files\Microsoft Visual Studio\18\Community`):

```bat
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat"
cmake --preset debug && cmake --build --preset debug && ctest --preset debug
```

vcpkg bootstrapped in `third_party/vcpkg`. Dependencies so far: gtest (`tests` feature), `nlohmann-json`, `opencv4`.

## Key files

- [include/PMSDK/PMSDK.h](../include/PMSDK/PMSDK.h) — umbrella header
- [include/PMSDK/Calibration/](../include/PMSDK/Calibration/) — new camera and calibration headers.
- [vcpkg.json](../vcpkg.json) — uses `opencv4`.

## Conventions locked in (see decisions.md D-001…D-017)

- Namespace `pmsdk` (internal: `pmsdk::detail`).
- Export pattern: classes NOT dllexported; each public method carries `PMSDK_API` (avoids MSVC C4251 warnings). PImpl members stay private. 
- `pmsdk_apply_compiler_options()` on every target; builds must stay clean under `/W4 /WX`.
- **Serialization Architecture**: `nlohmann::json` is completely hidden from the public ABI.
- **Calibration Architecture**: OpenCV is dynamically/privately linked via vcpkg. We completely hide `cv::Mat` from the public APIs, instead defining our own `Intrinsics`/`Extrinsics` structs, which wrap vectors and matrices. This ensures P/Invoke and external users do not need OpenCV.

## Next recommended task — Milestone 10 (Public C API)

With all core logic (geometry, warping, blending, serialization, calibration) written in Modern C++20, it is now time to write the **C API Wrapper** layer so other languages (Unity C#, Unreal Engine C++, Python, Godot) can consume it safely across ABI boundaries.

## Modified files (Milestone 9)

New: `Calibration/Intrinsics.h/.cpp`, `Calibration/Extrinsics.h/.cpp`, `Calibration/Calibrator.h/.cpp`, `Calibration/GrayCode.h/.cpp` + Tests.
Changed: `vcpkg.json`, `include/PMSDK/PMSDK.h`, `src/CMakeLists.txt`, `tests/CMakeLists.txt`, docs.
