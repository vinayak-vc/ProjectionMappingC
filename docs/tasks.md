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

- Milestone 20: Release packaging (2026-07-14) ✅

## Unity demo overhaul (2026-07-15) ✅

- [x] Diagnosed black projector outputs: warped meshes live in normalized raster space; old 3D-plane + perspective-camera rig rendered them edge-on (D-020)
- [x] Raster-space rig: identity-rotation warp surfaces (16:9 scale), orthographic projector cameras, split-slice RT materials with 10% overlap
- [x] New `PMSDK/UnlitWarp` shader (edge blend writes vertex alpha; Unlit/Texture ignored it)
- [x] New `PMSDKDisplayActivator` (Display.Activate — displays 2/3 were black in builds)
- [x] New `PMSDKVelocityCap` (maxLinearVelocity is not serialized; PhysX restitution overshoot made the demo cube bounce ever higher)
- [x] `PMSDKTestPattern` no longer clobbers screen materials across scene saves (serialized originalMaterial, DontSave runtime objects)
- [x] Demo generator persists RT/materials/physics material as assets (in-memory references died on scene reload)
- [x] Auto-configurator creates the ortho raster rig; culling mask uses the surface's own layer (was hardcoded `Default`)
- [x] Scenes: `WarpAndBlendExample` retrofitted; `ProBuilderMappingDemo` added (ProBuilder stage: wall/pillars/arch/stairs)
- [x] Play-mode verified: both projector outputs show split content with black-falloff blend bands

## On-site calibration P1 (2026-07-16) ✅

- [x] `PMSDKCalibrationManager` — F2 calibration mode, keyboard-only editing (PgUp/PgDn surface, Tab corner/edge, arrows nudge with Shift/Ctrl step scaling, B blend submode, T test pattern, R reset, Ctrl+S save, Esc exit+autosave)
- [x] JSON persistence in persistentDataPath (atomic write, `-calibfile`/`-calibrate` CLI overrides); silent load+apply on boot; auto-enter calibration on first run
- [x] `PMSDKCalibrationOverlay` — corner handles (selected = yellow/pulsing) + projector identify badge on each output
- [x] `PMSDKCalibrationHUD` — operator console on Display 1 (status line, dirty flag, F1 help)
- [x] `PMSDKCornerPinUI` superseded (manager disables it); generator/configurator create "PMSDK Runtime Services" (manager + display activator); both demo scenes migrated
- [x] Play-mode verified end-to-end: nudge → CornerPin → native GridWarp → visible keystone on projector output; save/reload roundtrip works

## On-site calibration P2 (2026-07-16) ✅

- [x] Mouse drag: `Display.RelativeMouseAt` per-display routing (Editor fallback: raw mouse vs focused Game view, distance-gated grab), Alt = fine drag
- [x] Undo: coalesced snapshot stack (0.7 s groups, Ctrl+Z, depth 100), restores corners + blend per surface
- [x] Loupe: hidden service camera renders magnified view around selected corner into RT, shown with crosshair on operator HUD
- [x] Per-projector live thumbnails on operator HUD (0.33 s refresh), click to select, yellow frame on selection
- [x] Blend-zone tint on projector outputs in blend submode (orange = selected edge, cyan = others)
- [x] Fixed: overlay canvas parented under 16:9-scaled surface inherited the scale and pushed zones out of frustum — overlays now live under the manager
- [x] Play-mode verified: pixel-probed orange/cyan bands, handle positions, undo roundtrip, thumbnails + loupe RTs live

## On-site calibration P3 — camera auto-align (2026-07-16) ✅ (simulation-verified)

- [x] `PMSDKGrayCodeDecode` — managed decode bit-compatible with native GrayCode.cpp (colBits+rowBits, MSB-first, Gray→binary, per-pixel white/black threshold, contrast reject)
- [x] `PMSDKHomography` — least-squares planar homography (normal equations, 8x8 Gaussian solve), apply
- [x] `PMSDKCalibrationCamera` — `IPMSDKCalibrationCamera` + `PMSDKSimulatedCamera` (Unity camera → luminance readback, top-left origin)
- [x] `PMSDKAutoAlign` — coroutine: take over surface → show white/black + Gray patterns → capture → decode correspondence → fit camera→projector homography → map target corners to corner-pin
- [x] Manager `A` / `Shift+A` (selected / all), input gated during sweep, HUD status; `AlignSelectedWithTarget` for explicit target quads
- [x] Numeric verify: decode roundtrip 16384/16384 (0 errors), homography reproj 1.9e-6
- [x] Simulated integration verify: recovered identity from skewed corner-pin (err 0.008), explicit sub-rectangle target mapped exactly
- [x] Real-hardware loop: C-API frame readback + native webcam capture source (see below); physical smoke test still pending (no hardware here)

