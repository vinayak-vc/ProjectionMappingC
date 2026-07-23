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

## Projector-pose calibration for object mapping (2026-07-16) ✅
- [x] `PMSDKPoseSolver` — managed LM reprojection solve of projector pose+FOV against Unity's camera (no native/OpenCV conversion, D-024). Closed-loop verified: noise-free = exact recovery (0 px); 0.5% marking noise = ~3 px / 0.03 m / 0.3° FOV.
- [x] `PMSDKProjectorPoseCalibrator` — workflow component (F3): place anchors on the twin, mark each in the output, Space to solve + apply to the projector camera. Wired into `ObjectMappingDemo` (9 anchors on the monument).
- This is the enabler for real-world object mapping: align the virtual twin's projector to the physical projector, then fine-tune with corner-pin/grid warp.

## Demo scenes (2026-07-16)
- [x] `ObjectMappingDemo` — object mapping / spatial AR: projector cam renders a virtual
  twin of a ProBuilder monument directly to its display (content wraps the object faces).
  `PMSDKMappingContentAnimator` drives the mapping content; scene self-contained in the
  nested repo. Verified in play mode. Shows the "true 3D mapping" differentiator.

## Quality + reach batch (2026-07-17) ✅

- [x] Fixed long-red `DecoderTests.DecodeAndTriangulate` — exposed a REAL SDK bug: `cv::triangulatePoints` output depth follows the input points (CV_32F), was read as double → garbage w → always-empty cloud. Plus degenerate test geometry (parallel rays) replaced with convergent geometry (z=10 assertion). Native suite 133/133.
- [x] Unity Test Framework regression suite (`vxpmsdk.Tests.Editor`, 21 tests green): homography, gray decode (+inverse/albedo/shadow), auto-blend (2/3-projector/no-overlap), dense-warp, pose solver, calibration IO, presets, OSC codec. Host project needs the package in manifest `testables`.
- [x] Named presets/cues (gap #10): `pmsdk_preset_<name>.json` siblings of the calibration file; manager `SavePreset/LoadPreset/ListPresets` + `ToggleAB` (pre-load snapshot swap). Keys: Ctrl+1..4 load, Ctrl+Shift+1..4 save, V = A/B. Live-verified (load restores, A/B flips both ways).
- [x] OSC remote (gap #9): dependency-free OSC 1.0 codec (`PMSDKOsc`, unit-tested) + `PMSDKOscServer` UDP listener (background thread → main-thread dispatch) covering calibration/surface/corner/blend/test-pattern/auto-align/presets/AB. Loopback-verified: 5/5 packets received and applied.
- [x] ObjectMappingDemo second projector: `PMSDK_Projector_B` (side angle, Display 3) with its own pose calibrator sharing the 9 anchors — two-angle coverage verified in play mode.

## Final two Low gaps (2026-07-17) ✅

- [x] #12 Test patterns: `PMSDKTestPatterns` static rasterizers — Checkerboard (+overlays), FocusGrid (1-px lattice + diagonals), Convergence (9 crosshair+circle targets), SMPTE-style ColorBars (75% bars + W/K/R/G/B strip), GrayRamp (continuous + 16-step wedge), solids (W/K/R/G/B). `Y`/`Shift+Y` cycles in calibration mode; 6 deterministic pixel tests; ColorBars verified visually on the projector output.
- [x] #11 NDI/Spout interop: `PMSDKExternalContent` (feeds any external texture into all warp surfaces in place of the content RT, per-projector slicing/blend/color intact — verified live: swap + restore). Adapters `PMSDKSpoutIn`/`PMSDKNdiIn` bridge KlakSpout/KlakNDI receivers, in separate asmdefs compile-gated by versionDefines (`jp.keijiro.klak.spout`/`jp.keijiro.klak.ndi`) — zero hard dependency; NOT compiled/tested against the real Klak packages here (none installed). Output side: attach a Klak sender to the projector camera directly.

Pro-feature gap list: **12 of 12 implemented** (caveats: Klak adapters untested without the packages; camera path still needs real hardware).

## Real-hardware two-projector wall blend — VERIFIED (2026-07-17) ✅

The physical projector + webcam loop — the roadmap's last unverified link — ran
successfully on real hardware: two ultra-short-throw 1080p projectors side by side on
the floor, both aimed at one wall with ~15% physical overlap, observed by a Logitech
C270 (640×480 via the native `pmsdk_decoder_*` capture path) from ~4 m back, room dark.
Entirely editor-driven (no build): fullscreen Game-view popups on the projector
displays, `ProBuilderMappingDemo` content.

- [x] End result: single continuous canvas across both projectors; corner-pin fit
  **RMS 0.55 px (left) / 0.57 px (right)** on the 128-px calibration raster
  (1689/3542 and 1452/2420 RANSAC inliers); blend bands coincide by construction;
  animated content crosses the seam without doubling. Calibration persisted to JSON.
- [x] Real-world failure modes found and fixed (none of these exist in simulation —
  details in `docs/ai_handoff.md` and D-025):
  1. **Light spill** (the fit killer): UST projectors light the floor/ceiling; that
     spill decodes into VALID correspondences lying on other planes, and the plain
     least-squares homography inside `PMSDKAutoAlign` is poisoned by them (observed
     reproj 100–2700 px). Fixed with a RANSAC consensus fit (~45–55% inlier rate).
  2. **Frame-based settle is too short on real chains**: projector input lag + webcam
     exposure + USB buffering total hundreds of ms; captures landed on stale patterns
     (intermittent garbage decodes). `SettleFrames=45` + `flushFrames=5` works at
     editor frame rates; a time-based settle in `PMSDKAutoAlign` would be sturdier.
  3. **Webcam auto-exposure vs white/black references**: AE partially equalizes the
     two reference captures (mask starvation with lights on — 47 valid points).
     Lights off is mandatory; exposure lock via the C API would remove the caveat.
  4. **`StartAutoAlign(all)` does not produce a shared canvas**: null-target align
     only recovers each projector's own footprint. Two projectors on one wall need
     slice targets on a common canvas (see `PMSDKWallCanvasAlign` below).
  5. During any sweep the OTHER projector must be blanked and animated content frozen
     (its light modulates the overlap mid-sweep), and overlay badges hidden (they
     project on top of the patterns).
- [x] New game-repo tooling (nested repo `ProjectionMapping-unity/Scripts/`):
  `PMSDKWallCanvasAlign` — F4 hotkey; one Gray-code sweep per projector (other one
  blanked, physics frozen, overlays hidden) → RANSAC camera→projector fits → shared
  canvas from the outer corners of the two observed quads → each projector corner-
  pinned onto its content slice (slice bounds read from the split-slice material
  scale/offset) → analytic blend widths (overlap/slice) → `SaveNow()`.
  `PMSDKFullscreenPreview` (editor) — borderless Game view per non-primary monitor
  via `GameView.showToolbar=false`, left-to-right → Display 2, 3.
- [ ] Upstream candidates: RANSAC option inside `PMSDKAutoAlign`; time-based settle;
  `pmsdk_decoder_set_property` (exposure lock); shared-canvas multi-projector align in
  the package instead of the game repo.

## Canvas-space reference pattern (2026-07-17) ✅

- [x] `PMSDKCanvasReferencePattern` — end-to-end plus + level/vertical reference lines rasterized in SHARED canvas space, fed through `PMSDKExternalContent` so every projector shows its warped slice: lines land straight and seam-continuous on the physical wall even with tilted projectors (addresses the on-wall "horizontal line isn't horizontal" problem from the hardware run). `C` toggles in calibration mode. Deterministic rasterizer test (28/28 suite). Verified in play: skewed pin → pattern pre-warps in raster (i.e. straight on wall); centre plus falls in the overlap of both slices.
- Note: level is relative to the canvas; if the auto-align camera was tilted, verify once against a laser level / chalk line.

## Milestone — per-region black-level compensation (2026-07-21) ✅ IMPLEMENTED

Built (Unity binding; additive twin of the luminance milestone below). See D-028.
Fixes the bright, slightly-yellow band over the projector overlap on DARK content
(doubled projector-black; a multiply cannot touch an additive floor).
- [x] Retain the sweep's all-black capture on `PMSDKAutoAlign.Result.Black`.
- [x] `PMSDKBlackLevelCompensation.Compute` — pure core: camera-space litCount + summed
      floor, high-percentile uplift target, per-projector `deficit/litCount` → signal lift
      via each projector's black→white span, scatter to raster, hole-fill + box-blur + clamp.
- [x] `PMSDKBlackLevelLift` component drives `_BlackLiftTex` / `_UseBlackLift`.
- [x] `PMSDK/UnlitWarp`: uplift `c = c·(1−lift)+lift` at the black-level stage, raster UV1.
- [x] `PMSDKMeshWarp` writes UV1 when a gain OR a black-lift map is active.
- [x] Persist per-surface, quantized + base64 (`PMSDKGainCodec`), in the calibration JSON.
- [x] Manager: `AutoBlackLevelAfterAlignAll` (opt-in) runs it after Shift+A; save/restore.
- [x] EditMode tests: single-region lift-to-overlap, no-lift on uniform full overlap,
      clamp, empty/missing-safe. Core also validated by a standalone compile-and-run harness.
- Residual floor tint left to a per-projector `PMSDKColorCorrection` offset (RGB capture future).

## Milestone — camera-measured luminance compensation (2026-07-21) ✅ IMPLEMENTED

Built (Unity binding; no native change — apply is a shader multiply). See D-027.
- [x] Retain the sweep's all-white capture on `PMSDKAutoAlign.Result.White`.
- [x] `PMSDKLuminanceCompensation.Compute` — pure core: raster luminance from correspondence,
      hole-fill + box-blur, global robust-min target, `gain = clamp(target/measured, gainMin, 1)`.
- [x] `PMSDKLuminanceGain` component drives `_GainTex` / `_UseGainTex`.
- [x] `PMSDK/UnlitWarp`: gain multiply after the blend ramp, sampled by raster UV1.
- [x] `PMSDKMeshWarp` writes UV1 (warped raster position) only while a gain map is active.
- [x] Persist per-surface, quantized + base64 (`PMSDKGainCodec`), in the calibration JSON.
- [x] Manager: `AutoLuminanceAfterAlignAll` (opt-in) runs it after Shift+A; save/restore.
- [x] EditMode tests: flatten-to-target, cross-projector equalize, floor clamp, hole-fill, codec.

Original plan (retained for context):

Why: projectors are brightest at centre and dimmer at the edges (vignetting); a blend
overlap is built from both projectors' dimmest edges, so even a perfect alpha ramp leaves
a visible band (observed on the real wall 2026-07-17). Hand-tuning gamma cannot fix a
spatial brightness difference — it must be measured.

Plan:
1. During the F4/auto-align sweep, keep each projector's all-WHITE capture (already
   taken for the shadow mask — measurement data is free) as a per-projector wall
   luminance map, warped into that projector's raster space via the fitted homography.
2. Normalize: target luminance = min envelope across the canvas (compensation can only
   dim, never add light); derive a per-projector gain map = target / measured.
3. Apply as a small gain texture sampled in `PMSDK/UnlitWarp` (multiply after the blend
   ramp, before colour correct) — reuse the vertex-colour or a dedicated `_GainTex`.
4. Smooth/lowpass the map (webcam noise), clamp gain range (e.g. 0.6..1.0), persist it
   in the calibration JSON per surface.
5. Scope: fixes bright-content banding/unevenness. Explicitly does NOT fix the doubled
   projector-black glow on dark content — that is the separate per-region black-level
   item below.

Pairs naturally with the same branch: RANSAC upstreaming into `PMSDKAutoAlign`,
time-based settle, C-API exposure lock (D-025 upstream list), and N-projector
generalization of the wall-canvas align.

## Blended stereo (SBS 3D) + content-UI canvas (2026-07-17) — nested `SBS` branch

Built and editor-verified; on-wall glasses test pending. DLP-Link cross-projector sync
confirmed stable in the overlap beforehand (the go/no-go gate). Architecture: D-026.

- [x] `PMSDKStereoContentRig` (Content Camera) — two eye textures: `SceneCameras` (parallel
  L/R pair, asymmetric off-axis frusta, zero-parallax at the wall) or `SbsTexture` (halves
  of an SBS video RT). `EyeSeparation`/`ZeroParallaxDistance` tuning; F6 stereo toggle, F7
  source toggle; re-parks screen-space content canvases to the zero-parallax plane in stereo.
- [x] `PMSDKStereoComposer` (per projector) — renders the existing warp surface once per eye
  (MaterialPropertyBlock texture swap, slice scale/offset composed in) and packs into an SBS
  frame via `PMSDK/StereoPack`. Calibration → mono passthrough; sweep/test-pattern packed
  into both halves so F4 runs with 3D SBS mode left on.
- [x] `StereoMappingDemo` scene — calibrated ProBuilderMappingDemo clone (shares the
  calibration JSON) + VideoPlayer→`SBS_Video_RT` (clip slot left empty) + depth objects at
  3/4.5/9 m. Content-UI canvas (`ContentUI` layer, Content-Camera-only) added to both demo
  scenes; fixed a stale grid warp on Left_Screen that the straight title bar exposed.
- [x] Editor-verified: 0.00 px disparity at zero-parallax, −18.7 px crossed at 2 m; both
  eyes warped+blended+packed; clean mono round-trip; console clean.
- [ ] On-wall glasses test (projectors in 3D SBS mode) — the gate before merge to main.
  If depth inverts, swap `_LeftTex`/`_RightTex` in the composer.
- [ ] Upstream candidate: move the stereo composer/rig into the package (currently game-side).

## HoloTrackSDK — Head-Tracked Holographic Projection (independent DLL, 2026-07-21)

Separate product/DLL (D-029). Branch `Head-Tracked-Holographic-Projection-System`. No OAK
hardware on this machine → hardware-free first (D-032). See `docs/holotrack-architecture.md`.

- [x] H1 Docs — D-029..D-032, roadmap entry, architecture doc.
- [x] H2 Core pure logic (all header-only except the Tracker): `Detection`/`PoseKeypoints`,
      `TrackedViewer`/`TrackingState`, `TrackerConfig` (+`CalibrationTransform`), `IHeadFilter`
      + `PassThrough`/`Exponential`/`OneEuro`/`Kalman` + `MakeFilter`, `TrackingStateMachine`,
      `ViewerSelector` (nearest/largest-box, depth gate, hysteresis, stable id, no viewer-hop),
      `HeadEstimator` (pose eyes→nose→neck, else bbox+depth+height), `CoordinateTransform`
      (OAK→world T/R/S + inverse), `OffAxisProjection` (Kooima generalized perspective),
      `Tracker` orchestrator (select→estimate→filter→state→transform→off-axis, velocity +
      prediction extrapolation). `IDetectionSource` interface defined.
- [x] H2 verification — standalone `holotrack_harness` (no gtest dependency): **161/161 checks,
      exit 0**. GoogleTest suite authored too, gated behind `HOLOTRACK_BUILD_GTESTS` (OFF)
      pending a gtest-matching MSVC toolset (prebuilt vcpkg gtest pulls unresolved `__std_rotate`
      under the local link toolset; the pure test target must not drag OpenCV to mask it).
- [x] H3 C-API (`HoloTrack/C_API/Types.h` + `TrackingAPI.h/.cpp`) — opaque `ht_tracker_t`,
      create/destroy/set-config/get-config/push-frame/get-viewer/compute-offaxis/reset +
      version + thread-local last-error; flat POD structs. Build target `holotrack` →
      `HoloTrackSDK.dll` (10 `ht_*` exports confirmed via dumpbin). C boundary smoke-tested in
      the harness.
- [ ] H4 OAK device source behind DepthAI support (spatial MobileNet-SSD, bg thread,
      `IDetectionSource`) + a `SimulatedSource`/recorded source. Live camera is now present;
      smoke test still needs a C++ DepthAI package and model blobs.
- [x] H5 Unity package `com.viitorx.holotrack` AUTHORED (`bindings/unity/com.viitorx.holotrack`):
      `HoloTrackNative` (P/Invoke + blittable structs/enums, lib `HoloTrackSDK`), `PMHTHeadTracker`
      (owns native handle, pumps `IHeadTrackingSource`, zero per-frame alloc), `PMHTSimulatedSource`
      (proxy-transform head, no hardware), `HeadTrackingConfig` ScriptableObject (+`ToNative`),
      `HeadTrackingDisplaySurface`, `HeadTrackedCameraController` (off-axis matrices +
      movementScale/deadZone/max-travel), `HeadTrackingDiagnostics` (overlay + gizmos),
      `HeadTrackingRecorder` (CSV), asmdef `vxholotrack`, package.json, README.
      NOT yet compiled in Unity (no editor session here) — that happens in H6. Known follow-up:
      `HtOffAxis` marshals two float[16]/call (per-frame GC); make blittable/unsafe if it matters.
- [x] H6 Consumer sample in the nested game repo (committed there, `dc18b10`): base project's
      `Packages/manifest.json` references `com.viitorx.holotrack` (file:), `HoloTrackSDK.dll`
      deployed to `Plugins/HoloTrack/` (force-added past the global *.dll ignore, like the PMSDK
      DLL). Scene `HoloTrackDemo` (surface + off-axis camera + sim source + head proxy + 3 depth
      cubes + diagnostics + config asset). Package compiles clean in Unity (0 errors).
      **Play-mode verified**: head at centre → surface NDC.x 0.000, near cube 0.600, far 0.000;
      head +0.8 x → surface stays 0.000 (window anchored), near→0.200, far→0.400 (near/far shift
      OPPOSITE directions) = correct holographic motion parallax. No view-handedness flip needed.
      Gotchas hit: MCP link drops on domain reload (reconnects); `manage_editor(play)` started
      PAUSED so the sim head looked frozen until `EditorApplication.isPaused=false` (AGENTS
      gotcha). MCP screenshot returns white for this scene (built-in pipeline capture quirk —
      reproduces with the default camera too, unrelated to HoloTrack); verification was done
      numerically via NDC projection, which is stronger than eyeballing.
- [x] H4 OAK/DepthAI device source CODE done (live test deferred — no camera here):
      `holotrack::OakDevice` (`include/HoloTrack/Tracking/Device/OakDevice.h`,
      `holotrack/src/Tracking/Device/OakDevice.cpp`) — PImpl `IDetectionSource`; DepthAI pipeline
      (ColorCamera 300² preview + MonoL/R → StereoDepth → `MobileNetSpatialDetectionNetwork`,
      depth-aligned) on a background thread, spatial detections → metres with +Y up. Compiled
      only under `HOLOTRACK_HAVE_DEPTHAI`; otherwise an inert stub (Start fails cleanly), so the
      default DLL is camera-free. Device C-API `HoloTrack/C_API/DeviceAPI.{h,cpp}` (opaque
      `ht_oak_source_t`, is_supported/create/destroy/start/stop/is_running/poll/last_error — 8
      exports, dumpbin-confirmed). Unity `PMHTOakSource` (`IHeadTrackingSource` over `ht_oak_*`,
      same push path as the sim source; warns + no-ops when the DLL lacks DepthAI). CMake option
      `HOLOTRACK_WITH_DEPTHAI` (OFF default) consumes an external `depthai` CMake package by
      default; vcpkg resolution is opt-in via `HOLOTRACK_DEPTHAI_USE_VCPKG` (D-033).
      Verified: default (feature-OFF) DLL builds; harness 180/180 incl. device C-API stub checks;
      Unity C# compiles clean (0 errors). Targets depthai-core v2.x.
- [ ] H4 live: install Luxonis DepthAI C++ (`depthaiConfig.cmake` / `depthai::core`), configure
      with `HOLOTRACK_WITH_DEPTHAI=ON` and `CMAKE_PREFIX_PATH` pointing at that install, supply
      person/face `.blob` files, rebuild + redeploy the DLL (Unity closed — plugin lock), close
      OAK Viewer for exclusive device access, add a `PMHTOakSource` to the scene (replacing the
      sim source), smoke-test with `holotrack_oak_smoke` first and Unity second.
- [ ] H4 first live hologram pass: use **Person** detection mode first, with only a MobileNet-SSD
      300x300 person `.blob`. Do not start with `FaceThenPerson`; add the face blob/mode only
      after person-only proves the DepthAI build, native smoke executable, Unity P/Invoke,
      `PMHTOakSource`, and `PMHTHeadTracker` path.

## HoloTrack H7 — head-tracked SBS-3D integration (2026-07-22)

Design/brief: `docs/holotrack-stereo-integration.md`. SDK-side + game-side rig code DONE; the
ProBuilder scene and live stereo verification are DEFERRED (Unity MCP was disconnected).

- [x] Native stateless `ht_compute_offaxis_eye(pa,pb,pc,eye,near,far,out)` (arbitrary eye, no
      tracker handle) in `TrackingAPI.{h,cpp}`; harness checks it (lateral eye shift skews the
      frustum; eye-on-plane degenerate; null guard). Build feature-OFF green, **harness 180/180**.
- [x] P/Invoke `ht_compute_offaxis_eye` in `HoloTrackNative.cs` + `PMHTHeadTracker.TryComputeOffAxis(
      bl,br,tl,eye,near,far,...)` eye overload.
- [x] Generic package component `HeadTrackedStereoController` — head ± IPD/2 along the display
      right axis → two off-axis frusta on two assigned eye cameras; `ApplySafety`, `swapEyes`,
      rest-eye, `RestoreDefaultProjection`.
- [x] Detection modes: `holotrack::DetectionMode {Person,Face,FaceThenPerson}` (+ `faceBlobPath`,
      `faceFallbackFrames`) in `OakOptions`/`ht_oak_options_t`; `OakDevice` builds face and/or
      person spatial nets on a shared preview+depth and, in FaceThenPerson, prefers the face
      (emitted with its centre as a nose keypoint so `HeadEstimator` uses it directly), falling
      back to the person box after N faceless frames. Stub path intact; feature-gated (untested —
      no DepthAI/hardware). Exposed on `PMHTOakSource`.
- [x] Game-side rig hook (nested repo): `PMSDKStereoContentRig.ExternalEyeMatrices` flag +
      `LeftEyeCamera`/`RightEyeCamera` getters + `EnsureEyeCameras()`; `UpdateEye` skips its own
      shear when driven externally.
- [x] **DONE + play-mode verified (2026-07-22)**: `HoloStereoDioramaDemo` scene (nested repo
      `Scenes/`) built per brief §C — display surface (2.0×1.2, z=0) + rest eye (0,0,+2),
      recessed box as 5 inward walls (robust fallback vs ProBuilder face-flipping), 4-bar bezel,
      3 depth props (z −1.05/−0.55/−0.15), pop-out hero (z +0.25). Wiring: added clean
      `HeadTrackedStereoController.SetEyeCameras(l,r)` API + game-side `PMSDKStereoHeadTrackBinder`
      (rig `SceneCameras`+`ExternalEyeMatrices`+`EnsureEyeCameras`, then binds the rig's eye pair
      to the controller). **Root cause found**: the deployed `HoloTrackSDK.dll` was the pre-H7
      build (missing `ht_compute_offaxis_eye` → EntryPointNotFound); rebuilt feature-OFF
      standalone (no vcpkg/OpenCV needed — 4 files) with all 19 `ht_` exports and redeployed.
      Verified numerically: stereo disparity 0 at window, +far > +near behind, −hero in front
      (crossed) — correct sign, no `swapEyes` flip; motion parallax window-anchored (Δ0), behind
      props shift right (far ≫ near), pop-out shifts opposite; IPD offset exactly 0.06. Console
      clean.
- [x] **Unified full-stack scene DONE + play-mode verified (2026-07-23)**: `HoloProjectionStereoDemo`
      (nested repo) — the first scene combining **projection mapping + SBS 3D + head-tracking**.
      Head-tracked off-axis stereo (HoloTrack drives the rig's eye cameras) flows through the
      two-projector `PMSDKStereoComposer` warp/blend and is packed SBS for 3D projectors — HoloTrack =
      *what* each eye sees, PM = *where* it lands. Built from `StereoMappingDemo`'s projection+SBS
      rig + `HoloStereoDioramaDemo`'s head-track layer (framed diorama, sim head source; OAK a
      one-flag swap). Enabler: `PMSDKStereoHeadTrackBinder.DirectScreenSbs` is now serialized
      (true=direct-to-display, default; false=through the composer — mutually exclusive paths).
      Verified: IsStereoReady + both composers active, 0.06 IPD; projector captures show
      split-sliced/warped/blended/SBS-packed head-tracked content; window-anchored parallax,
      depth-ordered + crossed disparity. Nested commit `c09b4a0`.
- [ ] H4 face/person DepthAI live path still unverified, but the missing-vcpkg-port blocker is
      removed (2026-07-22, D-033): `HOLOTRACK_WITH_DEPTHAI=ON` now consumes an externally
      installed Luxonis `depthai` CMake package by default; vcpkg is opt-in via
      `HOLOTRACK_DEPTHAI_USE_VCPKG=ON` only for machines with a working custom/updated port.
      Added `holotrack_oak_smoke` for hardware polling. Current machine evidence:
      OAK Viewer sees `OAK-D-PRO-W-97` / MXID `14442C10F143D3D200`; feature-OFF build +
      harness green; live test still needs a C++ DepthAI install (`depthaiConfig.cmake`),
      person/face `.blob` files, and exclusive camera access (close OAK Viewer).

Build/run: `cmake --preset vs2022 && cmake --build build/vs2022 --config Release --target
holotrack holotrack_harness`, then run `build/vs2022/bin/Release/holotrack_harness.exe`.

## Depth-camera 3D / curved-surface mapping (SCOPED — not started, 2026-07-21)

A proper feature project, not a camera swap. Reference device on hand: **Luxonis
OAK-D-PRO-W** (stereo depth + wide FOV). Decision + rationale: **D-034**.

**Why / when it's worth it.** The current auto-align is a 2D planar homography: it needs
only a flat grayscale image of the wall and is already sub-pixel (0.55 px, hardware-run
2026-07-17). On a flat wall the result is limited by projector focus / optics / light
spill — NOT by the camera — so a depth camera does **not** improve the flat-wall blend.
Depth becomes valuable only for problems the current pipeline cannot do:
- **Curved / irregular walls** — reconstruct the actual surface and warp to it, instead
  of hand-gridding in G-mode.
- **Object mapping without a hand-built virtual twin** — scan the real geometry directly
  (today F3 requires modelling the object in Unity first).

**Explicit non-goal:** do NOT add this expecting better numbers on the flat wall. The one
real flat-wall pain point (webcam auto-exposure forcing lights-off) is exposure *control*,
not depth — solved more cheaply by the C-API exposure-lock item (D-025 follow-up), not by
the OAK.

**Hardware note (verified 2026-07-21):** the OAK-D enumerates as `Movidius MyriadX`
(DepthAI/XLink), NOT a UVC webcam, so the current OpenCV `pmsdk_decoder` cannot open it
(probed indices 0–6, all fail). Two ways in: (a) run it in Luxonis UVC mode → appears as a
plain 2D webcam our existing path opens (gains nothing over the C270 — depth unused);
(b) native DepthAI capture source (below), the only way to use its depth.

**Scope (subtasks):**
- [ ] DepthAI capture source: `IPMSDKCalibrationCamera` (or a depth-capable sibling
  interface) backed by the `depthai` runtime over XLink — mono/RGB frames for decode +
  the metric depth map. New third-party dep (`depthai`); keep it behind an optional
  assembly/define so non-depth builds don't require it.
- [ ] Metric camera + projector calibration: camera intrinsics (OAK ships factory
  intrinsics) + projector intrinsics/extrinsics, so triangulation is metric.
- [ ] Wire the native structured-light **triangulation** path into Unity
  (`pmsdk_decoder_decode_and_triangulate` — exists in native, fixed 2026-07-17 CV_32F
  bug, but not surfaced in the Unity flow) → dense 3D correspondence cloud.
- [ ] 3D → warp: fit each projector's warp (dense grid / mesh) to the reconstructed
  surface instead of a planar homography; reuse `PMSDKDenseWarp.FitGrid` where possible.
- [ ] Workflows: curved-wall auto-map and object-scan auto-map (twin-free), analogous to
  the F4 flat-wall flow.
- [ ] Blend/luminance on non-planar surfaces (overlap width varies along a curved seam —
  the measured `PMSDKAutoBlend`/luminance paths already suit this better than the analytic
  constant band).

**Effort:** multi-day; treat as its own milestone. Overlaps the existing backlog item
"auto-align onto true 3D geometry via native stereo triangulation" — this supersedes and
scopes it, with the OAK-D as the reference sensor.

## Next Items / Backlog
- [x] Camera-measured luminance compensation (implemented 2026-07-21 — see milestone above, D-027)
- [ ] Install KlakSpout in a host project and loopback-verify the PMSDKSpoutIn adapter
- [ ] Depth-camera 3D / curved-surface mapping — see the scoped milestone above (OAK-D reference device, D-034)
- [x] True per-region black-level compensation (implemented 2026-07-21 — see milestone above, D-028; `_BlackLevel` uniform floor retained as a manual override)
- [ ] GPU warp path for extreme projector counts / grid density
- [ ] Milestone 19: Plugin SDK
- [ ] Version header generation via `configure_file` if hand-sync becomes annoying (static_assert guards it for now)
- [ ] Code coverage job in CI
