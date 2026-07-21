# AI Agent Handoff Document

## HoloTrackSDK — NEW independent product (2026-07-21)

Branch `Head-Tracked-Holographic-Projection-System` (off `main`). Building the **Head-Tracked
Holographic Projection System** as a **separate DLL** — `HoloTrackSDK.dll`, namespace
`holotrack`, no runtime dependency on `ProjectionMappingSDK` (D-029). OAK-D-PRO-W-97 →
single-viewer head tracking → off-axis generalized-perspective render (D-030) that feeds a
content camera; projection mapping is composed downstream in the consumer scene, not by linking
the two DLLs. Design: `docs/holotrack-architecture.md`; decisions D-029..D-032; plan/status in
`docs/roadmap.md` + `docs/tasks.md` (HoloTrack sections).

**Done (H1–H3), verified with no camera:**
- C++ core in `include/HoloTrack/**` + `holotrack/src/**`: filters (Exp/OneEuro/Kalman),
  Searching→Tracking→Prediction→Lost state machine, viewer selector (nearest/largest-box +
  depth gate + switch hysteresis + stable id + no viewer-hop on transient loss), head estimator
  (pose keypoints → bbox+depth+body-height fallback), OAK→world transform, Kooima off-axis
  projection, and the `Tracker` orchestrator (velocity + prediction extrapolation).
- C-API `include/HoloTrack/C_API/**` → `HoloTrackSDK.dll` with 10 `ht_*` exports (dumpbin-
  confirmed). Independent CMake target wired via top-level `add_subdirectory(holotrack)`
  (`HOLOTRACK_BUILD`, default ON).
- Verification: `holotrack_harness` (dependency-free assertion exe) **161/161, exit 0**. A
  GoogleTest suite is also authored but gated behind `HOLOTRACK_BUILD_GTESTS` (default OFF):
  the prebuilt vcpkg gtest was built against a newer MSVC STL than the local link toolset, so
  it needs `__std_rotate` which only resolves when a heavy lib (OpenCV) drags in the newer CRT —
  the pure test target must not depend on that, hence the harness is the primary verifier.
  Enable the gtest suite once the build/gtest toolsets are aligned.

**H5 Unity package AUTHORED** (`bindings/unity/com.viitorx.holotrack`, asmdef `vxholotrack`):
`HoloTrackNative` (P/Invoke, lib `HoloTrackSDK`), `PMHTHeadTracker` (owns handle, pumps
`IHeadTrackingSource`, zero per-frame alloc), `PMHTSimulatedSource` (proxy transform — no
hardware), `HeadTrackingConfig` SO, `HeadTrackingDisplaySurface`, `HeadTrackedCameraController`
(off-axis + safety layer), `HeadTrackingDiagnostics`, `HeadTrackingRecorder`, README. NOT
compiled in Unity yet (no editor here) — that is H6.

**H6 DONE — consumer sample verified live** (nested game repo `main`, commit `dc18b10`): base
project `Packages/manifest.json` references `com.viitorx.holotrack` (file:), `HoloTrackSDK.dll`
in `Plugins/HoloTrack/` (force-added past the global `*.dll` ignore). Scene
`HoloTrackDemo` (display surface + off-axis camera + sim source + head proxy + 3 depth cubes +
diagnostics). Package compiled clean. Play-mode parallax verified numerically: head +0.8 x →
on-plane surface stays NDC.x 0.000 (window anchored) while near cube 0.600→0.200 and far cube
0.000→0.400 move OPPOSITE ways = correct off-axis hologram. No view-handedness flip needed.
Gotchas: MCP drops on domain reload (reconnects); play mode started PAUSED (clear
`EditorApplication.isPaused`); MCP screenshot renders white for this scene (built-in-pipeline
capture quirk, reproduces with the default camera — unrelated to HoloTrack; verify numerically).

**Next: H4** — OAK device source behind a vcpkg `depthai` feature (spatial MobileNet-SSD,
background thread implementing `IDetectionSource`) + a recorded source; live test needs an
OAK-D-PRO-W-97 attached. Everything above H4 is complete and independently usable now.