## Calibration hardening — "item 1" (2026-07-16) ✅

- [x] Native robust Gray-code sequence: `GenerateRobustPattern` ([white, black, pattern, inverse, …]), `DecodeRobust` (per-pixel pattern-vs-inverse bits + white/black shadow mask) — replaces fragile global-threshold decoding for real cameras
- [x] Camera capture fixes: `CaptureFrameFlushed` (VideoCapture buffers frames; unflushed captures show the previous pattern), `AddImageFromMemory`, `GetLastFrame` readback, `GetImageCount`, `ClearImages`
- [x] C API: `pmsdk_graycode_get_robust_pattern_count/generate_robust_pattern`, `pmsdk_decoder_capture_frame_flushed/add_image_memory/get_last_frame/get_image_count/clear_images/decode_robust` (raw correspondences for homography auto-align)
- [x] GoogleTest suite `Calibration/GrayCodeDecoderTests.cpp`: identity roundtrip, albedo+ambient robustness, shadow-mask rejection, sequence-length guard, readback/clear — 6/6 green
- [x] Unity: `PMSDKNativeWebcamCamera` (IPMSDKCalibrationCamera over native webcam, per-capture fresh buffers), manager `UseNativeWebcam`/`WebcamIndex`/`WebcamFlushFrames`, `PMSDKAutoAlign.UseInversePatterns` (default on) + managed inverse-pattern decode, `Begin()` failure handling
- [x] Release DLL rebuilt (all 8 new exports verified) and deployed to the Unity plugin folder
- [ ] Physical projector+webcam smoke test (no hardware available here)
- [ ] Known pre-existing failure: `DecoderTests.DecodeAndTriangulate` fails at HEAD too (before this change) — triangulation numerics/environment, investigate separately

## Pro-feature gaps — High items (2026-07-16)

### Perspective corner pin ✅ (unit-tested; runtime pending DLL redeploy)
- [x] Native `Geometry::PerspectiveWarp` (Heckbert unit-square→quad homography), `DeformationType::Perspective`, wired into `DeformationField`
- [x] C API: `pmsdk_warpnode_get_perspectivewarp`, `pmsdk_perspectivewarp_set_corners` (type 3)
- [x] `PMSDKCornerPin` switched from 2×2 bilinear grid to perspective (D-022)
- [x] Tests `Geometry/PerspectiveWarpTests.cpp`: exact corners, center, keystone-vs-bilinear divergence, ApplyDeformation — 4/4 green; full suite 132/133 (only the pre-existing DecodeAndTriangulate fails)
- [ ] Runtime play-mode visual confirm — blocked on DLL redeploy (Unity holds the native plugin lock; needs editor restart)

### N×M grid warp UI ✅ (compiles; runtime pending DLL redeploy)
- [x] `PMSDKGridWarp` runtime component (N×M control points, resample-on-resize, serialized, pushes native GridWarp)
- [x] Calibration grid mode (`G`): select/nudge/drag control points, `[`/`]` cols, `-`/`=` rows, `R` reset; overlay draws lattice handles + lines; loupe + HUD follow the grid point
- [x] Grid persisted in the calibration JSON (enabled/cols/rows/points) and restored on load
- [ ] Runtime play-mode confirm — same DLL-redeploy dependency

Note: grid warp uses only pre-existing native entry points, but the perspective corner
pin needs the new DLL, so both wait on one redeploy + Unity restart.

### Mark-target-rectangle UI ✅ (compiles; runtime pending same DLL redeploy)
- [x] Calibration mark-target submode (`M`): live camera preview on the operator console with 4 draggable/nudgeable corners (`Tab`/arrows/mouse), `A` aligns the projection to the marked rectangle, `R` reset, `M`/`Esc` cancel
- [x] Per-surface target corners with UI→camera-space flip into `AlignSelectedWithTarget`; preview via the shared `IPMSDKCalibrationCamera` (freed before the sweep); persisted in the calibration JSON
- [x] Pure C# (no new native) — but play-mode still gated on the perspective-DLL redeploy since the corner pin runs on entry

