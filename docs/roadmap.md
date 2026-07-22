# Roadmap — ProjectionMappingSDK

One milestone at a time. Each milestone must compile, pass tests, and keep examples building
before the next begins.

| # | Milestone | Status |
|---|-----------|--------|
| 1 | Repository setup, CMake, vcpkg, CI, folder structure | ✅ done (2026-07-10) |
| 2 | Core module (logging, errors, versioning, handles) | ✅ done (2026-07-10) |
| 3 | Math library | ✅ done (2026-07-10) |
| 4 | Geometry library | ✅ done (2026-07-10) |
| 5 | Mesh data structures | ✅ done (2026-07-10) |
| 6 | Warp engine | ✅ done (2026-07-10) |
| 7 | Blend engine | ✅ done (2026-07-10) |
| 8 | Serialization | ✅ done (2026-07-10) |
| 9 | Calibration (OpenCV) | ✅ done (2026-07-10) |
| 10 | Public C API | ✅ done (2026-07-10) |
| 11 | Unit tests (full sweep to 90% coverage) | ✅ done (2026-07-10) |
| 12 | Documentation (Doxygen) | ✅ done (2026-07-10) |
| 13 | Unity wrapper | ✅ done (2026-07-11) |
| 14 | Unreal wrapper | ✅ done (2026-07-13) |
| 15 | Sample applications | ✅ done (2026-07-13) |
| 16 | Advanced OpenCV calibration wrappers | ✅ done (2026-07-13) |
| 17 | GrayCode Decoder & Triangulation | ✅ done (2026-07-13) |
| 18 | Performance optimization (SIMD, `par_unseq`) | ✅ done (2026-07-14) |
| 19 | Plugin SDK | pending |
| 20 | Release packaging | ✅ done (2026-07-14) |

## Post-1.0 — Unity integration & pro-feature parity (2026-07-15/16)

Driven by the Unity host work and a gap audit against Resolume / MadMapper / VIOSO.

| Item | Status |
|---|---|
| Raster-space projector rig (ortho cameras, split-slice RT, UnlitWarp shader, display activator) | ✅ done |
| On-site calibration P1 — runtime keyboard mode, JSON persistence, auto-enter, HUD | ✅ done |
| On-site calibration P2 — mouse drag, undo, loupe, thumbnails, blend-zone tint | ✅ done |
| On-site calibration P3 — camera auto-align (Gray-code → homography) | ✅ done (sim-verified) |
| Robust structured-light decode (inverse patterns + shadow mask) + webcam capture path | ✅ done (unit-tested) |
| Perspective corner pin (homography, replaces bilinear) | ✅ done (unit-tested; runtime pending DLL redeploy) |
| N×M grid warp UI (curved surfaces) | ✅ done (runtime pending DLL redeploy) |
| Mark-target-rectangle UI (map onto a specific physical screen) | ✅ done (runtime pending DLL redeploy) |
| Auto-blend from camera overlap (edge widths from Gray-code overlap) | ✅ done (core unit-tested; runtime pending DLL + shared camera) |
| Blend gamma fix + black-level + per-channel gamma (dark-seam fix, D-023) | ✅ done (math-proven; runtime pending DLL) |
| Per-projector color correction (gain/offset/gamma) | ✅ done (compiled; runtime pending DLL) |
| Output rotation / mirroring (portrait/ceiling/rear) | ✅ done (compiled; runtime pending DLL) |
| Dense auto-warp (fit N×M grid from camera, curved surfaces) | ✅ done (compiled; numeric test pending MCP reconnect) |

All 12 pro-feature gaps implemented (see docs/tasks.md for verification caveats).

