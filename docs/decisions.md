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

## D-026 2026-07-21 — Camera-measured luminance compensation reuses the white sweep capture

Projector vignetting (bright centre, dim edges) means a soft-edge blend overlap is the sum
of both projectors' DIMMEST edges, so even a perfect 1/γ alpha ramp leaves a visible band
on the real wall (observed 2026-07-17, D-025 run). Gamma tuning cannot fix a *spatial*
brightness difference — it must be measured.

Design (Unity-side, no native change — the apply path is a shader multiply):

- **Measurement is free.** The auto-align sweep already captures an all-white frame per
  projector for the shadow-mask contrast gate (`PMSDKAutoAlign`). That frame IS a picture
  of each projector's wall luminance; it is now retained on `PMSDKAutoAlign.Result.White`.
- **Pure core.** `PMSDKLuminanceCompensation.Compute` (same testable contract as
  `PMSDKAutoBlend`) scatters each lit camera pixel's luminance into its projector-raster
  bin via the Gray-code correspondence, fills unmeasured bins by dilation, box-blurs out
  webcam noise, then derives `gain = target / measured` where `target` is a robust-min
  (percentile) of pooled luminance across ALL projectors. A *global* target flattens every
  projector to the same absolute level, so both single-projector regions and blend overlaps
  (α_A·T + α_B·T = T under the partition-of-unity ramp) land at one uniform brightness.
- **Dim only.** target ≤ every measured value ⇒ gain ≤ 1; a `gainMin` floor caps how far a
  bright region may be dimmed, guarding against a noisy-dark target over-dimming everything.
- **Raster-space apply.** A per-projector `_GainTex` is multiplied in `PMSDK/UnlitWarp`
  after the blend ramp, before colour-correct. It is sampled by UV1 = the warped vertex
  raster position (D-020: `position = gridwarp(uv)` on the [0,1]² quad), so the vignette
  stays locked to the physical projector as the warp changes. `PMSDKMeshWarp` only writes
  UV1 while a `PMSDKLuminanceGain` map is active, so the common path pays nothing.
- **Persistence.** The map is quantized to 1 byte/cell + base64 in the calibration JSON
  (`PMSDKGainCodec`) — 8 bits over the 0.5..1 usable range is sub-visible, and a 96² map
  stays ~12 KB instead of a giant float array.
- **Opt-in.** `AutoLuminanceAfterAlignAll` defaults OFF (unlike auto-blend): it is a new,
  more aggressive correction on a hardware-verified pipeline, and each surface's
  `PMSDKLuminanceGain` can be disabled to revert instantly.

Explicitly out of scope: the doubled projector-black glow on dark content (a separate
per-region black-level item; `_BlackLevel` is still only a uniform floor).

## D-025 2026-07-17 — Real-room auto-align needs consensus fitting and shared-canvas targets

First run on physical hardware (2× UST projectors, one wall, Logitech C270) exposed two
gaps in the P3 auto-align design that simulation could never show.

(1) **Consensus fitting.** Ultra-short-throw projectors spill decodable light onto the
floor and ceiling. Those pixels pass the shadow mask and decode into perfectly VALID
correspondences — but they lie on different planes than the wall, so the single
least-squares homography inside `PMSDKAutoAlign` is poisoned wholesale (measured reproj
100–2700 px where the sim gave 0.39 px). A RANSAC consensus fit over the same
correspondence set (300 iterations, 4-point samples, ~0.02 raster-normalized inlier
tolerance) recovers RMS 0.55 px with ~half the points rejected as off-wall spill.
Robust fitting is not an optimization here; on real walls it is a correctness
requirement. Currently implemented game-side (`PMSDKWallCanvasAlign` in the nested
repo); should be upstreamed into `PMSDKAutoAlign` as the default fit.

(2) **Shared-canvas targets.** Null-target align recovers each projector's own footprint
individually — correct per projector, but N projectors on one wall then each map their
full raster onto their own throw, and the split-slice content does NOT meet in the
overlap. The multi-projector case needs one wall canvas (outer corners of the union of
observed quads, projectively parametrized) and per-projector targets equal to each
content slice of that canvas (slice bounds = the split-slice material scale/offset).
With that, blend bands coincide by construction and the analytic blend width
(overlap/slice) is exact — no measured auto-blend needed on a flat wall.

Also codified from the same run: during any sweep, blank all other projectors, freeze
animated content, and hide overlay badges (all three contaminate the decode); settle
must cover projector input lag + webcam exposure + USB buffering (hundreds of ms — use
generous `SettleFrames`, ideally time-based); room lights off (webcam auto-exposure
otherwise equalizes the white/black references).

## D-024 2026-07-16 — Projector-pose calibration solves against Unity's camera, not OpenCV

Object mapping needs the virtual projector camera's pose+FOV to match the real projector
so the virtual twin overlays the physical object. Rather than expose OpenCV
`solvePnP`/`calibrateCamera` extrinsics through the C API (which the C API does not
currently return) and then convert OpenCV↔Unity coordinate conventions, the solve is done
directly in managed code as reprojection minimization over Unity's own
`Camera.WorldToViewportPoint` (`PMSDKPoseSolver`, Levenberg–Marquardt over
[posXYZ, eulerXYZ, fov] with a numeric Jacobian). This removes the coordinate-conversion
class of bugs, needs no native rebuild, and is fully sim-verifiable. Verified closed-loop:
noise-free recovery is exact (0 px, exact pose/FOV); with 0.5% marking noise, ~3 px
reprojection / 0.03 m / 0.3° FOV. Trade-off: needs a rough initial pose (operator aims the
projector cam near the object first) and is iterative, not closed-form — fine for a
one-time on-site calibration. `PMSDKProjectorPoseCalibrator` drives the workflow (place
anchors on the twin → mark each in the output → solve → apply); fine registration then uses
corner-pin/grid warp as usual.

## D-023 2026-07-16 — Edge-blend ramp exponent is 1/projectorGamma (fixes the dark seam)

The projector shader blends in linear framebuffer space (output = content × alpha) and
two projectors' emitted light adds after each applies its ~2.2 display gamma. For the
overlap to sum to full brightness the ramps must satisfy alpha_A^γ + alpha_B^γ = 1, i.e.
alpha(x) = x^(1/γ). The native `EdgeBlend` Power curve computes `pow(x, gammaArg)`, so the
Unity `PMSDKEdgeBlend` now exposes **projector gamma** (default 2.2) and passes
`1/gamma` as the ramp exponent. Previously the default fed 2.2 straight in, giving
`pow(x,2.2)` — a visibly DARK seam (0.5^2.2×2 ≈ 0.43 at the centre). This was the cause of
the reported black strip. Black level is a uniform shader floor (`_BlackLevel`); true
per-region black-level compensation is future work. Per-channel gamma + gain/offset live
in `PMSDKColorCorrection`; output rotation/mirror in `PMSDKOutputTransform`; all fold into
the one `PMSDK/UnlitWarp` fragment pipeline.

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
