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

## D-029 2026-07-21 — Head-tracked holographic system ships as an independent SDK/DLL (HoloTrackSDK)

The head-tracked holographic projection feature is a **separate product** from the projection
mapping SDK, delivered as its own DLL (`HoloTrackSDK.dll`, namespace `holotrack`, Unity
package `com.viitorx.holotrack`) that has **zero runtime dependency** on
`ProjectionMappingSDK.dll`. Rationale: the two capabilities are orthogonal — a customer can
run a head-tracked hologram without projection mapping, and vice-versa. Coupling them into
one DLL would force every projection-mapping consumer to carry the DepthAI/OAK dependency
and would let one product's ABI churn break the other.

- **Same repo, separate target.** Lives in this repo (`include/HoloTrack`, `holotrack/src`,
  `holotrack/tests`, top-level `add_subdirectory(holotrack)`), shares CI and the header-only
  `pmsdk::Math` types (constexpr value types, no link — so reuse costs nothing and adds no
  runtime dependency). It does **not** link `pmsdk`.
- **Own C-API** (`include/HoloTrack/C_API`) following the same rules as PMSDK's (D-006):
  opaque handles, status-code returns, `HT_GetLastError`, no C++/STL/exceptions across the
  boundary.
- **Composition happens in the consumer**, not by linking DLLs: the Unity scene feeds the
  tracked head pose into the content camera, whose output the (separately installed)
  projection-mapping package then warps/blends. Neither SDK knows about the other.

## D-030 2026-07-21 — Holographic render uses off-axis generalized perspective, not camera translation

The spec (§7/§8) lists "move the virtual camera / movement multiplier / clamp / dead zone",
which is a naive camera *translation*. That does not produce a geometrically correct
hologram: the illusion requires the rendered frustum to treat the physical display surface as
a fixed window and the tracked eye as the projection centre — i.e. an **asymmetric off-axis
projection** (Kooima 2008, "generalized perspective projection"). The camera's near/far
planes stay parallel to the wall; only the frustum skews toward the eye. This is the same
off-axis construction already used game-side by `PMSDKStereoContentRig` for the two-projector
wall (D-026), reimplemented here as pure math (`holotrack::OffAxisProjection`: 4 world-space
display corners + eye position → view + projection `Matrix4`). The spec's
movement-multiplier / clamp / min-max / dead-zone become a **safety + tuning layer** applied
to the tracked eye position before it feeds the frustum, not the core model.

## D-031 2026-07-21 — Spatial person-detection first; pose estimation is a pluggable refinement

Per the spec's own priority (Person Detection → Pose → Stereo Depth), v1 derives the head
position from **on-device spatial person detection** (OAK MobileNet-SSD spatial network:
2D bbox + metric XYZ from stereo depth). The head estimate is the bbox top-centre lifted by a
fraction of the detected body height, at the detection's spatial Z. This always yields a
`Vector3 HeadPosition` and needs no fragile face/pose network to be present. Pose estimation
(nose/eyes/neck) is defined behind the same `Detection` type (optional keypoints) and consumed
by `HeadEstimator` as a **more accurate refinement when available**, with graceful fallback to
the bbox+depth+height estimate — matching the spec's "if pose fails, use torso/centroid/height"
requirement. This keeps the fastest path to a working hologram and defers the heavier pose
blob integration without changing the tracking/filter/render pipeline above it.

## D-032 2026-07-21 — Pure tracking logic is hardware-free; OAK/DepthAI I/O is feature-gated

Mirrors D-002 (OpenCV confined to Calibration behind the `calibration` vcpkg feature). All of
HoloTrack's decision logic — filters (exponential / One-Euro / Kalman), the
Searching→Tracking→Prediction→Lost state machine, active-viewer selection + hysteresis, head
estimation, OAK→world coordinate transform, off-axis projection — is **pure C++ with no camera
or DepthAI dependency**, fully unit-tested against synthetic detections. Only the concrete OAK
device source (`holotrack::OakDevice`, DepthAI pipeline on a background thread) sits behind a
new vcpkg feature `depthai`; detections reach the tracker through an `IDetectionSource`
abstraction, so a `SimulatedSource` / recorded source drives development and CI with no
hardware (the same no-hardware strategy used for calibration auto-align, D-021). The DLL and
its entire test suite build green with DepthAI absent.

## D-035 2026-07-23 — A phone (ARCore 6DoF) is a pluggable head-tracking SOURCE, not a camera replacement

Prompted by "can I use my Samsung S21 as the tracker instead of the OAK face/person detection?"

Decision: support a phone as a **worn/held 6DoF source** via ARCore visual-inertial odometry,
implemented as a new `IHeadTrackingSource` (`PMHTPhoneSource`) that streams the phone's own world
pose to the PC — NOT as a fixed camera that face-tracks the viewer. Rationale:

- The head-tracking pipeline was deliberately built around a pluggable `IHeadTrackingSource`
  (sim / OAK). A phone pose stream is just a third source; it emits the head position directly
  (no detection/selection/estimation) and reuses the tracker's smoothing + prediction. Zero
  native/DLL change.
- ARCore gives metric, low-drift 6DoF *position* (camera+IMU fusion); the phone's raw IMU alone
  gives only orientation (position integration drifts). So ARCore is the requirement, not "the
  phone's sensors."
- **Worn, not fixed.** As a FIXED face-tracker the S21 loses to the OAK: it has no ToF and (base
  model) no UWB, so head distance would be ML-estimated, not measured. Its advantage is only in
  the *worn* role, where it reports its own precise pose.
- **Not a replacement for device-free installs.** The camera path's value is that the viewer
  wears nothing; the phone must be head-mounted to equal eye position (hand-held wobbles). So the
  phone is for development/testing, single-user/controlled experiences, and OAK ground-truth —
  complementary to, not a substitute for, the OAK in public installs.
- Coordinate alignment reuses the existing OAK→world calibration mechanism
  (`HeadTrackingConfig.calib*`): ARCore's arbitrary session frame → the wall/world frame via a
  rigid transform from a known reference.

Scope in docs/tasks.md ("HoloTrack — phone (ARCore 6DoF) head-tracking source").

## D-033 2026-07-22 — HoloTrack DepthAI resolves as an external CMake package by default

Live H4 work on the OAK-D-PRO-W-97 showed the camera is connected and calibrated
(OAK Viewer reports MXID `14442C10F143D3D200`, product `OAK-D-PRO-W-97`), but this repo's
pinned vcpkg baseline has no `depthai` port. Enabling `HOLOTRACK_WITH_DEPTHAI` previously
forced `VCPKG_MANIFEST_FEATURES=depthai`, so configure failed before CMake could find any
external Luxonis SDK install.

Decision: `HOLOTRACK_WITH_DEPTHAI=ON` means "compile the real OAK source", and
`find_package(depthai CONFIG REQUIRED)` resolves DepthAI from `CMAKE_PREFIX_PATH` or a normal
CMake package location. The vcpkg manifest feature is only appended when
`HOLOTRACK_DEPTHAI_USE_VCPKG=ON`, for machines that provide a custom/updated vcpkg registry or
overlay port. This matches Luxonis' C++ integration model (install `depthai-core`, then consume
`depthai::core` through `find_package`) and avoids making all developers depend on an absent
port.

