# Auto Adjust (camera-assisted surface alignment) - Plan

Status: Phases 0-4 ENGINEERED 2026-11-16 (O1-O5 answered, D-044; P2-P4 in D-045..D-047; C++ and Unity tests green on
synthetic data). Hardware testing on the soffit is still open - see `docs/ai_handoff.md` and `tasks.md`.
Written 2026-11-16 after a design discussion. Read `AGENTS.md` and `docs/ai_handoff.md` first.

## Start here (checklist for a new session)

1. Read `AGENTS.md`, then this file, then `docs/ai_handoff.md`, `tasks.md`, `decisions.md` (D-019..D-028, D-041..D-043).
2. Summarise your understanding in a few lines before changing anything.
3. Ask the user the open decisions in section 8 (O1-O5) and wait for answers.
4. Check the environment (section 9): Unity closed before DLL copies / heavy C++ builds, project path quoted when launching Unity, scratch skin set (never the user's sets), MCP link may drop on domain reload.
5. Run the baseline tests first (C++ `pmsdk_tests.exe`: 155; Unity EditMode: 237) so you know what was green before you start.
6. Do Phase 0 and Phase 1 only; ask before Phase 2+.
7. Write tests with the code; report what could NOT be verified without the camera and projector.
8. Update docs (`tasks.md`, `decisions.md`, `ai_handoff.md`) before any commit; ask before commit/push or any download/install.

## 1. Problem

A skin made from a photo (or drawn by hand) is placed on a real, often non-planar surface. Example (user's
photo): a folded ceiling soffit made of several flat facets at different angles. The skin only knows an
OUTLINE; inside it the test grid is laid linearly over the bounding box, so on the wall some squares are
stretched. A single flat warp cannot be right for several differently tilted facets.

Goal: let the app measure the result with a camera, correct it automatically, and verify it numerically.

## 2. Pipeline the user wants

1. Identify the target surface(s) in the camera image.
2. Create the skin(s), apply and map them.
3. Capture the camera image again and process it.
4. Adjust (corner pin / grid points / shader warp map) and verify the UV result is correct. Repeat.

## 3. Feasibility (agreed in discussion)

| Step | Feasible? | How |
|---|---|---|
| 1 identify surface | PARTLY | Geometry can be found automatically only with depth (plane fitting). WHICH surface to map needs the user (click/box) or a reference. v1 skips it: the user supplies the PNG and a coarse placement (see 5). |
| 2 create/apply | YES | existing skin tools + `SkinGen` photo module + corner pin / grid warp |
| 3 capture + process | YES | Gray-code structured light (exists) + marker/checker corner detection |
| 4 adjust + verify | YES | iterative refinement of control points, numeric residual score |

Camera: a normal RGB webcam is enough for steps 2-4. An OAK-D adds depth (metric facets, automatic plane
finding) and is a LATER upgrade; it is not a UVC webcam (needs DepthAI; HoloTrack already has a feature-gated
DepthAI source). Camera POSITION: anywhere, as long as it sees every facet without occlusion and not at a
very grazing angle; keep it fixed (tripod), lock focus + exposure. Position only decides "correct from where":
camera = reference viewpoint. Put it near the audience spot, or near the lens, for viewer-correct results.

What cannot be promised: identifying "which surface" from a graphic PNG (no visual correspondence to the
photo). Matching works only if the PNG is a photo of the same scene from a similar view. A phone photo from
another angle is uncalibrated and unusable for geometry (visual reference only).

## 4. What already exists (re-use, do not duplicate)

Native C++ (`C:\Unity\ProjectionMappingC`): `Calibration/GrayCode*` (robust sequence, inverse patterns, shadow
mask, `pmsdk_decoder_decode_robust` = raw camera->projector correspondences, direct webcam capture via
`cv::VideoCapture`), homography, `GridWarp`, `PerspectiveWarp`, `SkinGen` (photo -> parts: masks, polygons,
holes, quads; see D-041/D-043).
Unity package (`bindings/unity/com.viitorx.pmsdk/Runtime`): `Calibration/PMSDKAutoAlign` (orchestrator
coroutine), `PMSDKGrayCodeDecode`, `PMSDKHomography`, `IPMSDKCalibrationCamera` (+ `PMSDKNativeWebcamCamera`,
`PMSDKSimulatedCamera`), `PMSDKCornerPin`, `PMSDKGridWarp`, dense auto-warp, luminance/black-level
compensation. Skin app: `Skin/App/*` (Workspace with Focus view, Player, media panel, photo skins, FFmpeg flow),
`PMSDKSkinStore` (named sets), `PMSDK/UnlitWarp` shader (has `_AlphaMask`).
Docs to read: `docs/calibration-ux-design.md`, `docs/decisions.md` D-019..D-028, D-041..D-043,
`docs/unity-architecture.md`.

## 5. Concept: what "correct" means and how v1 gets a target

Two candidate definitions - the user must pick (open decision O1):
- SURFACE-METRIC: squares are the same physical size on every facet. Needs real facet sizes (user-entered, or
  depth later).
- VIEWER-PERSPECTIVE: squares look square from one chosen viewpoint (the camera, or a tracked viewer).

v1 (no Step 1): the PNG is processed by `SkinGen` into parts; the user places each part roughly on the wall
(corner handles) OR clicks 4+ points on the live camera image per part. That coarse placement replaces
"identify the surface". The loop then refines each part so its outline lands on the lit facet edges seen by the
camera, and the interior grid is rectified per facet (one skin per facet, each with its own corner pin / grid).

## 6. Phases

### P0 - Decisions + test rig (1 short session)
- Resolve O1-O5 (section 8). Pick camera (webcam first). Record camera intrinsics approach.
- Bench: confirm Gray-code auto-align (`A` key) works on the user's room with the chosen camera; save a
  reference capture set to a scratch folder (outside the repo).
- Exit: a repeatable manual alignment result + photo for comparison.

### P1 - Verify-and-refine one planar facet (webcam)  [core value]
Native (C++, engine-agnostic, new module `AutoAdjust` or inside Calibration):
- Marker/checker corner detection on the camera frame (ChArUco or plain checker + known cell layout).
- Residual: compare detected corners to the ideal grid mapped through the current warp -> per-point error
  (pixels in camera, mm if scale known), summary score (RMS, max).
- Refinement step: solve updated corner-pin (homography) and/or N x M grid points from the residuals; damped
  iteration, stop at tolerance or max iterations.
- C API + unit tests with synthetic images (known homography, noise, partial occlusion).
Unity:
- New orchestrator `PMSDKAutoAdjust` (coroutine like `PMSDKAutoAlign`): project pattern for the selected skin,
  wait/flush, capture, detect, update corner pin/grid, repeat, show score; Undo-able; progress UI.
- UI: "Auto adjust" button in the Workspace (+ Player-safe: editor only), live residual readout.
- Exit: on a real planar facet the measured RMS error drops below the target (propose <= 3 px at camera
  resolution, to confirm) within <= 4 iterations; result persisted in the skin set.

### P2 - Several facets (folded surface), PNG as input
- One skin per `SkinGen` part, each refined independently; facet edge alignment using the lit-region boundary
  from the Gray-code shadow mask / white-frame capture.
- Optional user-entered facet sizes for SURFACE-METRIC mode.
- Overlap/seam handling between adjacent facets (z-order, optional feather).
- Exit: the user's soffit photo case: grid squares visually even on all facets; per-facet score reported.

### P3 - Non-planar / curved: shader warp map
- Dense correspondences -> per-skin warp texture (projector pixel -> content UV), applied in `PMSDK/UnlitWarp`
  (new `_WarpMap`), content defined in the reference (camera or metric) frame. Fallback when grid warp is not
  accurate enough. Needs inversion of the camera->projector map (splat + inpaint) and tests.

### P4 - OAK-D depth + automatic surface identification
- New camera source via DepthAI (colour + depth) behind `IPMSDKCalibrationCamera`; reuse HoloTrack's DepthAI
  device code where possible (feature-gated, see D-032/D-033).
- Plane detection (RANSAC) on the depth point cloud -> facet candidates -> user picks / auto selects those
  inside the projector-lit area; metric facet sizes -> SURFACE-METRIC mode without manual sizes.
- Optional later: click-to-segment model, feature matching of the PNG to the live image. (User wants to study
  these first.)

## 7. Risks and mitigations

- Camera lens distortion (wide OAK 97 deg): do a one-off intrinsics calibration (checkerboard) or let the grid
  warp absorb residual; check error map for radial pattern.
- Ambient light / auto-exposure: lock exposure, dim room, use inverse patterns + shadow mask (already there).
- Camera moved between steps: detect by re-measuring a reference marker; warn.
- Hidden/oblique facets: compute a per-facet "visibility/angle" quality and refuse or warn below a threshold.
- Loop divergence: damped updates, keep best-so-far, always allow undo to the pre-adjust state.
- Process isolation: never let the editor autosave test shapes into the user's sets (see section 9).

## 8. Open decisions (ask the user at the start of the next session)

- O1 definition of "correct": surface-metric vs viewer-perspective (head tracking exists, so viewer-based is
  possible later).
- O2 camera for v1: confirm plain webcam first, OAK-D later.
- O3 PNG semantics: design image to place on the surface, or an outline of the facet(s)? (v1 assumes: it is run
  through `SkinGen` and its parts are placed by the user.)
- O4 initial placement method: manual corner handles vs 4-point clicks on the live camera image.
- O5 accuracy target and success metric (px at camera, or mm if sizes are known).

## 9. Working notes for the next agent (environment)

- Repos: SDK `C:\Unity\ProjectionMappingC` (branch feat/multi-skin-m-sk1 at last check; writable); Unity project
  `C:\Unity\Multi Projector` (READ-ONLY except the nested repo); nested game repo
  `C:\Unity\Multi Projector\Assets\Games\ProjectionMapping-unity` (writable, has the scenes in `Skins/`, plugins in
  `Plugins/Mapping`). `AGENTS.md` rules apply (explicit types, no `var`, K&R braces, update docs before commit,
  ask before git commit/push).
- Unity MCP: the link drops after domain reload and when entering play mode; wait, retry `read_console`.
  Play mode is often paused: set `EditorApplication.isPaused = false`. Screenshots must go to a project-relative
  folder (used `Assets/Games/ProjectionMapping-unity/.shots`; delete it before committing).
- Editor play sessions share `persistentDataPath` (`%USERPROFILE%\AppData\LocalLow\ViitorX\ViitorX Projection
  Skin`) with the built EXE. Never write to the user's skin sets while testing: switch `PMSDKSkinStore.ActiveName` to
  a scratch name first and restore it afterwards. Workspace save-on-quit is compiled out in the Editor.
- Launch Unity from a shell WITH THE PATH QUOTED: `Start-Process Unity.exe -ArgumentList '-projectPath','"C:\Unity\Multi Projector"'`
  (version 6000.3.9f1). Close Unity before copying DLLs into `Plugins/Mapping` and before the heavy C++ build.
- C++ build (VS 2026 toolchain, 16 GB RAM machine): vcpkg deps are already built in
  `build/vs2022/vcpkg_installed`. In a cmd with `vcvars64.bat` from
  `C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build`:
  `cmake --preset release -DVCPKG_INSTALLED_DIR=C:/Unity/ProjectionMappingC/build/vs2022/vcpkg_installed -DPMSDK_BUILD_EXAMPLES=OFF -DHOLOTRACK_BUILD=OFF`
  then `cmake --build build/release --target pmsdk pmsdk_tests -j 2`; run `build/release/bin/pmsdk_tests.exe`.
  Never run more than 2-3 parallel compiles (they get killed: exit 0x40010004). The `vs2022` preset does not
  work (no VS 2022 installed). Old MSVC stub symbols are guarded for MSVC >= 1950.
- Deploy: copy `build/release/bin/*.dll` EXCEPT gtest*.dll to `Plugins/Mapping` (the DLL links OpenCV
  dynamically; 18 DLLs total). Back up the old DLL first.
- Tests: Unity EditMode 237/237 and C++ 155/155 at the time of writing (assembly `vxpmsdk.Tests.Editor`).
- Hardware available in the user's room: a projector on a folded ceiling soffit, an OAK-D (not yet wired to
  the calibration code), a phone photo (not usable for geometry).
- Test files used so far: `C:\Unity\1.mp4`, `2.mp4`, `3.png`, `2.png` (statue renders), fixtures in
  `tests/data/`.

## 10. Definition of done for the feature (v1 = P1 + P2)

- Button in the Skin Workspace runs the loop on the selected skin or all photo parts.
- A numeric score per facet before/after; improvement shown in the UI; Undo restores the previous warp.
- Works with a plain webcam; documented camera setup (position, focus, exposure).
- Unit tests for detection, residual, and the update step; integration test with the simulated camera;
  one manual acceptance test on the real soffit with a photo recorded in the docs.
- Docs updated: `tasks.md`, `decisions.md` (new D-0xx), `ai_handoff.md`, `calibration-ux-design.md`.
