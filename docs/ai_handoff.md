# AI Agent Handoff Document

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
**NOT verified**: real projector+camera loop (no hardware), mark-target/webcam runtime,
per-frame warp-readback perf at scale.
**Current Task**: Milestone 19 (Plugin SDK) open. Immediate pending: (a) redeploy the z-fix
DLL to the nested repo (needs Unity closed — plugin lock) and commit it there; (b) sim
closed-loop calibration test + warp-readback profiling (both no-hardware); (c) real-hardware
calibration smoke test when hardware is available.
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