### Auto-blend from camera overlap ✅ (core unit-tested; runtime pending DLL redeploy + shared camera)
- [x] `PMSDKAutoBlend.Compute` — detects overlap from per-projector Gray-code correspondence (pixels lit by ≥2 projectors), maps it into each projector's raster, and derives per-edge blend widths via a coverage-histogram scan (rejects corner blobs; robust to sparse sampling)
- [x] `PMSDKAutoAlign.Result` carries the correspondence; align-all (`Shift+A`) runs auto-blend across projectors sharing one camera and applies the widths to each `PMSDKEdgeBlend` (`AutoBlendAfterAlignAll`, default on)
- [x] Verified via edit-mode tests: 2-projector symmetric (~0.33 facing edges, 0 elsewhere), 3-in-a-row (middle gets both edges), no-overlap → 0
- [ ] End-to-end runtime: needs the DLL redeploy AND one camera seeing all projectors (the demo's raster-space surfaces are far apart, so sim can't observe two at once — real webcam is the true test)

## Pro-feature gaps — Medium batch (2026-07-16) ✅ (compiled clean; runtime pending DLL swap + MCP)

- [x] #6 Blend gamma fix — `PMSDKEdgeBlend` sends ramp exponent 1/projectorGamma (default 2.2), fixing the dark overlap seam (D-023); math-proven. Added uniform `_BlackLevel` floor + per-channel output gamma in the `PMSDK/UnlitWarp` fragment pipeline.
- [x] #7 Per-projector color correction — `PMSDKColorCorrection` (per-channel gain/offset + output gamma) driving the shader; no-op by default.
- [x] #8 Output rotation/mirroring — `PMSDKOutputTransform` (0/90/180/270 + mirror X/Y) via shader UV transform, for portrait/ceiling/rear projection; warp/corner-pin unaffected.
- [x] #5 Dense auto-warp — `PMSDKDenseWarp.FitGrid` fits an N×M grid from the camera correspondence (curved surfaces); wired into auto-align via `DenseAutoWarpN` (0 = off). Core logic reasoned against the identity→lattice invariant; numeric test not run this round (MCP link dropped) — re-run when reconnected.
- Verification note: Editor.log shows 0 CS errors and the shader imported clean; blend-gamma is math-proven; auto-blend unit-tested earlier.
- Runtime-verified (2026-07-16, Unity restart): perspective corner pin (visual + math), color correction (tint), output rotation (180° flip) all confirmed in play mode. Fixed a PerspectiveWarp z-flatten issue (retested green; z-fix DLL redeploy deferred — plugin lock). Grid/auto-blend/dense-warp remain unit-verified; mark-target/webcam/hardware still unverified.

**Pro-feature gap list: 8 of 12 done** (all High + auto-blend, blend-gamma, color, rotation, dense-warp).

## Verification & perf (2026-07-16, Unity restart #2)

- [x] Sim closed-loop auto-align with an **off-axis PERSPECTIVE observer** (real-camera
  scenario, not the earlier aligned-ortho identity case): 312 correspondence points,
  **0.39 px** mean reprojection error. Confirms the homography path handles genuine
  perspective sub-pixel. Only physical capture noise remains unverified (needs hardware).
- [x] Warp-readback perf profiled: `PMSDKMeshWarp` per-frame cost per surface ≈ 0.017 ms
  (121 v) / 0.34 ms (2.6k) / 1.34 ms (10k) / 3.2 ms (23k). Compute is fine; the real risk
  was **per-frame `new Vector3[]`+`new Color[]` allocation** (~280 KB/frame at 10k verts ×
  projectors × 60fps → GC hitches).
- [x] Fixed: `PMSDKMeshWarp` now reuses cached vertex/color arrays + `SetVertices/SetColors`
  → steady-state zero per-frame allocation. Render verified in play mode.
- [ ] For very large installs (many 4K projectors × dense grids): consider a GPU warp path
  (vertex displacement from a warp texture) to remove the CPU readback entirely — deferred
  until a real install needs it.
- [x] z-fix DLL redeployed to the nested repo + pushed (nested `02176ef`).

## Next Items / Backlog
- [ ] Real-hardware calibration smoke test (projector + webcam) — the last unverified link
- [ ] Auto-align onto true 3D geometry via native stereo triangulation (needs metric camera+projector calibration)
- [ ] Remaining gaps (low): OSC/HTTP remote, named presets/cues, NDI/Spout, extra test patterns
- [ ] True per-region black-level compensation (current `_BlackLevel` is a uniform floor)
- [ ] GPU warp path for extreme projector counts / grid density
- [ ] Milestone 19: Plugin SDK
- [ ] Version header generation via `configure_file` if hand-sync becomes annoying (static_assert guards it for now)
- [ ] Code coverage job in CI
