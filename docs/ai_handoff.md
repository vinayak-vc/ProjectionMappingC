# AI Agent Handoff Document

## Current State (2026-07-16)

**Last Completed**: Milestones 1-18 + 20; Unity raster-space rig; on-site calibration
P1-P3; robust structured-light decode + webcam path; perspective corner pin; N×M grid
warp UI; mark-target-rectangle UI.
**Current Task**: Milestone 19 (Plugin SDK) still open. Immediate pending step: redeploy
the rebuilt DLL (needs Unity closed — plugin lock) then run the play-mode pass on
perspective / grid / mark-target and commit+push the DLL to the nested repo.
**Verified**: native robust-decode + perspective are unit-tested (green); Unity C#
compiles clean; sim auto-align verified. Not yet: play-mode runtime for the DLL-dependent
features, and any physical camera loop.

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
  screen instead of the full throw; persisted in the JSON. All **compile + unit-tested
  but not yet runtime-verified**: the new DLL couldn't be deployed because the open Unity
  editor holds the native-plugin lock. Deploy step: close Unity → copy
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