**Camera-measured luminance compensation (implemented 2026-07-21, D-027)** — the sweep's
all-white captures become per-projector wall luminance maps; `PMSDKLuminanceCompensation`
derives a per-projector gain map (global robust-min target, dim-only) that `PMSDK/UnlitWarp`
multiplies in raster space via `_GainTex`, so the blended wall reads as one even light
source (fixes the residual seam band gamma tuning cannot). Opt-in via
`AutoLuminanceAfterAlignAll`; persisted quantized in the calibration JSON. Related
follow-ups: per-region black-level (doubled projector-black on dark content is still open),
D-025 upstreaming (RANSAC/settle/exposure), N-projector wall-canvas align.

Pro-feature gap list: **8 of 12 done** (all 3 High + auto-blend, blend-gamma, color,
rotation, dense-warp).

## HoloTrackSDK — Head-Tracked Holographic Projection (independent product, started 2026-07-21)

Separate DLL (`HoloTrackSDK.dll`, `com.viitorx.holotrack`), zero runtime dependency on the
projection-mapping SDK (D-029). OAK-D-PRO-W-97 → single-viewer head tracking → off-axis
perspective (D-030). Development is hardware-free first (no OAK on this machine): all
decision logic is pure and unit-tested; the DepthAI device path is feature-gated (D-032).

| Phase | Scope | Status |
|---|---|---|
| H1 | Docs — decisions D-029..D-032, this roadmap entry, `holotrack-architecture.md` | ✅ done |
| H2 | Core pure logic — Detection/Viewer types, filters (Exp/OneEuro/Kalman), tracking state machine, viewer selector + hysteresis, head estimator, OAK→world transform, off-axis projection | ✅ done (harness 161/161) |
| H3 | C-API (`HoloTrack/C_API`) — opaque tracker handle, config, push-detections, poll, get-head-pose, get-off-axis matrices; build target `holotrack` → `HoloTrackSDK.dll` (10 exports) | ✅ done |
| H4 | OAK device source behind DepthAI support (spatial MobileNet-SSD, bg thread) + device C-API + Unity `PMHTOakSource` | ✅ code done; live camera detected; dependency path fixed to external `depthai` CMake package; live test still needs DepthAI C++ install + blobs |
| H5 | Unity package `com.viitorx.holotrack` — P/Invoke, `PMHTHeadTracker`, `HeadTrackedCameraController` (off-axis), config SO, display surface, diagnostics overlay, gizmos, CSV recorder, sim source | ✅ authored (Unity compile/verify pending in H6) |
| H6 | Consumer sample in the nested game repo — `HoloTrackDemo` scene, DLL deployed to Plugins, off-axis parallax play-mode verified | ✅ done (parallax verified live) |
| H7 | Head-tracked **SBS-3D** integration — stateless arbitrary-eye off-axis C-API + `PMHTHeadTracker` eye overload, generic `HeadTrackedStereoController`, OAK detection modes (Person/Face/FaceThenPerson), game-side rig `ExternalEyeMatrices` hook | ✅ SDK+rig code done (harness 180/180); ProBuilder scene + live stereo verify DEFERRED (needs Unity + DLL redeploy) |

Extensibility carried through the design (not built in v1): multi-user tracking (the selector
already tracks a list, v1 exposes one), multiple projectors / edge-blended output (downstream
in the PM package), stereo rendering (off-axis already produces per-eye frusta), networked
tracking + multi-OAK sync (the `IDetectionSource` abstraction).

## Dependency plan (vcpkg manifest features)

| Milestone | Dependency added |
|---|---|
| 1 | `gtest` (feature `tests`) |
| 3 | `glm` (internal SIMD-friendly math backend, optional) |
| 8 | `nlohmann-json` |
| 9 | `opencv4` (feature `calibration`) |
| later | `spdlog` — deferred; M2 logging is a zero-dependency callback facade (see D-008) |

DepthAI note (2026-07-22, D-033): the pinned vcpkg baseline does not contain a `depthai` port.
`HOLOTRACK_WITH_DEPTHAI=ON` now resolves Luxonis DepthAI through `find_package(depthai CONFIG)`
from an external install by default; vcpkg resolution is opt-in with
`HOLOTRACK_DEPTHAI_USE_VCPKG=ON` for machines that provide a custom/updated port.