Added `holotrack_oak_smoke`: a native executable that starts `ht_oak_*`, polls the device for a
short window, and prints frame/detection counts. It is intentionally not a CTest because it
requires hardware, valid model blobs, and exclusive device access. Close OAK Viewer before using
it; its logs show it owns/runs a DepthAI pipeline while open.
## D-034 2026-07-21 — A depth camera (OAK-D) is a 3D-mapping enabler, not a flat-wall quality upgrade

(Renumbered from D-029 on merge into the HoloTrack branch, which had already taken D-029..D-033.)

Prompted by "will adding OAK-D-PRO-W support enhance our current results?" Verified answer:
for the current flat-wall, 2D-homography blend — **no.**

The current auto-align recovers a planar camera→projector homography from Gray-code
correspondence; on a plane that fit is exact and already sub-pixel (0.55 px on real
hardware, over ~1,500 RANSAC inliers). The visible seam is bounded by projector focus,
lens quality, and light spill — physical factors a better *camera* does not change. A
higher-res / global-shutter / depth sensor would shrink the reported reprojection number
without changing what lands on the wall. The OAK's differentiators are unused by this
pipeline: depth (2D homography ignores it), active IR (we decode *visible* projected
light), global shutter (the sweep is static).

The one real flat-wall pain point — webcam auto-exposure equalizing the white/black
references (forcing lights-off) — is a property of exposure *control*, not depth, and is
addressed by the D-025 follow-up (C-API exposure lock) on any controllable camera far more
cheaply than by adopting a DepthAI device.

Where a depth camera IS the right tool: **non-planar** targets — curved/irregular walls
and twin-free object mapping — where the homography assumption breaks and a metric 3D
reconstruction is genuinely required. That is a feature project (DepthAI capture source +
metric calibration + structured-light triangulation + 3D→warp), scoped in
docs/tasks.md ("Depth-camera 3D / curved-surface mapping"), with the OAK-D-PRO-W as the
reference sensor. Decision: do not adopt the OAK to chase flat-wall numbers; adopt it only
when curved-surface / 3D-scan mapping is greenlit, and treat it as that milestone's sensor.

Verified on the machine (2026-07-21): the OAK-D enumerates as `Movidius MyriadX`
(DepthAI/XLink), not UVC; OpenCV `pmsdk_decoder` cannot open it (indices 0–6 all fail),
confirming it needs either Luxonis UVC mode (plain 2D, no depth benefit) or a native
DepthAI source (to use depth).

## D-028 2026-07-21 — Per-region black-level uplift is the additive twin of luminance comp

The residual seam that luminance compensation (D-027) explicitly could NOT fix: on dark
content a bright, slightly-yellow band sits over the projector overlap. Cause — projectors
cannot emit true black; each leaks a floor at signal 0, and in the overlap BOTH floors add,
so overlap black ≈ 2× a single projector's black. It is an ADDITIVE offset, so it dominates
near-black content and vanishes on bright content (observed on the real wall). A multiply
(the luminance gain) can't touch it: gain × 0 = 0.

Fix — "black-level uplift": you can't remove the overlap's extra light, so raise every
other region's black UP to match it; the whole wall then sits at one uniform, slightly-grey
floor (the eye accepts a uniform grey far more readily than a bright band). Same architecture
as D-027, additive instead of multiplicative:

- **Measurement is (again) free.** The sweep already captures an all-BLACK frame per
  projector for the shadow-mask gate; that frame IS each projector's per-pixel floor. Now
  retained on `PMSDKAutoAlign.Result.Black` alongside `.White`.
- **Pure core.** `PMSDKBlackLevelCompensation.Compute`: in camera space, count how many
  projectors cover each pixel (`litCount`) and the summed floor there; the uplift target is
  the high percentile of that summed floor (the overlap). Per projector, the deficit to the
  target — `max(0, target − Σ floor) / litCount` — is converted to a signal lift by dividing
  by that projector's own black→white span, then scattered into raster space. In the overlap
  the summed floor already ≈ target so the lift is ~0; single-projector regions carry the
  full lift. Correct even with unequal projector blacks (the deficit form, not an equal
  share, avoids overshoot).
- **Apply.** `_BlackLiftTex` in `PMSDK/UnlitWarp`, sampled by the same raster UV1 as the
  gain map, applied at the black-level stage as `c = c·(1−lift) + lift`. `PMSDKMeshWarp`
  writes UV1 when either map is active.
