# AI Handoff — ProjectionMappingSDK

Updated: 2026-07-10 (Milestone 4)

## Current state

Milestones 1–4 are complete! Library `pmsdk` (output `ProjectionMappingSDK`) now contains:
- **Core module**: ErrorCode, Status, Logger, Config, Context.
- **Math module**: Vector, Matrix, Quaternion, Ray, Plane, BoundingBox.
- **Geometry module**: `Vertex`, `Mesh` (PImpl), `MeshBuilder` (Plane, Grid, Cylinder), `MeshSubdivision` (linear), `Intersection` (Möller–Trumbore Ray-Triangle), `BVH` and `KDTree` (PImpl acceleration structures), `BezierCurve`, `Spline`, `UVMapping`.

78 unit tests are currently running and passing under strict `/W4 /WX` on MSVC.

## How to build here

Windows dev box (VS 2026 at `C:\Program Files\Microsoft Visual Studio\18\Community`):

```bat
call "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvars64.bat"
cmake --preset debug && cmake --build --preset debug && ctest --preset debug
```

vcpkg bootstrapped in `third_party/vcpkg`. Dependency so far: gtest (`tests` feature).

## Key files

- [include/PMSDK/PMSDK.h](../include/PMSDK/PMSDK.h) — umbrella header
- [include/PMSDK/Geometry/](../include/PMSDK/Geometry/) — newly added geometry features
- [src/Geometry/](../src/Geometry/) — PImpl classes (`Mesh`, `BVH`, `KDTree`) hiding STL allocations.
- [tests/Geometry/](../tests/Geometry/) — unit tests for the geometry structures.

## Conventions locked in (see decisions.md D-001…D-015)

- Namespace `pmsdk` (internal: `pmsdk::detail`).
- Export pattern: classes NOT dllexported; each public method carries `PMSDK_API` (avoids MSVC C4251 warnings). PImpl members stay private. This rule was strictly enforced in Milestone 4 (`Mesh.h`, `KDTree.h`, `BVH.h`).
- `Vertex` uses a fixed layout (Position, Normal, UV, Color) for simplicity and performance (D-014).
- Internal-only code lives in `src/`, tests compile such .cpp files directly (symbols hidden in DLL) when needed.
- `pmsdk_apply_compiler_options()` on every target; builds must stay clean under `/W4 /WX` and `-Wall -Wextra -Wpedantic -Wshadow -Wconversion -Werror`.
- Update `docs/tasks.md` + `docs/decisions.md` during work; this file after.

## Next recommended task — Milestone 5 (Mesh Data Structures / Serialization) & Milestone 6 (Warp Engine)

Milestone 4 fulfilled most of the "Mesh data structures" (Milestone 5) requirements. 
The next logical step is either:
1. **Mesh I/O**: Implement OBJ or GLTF loaders to serialize/deserialize the `Mesh` object.
2. **Warp Engine**: Begin the core projection mapping algorithm. Use the `BezierCurve` and `Mesh` structures to implement a Bezier Surface Warper or Grid Warper.

## Modified files (Milestone 4)

New: `include/PMSDK/Geometry/*.h`, `src/Geometry/*.cpp`, `tests/Geometry/*Tests.cpp`.
Changed: `include/PMSDK/PMSDK.h`, `src/CMakeLists.txt`, `tests/CMakeLists.txt`, docs (roadmap, tasks, decisions, this file).