Build/verify: `cmake --preset vs2022 && cmake --build build/vs2022 --config Release --target
holotrack holotrack_harness`, then `build/vs2022/bin/Release/holotrack_harness.exe`.

## Current State (2026-07-16)

New machine / new agent: see `docs/dev-setup.md` for environment setup; repo layout and
gotchas are in `AGENTS.md`. Everything needed to continue is in git (this repo + the nested
game repo).

**Last Completed**: Milestones 1-18 + 20; Unity raster-space rig; on-site calibration
P1-P3; robust structured-light decode + webcam path; **pro-feature gap list 8/12** (all
High + all Medium: perspective corner pin, N×M grid UI, auto-blend, blend-gamma fix, color
correction, output rotation/mirror, dense auto-warp) + mark-target UI.
**Runtime-verified** (play mode): perspective corner pin, color correction, output rotation.
**Unit/ math-verified**: grid, auto-blend, dense-warp, blend-gamma, Gray-code decode.
**NOT verified**: mark-target runtime, per-frame warp-readback perf at scale.
**HARDWARE-VERIFIED (2026-07-17)**: the real projector+webcam loop — two UST projectors
blended on one wall, RMS 0.55/0.57 px, editor-driven, calibration persisted. See
"Real-hardware run" section below for the workflow and the four real-world gotchas.
**BUILT, EDITOR-VERIFIED, glasses-test pending (2026-07-17)**: blended stereo (SBS 3D) on
the two-projector wall + a `ContentUI` screen-space canvas that reaches the wall. Both are
game-side on the nested repo's `SBS` branch (unmerged). See "Stereo (SBS 3D)" and
"Content UI on the wall" sections below.
**Current Task**: Milestone 19 (Plugin SDK = UE5 bindings) is the main open track (user:
"UE will do later"). Latest batch (2026-07-17): fixed the long-red triangulation test
(real SDK bug — CV_32F read as double), Unity Test Framework suite 21/21 green, named
presets + A/B (live-verified), OSC remote (loopback-verified 5/5), ObjectMappingDemo
second projector (two-angle coverage). Native suite 133/133; Unity suite 27/27.
**Pro-feature gap list 12/12 implemented** (2026-07-17: #12 pattern set —
focus/convergence/SMPTE-bars/ramp/solids, Y cycles, pixel-tested + bars verified on
output; #11 interop — PMSDKExternalContent verified live, Klak Spout/NDI adapters
compile-gated via versionDefines but NOT tested against the real packages here).
Remaining elsewhere: Klak adapter loopback once a host installs KlakSpout, GPU warp path
for extreme scale, per-region black-level, and upstreaming the D-025 hardware findings
(RANSAC consensus fit into `PMSDKAutoAlign`, time-based settle, C-API exposure lock).
**Camera-measured luminance compensation — IMPLEMENTED 2026-07-21 (D-027).** Per-projector
wall luminance maps from the sweep's all-white captures (`PMSDKAutoAlign.Result.White`) →
`PMSDKLuminanceCompensation.Compute` (pure/testable) → per-projector `_GainTex` multiplied
in `PMSDK/UnlitWarp` in raster space (UV1), so the blend seam disappears on bright content.
Opt-in: `PMSDKCalibrationManager.AutoLuminanceAfterAlignAll` (default OFF) runs it after
Shift+A; persisted quantized+base64 in the calibration JSON (`PMSDKGainCodec`); revert by
disabling a surface's `PMSDKLuminanceGain`. EditMode tests in PMSDKCalibrationLogicTests.
Verify on real hardware next (was validated only in code + unit tests). Still open:
per-region black-level (doubled projector-black on dark content), D-025 upstreaming, and
N-projector wall-canvas generalization — all natural for the same branch.

## Machine note (2026-07-17) — current layout on this machine

Paths in older sections and AGENTS.md refer to the previous machine. Current layout:

- SDK repo: `C:/Unity/ProjectionMappingC`, branch `main` (PR #5 merged `release-management`).
- Unity project: `C:/Unity/Multi Projector` (template — never commit).
- Nested game repo: `C:/Unity/Multi Projector/Assets/Games/ProjectionMapping-unity`, branch `main`.
- Package reference: `Packages/manifest.json` now points `com.viitorx.pmsdk` at THIS
  repo's source (`file:C:/Unity/ProjectionMappingC/bindings/unity/com.viitorx.pmsdk`) —
  package edits reach Unity directly. (It briefly pointed at the v1.0.3 release archive
  right after the machine move; that gotcha is resolved.)
- vcpkg submodule arrived broken after the copy (missing `.git/modules`, dangling gitdir;
  `git status` failed). Fixed 2026-07-17: re-init in place, shallow fetch of the pinned
  `97b19ca`, then `git submodule absorbgitdirs`.
- **Demo assets**: the canonical `PMSDKDemo/` now lives inside the nested repo with the
  original GUIDs (the demo scenes reference them) and the newer UnlitWarp material
  properties. A stale duplicate at root `Assets/PMSDKDemo` had caused Unity to regenerate
  GUIDs on the nested copy (meta churn); it was deleted. Re-running the demo generator
  recreates root `Assets/PMSDKDemo` with fresh GUIDs — harmless, but never commit that
  copy into the nested repo over the canonical one.
- `ProBuilderMappingDemo` scene: commit `02176ef` had accidentally regenerated the scene
  WITHOUT the ProBuilder stage; restored 2026-07-17 from `3512932` (PB_Stage +
  5 embedded pb_Meshes) — nested commit `1d57868`.

## Real-hardware run (2026-07-17) — two-projector wall blend, editor-driven

First physical validation of the whole calibration stack. Setup: two ultra-short-throw
1080p projectors on the floor aiming at one wall (~15% physical overlap, Windows
extended desktop, both forced to 1920×1080 — at their native 1920×1200 the 16:9 raster
gets edge-cropped), Logitech C270 as observer ~4 m back facing the wall, room dark.
No build needed: `Tools > Projection Mapping > Fullscreen Previews > Open` puts a
borderless Game view on each projector (`GameView.showToolbar=false` internally — do
NOT try y-offset tricks, the window manager clamps popups back onto the screen).

**Result**: one continuous canvas, corner-pin RMS 0.55 px (left) / 0.57 px (right) on
the 128-px raster, blend bands coincident by construction, calibration saved to
`%USERPROFILE%/AppData/LocalLow/ViitorCloud/Projection mapping/pmsdk_calibration.json`.

**Operator workflow (repeatable without an agent)**: play `ProBuilderMappingDemo`,
open the fullscreen previews, place the webcam so it sees BOTH full projections with
margin, lights off, press **F4** (`PMSDKWallCanvasAlign` on "PMSDK Runtime Services",
nested repo). Two Gray-code sweeps (~1–2 min, other projector auto-blanked), then the
canvas mapping + blend widths apply and save automatically. `A`/`Shift+A` (package
auto-align) only RECOVERS each projector's own footprint — it does not build the
shared canvas; use F4 for the wall workflow. F2 = manual touch-up, Ctrl+S saves.

**Why the first attempts failed — the four real-world gotchas (also D-025)**:
1. Floor/ceiling **light spill decodes as valid correspondences** on other planes →
   plain least-squares fit poisoned (reproj 100–2700 px). RANSAC consensus fit fixes
   it (~half the points rejected as spill). Upstream into `PMSDKAutoAlign` eventually.
2. **Settle time**: projector input lag + webcam exposure + USB buffering ≈ hundreds
   of ms; the default `SettleFrames=2` captures stale patterns. Use `SettleFrames≈45`,
   `flushFrames≈5` (frame-based — fps-dependent; time-based settle would be sturdier).
3. **Webcam auto-exposure** partially equalizes the white/black reference frames →
   shadow mask starves (47 valid px with lights on). Lights off is mandatory; a C-API
   exposure lock (`pmsdk_decoder_set_property`) would remove the caveat.
4. During a sweep, **blank the other projector, freeze animated content, hide overlay
   badges** — all three otherwise contaminate the decode (animated content in the
   overlap is the worst).

Also learned: OpenCV device indices ≠ Unity `WebCamTexture` order (Meta Quest virtual
cameras don't consume OpenCV indices — the C270 was index 0); `pmsdk_decoder_create`
runs fine but `open_camera` fails while any other app holds the camera.

## Stereo (SBS 3D) on the blended wall (2026-07-17) — nested repo, `SBS` branch

Blended stereoscopic 3D across the two-projector wall, for glasses + DLP-Link projectors in
3D SBS mode. DLP-Link sync was verified stable across BOTH projectors in the overlap zone
before any code (the go/no-go gate — free-running projectors can ghost in the overlap; this
hardware did not). Architecture + rationale: D-026. Files (nested repo `Scripts/`,
`Shaders/`):

- `PMSDKStereoContentRig` (on the Content Camera): produces two eye textures. `SceneCameras`
  mode renders a parallel L/R pair with asymmetric off-axis frusta, zero-parallax at the
  wall plane (`ZeroParallaxDistance`, default 6 m; nearer = pops out, farther = recedes);
  `SbsTexture` mode uses the two halves of a side-by-side texture (e.g. a VideoPlayer RT) as
  the eyes. `EyeSeparation` (default 0.06) tunes depth strength. **F6** toggles stereo,
  **F7** flips source. When stereo turns on it also re-parks screen-space content canvases
  to the zero-parallax plane (world space) so flat UI renders correctly in both eyes at wall
  depth, and restores them on toggle-off.
- `PMSDKStereoComposer` (on each projector camera): renders the projector's EXISTING warp
  surface once per eye (swaps only `_MainTex` via a MaterialPropertyBlock, with the eye's
  slice scale/offset composed in), then packs both warped images into an SBS frame via
  `PMSDK/StereoPack`. Calibration mode → full mono passthrough; a material takeover
  (test pattern / Gray-code sweep) → the pattern packed identically into both halves, so F4
  works with the projectors left in 3D SBS mode.
- `PMSDK/StereoPack` shader: samples `_LeftTex`/`_RightTex` into the left/right output
  halves; `_Mono=1` packs left into both (calibration/2D passthrough).

**Demo scene**: `Scenes/StereoMappingDemo.unity` — a clone of the calibrated
ProBuilderMappingDemo (reads the SAME calibration JSON, so the wall alignment carries over),
plus a VideoPlayer → `PMSDKDemo/SBS_Video_RT` (assign your SBS clip to the VideoPlayer's
Video Clip; slot intentionally empty in git) and three depth showcase objects at ~3 m
(pops out), ~4.5 m, ~9 m (recedes). Title bar retitled.

**On-wall procedure**: projectors → 3D SBS mode; play the scene; open fullscreen previews;
**F6** (stereo); glasses on; **F7** flips scene-objects ↔ video. Tuning knobs on the rig:
`EyeSeparation` (depth strength), `ZeroParallaxDistance` (what sits at wall depth). If depth
reads INVERTED (some projectors swap eye order), swap `_LeftTex`/`_RightTex` in the composer
— one-line fix. **Editor-verified** (0.00 px disparity at zero-parallax, −18.7 px crossed at
2 m, both eyes warped+blended+packed, clean mono round-trip); **NOT yet verified on the wall
with glasses** — that is the remaining gate before this merges to main / upstreams.

## Content UI on the wall (2026-07-17) — nested repo, `SBS` branch

Screen-Space **Overlay** canvases never reach the projectors (Unity composites Overlay onto
the display backbuffer after all cameras; the wall only shows what lands in `Projection_RT`).
To put audience UI on the wall: a **Screen Space – Camera** canvas rendered by the Content
Camera, on a dedicated `ContentUI` layer added to the Content Camera's culling mask ONLY.
The projector cameras must NOT see that layer or they draw the UI a second time, unwarped —
they cull `UI | TransparentFX` (calibration overlays), so keep audience UI off `UI`.
Implemented in ProBuilderMappingDemo (a spanning title bar) + StereoMappingDemo. Operator UI
(calibration HUD/loupe) deliberately stays Overlay on Display 1 — never projected. Caveat:
the `ContentUI` layer NAME lives in the template project's `ProjectSettings/TagManager.asset`
(not committed); scene/camera store the layer by index (bit 8), so it works on a fresh
machine but shows unnamed until re-added. A straight-edged UI element is also a great
residual-warp detector — the title bar is what exposed a stale grid warp on Left_Screen.

## Current State
- The Core, Math, Geometry, Warp, Blend, Serialization, and Calibration modules are implemented.
- We have fully decoupled OpenCV behind a strict PImpl interface to prevent ABI spillage.
- Milestone 18 (Performance Optimization) is complete. The Warp, Mesh, and Math components now heavily utilize `std::execution::par_unseq` for multithreaded SIMD vectorization.
- The `MSVCWorkaround.cpp` was introduced as a C++ file wrapped in `extern "C"` to provide missing static linker symbols on older MSVC environments without conflicting with GHA MSVC CI headers.
- **Unity integration overhauled (2026-07-15, D-020)**: the Unity demo rig now matches the
  native warp engine's output contract (normalized raster-space meshes → orthographic
  projector cameras). New package pieces: `PMSDK/UnlitWarp` shader, `PMSDKDisplayActivator`,
  `PMSDKVelocityCap`. Demo scenes `WarpAndBlendExample` (retrofitted) and
  `ProBuilderMappingDemo` (new, ProBuilder content stage) both verified in play mode.
  Full rig documentation: docs/unity-architecture.md.

## Unity project locations
- Unity project: `C:/Unity/ProjectionMapping-base-project`
- Package `com.viitorx.pmsdk` is a local `file:` reference to
  `C:/UnrealProject/ProjectionMapping/bindings/unity/com.viitorx.pmsdk` — edit sources in
  this repo, then refresh/compile in Unity.
- Generated demo assets live in `Assets/PMSDKDemo/` (RT, split-slice screen materials,
  bouncy physics material, ProBuilder stage materials).

- **On-site calibration P1 + P2 + P3 shipped (2026-07-16)**: runtime keyboard calibration
  with JSON persistence (`Runtime/Calibration/*`). F2 mode toggle, auto-enter on first run,
  operator HUD on Display 1 (status + loupe + clickable projector thumbnails), identify
  badges + handles + blend-zone tint on projector outputs, mouse drag
  (Display.RelativeMouseAt), Ctrl+Z undo. `PMSDKCornerPinUI` superseded.
  **P3 camera auto-align** (`A` key): Gray-code structured light → managed decode →
  camera→projector homography → corner-pin. Homography, NOT native metric triangulation
  (D-021). Design + key map: docs/calibration-ux-design.md §9.
- **Calibration hardening "item 1" (2026-07-16)**: native robust decode
  (pattern-vs-inverse bits + white/black shadow mask, `DecodeRobust`), capture-buffer
  flushing, frame readback, `pmsdk_decoder_decode_robust` C API; Unity
  `PMSDKNativeWebcamCamera` + manager `UseNativeWebcam`; `PMSDKAutoAlign` projects
  inverse patterns by default. 6 new native tests green; DLL redeployed to the Unity
  plugin folder. Simulated loop verified; **physical projector+webcam smoke test still
  pending** (no hardware). Pre-existing failure `DecoderTests.DecodeAndTriangulate`
  fails at HEAD before these changes — separate investigation.
  Gotcha: never parent ScreenSpaceCamera overlay canvases under a warp surface — its
  16:9 X-scale leaks into canvas geometry (blend zones rendered outside the frustum).

## Next Recommended Task
- **Milestone 19: Plugin SDK**: Create plugin bindings for UE5 or wrap the C API in C# for advanced Unity rendering hooks.
- Calibration auto-align real-hardware path: add `pmsdk_decoder_get_frame` (grayscale
  readback) to the C API, implement an `IPMSDKCalibrationCamera` over the native webcam
  decoder, then run a physical projector+camera smoke test.
- Unity backlog: expose `pmsdk_gridwarp` grids beyond 2×2 in `PMSDKCornerPin` (bezier/grid
  UI), per-projector color correction, and a Game-view-independent preview window.

- **Pro-feature High items (2026-07-16)**: (1) perspective corner pin — native
  `PerspectiveWarp` (Heckbert homography, `DeformationType::Perspective`, C API
  `pmsdk_perspectivewarp_set_corners`); `PMSDKCornerPin` now projective, not 2×2 bilinear
  (D-022); 4 unit tests green. (2) N×M grid warp UI — `PMSDKGridWarp` component +
  calibration grid mode (`G` key, drag/nudge control points, `[]`/`-=` subdivision),
  persisted in the calibration JSON. (3) mark-target-rectangle UI — calibration `M`
  submode: live camera preview on the operator console with 4 draggable corners →
  `AlignSelectedWithTarget`, so the projection can be mapped onto a specific physical
  screen instead of the full throw; persisted in the JSON. (4) auto-blend from camera
  overlap — `PMSDKAutoBlend` derives edge-blend widths from the Gray-code overlap between
  projectors (coverage-histogram scan), applied after `Shift+A` align-all; core verified
  by edit-mode tests. All **compile + unit-tested but not yet runtime-verified**: the new
  DLL couldn't be deployed because the open Unity editor holds the native-plugin lock.
  Pro-feature gap list now 4/12 (all High + auto-blend).
- **Medium batch (2026-07-16)**: (#6) blend-gamma fix — `PMSDKEdgeBlend` sends ramp
  exponent 1/projectorGamma, fixing the reported dark overlap seam (D-023), + uniform
  black-level floor + per-channel output gamma in `PMSDK/UnlitWarp`; (#7)
  `PMSDKColorCorrection` gain/offset/gamma; (#8) `PMSDKOutputTransform` rotate/mirror;
  (#5) `PMSDKDenseWarp.FitGrid` dense N×M grid from correspondence (off by default via
  `DenseAutoWarpN`). Compiled clean (Editor.log 0 CS errors, shader imported); blend-gamma
  math-proven; dense-warp numeric test NOT run (MCP client link dropped after a domain
  reload — Unity healthy, reconnect to run it). Gap list now **8/12**.
- **Runtime verification (2026-07-16, after Unity restart, MCP reconnected)**: deployed
  DLL == fresh build (has perspective exports). Play-mode confirmed on ProBuilderMappingDemo:
  perspective corner pin (corners exact + mid-edge perspective, not bilinear; visually
  foreshortens correctly), per-projector color correction (red-gain tint), output rotation
  (180° flip). Restored `Assets/PMSDKDemo` resolved the missing-material refs. Grid /
  auto-blend / dense-warp remain unit-test-verified; blend-gamma math-proven; mark-target +
  webcam + real-hardware still unverified. Found + fixed: `PerspectiveWarp` left the source
  mesh's z instead of flattening to 0 (harmless under the ortho projector cam, wrong under a
  perspective one) — fixed + retested; **DLL redeploy of the z-fix deferred** (Unity holds
  the plugin lock; current deployed DLL renders the ortho rig correctly).
- **Env note**: the Unity MCP client transport can drop after a forced recompile/domain
  reload and not re-handshake for the session; Unity stays healthy. Editor.log
  (`%LOCALAPPDATA%/Unity/Editor/Editor.log`, grep `error CS`) is the fallback to confirm
  compilation when MCP is down. Deploy step: close Unity → copy
  build/vs2022/bin/Release/ProjectionMappingSDK.dll into the nested repo's
  Plugins/Mapping/ → reopen → hit play. Until then, entering play mode with the OLD DLL
  throws EntryPointNotFound in PMSDKCornerPin (perspective entry points missing).

## Gotchas for the next agent
- Unity MCP `manage_editor(play)` sessions can end up PAUSED (`EditorApplication.isPaused`)
  — if play-mode state looks frozen (Update not running, time stuck), check/clear pause
  before debugging phantom bugs.
- Native plugin DLL is process-locked while the Unity editor is open (loaded via
  DllImport, never unloaded on domain reload). To update ProjectionMappingSDK.dll you must
  close Unity, copy, then reopen — there is no hot-swap. Deploy DLL changes while Unity is
  closed.

## Project Structure Notes
- The DLL is completely self-contained.
- Unity wrapper expects `ProjectionMappingSDK.dll` in its plugins directory.
- Unreal wrapper uses the standard ThirdParty plugin structure to load the DLL.

## Commands
Build C++ core: `cmake --preset vs2022` then `cmake --build build/vs2022 --config Release`
