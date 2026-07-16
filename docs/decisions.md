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

## D-022 2026-07-16 — Corner pin is projective; grid warp is bilinear; they are distinct deformation modes

The 4-corner pin now uses a true homography (`DeformationType::Perspective`,
`Geometry::PerspectiveWarp`, Heckbert unit-square→quad map) instead of pushing 4 points
into a 2×2 bilinear `GridWarp`. Bilinear interpolation of 4 corners shears the texture
along the quad diagonal on any non-parallelogram (keystoned) quad — visibly wrong and not
what a projector/media-server corner pin does. Verified by unit test: perspective and
bilinear agree at the 4 corners but the mesh interior diverges (~0.17 at the midpoint of a
trapezoid).

The N×M `GridWarp` (bilinear per cell) remains the correct model for curved/irregular
surfaces and is now exposed at runtime (`PMSDKGridWarp` + calibration grid mode). The two
are mutually exclusive per warp node (a node has one deformation type): when a
`PMSDKGridWarp` is enabled it owns the node and `PMSDKCornerPin` yields. C API adds
`pmsdk_warpnode_get_perspectivewarp` + `pmsdk_perspectivewarp_set_corners` (deformation
type 3). See docs/unity-architecture.md §Warp modes.

## D-021 2026-07-16 — Camera auto-align uses a homography, not the native stereo triangulation

On-site auto-align (calibration P3) recovers a camera→projector planar homography from
Gray-code correspondence and maps target corners into the corner-pin. It deliberately
does NOT use `pmsdk_decoder_decode_and_triangulate`: that produces a metric 3D point
cloud but requires a fully calibrated camera AND projector (intrinsics + extrinsics),
which an on-site technician cannot supply quickly. A homography needs zero calibration,
matches the model the corner-pin already implements, and is sufficient for flat/quasi-flat
mapping surfaces. Gray decode is reimplemented in managed C# (bit-compatible with
src/Calibration/GrayCode.cpp) so the correspondence pipeline is deterministic and testable
without hardware. The native triangulation path remains for future true-3D calibration
onto complex geometry. Capture is abstracted (`IPMSDKCalibrationCamera`): a simulated
Unity-camera source ships now (works for virtual/LED-volume rigs and testing); the
physical webcam source needs a per-frame grayscale readback added to the C API
(`pmsdk_decoder_get_frame`). See docs/calibration-ux-design.md §9.

## D-020 2026-07-15 — Unity renders warped meshes in normalized raster space with orthographic projector cameras

The native warp engine's output contract is `position = gridwarp(uv)`: processed meshes
always land on a normalized [0,1]² XY quad, regardless of the input mesh's 3D pose. The
original Unity demo rig (3D planes + perspective projector cameras) therefore rendered the
warped quad edge-on — projector outputs were pure black in play mode. The Unity integration
now standardizes on: identity-rotation warp surfaces scaled to raster aspect (16:9, 1, 1),
orthographic projector cameras framing the unit quad, split-slice RT materials with an
overlap band, and the `PMSDK/UnlitWarp` shader (native edge blend writes vertex ALPHA only;
stock Unlit/Texture ignores it). Multi-display output requires `PMSDKDisplayActivator`
(Display.Activate is mandatory in standalone builds). See docs/unity-architecture.md §2–4.

## D-019 2026-07-13 — Direct SDK Camera Capture for Structured Light

Instead of requiring the host engine (Unity/Unreal) to capture WebCam frames, allocate large `Texture2D` memory on the main thread, and serialize/pass them to the SDK, we enabled the `GrayCodeDecoder` to directly bind to the physical camera hardware using OpenCV's `cv::VideoCapture`. This keeps memory and frame extraction strictly off the host's main thread, drastically improving performance.
