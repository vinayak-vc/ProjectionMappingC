# AI Handoff — ProjectionMappingSDK

Updated: 2026-07-10 (Milestone 8)

## Current state

Milestones 1–8 are complete! Library `pmsdk` (output `ProjectionMappingSDK`) now contains:
- **Core module**: ErrorCode, Status, Logger, Config, Context.
- **Math module**: Vector, Matrix, Quaternion, Ray, Plane, BoundingBox, Transform.
- **Geometry module**: `Vertex`, `Mesh`, `DynamicMesh`, `MeshBuilder`, `MeshSubdivision`, `MeshOptimizer`, `Intersection`, `BVH`, `KDTree`, `BezierCurve`, `Spline`, `UVMapping`, `BezierPatch`, `GridWarp`.
- **Warp module**: `Projector`, `DeformationField`, `WarpNode`, `Sampler`.
- **Blend module (M7)**: `EdgeBlend`, `BlendConfig`, `MaskGenerator`.
- **Serialization module (M8)**: `GeometrySerializer`, `WarpSerializer`, `BlendSerializer`.

103 unit tests are currently running and passing under strict `/W4 /WX` on MSVC.

## How to build here

Windows dev box (VS 2026 at `C:\Program Files\Microsoft Visual Studio\18\Community`):

```bat
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat"
cmake --preset debug && cmake --build --preset debug && ctest --preset debug
```

vcpkg bootstrapped in `third_party/vcpkg`. Dependencies so far: gtest (`tests` feature), `nlohmann-json`.

## Key files

- [include/PMSDK/PMSDK.h](../include/PMSDK/PMSDK.h) — umbrella header
- [include/PMSDK/Serialization/](../include/PMSDK/Serialization/) — newly added serialization endpoints returning `std::string` JSON.
- [src/Serialization/JsonHelpers.h](../src/Serialization/JsonHelpers.h) — internal `nlohmann::json` mappings for SDK types.
- [vcpkg.json](../vcpkg.json) — updated with `nlohmann-json` dependency.

## Conventions locked in (see decisions.md D-001…D-017)

- Namespace `pmsdk` (internal: `pmsdk::detail`).
- Export pattern: classes NOT dllexported; each public method carries `PMSDK_API` (avoids MSVC C4251 warnings). PImpl members stay private. 
- Warp nodes use a scene-graph style hierarchy (`WarpNode`) for hierarchical slicing (D-017).
- `pmsdk_apply_compiler_options()` on every target; builds must stay clean under `/W4 /WX`.
- **Serialization Architecture**: We hide `nlohmann::json` completely from the public ABI/API. Our public interfaces (`Serialize*`) return `std::string` or take `std::string`. The `.cpp` files include `<nlohmann/json.hpp>` and manage all conversions.

## Next recommended task — Milestone 9 (Calibration)

The SDK now has full core math, geometry, warping, blending, and serialization logic. We are ready for **Milestone 9 (Calibration)**, which involves integrating OpenCV to perform camera intrinsic/extrinsic calibration and generate structured light patterns (e.g. Gray code) for automated projector calibration.

## Modified files (Milestone 8)

New: `GeometrySerializer`, `WarpSerializer`, `BlendSerializer`, `JsonHelpers.h`.
Changed: `vcpkg.json`, `include/PMSDK/PMSDK.h`, `src/CMakeLists.txt`, `tests/CMakeLists.txt`, docs.
