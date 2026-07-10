# AI Handoff — ProjectionMappingSDK

Updated: 2026-07-10 (Milestone 6)

## Current state

Milestones 1–6 are complete! Library `pmsdk` (output `ProjectionMappingSDK`) now contains:
- **Core module**: ErrorCode, Status, Logger, Config, Context.
- **Math module**: Vector, Matrix, Quaternion, Ray, Plane, BoundingBox, Transform.
- **Geometry module**: `Vertex`, `Mesh`, `DynamicMesh`, `MeshBuilder`, `MeshSubdivision`, `MeshOptimizer`, `Intersection`, `BVH`, `KDTree`, `BezierCurve`, `Spline`, `UVMapping`, `BezierPatch`, `GridWarp`.
- **Warp module (M6)**: `Projector`, `DeformationField`, `WarpNode`, `Sampler`.

92 unit tests are currently running and passing under strict `/W4 /WX` on MSVC.

## How to build here

Windows dev box (VS 2026 at `C:\Program Files\Microsoft Visual Studio\18\Community`):

```bat
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat"
cmake --preset debug && cmake --build --preset debug && ctest --preset debug
```

vcpkg bootstrapped in `third_party/vcpkg`. Dependency so far: gtest (`tests` feature).

## Key files

- [include/PMSDK/PMSDK.h](../include/PMSDK/PMSDK.h) — umbrella header
- [include/PMSDK/Warp/](../include/PMSDK/Warp/) — newly added warp structures
- [src/Warp/](../src/Warp/) — PImpl classes (`WarpNode`, `DeformationField`) 
- [tests/Warp/](../tests/Warp/) — unit tests for the warp structures.

## Conventions locked in (see decisions.md D-001…D-017)

- Namespace `pmsdk` (internal: `pmsdk::detail`).
- Export pattern: classes NOT dllexported; each public method carries `PMSDK_API` (avoids MSVC C4251 warnings). PImpl members stay private. 
- Warp nodes use a scene-graph style hierarchy (`WarpNode`) for hierarchical slicing (D-017).
- `pmsdk_apply_compiler_options()` on every target; builds must stay clean under `/W4 /WX`.

## Next recommended task — Milestone 7 (Blend Engine)

With the Warp Engine complete, we are ready for **Milestone 7 (Blend Engine)**. This will involve the math required for edge blending between multiple projectors (gamma correction, soft-edge gradients, and black level matching).

## Modified files (Milestone 6)

New: `Projector`, `DeformationField`, `WarpNode`, `Sampler` headers, sources, and tests.
Changed: `include/PMSDK/PMSDK.h`, `src/CMakeLists.txt`, `tests/CMakeLists.txt`, docs (roadmap, tasks, decisions, this file).