- **Persistence / opt-in / revert:** identical to D-027 (`PMSDKGainCodec`,
  `AutoBlackLevelAfterAlignAll` default OFF, disable the surface's `PMSDKBlackLevelLift`).

Scope: the captures are single-channel luminance, so this flattens the band's BRIGHTNESS
(the dominant artifact). A residual colour tint of the lifted floor is left to a
per-projector `PMSDKColorCorrection` offset; measured per-channel tint would need an RGB
capture (future).

## D-027 2026-07-21 — Camera-measured luminance compensation reuses the white sweep capture

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

## D-026 2026-07-17 — Blended stereo (SBS 3D) splits eyes at the content RT and re-packs AFTER the warp

Client has a side-by-side stereoscopic video and DLP-Link projectors that support 3D SBS;
they want depth on the blended two-projector wall (glasses, DLP-Link sync verified stable
across both projectors in the overlap before any code). The pipeline question: where do the
two eye views split, and where do they re-merge relative to the warp/blend stage?

Decision: split the eyes at the CONTENT source (two RenderTextures — either a parallel L/R
camera pair, or the two halves of an SBS texture), run the EXISTING warp surface once per
eye (swap only the source texture via a MaterialPropertyBlock), and re-pack the two fully
warped+blended images into a single side-by-side display frame as the very LAST step (a
`PMSDK/StereoPack` blit). The projector's 3D SBS mode stretches each half ×2, so
squeeze-then-stretch round-trips and the calibration geometry inside each eye is preserved
bit-for-bit.

Why this ordering:
- **Calibration applies to both eyes for free.** One surface, one corner pin, one edge
  blend renders both eyes; there is nothing to keep in sync between eyes and no second
  calibration. Splitting eyes AFTER the warp (e.g. two warp surfaces per projector) would
  double the calibration surface count and let the eyes drift.
- **Blend and stereo are orthogonal.** Each projector's composer samples its own content
  slice of each eye texture, so the split-slice blend logic is unchanged; edge-blend ramps
  still sum across the overlap, per eye.
- **Calibration stays runnable with 3D mode left ON.** In calibration mode the composer
  does full mono passthrough; when a test pattern or Gray-code sweep takes over the surface
  material, the composer packs that pattern IDENTICALLY into both halves — so the projector
  decodes the sweep correctly without toggling out of 3D SBS mode. This matters on site:
  the operator never has to touch the projector OSD between calibrating and showing content.
- **Parallel eyes, not toe-in.** Eye cameras are offset laterally with an asymmetric
  frustum shift (off-axis projection) so both eyes agree at the zero-parallax plane (the
  wall). Toe-in (rotating the cameras inward) would introduce vertical parallax at frame
  edges — uncomfortable and wrong for a flat wall.

Cost owned: SBS halves each eye's horizontal resolution (960 native columns) — inherent to
the format, not the pipeline. The per-eye double render is one extra quad blit per projector
per frame — negligible.

Lives game-side for now (`PMSDKStereoContentRig`, `PMSDKStereoComposer`,
`PMSDK/StereoPack` in the nested repo, `SBS` branch). Candidate to upstream into the package
once hardware-proven with glasses. Editor-verified (0.00 px disparity at zero-parallax,
crossed disparity with correct sign at 2 m); NOT yet verified on the wall with glasses.

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

## D-036 2026-08-20 — Single projector can host multiple independent skins

**Context.** One projector currently owns exactly one warp surface that samples a horizontal
slice of `Projection_RT`. Physical objects with several visible faces (box, prism, signage
cluster) need one projector with N independent content regions, each with its own geometry
and its own source (video, image, RT). Industry tools (Resolume slices, MadMapper surfaces,
TouchDesigner) all separate "geometry" from "content" and let one projector host many
"slices".

**Decision.** Introduce `PMSDKSkin` — one skin per content region — and lift the "one warp
surface per projector" invariant. A projector may host N skins, each a raster-space mesh
child of the projector's warp root, each carrying its own content-source binding.

Shape primitives in v1: Rectangle/Square, Ellipse/Circle, N-gon (up to 32 vertices), Freeform
polygon (up to 128 vertices). Three underlying warp paths keep the native SDK unchanged:

- **Rectangle/Ellipse** → existing `PMSDKCornerPin` (projective, `PerspectiveWarp`), with the
  ellipse cut by a shader mask (`_ShapeMask=1`).
- **N-gon/Freeform** → Unity mesh regenerated from the vertex list; no native warp; content
  mapped via axis-aligned bounding-box UVs.
- **Curved faces** → promote a skin to `PMSDKGridWarp` (existing).

**Alternatives considered.**

- *One composite RT per projector, sub-sampled by skins in shader.* Rejected — loses the
  per-skin corner-pin homography benefit; overkill.
- *Native composition node (`pmsdk_projector_composite`) to render N skins GPU-side.*
  Deferred — the per-skin overhead in Unity (one small mesh + one MPB per skin) is negligible
  at the scales expected here (tens of skins per projector). Revisit if a real workload
  needs it.

**Consequences.** The current single-warp-surface projectors continue to work unchanged
(a projector without any `PMSDKSkin` children uses the split-slice RT path as before).
Adding skins to a projector opts it out of the split-slice path — each skin brings its own
source. Native SDK unchanged for v1. Design: `docs/multi-skin-design.md`.

## D-037 2026-08-20 — Skin authoring lives in a dedicated scene; the artifact is a portable `PMSDKSkinSet`

**Context.** Authoring skins and playing back content are two different jobs done by two
different people (or at two different times). Coupling them into one scene forces content
authors to re-align geometry every time they change scenes, and forces tech-ops to know the
content pipeline. The industry pattern (MadMapper's mapping vs content mode, Resolume's
slice editor vs composition) separates them.

**Decision.**

1. Ship a **Skin Editor scene** (`PMSDKSkinEditor.unity`) in the package. Its job is drawing
   and stretching skins on live projector output — nothing else. It writes a `PMSDKSkinSet`
   asset and a JSON sidecar.
2. Any content scene loads a `PMSDKSkinSet` via a **`PMSDKSkinLoader`** component. At
   runtime the loader instantiates one `PMSDKSkin` per saved skin, with saved geometry /
   shape / mask / color. A separate `PMSDKSkinBindings` component (optional) binds content
   sources per skin ID; if omitted, the loader falls back to each skin's optional
   `defaultContent`.
3. The skin set is persisted as **both a ScriptableObject** (Unity-native reference) **and a
   JSON sidecar** (portable canonical form). The SO reads the JSON; the JSON is
   authoritative.
4. Skin set JSON files are kept **separate from `pmsdk_calibration.json`** — the
   calibration file continues to hold per-projector corner-pin + blend as today; skin sets
   are their own files (`pmsdk_skinset_<name>.json`). This keeps back-compat and lets a
   project version-control skin sets independently.
5. Ship the editor scene **in both channels**:
   - `Samples~/SkinEditor/SkinEditor.unity` — imported via UPM Package Manager > Samples.
   - `Tools > Projection Mapping > Generate Skin Editor Scene` — creates a local copy at
     `Assets/PMSDKDemo/SkinEditor.unity` (same pattern as the existing demo generator).

**Consequences.** One calibration is reused across many content scenes. Content authors
never touch geometry unless they want to. The skin set is a portable artifact — copy the
JSON to another machine and it works if the physical setup matches. The Skin Editor scene
becomes the canonical mapping surface. Design: `docs/multi-skin-design.md`.

## D-038 2026-11-06 — Skin App: one EXE, shared JSON, per-surface corner-pin applied before mesh processing

**Decision:** (a) The EXE contains the existing `SkinPlayback` (boot scene) and `SkinEditor`
scenes; a new code-built UGUI workspace/player replaces the IMGUI HUD. (b) Both modes share
`persistentDataPath/pmsdk_skinset_default.json`; the Player only rewrites `defaultContent`.
(c) Media lives outside the project as absolute paths: new `ImageFile` content mode + existing
`VideoUrl`; file picking via an own comdlg32 wrapper (no third-party package, Windows only).
(d) `PMSDKMeshWarp.Update` now calls `PMSDKCornerPin.ApplyToNative()` right before processing.

**Why (d):** every skin on a projector shares ONE native warp node. With Unity's interleaved
Update order each mesh was warped with the previous skin's corners, so the Box (3 faces)
preset rendered its textures on the wrong faces (reproduced and verified fixed in play mode).
**How to apply:** any new per-surface native deformation component must push its state
immediately before its own `process_mesh` call. GridWarp still has the same latent issue.

## D-039 2026-11-06 — Skin sets are named JSON files in persistentDataPath/SkinSets
**Decision:** The Editor and Player share a library of named sets (`SkinSets/<name>.json`, `active.txt`
= current). Names are sanitised to letters/digits/space/-/_ (no path escapes). The controller's
Save/Load now target the active set. The old single `pmsdk_skinset_default.json` is no longer read.
**Why:** artists need several skin layouts per install and a way to pick one for show.
**How to apply:** Player switching re-spawns via `PMSDKSkinLoader.SpawnAll` with `OverrideJsonPath`.
Delete/rename of sets is not built (files can be removed in the folder).

## D-040 2026-11-06 — Output background colour is an app-level setting; Editor never autosaves on quit
**Decision:** `PMSDKSkinStore.BackgroundColor` (`background.txt`, RRGGBB) drives both the preview and
the output camera clear colour. The Workspace's save-on-quit is compiled out in the Unity Editor.
**Why:** a grey wall needs a matching background instead of black; and leaving Play mode in the
Editor raises OnApplicationQuit, which overwrote the saved set shared with the built EXE.
**Bug fixed with it:** `PMSDKSkin.Update` now re-caches while `content == null`; polygon/freeform
skins have no corner pin to trigger the old re-cache, so their video/image was never pushed (white).

## D-041 2026-11-06 — Photo -> skin extraction lives in the C++ SDK (SkinGen module)

**Decision:** New module `pmsdk::SkinGen` (`include/PMSDK/SkinGen/SkinGen.h`, C API
`PMSDK/C_API/SkinGenAPI.h`). Input: a photo, ideally a PNG with a transparent background (also
colour-key and GrabCut for ordinary photos, or a caller mask). Output per detected PART of the
object: pixel mask (cropped), simplified outline polygons with holes, bounding quad. Hosts build
either a "quad + mask" (corner pin) skin or a polygon skin from it. OpenCV use is allowed in SkinGen
as well as Calibration (it is already a pmsdk link dependency).
**Parts:** thick body vs thin appendage via morphological opening (width = `thinPartFraction` of the
object's short side), then touching small pieces merge (statue + trident -> 2 parts); analysis runs at
<= 640 px, final masks at full resolution with every pixel in exactly one part.
**Why:** one implementation for every engine binding; pixel-accurate silhouettes keep holes/thin
features that a 128-point polygon cannot. **Limits:** a photo only registers with the projector if taken
from the projector's viewpoint (or after camera->projector mapping); holes need the mask path.
**Verification note:** algorithm parameters were tuned and cross-checked in Python/OpenCV on the two
fixture PNGs (`tests/data`); the C++ build + gtest run is tracked in tasks.md.

## D-042 2026-11-16 — Unsupported videos: offer an FFmpeg conversion instead of failing silently

**Decision:** A video the player cannot decode raises `PMSDKSkinContent.VideoProblem`; a modal flow offers
to convert it to H.264 MP4 (HD <=1920 wide, or Light <=1280) next to the original (`name_pmsdk.mp4`,
never overwriting; fallback `persistentDataPath/Converted`). FFmpeg is found on PATH, in the registry
PATH (so a fresh install works without restarting), winget and app-local folders. If missing, the user
is asked before an installer runs: `winget install Gyan.FFmpeg`, else a download from gyan.dev that is
SHA-256 checked, copied to `%LOCALAPPDATA%\PMSDKfmpegin` and added to the USER PATH only (no admin).
**Why:** artists hit mkv/HEVC/ProRes files the platform decoder rejects; converting once is the fix.
**Safety:** both the conversion and the install need an explicit click; arguments are passed as a list
(no shell quoting/injection); the original file is never modified; Cancel kills the process tree.
**Caveat:** the installer downloads ~110 MB and trusts the checksum published by the same host; winget
(first choice) verifies its own manifest hash.

## D-043 2026-11-16 - Photo skins in Unity: mask on the quad (primary), background-coloured hole skins (fallback)

**Decision:** A photo part becomes a corner-pin Rectangle skin whose silhouette comes from a grayscale PNG
mask (`SkinMasks/<skinId>.png`, bare file name in `mask.maskFile`) sampled in `PMSDK/UnlitWarp` (`_AlphaMask`,
bilinear, discard below ~0.004). Alternative modes: polygon (<=128 pts) with each hole as a separate polygon
skin using content mode `Background` (the output background colour) at a higher z-order, or outline only.
**Why:** the mask keeps holes and thin features exactly and reuses corner-pin registration; hole skins are
the usual mapping-tool fallback (stacked mask shapes) when only polygons are available.
**Deployment note:** the new DLL links OpenCV dynamically: ship `ProjectionMappingSDK.dll` with the opencv_*/image DLLs.

## D-044 2026-11-16 - Auto Adjust Phase 1: camera verify-and-refine of one planar skin (OAK-D, checker, correction grid)

**Answers to the plan's open decisions (O1-O5):** O1 viewer-perspective - "correct" means the content seen from
the camera is one exact perspective image of the unit square; by default that homography is the least-squares
fit to the detected corners, so the skin stays where the user put it and only its inside is straightened
("keep corners, fix interior"; an explicit camera-space target quad is supported in the C++/C API for later).
O2 OAK-D only (no RGB webcam available). O3 the PNG is a design image run through SkinGen (Phase 2 concern).
O4 manual corner handles. O5 success = RMS <= 3 px and max <= 8 px at camera resolution within <= 4 corrections.

**Decisions:**
1. **Measurement = projected checkerboard, not Gray code.** A 10 x 7 checker (white margin; squaresX+squaresY odd
   so a 180-degree view is told apart by colour; mirrored orderings rejected by handedness) is found with OpenCV
   `findChessboardCornersSB`; when the whole board is not visible, each corner is refined around the previous
   detection and kept only if it shows the checker's saddle pattern (partial occlusion). The camera -> projector
   relation needed for the update comes from the corners themselves (their projector positions are known from
   the current warp), so no Gray-code pass is needed in the loop. Gray code stays for coarse placement (Phase 0/2).
2. **Correction = per-skin offset grid after the corner pin** (`PMSDKSkin.SetCorrection`, 9 x 9 default, bilinear in
   content UV, raster units), fitted by regularised least squares (second-difference smoothness, pinned corner
   nodes), damped (0.8) and step-limited. Applied in C# by `PMSDKMeshWarp` through `IPMSDKWarpOffsetSource` after
   the native warp; persisted as `correction {columns, rows, offsets}` in the skin-set JSON (old files: none).
3. **Quad skins use a 16 x 16 subdivided mesh** instead of 4 vertices (user-approved). A 4-vertex quad is
   interpolated per triangle and shears along the diagonal on a keystoned skin (the artefact D-022 meant to
   remove); the subdivided mesh follows the homography and gives the correction grid vertices to move.
4. **OAK-D frames come from a separate optional plugin `PMSDKOakCamera.dll`** (target `pmsdk_oakcam`, CMake option
   `PMSDK_OAKCAM_BUILD`, OFF by default) linked to the prebuilt depthai-core v2.32.0 (same version HoloTrack ships;
   all 37 imported symbols verified present in HoloTrack's copy) and deployed next to it in `Plugins/HoloTrack`.
   `ProjectionMappingSDK.dll` stays camera-free. The OAK is not UVC (D-034). The plugin uses the dynamic CRT
   (`/MD`) because depthai's C++ API passes STL objects across the DLL boundary. Lens distortion is removed with
   the OAK's factory intrinsics before scoring.
5. **Loop policy** (`PMSDKAutoAdjust`): measure -> correct up to 4 times -> final measure; stop early below 0.5 px;
   keep the best state seen; on abort restore the previous correction. The checker is shown through
   `PMSDKSkin.TestPatternOverride` (content untouched), the photo mask is lifted and other skins hidden while
   measuring, all restored afterwards. Rectangle skins only in Phase 1. The Workspace records one undo step and saves.

## D-045 2026-11-16 - Auto Adjust Phase 2: facets from a white/black flood, corner snap, shape-adapted checker

**Decisions:**
1. **Facets come from the projector's own light.** With every skin hidden, the projector background is flooded
   black then white (all cameras rendering the skin layer, restored afterwards). Facets of a folded surface
   differ in brightness, so the lit area (white - black > 25) is split where the blurred log-brightness
   changes faster than 0.035 per pixel; the border band is then regrown into the neighbouring regions.
   (Thinned Canny edges were tried first and rejected: they leave gaps where an inner fold meets the outline,
   so regions leaked into each other.) Each facet gets an outline and a fitted quad.
2. **Snap**: the facet holding most checker corners is the skin's facet; new corner-pin corners come from the
   camera -> projector homography fitted to the checker corners (exact for a flat facet), the correction grid
   is cleared, and refinement continues against the facet quad (corners not pinned). Opt-in toggle.
3. **Checker adapts to the skin shape** (~5 squares across the short side, proportionally more along the long
   side, sum odd): long thin facets made the fixed 10 x 7 board's squares too elongated to detect.
4. **Ellipse and photo-mask skins** are measured with masks suppressed at runtime (`PMSDKSkin.SuppressMasks`),
   not by editing their data. **Rectangle edge feather** for seams is opt-in (Per-edge feather on a Rectangle,
   `_RectFeather` in `PMSDK/UnlitWarp`; Rectangles ignored it before).
5. **Surface-metric** is reported, not enforced: with a facet size (mm) the score is also given in mm
   (mean edge scale of the facet / content quad). Making squares the same physical size ACROSS facets is a
   content-layout problem (per-facet crop of a shared canvas) and is not implemented.
6. "Adjust all" runs skins one after another in one camera session (OAK start takes ~2 s).

## D-046 2026-11-16 - Auto Adjust Phase 3: per-skin warp map (raster -> content UV) for curved surfaces

**Decision:** Gray code (robust, with inverses) is projected in the skin's CONTENT space through its current
warp, so decoding gives camera pixel -> content UV directly. "Correct" stays viewer-perspective (D-044): the
target UV of a camera pixel is the best-fit perspective (normalised homography) camera -> UV. Each sample's
projector position comes from the skin's warp (batch native evaluation), samples are splatted onto a map over
the skin's raster bounds and holes are filled by pull-push, limited to 12 map pixels from data. The map file
(`<skinId>_warp.pmwm`, small binary format, `PMSDKWarpMapFile`) lives next to the photo masks; the shader samples
it at the raster position (UV1) and uses the result as the content UV (RG premultiplied by validity so bilinear
filtering never mixes in invalid texels). Masks keep using the mesh UV. **Why a map, not a finer grid:** it
follows any smooth shape at map resolution and costs one texture fetch. **Lifecycle:** the map is built last;
running Auto Adjust (which changes geometry) switches it off and says so.

## D-047 2026-11-16 - Auto Adjust Phase 4: surfaces from OAK-D depth, placed with a full-raster Gray-code map

**Decisions:**
1. `PMSDKOakCamera.dll` optionally streams stereo depth aligned to the colour camera at the colour output size
   (StereoDepth DEFAULT preset, LR check, subpixel; per-pixel median of 5 frames in C#), so depth pixels share
   the colour intrinsics and the Gray-code camera pixels.
2. Planes: sequential RANSAC on the back-projected cloud (every 4th pixel), least-squares refit, and only the
   largest image-connected piece per plane (parallel facets elsewhere become separate surfaces). Pixel rays are
   undistorted with the lens model first: on the wide OAK lens, ignoring distortion bends a flat wall (tested).
3. Placement: a temporary full-raster skin shows Gray code, giving a dense camera -> projector map; per plane,
   the decoded pixels inside its outline give an exact camera -> raster homography (after undistortion), which
   maps the plane's quad to the new skin's corners. Planes not lit by the projector, seen at > 75 degrees, or
   mapping far outside the image are skipped with a reason. Measured plane size becomes the skin's facet size.
4. Click-to-segment and PNG-to-scene feature matching stay out (the user wants to study them first).
**Unverified:** whether OAK's aligned depth is in the raw (distorted) colour geometry, as assumed here.

## D-048 2026-11-16 - Auto Adjust: measure the skin border, keep-corners target from the observed corners

**Problem (found in the sim walkthrough):** the checker's inner corners stop one margin plus one square short
of the skin border (~18 % per side), so Phase 1 never measured that band; the grid there was only
extrapolated, and the score (checker corners only) could pass while the edges still bent. On the waved sim wall
the whole-skin error went 13.3 -> 16.5 px max while the checker score went 1.03 -> 0.18 px.
**Decisions:**
1. `PMSDKSkinEdgeProbe` (managed, no native change) reads the camera profile along the edge normal at evenly
   spaced points on each border (one per checker square along that edge), takes the outermost strong
   bright-to-dark step (white margin -> dark outside) to sub-pixel precision, undistorts it, and drops samples
   that disagree with every line through two of their nearest neighbours on the same edge.
2. Only the distance across the edge is observable, so a border sample is scored as the ideal point moved by
   that normal distance alone (`AcrossOnly`). Border points join the checker corners in Measure/Refine; the
   score and status now count both ("N corners, M border points").
3. The target is fixed before the border points join, as an explicit quad: the facet quad after a snap;
   with pinned corners ("keep corners", O4) the skin's observed corners, where lines through the two border
   points nearest each corner meet, so the edges become straight between corners that do not move; otherwise
   (or if a corner cannot be found) the best fit to the checker corners. Previously the target was the checker
   best fit, which conflicts with pinned corners as soon as the border is measured.
4. Rectangle skins only (Ellipse borders are not straight); `PMSDKAutoAdjust.MeasureSkinEdges` (default on)
   switches it off. `MeasureSkin` (warp-map verification) still scores checker corners only.
**Sim result:** worst point off the straight quad through the corners 13.0 -> 4.8 px, whole-skin best fit max
13.3 -> 3.3 px, checker interior 0.24 px RMS. **Unverified on hardware:** real margin contrast and how close the
OAK sees the border to other lit surfaces.


## D-049 2026-11-17 - Map a surface marked in the camera image (hook for the AI cutout)

**Context (first hardware run, OAK-D + soffit):** the user wants to choose the surface on a camera photo; today
by hand (red outline on the raw OAK frame), later by an AI cutout of the object. The marked soffit band spans
several facets, so a plane fit (Find surfaces) or a plain corner-pinned quad cannot describe it.
**Decisions:**
1. Input is a camera-space mask (raw OAK size, row 0 = top, > 127 = surface). This is the single hook: a
   hand-drawn mark and an AI cutout produce the same thing, nothing downstream changes.
2. `PMSDKSurfaceFinder.MapMarkedSurface` projects Gray code through a temporary full-raster skin (same probe as
   Find surfaces), keeps the decoded samples inside the mask, and creates ONE Rectangle skin = raster bounding
   box of those samples, cut to their raster footprint by an alpha mask (mesh UV is a plain rescale of raster
   position). Folds need no model. Gray-code bit errors (~0.1 % of samples on hardware) land as isolated specks:
   the footprint's small islands (< 2 % of the largest, found on the raw splat before gap closing) are dropped.
3. `PMSDKWarpMapBuilder.SetCameraRegion` restricts the warp map to the same camera mask. Without it a reflection
   of the surface (the glass doors under the soffit) decodes to the same projector positions from other camera
   pixels and the map turns to speckle.
4. Probe skins use `ZOrder` = one above every skin. The old 100000 put the probe 50 units in front of the
   projector camera, outside its clip range, so Find surfaces / mapping projected nothing on hardware (the sim
   ignores depth, so tests never saw it).
**Hardware facts (2026-11-17):** auto exposure swings between the white and black frames (means 116 vs 211) and
breaks decoding; with exposure locked (3000 us, ISO 400) inside the mark white 243 / black 73. With that,
117,598 of 117,664 marked pixels decoded, 99 strays dropped, skin made in 13.8 s; warp map from 29,390 samples
in 12.3 s; the grid then reads straight and evenly spaced across the fold from the camera. The default settle (3
Unity frames + 3 camera frames) was enough at 15 fps.

## D-050 2026-11-17 - Workspace: locked exposure, marked-surface buttons, camera region kept per skin

**Context:** hardware run 1 (D-049) was driven from code; the user asked for buttons and a default exposure lock.
**Decisions:**
1. The Auto Adjust panel creates every OAK camera with a locked exposure: field "Exposure ms (0 = auto)",
   default 3 ms at ISO 400 (`PMSDKAutoAdjustPanel.DefaultExposureMs`), the value that worked on the soffit. No
   native change (the plugin already had `pmoak_set_manual_exposure`); locking the auto value in place would
   need a DLL rebuild and stays a follow-up.
2. File hand-off, no in-app drawing: "Camera photo" saves the raw OAK frame to
   `persistentDataPath/CameraMarks/camera.png` and opens the folder; the user (or an AI cutout) saves
   `camera_marked.png` next to it; "Map marked surface" reads it (`PMSDKCameraMark`). Accepted: a copy of the
   photo outlined or filled in pure red (filled at the file's own resolution so a thin outline stays closed), or
   a black-and-white mask (white = surface, the AI format). Same size as the photo or a whole-number upscale.
3. The camera region is kept per skin by name, `<skinId>_camera.png` in SkinMasks (no JSON schema change);
   "Build warp map" uses it automatically when present (`SetCameraRegion`), otherwise every lit pixel as before.
4. Warp-map default pattern stays 512: with the camera region it gave a clean map on hardware (the earlier 512
   speckle was the glass reflection).

## D-051 2026-11-17 - Physical grid spacing: surface-aware validation and a physical-spacing warp-map target

**Question (user, with photo `HardwareTest~/result_grid_warpmap.png`):** cells look stretched / compressed between POP
facets and some lines look bent; find the root cause and make the grid physically uniform on each facet.
**Findings (code + hardware):**
1. No UV-island layer exists: a skin is a 2D projector-raster quad whose mesh UV is the unit square (16 x 16
   subdivided). UV layout / "Average Islands Scale" cannot be the cause.
2. Camera lens distortion is already removed before every fit (factory intrinsics, 14 coefficients). The reference
   photo is the RAW frame: undistorted (`HardwareTest~/result_grid_warpmap_undistorted.png`) its lines are straight
   across the fold. The visible bending was the lens.
3. Root cause of the physical non-uniformity: the warp-map target (D-046, decision O1 "viewer perspective") is ONE
   best-fit homography of the undistorted camera image over the whole skin, i.e. "looks flat from the camera". On a
   folded POP band that forces different physical cell sizes on differently oriented facets (sim: -35 % / +57 %).
   This is by design of O1, not a bug in the mesh, the projector warp or the camera model.
4. No projector intrinsics/extrinsics exist; projector geometry is measured densely by Gray code instead, which
   already includes projector lens and keystone.
**Decisions:**
1. `PMSDKSurfaceGeometry`: facets from depth planes, every camera pixel assigned (outline, then measured depth near
   folds), facets unfolded about their fold lines into one mm frame (distances along the surface).
2. `PMSDKSurfaceMetrics` + `PMSDKSurfaceReport`: a physically correct projection maps content UV to the unfolded
   surface by one affine map; per facet spacing error (%), cell size, max cell-dimension error, straightness (mm),
   shear, fold transitions (jump mm, spacing change %), overall RMS / P95 / max after decode-outlier rejection,
   PASS / FAIL against `PMSDKSurfaceTolerance` (default RMS 3 mm, P95 6 mm, facet spacing 2 %, shear 1 deg, fold
   jump 5 mm). Camera pixels never enter the error terms.
3. `PMSDKWarpMapTarget.PhysicalSpacing` in `PMSDKWarpMapBuilder` (camera perspective stays the default): target UV =
   unfolded mm (trimmed extent, optional `ContentAspect`), then measured through the map and corrected in closed loop
   (`RefinePasses`, a pass is kept only if RMS drops >= 5 %). Both targets now drop isolated Gray-code decode errors
   (a sample must agree with 2 camera neighbours).
4. Diagnostics: `PMSDKCalibrationGrid` (labelled "column.row" grid), `PMSDKSurfaceDiagnostics` PNG (facet tint,
   expected vs measured intersections, error vectors), reports in `persistentDataPath/SurfaceReports`.
5. Panel: "Physical spacing (depth)" toggle, "Content aspect", "Show calibration grid", "Measure on surface (depth)".
6. `PMSDKSurfaceGeometry` refuses when < 60 % of the surface region lies on measured facets, instead of flattening
   the surface onto wrong planes.
**Hardware result (2026-11-17, OAK-D Pro W, soffit 1.6-2.9 m away):** depth is 85-97 % valid but cannot resolve the
facets: inside the marked band RANSAC finds parallel slabs with one normal (~60 deg view angle, 13-17 mm RMS),
16 px patch noise 9-15 mm, projected random dots do not help; the native detector finds 0-1 planes. The physical
target therefore cannot be built from OAK stereo depth on this installation; the guard reports it. Simulation:
camera-perspective map RMS 326 mm FAIL -> physical-spacing map RMS 1.39 mm PASS; lens model required (no model: RMS
103 mm FAIL); closed loop converges in one pass. **Next:** metric projector-camera calibration + native
`pmsdk_decoder_decode_and_triangulate` (large baseline, mm-level geometry) as the geometry source for
`PMSDKSurfaceGeometry` - or the camera much closer / OAK IR dot projector (native change) for depth.

## D-052 2026-11-17 - Projector-camera self-calibration and triangulated depth for physical spacing

**Context:** D-051 needs facet geometry; OAK stereo cannot resolve the POP at ~2 m. With the OAK on top of the
projector (baseline ~1/25 of the distance) projector-camera triangulation was just as coarse (~100 mm per Gray step).
The user moved the OAK ~1 m to the right of the projector.
**Decisions:**
1. `PMSDKProjectorCalibration` (managed, no DLL change): camera intrinsics known; projector = pinhole, square pixels,
   cx = raster centre, unknown f and cy. Normalised 8-point fundamental matrix with RANSAC (Sampson, 2 px), self-
   calibration from the equal-singular-value constraint of Kp^T F Kc (grid + local refinement), pose from E
   (cheirality), midpoint triangulation, metric scale from the OAK depth median. Refuses single-plane scenes
   (homography explains >= 90 % of F inliers) and poor fits (median reprojection > 2.5 px). Output includes a
   triangulated depth image of everything the projector lights, saved in `persistentDataPath/ProjectorCalibration`.
2. `PMSDKSurfaceFinder.CalibrateProjector` (full-raster Gray code, 1024 cells) also rewrites the camera region of every
   marked skin from its projector-space quad + alpha mask (largest piece), so moving the camera needs no re-marking.
3. `PMSDKWarpMapBuilder.ProjectorCalibration`: when set (same camera size) its depth replaces OAK stereo for the
   physical-spacing target and measurements; depth outside the camera region is ignored for plane detection.
4. Panel: "Calibrate projector" + status line; the saved calibration is loaded at start. Valid only while camera and
   projector stay put.
5. Editor `Open On Primary` keeps the preview topmost (the chat window had covered a capture).
**Hardware (2026-11-17, OAK ~1 m right of the projector):** calibration f 901 px @1920 (throw ratio 0.47), cy 1.04 H,
baseline 913 mm, median reprojection 0.46 px, 77 % inliers; first camera position gave 885 px / 1.02 H independently.
Triangulated patch noise ~3.5 mm. The marked band is ONE plane (view angle ~38 deg); the zig-zag is its outline.
Measured on the surface: camera-perspective map RMS 154 mm, P95 311 mm, shear 13 deg -> physical-spacing map RMS
3.27 mm, P95 5.89 mm, shear 0.01 deg, straightness 3.0 mm RMS. Formally FAIL on RMS (limit 3 mm); the closed-loop pass
could not improve it (rolled back), consistent with the ~3 mm measurement noise floor. Limits were not changed.

**D-052 addendum (2026-11-17, repeatability):** the OAK was bumped after the first calibration (frames differ by
~119 px); recalibrated (f 905 px, cy 1.04 H, baseline 892 mm, 0.39 px - third estimate, all within 2 %), no drift
during the runs (0.05 px). Two measurements of the SAME physical-spacing map: noise 1.5 mm RMS per run (P95 3.6 mm);
systematic 7.5 mm RMS (P95 12.7 mm, max 29 mm), a smooth bowl rising toward the band ends
(`HardwareTest~/repeatability_systematic_error.png`). The earlier 3.27 mm was self-consistent (same calibration built
and measured the map), not a noise floor - that claim is withdrawn. The bowl matches unmodelled lens distortion
(projector modelled as a distortion-free pinhole; UST lenses distort toward the edges). Next: projector radial
distortion in the calibration (non-linear refinement) and an independent tape-measure check of cells on the POP.

## D-053 2026-11-17 - Measured 3D points instead of ideal planes; projector distortion not added

**Evidence:** (1) offline LM fit of projector radial distortion on the real correspondences: k1 0.008, k2 -0.003,
reprojection 0.31 -> 0.25 px, band flatness unchanged - distortion does not explain the 7.5 mm systematic error, so it
was NOT added (the user had asked for it on my earlier hypothesis). (2) The triangulated band deviates from its best
plane by +-5 mm (ridge along the inner corner and the lower edge, `HardwareTest~/band_flatness.png`); placing pixels where
their ray meets the IDEAL plane shifts them sideways by deviation x tan(view angle ~42 deg) = 4.5 mm RMS, 9 mm P95,
different per camera position - matching the measured systematic error.
**Decision:** `PMSDKSurfaceGeometry.TryBuild(..., useMeasuredPoints)`: with precise dense depth (projector triangulation)
each pixel uses its measured 3D point (depth box-filtered 9 px), flattened into its facet's unfolded frame; planes still
define facets and folds. Points > 20 mm off their facet's plane (side faces, smoothing across edges) are dropped.
`PMSDKWarpMapBuilder` enables it whenever the projector calibration supplies the depth. OAK stereo keeps ray-plane.
**Hardware (camera position B, verified still since calibration: 0.03 px):** old map (built at A with ideal planes)
measured with points: 8.27 mm RMS / 15 mm P95. Rebuilt with points: 3.14 mm RMS / 5.5 mm P95 / shear 0.01 deg /
straightness 2.85 mm - self-consistent (same calibration builds and measures); independent confirmation still open.

**D-053 addendum (2026-11-17, independent check):** OAK moved 30 cm further right (position C; the band's left end is
near the image edge, seen at a grazing angle). Calibration C: f 900 px, cy 1.05 H, baseline 1089 mm, 0.41 px (4th
estimate, all within 2 %). The map built at B (3.14 mm self-measured) measured from C: 8.48 mm RMS / 14.3 mm P95 on
the full region (largest vectors at the band's lower fold edge), 6.68 mm RMS / 11.7 mm P95 with the region eroded by
15 px. Cell size B 205.8 x 115.1 mm vs C 199.0 x 111.5 mm (3 % scale difference from the OAK-depth scale). The
self-consistent 3.14 mm is NOT confirmed; two calibrations disagree by ~7 mm inside the band. Which one is closer to
reality needs ground truth (tape measure). Candidates: calibration drift between positions (self-calibration has no
redundancy beyond 2 unknowns), camera lens model at the image edge, depth-median scale.

**D-053 addendum (2026-11-17, phone photo, calibration-independent):** user photo from a third viewpoint
(`HardwareTest~/Media (4).jfif`, analysis `phone_photo_check.py`). Grid lines straight to 1.7 px median / 3.8 px P95
(~line width). Cross-ratio of 4 consecutive lines (= 4/3 for equal spacing in any perspective): along the band 63
quadruples, median spacing error -0.03 cell (~6 mm of a ~200 mm cell), no trend from end to end; across the band
inconclusive (rows 8-15 px tall in the photo, label boxes interfere). Consistent with the 3-8 mm camera measurements
and with the user's visual judgement; absolute scale and vertical spacing still want a tape check.

## D-054 2026-11-17 - Leave off-surface areas dark in physical-spacing warp maps; user guide

**Problem:** the marked skin's footprint included a thin side face below the band's lower fold. Its pixels were
rejected as off-surface, but the warp map's gap filling still spread content there (ragged grid fragments).
**Decision:** `PMSDKWarpMapBuilder.BlankOffSurface` marks map texels invalid where off-surface samples outnumber surface
samples (after the first build and after every correction pass; `MapSamples.OffSurfaceRaster`), so the shader leaves
them dark. The off-surface tolerance is 15 mm (~3x the band's noise + relief) instead of 20 mm. The skin outline
itself is unchanged. Hardware (position C): rebuilt band map 3.07 mm RMS / 5.7 mm P95 self-measured; side face dark
(`HardwareTest~/edge_fix_before_after.png`). User guide: `docs/auto-adjust-user-guide.md`.

## D-055 2026-11-17 - One-click "Calibrate skins" for hand-drawn skins (Polygon included)

**User requirement:** draw skins by hand (Rectangle or Polygon), press one button, and the camera makes the content
equal-sized on the surface - no new skins, no photo marking. Hardware run showed: warp maps refused Polygon skins
(buttons greyed), Find surfaces / Map marked surface created unexpected skins, and a leftover "Surface probe" was saved.
**Decisions:**
1. Warp maps on Polygon / Freeform skins: the polygon mesh carries its raster position in UV1
   (`PMSDKSkinMeshBuilder.BuildPolygonMesh`), `PMSDKSkin` enables `_WarpMap` for them, and
   `PMSDKWarpMapBuilder.ContentToRaster` maps content UV (= outline bounding box) to raster.
2. No marked region needed: without a region file the camera region is the largest connected piece of the pixels the
   skin itself lit during its capture (kept for that run only).
3. Moved-rig detection: `PMSDKProjectorCalibration.MedianEpipolarErrorPx` of the skin's correspondences against the
   stored calibration; > 3 px -> `PMSDKWarpMapResult.CalibrationStale`.
4. Panel: **Calibrate all skins** / **Calibrate selected skin** run: projector calibration if none, then a physical-spacing
   warp map per skin; on a stale calibration it recalibrates once and retries. Other tools are listed under "Advanced".
5. The temporary probe skin (`PMSDKSurfaceFinder.ProbeId`) is skipped when a set is captured, applied or loaded.
**Verification (simulation only, hardware tomorrow):** polygon physical map through the real build path (synthetic
camera + depth, no region): 5.0 mm RMS / shear 0.1 deg (<1 camera px at 7 mm/px); polygon map on exact geometry: PASS;
stale detection: < 1 px still, > 3 px bumped; probe never captured. The panel button wiring itself is not exercised in
simulation (the synthetic camera is not 3D-consistent, so projector calibration cannot run there); every step it calls
is tested.

## D-056 2026-11-18 - Despeckled warp maps and spike-robust measured depth

**Hardware finding:** first "Calibrate all skins" run on the user's Polygon skin over the POP: the button chain worked,
but the projected content showed coloured specks and black dots. The saved map (`051066dd_warp.pmwm`) had isolated
texels 0.1-0.2 UV (2-3 grid cells) off their neighbours and 1-2-texel holes inside the surface; the build report was
FAIL (211 mm RMS, driven by those outliers). The triangulated depth is ~1 mm on the faces, with 1-2 % spike pixels and
bad values along edges.
**Decisions:**
1. `PMSDKWarpMapBuilder.DespeckleMap` runs after `BlankOffSurface` on every built / corrected map: a valid texel more
   than 0.02 UV from the median of its valid 8-neighbours (>= 5) takes that median; an invalid texel with >= 6 valid
   neighbours is filled; up to 4 passes (clusters). Outlines and blanked side faces (>= 2 texels wide) are kept.
   On the real map: 5316 -> 119 rough texels.
2. `BlankOffSurface` blanks a texel only when off-surface samples also dominate its 3x3 neighbourhood (no lone dots).
3. `PMSDKSurfaceGeometry.SmoothDepth` replaces pixels > 30 mm from their 3x3 median by that median before the 9 px box
   mean (replace, not drop, so the window stays symmetric on steep surfaces). Sim: 2 % spikes of 300-1500 mm move
   points < 2 mm and drop < 0.5 %; on the real depth the gain is small (the depth there was mostly clean).
**Not verified:** a new hardware build with these changes; the source of the wrong samples is not proven (decode errors
through the content or edge depth) - the despeckle treats the symptom generically.

## D-057 2026-11-18 - Reuse the projector lens when only the camera moved

**Hardware finding:** after the camera was moved, "Calibrate all skins" recalibrated the projector and self-calibration
returned a degenerate lens (focal 249 px, lens centre 0.40 H, baseline 2.26 m; earlier runs: ~900-923 px, ~1.02-1.05 H,
~1.1-1.2 m). The map then evened out cells in a distorted space (report self-consistent at 4.5 mm, but cells 335 x 130 mm
and visibly stretched squares).
**Decisions:**
1. `PMSDKProjectorCalibration.TrySolve(..., previous, ...)`: a plausible earlier lens (focal, principal y) is reused and
   only the pose is solved (`LensReused`); self-calibration runs only without one or when it no longer fits (median
   reprojection > 2.5 px, e.g. zoom changed). `PMSDKSurfaceFinder.CalibrateProjector` loads the saved calibration as
   `previous` before overwriting it.
2. `IsPlausibleLens`: throw ratio 0.3-4, lens centre y -0.5..1.5 H; an implausible self-calibrated lens is refused with
   an error instead of being saved.
**Note:** the calibration saved at 10:35 has the degenerate lens, so it is not reused; the next calibration
self-calibrates again (and now fails loudly if it is degenerate).

## D-058 2026-11-18 - Fixed projector lens and a simplified inspector

**Hardware finding:** after D-057, recalibrating from the moved camera self-calibrated again and found another wrong
lens (focal 674 px, centre y 0.68 H vs ~900-923 px / ~1.02-1.05 H); the map had 688 x 309 mm "cells" and large blanked
(black) areas. Self-calibration of the lens is not reliable from every camera position; the lens never changes.
**Decisions:**
1. The projector lens can be fixed: `PMSDKProjectorCalibration.SaveLens / TryLoadLens / ClearLens`
   (`ProjectorCalibration/lens.json`). When set, `CalibrateProjector` solves only the camera pose
   (`TrySolve(..., lockLens: true)`) and fails with a message when the lens does not fit - no silent self-calibration.
2. The inspector shows only Calibrate all / selected skin, Calibrate projector, Clear selected skin, Show calibration
   grid and the status lines; exposure, projector lens, content aspect, Build warp map, Measure, Physical spacing, the
   older checker tools and the skin-creating tools are under "Show advanced settings".
3. The calibration distance is an output, never an input: the tape distance is only a check of the reported baseline.
**Verification:** simulation (fixed lens used / wrong fixed lens refused / lens file round trip). Office lens values
(923 / 1103) come from the 10:22 calibration and agree with the four earlier ones within ~3 %; not yet confirmed from
the new camera position on hardware.

## D-059 2026-11-18 - Projector lens learned from all camera positions; enclosed map holes filled; probe listing fix

**Hardware finding:** single-position self-calibration gave 249, 674 and 884 px for the same lens from three camera
positions. After two recalibrations (884 px, centre 1.10 H) the cells were equal-sized again, but black patches stayed
inside the skin (enclosed holes where samples near folds were judged off-surface) and the temporary probe skin stayed
in the skin list ("2/2: Surface probe").
**Decisions:**
1. Each projector calibration stores what it measured about the lens (fundamental matrix + camera K) in
   `ProjectorCalibration/lens_history.json` (newest 12). The lens is solved from the current and all stored positions
   together (sum of essential-matrix costs), inside the plausible range; a calibration saved before the history
   seeds it through its pose (`ObservationFromPose`). Order: fixed lens (Advanced) > learned lens > previous lens >
   error. "Forget projector lens (zoom changed)" clears the fixed lens and the history.
2. `PMSDKWarpMapBuilder.FillEnclosedHoles` after the despeckle: invalid texels not connected to the map border are
   filled inward from their valid neighbours; edge-connected blanks (side faces, outside) stay dark. Real map: 353
   texels filled.
3. `PMSDKSurfaceFinder.RemoveProbe` deactivates the probe before Destroy, so the skin-list refresh in the same frame
   no longer lists or selects it.
**Limits:** an enclosed side face inside a skin would now be filled with content; the outermost margin (no camera
samples, extrapolated) can still look wavy. Joint lens estimation verified in simulation only.
