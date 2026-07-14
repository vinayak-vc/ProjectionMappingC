# Tasks — ProjectionMappingSDK

## Milestone 1 — Repository setup (2026-07-10) ✅

- [x] Folder structure per spec (`include/PMSDK/<Module>`, `src`, `tests`, `examples`, `docs`, `cmake`, `third_party`)
- [x] Root `CMakeLists.txt` (C++20, options, unified output dirs)
- [x] `cmake/PMSDKCompilerOptions.cmake` (strict warnings, `/W4`·`-Wall -Wextra -Wpedantic -Wshadow -Wconversion`, opt-in WX)
- [x] vcpkg vendored as submodule `third_party/vcpkg` @ `97b19ca`, bootstrapped
- [x] `vcpkg.json` manifest with `builtin-baseline` + `tests` feature (gtest)
- [x] `CMakePresets.json` — Ninja debug/release, clang-*, gcc-*, vs2022
- [x] `pmsdk` library target (output `ProjectionMappingSDK`), export header, version API
- [x] Install/export rules (`PMSDKConfig.cmake`, targets, headers)
- [x] GoogleTest suite: `tests/Core/VersionTests.cpp` (3 tests)
- [x] Example: `examples/NativeExample` (links SDK, checks version handshake)
- [x] CI: GitHub Actions matrix (windows-msvc, linux-gcc, linux-clang, macos) with vcpkg GHA binary cache
- [x] `.clang-format`, `.editorconfig`, `.gitignore`, `README.md`
- [x] Docs: overview, architecture, roadmap, tasks, decisions, ai_handoff
- [x] Local verification: configure + build + ctest (MSVC debug)

## Milestone 2 — Core module (2026-07-10) ✅

- [x] `ErrorCode.h` — stable-valued enum + constexpr `ToString` (backs future C API codes)
- [x] `Status.h` — header-only `Status` + `Result<T>` (no exceptions across boundary)
- [x] `Log.h` / `Logger.cpp` — thread-safe callback facade; silent w/o callback; host sink via `LogCallback`; throwing-callback containment; spdlog deferred (D-008)
- [x] `Config.h` / `Config.cpp` — typed key/value store, shared_mutex, type-mismatch → default
- [x] `Context.h` / `Context.cpp` — root lifecycle object (no globals); owns Logger + Config
- [x] `src/Core/LastError.{h,cpp}` — internal thread-local last-status (errno-style, D-011)
- [x] `src/Core/HandleRegistry.h` — internal generation-checked 64-bit handle registry (D-010)
- [x] `PMSDK.h` umbrella header
- [x] Tests: 44 total (Config 6, Context 4, ErrorCode 3, HandleRegistry 8, LastError 4, Logger 9, Status/Result 7, Version 3)
- [x] NativeExample updated: Context + log callback + config
- [x] Verified: MSVC debug 44/44, release `/WX` 44/44
- [x] Adversarial multi-agent review pass

## Milestone 3 — Math library (2026-07-10) ✅

- [x] `Vector2/3/4`, `Quaternion`, `Matrix4` — header-only, constexpr-friendly, value types
- [x] `Transform` (TRS), `BoundingBox`, `Ray`, `Plane`
- [x] Decide: hand-rolled vs glm-backed internals (glm never in public headers)
- [x] Sub-millisecond ops, low-allocation, SIMD-ready layout (16-byte alignment where useful)
- [x] Exhaustive unit tests (identities, inverses, edge cases: degenerate matrices, zero-length vectors)

## Milestone 4 — Geometry library (2026-07-10) ✅

- [x] `Mesh` data structures
- [x] KD-Tree and BVH implementations
- [x] Ray Intersection utilities
- [x] Bezier Curves and Splines
- [x] UV Mapping utilities

## Milestone 5 — Mesh data structures (2026-07-10) ✅

- [x] Topology-Aware Mesh (`DynamicMesh`)
- [x] Control Grids (`BezierPatch`, `GridWarp`)
- [x] Mesh Optimization (`MeshOptimizer`: Welding and smooth normals)

## Milestone 6 — Warp engine (2026-07-10) ✅

- [x] Interactive warping API using control grids (`DeformationField`)
- [x] Deformation logic for projecting points on mesh (`WarpNode`, `Projector`)
- [x] Integration of Bezier patches into a global Warp field

## Milestone 7 — Blend engine (2026-07-10) ✅

- [x] Edge blending math (gamma, soft-edge gradients)
- [x] Luminance compensation structures
- [x] Black level matching variables

## Milestone 8 — Serialization (2026-07-10) ✅

- [x] Save/Load SDK state to JSON or XML
- [x] Implement serialization for `WarpNode` hierarchy, `Mesh`, `BlendConfig`

## Milestone 9 — Calibration (OpenCV) (2026-07-10) ✅

- [x] Incorporate OpenCV (via vcpkg)
- [x] Implement camera intrinsic/extrinsic calibration helpers
- [x] Implement structured light pattern generation (Gray code)

## Milestone 10 — Public C API (2026-07-10) ✅

- [x] Create `include/PMSDK/C_API.h` exposing core types (mesh, warp nodes)
- [x] Wrap `pmsdk::Warp` operations into `pmsdk_warp_*` functions
- [x] Ensure C-linkage (`extern "C"`) is completely PImpl-based to allow Python/C#/Unity consumption

## Done
- Milestone 18: Performance optimization (SIMD, Multithreading) (2026-07-14) ✅
- Milestone 17: GrayCode Decoder & Triangulation (2026-07-13) ✅
- Milestone 16: Advanced OpenCV calibration wrappers (2026-07-13) ✅
- Milestone 15: Sample applications (Unity Interactive Guided Setup Wizard)
- Milestone 14: Unreal Wrapper
- Milestone 13: Unity Wrapper (`vxpmsdk`)
- Milestone 12: Documentation (Doxygen)
- Milestone 11: Unit tests (full sweep to 90% coverage)
- Milestone 10: Public C API

## Next Items / Backlog
- [ ] Milestone 19: Plugin SDK
- [ ] Milestone 20: Release packaging
- [ ] Version header generation via `configure_file` if hand-sync becomes annoying (static_assert guards it for now)
- [ ] Code coverage job in CI
