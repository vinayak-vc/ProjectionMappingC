# HoloTrackSDK — Architecture

Head-Tracked Holographic Projection System. Independent product/DLL (D-029); creates the
illusion of a floating 3D hologram by rendering the content camera from the real-time
position of the viewer's head, tracked by an OAK-D-PRO-W-97 stereo depth camera. Not VR/AR:
a perspective-correct fixed display whose rendered frustum follows the physical eye.

## Boundaries (what belongs where)

```
   OAK-D-PRO-W-97 (on-device: stereo depth + RGB + spatial person detection)
                              │  DepthAI packets (bg thread)
                              ▼
   ┌───────────────────────── HoloTrackSDK.dll (C++, holotrack::) ─────────────────────────┐
   │  IDetectionSource  ──►  ViewerSelector ──►  HeadEstimator ──►  IHeadFilter             │
   │  (OakDevice | Sim)      (nearest/bbox,       (pose→head, or    (Exp | OneEuro | Kalman) │
   │                          hysteresis, id)      bbox+depth+ht)         │                  │
   │                              │                                       ▼                  │
   │                         TrackingStateMachine (Searching→Tracking→Prediction→Lost)       │
   │                              │  smoothed head pose (camera space)                       │
   │                         CoordinateTransform (OAK→world, configurable T/R/S, JSON)       │
   │                              │  head pose (world space)                                 │
   │                         OffAxisProjection (display corners + eye → view+proj Matrix4)   │
   └──────────────────────────────┼──────── C-API (HoloTrack/C_API, HT_*) ───────────────────┘
                                   ▼
   Unity package com.viitorx.holotrack (P/Invoke + MonoBehaviours)
     PMHTHeadTracker → HeadTrackedCameraController (sets Camera.worldToCameraMatrix +
     projectionMatrix) → the content camera
                                   ▼
   [consumer scene] content camera output → (separate) projection-mapping package warp/blend
                                   ▼
                            Ultra-short-throw projector
```

- **HoloTrackSDK does no rendering and no engine work** — same rule as PMSDK. It computes a
  head pose and the off-axis matrices; Unity applies them.
- **It never links `ProjectionMappingSDK`.** It reuses only the header-only `pmsdk::Math`
  value types at compile time (no runtime dependency). Composition with projection mapping is
  the consumer scene's job (D-029).

## Layering

```
            +----------------------------+
            |   C-API (HoloTrack/C_API)  |  stable ABI: opaque handle, status codes, HT_GetLastError
            +----------------------------+
            |  Tracker (orchestration)   |  owns source→select→estimate→filter→state machine
            +----------------------------+
            | Selection | Estimation |   |  ViewerSelector, HeadEstimator
            | Filters  | StateMachine |   |
            +----------------------------+
            | Transform | OffAxisProjection |  pure math, uses pmsdk::Math
            +----------------------------+
            |   Detection / Viewer types |  POD-ish value types
            +----------------------------+
   Device (OakDevice) — gated behind vcpkg feature `depthai`; implements IDetectionSource only
```

Pure logic (everything except `Device`) has no camera/DepthAI dependency and is unit-tested
with synthetic detections (D-032).

## Core types

- `Detection` — one detected person for one frame: 2D bbox (normalized), metric spatial
  position `Vector3` (OAK space, metres, from stereo depth), confidence, optional pose
  keypoints (nose/eyes/neck/…; present only when a pose network ran), monotonic timestamp.
- `TrackedViewer` — persistent viewer across frames: stable `id`, filtered head `Vector3`
  (camera space and world space), velocity, confidence, `TrackingState`.
- `TrackingState` — `Searching | Tracking | Prediction | Lost`.

## Pipeline stages

1. **IDetectionSource** — `Poll()` returns the latest frame's detections. `OakDevice` runs the
   DepthAI pipeline (RGB + stereo + `MobileNetSpatialDetectionNetwork`) on a background thread
   and hands the most recent packet to the tracker; `SimulatedSource` replays a scripted/
   recorded path for tests and no-hardware dev.
2. **ViewerSelector** — chooses the active viewer among detections (nearest Z, or largest
   bbox — configurable). Maintains a stable id and applies **hysteresis**: a challenger must
   beat the incumbent by a margin for a sustained number of frames before the id switches, so
   the system does not flip between two similar viewers. Associates detections to the incumbent
   by nearest-position gating.
3. **HeadEstimator** — turns the selected detection into a head-centre `Vector3`. If pose
   keypoints are present: head centre from nose/eyes/neck. Else: bbox top-centre lifted by a
   fraction of body height, placed at the detection's spatial Z (D-031). Always outputs a
   position.
4. **IHeadFilter** — smooths the head position; selectable at runtime:
   - `ExponentialFilter` — one-pole EMA, single `alpha`.
   - `OneEuroFilter` — adaptive low-pass (`minCutoff`, `beta`, `dCutoff`); low jitter at rest,
     low lag when moving. Recommended default.
   - `KalmanFilter` — constant-velocity model (position+velocity state), also yields the
     velocity used by Prediction.
5. **TrackingStateMachine** — `Searching` (no viewer) → `Tracking` (viewer held, filter fed) →
   `Prediction` (viewer lost momentarily: extrapolate from last velocity for up to
   `predictionTime`) → `Lost` (prediction expired: drop id, back to `Searching`). Recovers
   automatically after transient occlusion; timeout/prediction window configurable.
6. **CoordinateTransform** — rigid OAK→world transform (translation, rotation quaternion,
   uniform scale), configurable and JSON-serializable (spec §6/§9). Supports arbitrary,
   off-centre camera mounting.
7. **OffAxisProjection** — given the physical display surface (4 world-space corners:
   bottom-left, bottom-right, top-left) and the world-space eye, builds the view matrix
   (eye at the display's orthonormal basis) and an asymmetric projection matrix (Kooima)
   (D-030). Output: `Matrix4 view`, `Matrix4 projection` for Unity to apply.

## Threading & performance (spec: 60 fps min, 90 target, <20 ms latency)

- The OAK device runs on its own thread; the tracker reads the **latest** detection frame
  (triple-buffered / mutex-guarded swap) so the Unity main thread never blocks on USB.
- Per-frame work on the main thread is O(detections) arithmetic — no allocations. Unity-side
  buffers are reused (the `PMSDKMeshWarp` zero-alloc pattern).
- Filters and the state machine are branch-light constant-time updates.

## Persistence

Configuration (filter choice + params, timeouts, prediction time, depth min/max, hysteresis,
movement scale, dead zone, and the OAK→world calibration T/R/S) is a plain struct,
JSON-serializable, editable from the Unity inspector via a `HeadTrackingConfig`
ScriptableObject. No hardcoded values (spec §13).

## Diagnostics & recording (Unity side)

- Runtime overlay: tracking FPS, depth FPS, viewer position, confidence, state, distance,
  latency, active filter, tracking id (spec §11).
- Scene-view gizmos: skeleton, bbox, head point, camera frustum, depth point.
- CSV recorder: timestamp, head position, state, confidence, latency (spec §12).

## Extensibility (designed-for, not built in v1)

Multi-user (selector already reasons over a viewer list); multiple projectors / edge-blend
(downstream, in the PM package); stereo (off-axis already gives per-eye frusta); networked /
multi-OAK sync (new `IDetectionSource` implementations); automatic OAK→world calibration
(replace the manual T/R/S with a solved transform later).
