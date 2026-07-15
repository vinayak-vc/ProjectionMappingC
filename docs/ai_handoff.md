# AI Agent Handoff Document

## Current State (2026-07-15)

**Last Completed Milestone**: Milestone 18 (Performance optimization) + Unity demo overhaul
**Current Task**: Ready for Milestone 19 (Plugin SDK).

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
  (D-021). Capture abstracted; simulated Unity-camera source ships, real webcam needs a
  C-API frame-readback addition (`pmsdk_decoder_get_frame`). Simulation-verified only —
  no physical hardware loop yet. Design + key map: docs/calibration-ux-design.md §9.
  Gotcha: never parent ScreenSpaceCamera overlay canvases under a warp surface — its
  16:9 X-scale leaks into canvas geometry (blend zones rendered outside the frustum).

## Next Recommended Task
- **Milestone 19: Plugin SDK**: Create plugin bindings for UE5 or wrap the C API in C# for advanced Unity rendering hooks.
- Calibration auto-align real-hardware path: add `pmsdk_decoder_get_frame` (grayscale
  readback) to the C API, implement an `IPMSDKCalibrationCamera` over the native webcam
  decoder, then run a physical projector+camera smoke test.
- Unity backlog: expose `pmsdk_gridwarp` grids beyond 2×2 in `PMSDKCornerPin` (bezier/grid
  UI), per-projector color correction, and a Game-view-independent preview window.

## Gotchas for the next agent
- Unity MCP `manage_editor(play)` sessions can end up PAUSED (`EditorApplication.isPaused`)
  — if play-mode state looks frozen (Update not running, time stuck), check/clear pause
  before debugging phantom bugs.

## Project Structure Notes
- The DLL is completely self-contained.
- Unity wrapper expects `ProjectionMappingSDK.dll` in its plugins directory.
- Unreal wrapper uses the standard ThirdParty plugin structure to load the DLL.

## Commands
Build C++ core: `cmake --preset vs2022` then `cmake --build build/vs2022 --config Release`
