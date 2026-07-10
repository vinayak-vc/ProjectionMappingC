# AI Handoff — ProjectionMappingSDK

Updated: 2026-07-10 (Milestone 5)

## Current state

Milestones 1–5 are complete! Library `pmsdk` (output `ProjectionMappingSDK`) now contains:
- **Core module**: ErrorCode, Status, Logger, Config, Context.
- **Math module**: Vector, Matrix, Quaternion, Ray, Plane, BoundingBox.
- **Geometry module (M4 & M5)**: `Vertex`, `Mesh`, `DynamicMesh` (HEDS topology for editing), `MeshBuilder`, `MeshSubdivision`, `MeshOptimizer` (welding/smooth normals), `Intersection`, `BVH`, `KDTree`, `BezierCurve`, `Spline`, `UVMapping`, `BezierPatch`, `GridWarp`.

85 unit tests are currently running and passing under strict `/W4 /WX` on MSVC.

## How to build here

Windows dev box (VS 2026 at `C:\Program Files\Microsoft Visual Studio\18\Community`):

```bat
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat"
cmake --preset debug && cmake --build --preset debug && ctest --preset debug
```

vcpkg bootstrapped in `third_party/vcpkg`. Dependency so far: gtest (`tests` feature).

## Key files

- [include/PMSDK/PMSDK.h](../include/PMSDK/PMSDK.h) — umbrella header
- [include/PMSDK/Geometry/](../include/PMSDK/Geometry/) — newly added mesh structures
- [src/Geometry/](../src/Geometry/) — PImpl classes (`DynamicMesh`, `BezierPatch`, `GridWarp`) hiding STL allocations.
- [tests/Geometry/](../tests/Geometry/) — unit tests for the geometry structures.

## Conventions locked in (see decisions.md D-001…D-016)

- Namespace `pmsdk` (internal: `pmsdk::detail`).
- Export pattern: classes NOT dllexported; each public method carries `PMSDK_API` (avoids MSVC C4251 warnings). PImpl members stay private. 
- Separation of concerns between `Mesh` (render-optimized buffers) and `DynamicMesh` (topology-aware structures for editing) (D-016).
- `pmsdk_apply_compiler_options()` on every target; builds must stay clean under `/W4 /WX`.

## Next recommended task — Milestone 6 (Warp Engine)

With the advanced control grid structures (`BezierPatch`, `GridWarp`) built in M5, we are fully prepared to build the **Warp Engine** (Milestone 6). The Warp Engine should expose a high-level interactive API for managing deformation layers over a base mesh, applying non-linear transformations using the grids we just created.

## Modified files (Milestone 5)

New: `DynamicMesh`, `BezierPatch`, `GridWarp`, `MeshOptimizer` headers, sources, and tests.
Changed: `include/PMSDK/PMSDK.h`, `src/CMakeLists.txt`, `tests/CMakeLists.txt`, docs (roadmap, tasks, decisions, this file).
