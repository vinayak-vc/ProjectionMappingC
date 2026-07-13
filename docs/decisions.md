# Decisions — ProjectionMappingSDK

Format: D-NNN date — decision — rationale.

## D-001 2026-07-10 — vcpkg vendored as git submodule

`third_party/vcpkg` submodule instead of requiring a global vcpkg install. Self-contained
clones, CI bootstraps the same revision, `builtin-baseline` pinned to the submodule commit
(`97b19caab97ab0c8d36c93230046c8c45fcd41d0`) so dependency versions are reproducible.

## D-002 2026-07-10 — Dependencies gated behind vcpkg manifest features

`tests` feature → gtest now; `calibration` feature → opencv4 at Milestone 9. Keeps first
configure fast and lets consumers skip heavy deps. Core deps (spdlog, nlohmann-json, glm)
join the default dependency list only in the milestone that first uses them.

## D-003 2026-07-10 — Hand-written export header + CMake-injected version constants

`PMSDK_API` in `Core/Export.h` (no `generate_export_header` — keeps installed headers plain).
Version single-sourced from `project(VERSION)`; CMake injects `PMSDK_VERSION_*` defines and
`Version.cpp` `static_assert`s them against `kHeaderVersion` so header/CMake drift breaks the
build instead of shipping.

## D-004 2026-07-10 — Ninja-first presets; VS2022 preset retained per spec

Default presets use Ninja (works with MSVC from a developer prompt, Clang, GCC). `vs2022`
generator preset kept because the spec requires VS2022 support. Local dev machine runs
VS 2026 (v18) — Ninja + vcvars covers it since CMake 3.28 has no VS18 generator.

## D-005 2026-07-10 — Unified runtime output directory

All DLLs/EXEs land in `<build>/bin` so GoogleTest discovery and test runs find
`ProjectionMappingSDKd.dll` without PATH games on Windows.

## D-006 2026-07-10 — C API is the only stable ABI

C++ headers are for native convenience; ABI stability promised only for `PMSDK/CAPI`
(opaque handles + status codes, Milestone 10). Blocks exceptions/STL types at the boundary.

## D-007 2026-07-10 — codex-aider-bridge pipeline skipped this session

Bridge MCP server + code-review-graph not installed on this machine; user chose
"skip bridge". Milestone 1 implemented directly. Revisit if bridge gets installed.

## D-008 2026-07-10 — Logging is a zero-dependency callback facade; spdlog deferred

The SDK contract is "host receives log lines via registered callback; SDK never writes to
stdout/stderr/files itself". That needs no sink library. spdlog joins only when the SDK
itself needs sinks (file logging in Utilities) — public headers would never expose it anyway.

## D-009 2026-07-10 — Export pattern: per-method PMSDK_API, classes not dllexported

Whole-class `__declspec(dllexport)` with `std::unique_ptr<Impl>` members triggers MSVC C4251
under `/W4 /WX`. Instead each public method is individually exported; PImpl members stay
private, destructors are exported so Impl destruction happens inside the SDK binary.
Consequence: C++ layer assumes same-toolchain consumer (documented in D-006); the C API
remains the mixed-toolchain boundary.

## D-010 2026-07-10 — Handles are 64-bit generation+index with shared_ptr storage

`detail::HandleRegistry<T>`: high 32 bits generation, low 32 bits slot index; generation
bumps on Remove so stale handles fail lookup instead of aliasing reused slots. Lookup
returns `shared_ptr` so a concurrently-removed object stays alive for the caller. 0 is
never valid. Internal header (`src/Core/`), not installed.

## D-011 2026-07-10 — thread_local last-error is the sole exception to "no static mutable state"

C APIs need errno-style `PM_GetLastError`. Per-thread `Status` storage
(`detail::SetLastStatus/LastStatus/ClearLastStatus`) is function-local `thread_local`,
internal-only, not exported. Tests compile `LastError.cpp` directly into the test binary
since the symbols are hidden in the DLL.

## D-012 2026-07-10 — Hand-rolled public Math types

The public Math types (`Vector2/3/4`, `Matrix4`, `Quaternion`, `Transform`, etc.) are hand-rolled as simple `constexpr`-friendly value types rather than backed by `glm`. This avoids PImpl allocations overhead for small math types and keeps `glm` strictly out of the public API per the design guidelines.

## D-013 2026-07-10 — Column-Major Matrix4

## D-014 2026-07-10 — Fixed Vertex Layout

To keep the initial scope manageable while avoiding complex flexible vertex formats, `Vertex` is defined as a fixed interleaved struct with `Position`, `Normal`, `UV`, and `Color`. This is highly optimized for cache locality and suits 99% of projection mapping use cases.

## D-015 2026-07-10 — PImpl in Geometry Classes

Complex Geometry classes (`Mesh`, `BVH`, `KDTree`) utilize the PImpl (Pointer to Implementation) idiom to hide dynamically sized STL containers like `std::vector` from the ABI boundary, adhering strictly to D-009 to avoid MSVC C4251 warnings.

## D-016 2026-07-10 — Separation of Static and Dynamic Meshes

To balance rendering performance and editing flexibility, we provide two separate mesh representations: `Mesh` (highly optimized, linear buffers, ideal for rendering/drawing) and `DynamicMesh` (Topology-aware, allows vertex/face addition and removal, adjacency queries, ideal for interactive editing tools). Users manipulate the `DynamicMesh` and bake it to a `Mesh` for final rendering.

## D-017 2026-07-10 — Warp Node Hierarchy

The projection mapping structure uses a hierarchical Scene Graph for warped surfaces (`WarpNode`). Each node has a local transform and an optional `DeformationField` (Bezier or Grid warp). This mimics the standard slice-grouping mechanisms found in Resolume Arena and MadMapper, allowing complex grouped spatial transforms applied on top of individual deformations.

## D-018 2026-07-13 — C-API exposure of Mesh getters and BlendConfig

To support visual tooling and interactive sample applications (like the Unity setup wizard in Milestone 15), we extended the C-API to allow reading warped geometry back to the host environment (`pmsdk_mesh_get_vertices`). Although the SDK is primarily designed to push rendering output, exposing these getters is necessary for engine-agnostic preview capabilities.
