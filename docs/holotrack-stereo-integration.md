# HoloTrack × SBS-3D — Integration Brief (H7)

Spec for the head-tracked **SBS 3D** holographic path: drive the two stereo eyes from the
tracked head center ± IPD/2, each with a true off-axis frustum against the one physical screen
(fish-tank stereo). Derived from the `holo-stereo-design` research pass (SBS rig + HoloTrack
off-axis seam + OAK detection/FPS + ProBuilder recipe). Decisions below are LOCKED (user chose
"both" detection; other defaults taken as recommended).

## Facts that shaped this
- OAK detects **person boxes + metric XYZ** now (MobileNet-SSD spatial); head is *estimated*
  from the body box. A **face NN** localizes the head directly at the same ~30 FPS.
- On-device NN tops out **~30 FPS** (Myriad X). 120 FPS is a **render** target, reached by the
  already-built One-Euro/Kalman + velocity **prediction** decoupling detection from render
  (~1–2 detection-frames latency). SBS halves per-eye horizontal res (~960 cols/eye); the 120
  FPS bottleneck is GPU/render headroom, not the 30 FPS VPU.
- Existing `PMSDKStereoContentRig` (game repo, `main`) does symmetric off-axis with the eye
  center **hard-locked to its own camera transform** — fixed-viewpoint, not head-tracked. The
  arbitrary-eye solver `holotrack::ComputeOffAxis(pa,pb,pc,eye,near,far)` exists natively but is
  **not surfaced to Unity** (C-API/`PMHTHeadTracker` bind the eye to the tracker's internal head).

## Locked decisions
1. **Detection = both**: face NN primary (direct head, ~30 FPS), **person-SSD fallback** when the
   face is lost. `OakOptions.detectionMode ∈ {Person, Face, FaceThenPerson}`, default `FaceThenPerson`.
2. **Thin generic driver** `HeadTrackedStereoController` in the SDK package (reusable; references
   only a tracker + surface + two `Camera`s) + a tiny game-side rig hook — do NOT couple the rig
   to the SDK.
3. **IPD = 0.06 m**, serialized (tunable per install).
4. **Window = 2.0 × 1.2 m**, matching `HoloTrackDemo`'s `HeadTrackingDisplaySurface`.
5. **Zero-parallax = the screen plane** (automatic on the off-axis path). If the rig's symmetric
   `SceneCameras` path is used, set `ZeroParallaxDistance` = eye-to-window (not the default 6).
6. **Sim source drives a head proxy** for no-hardware validation (parallax sign, pop-out clipping,
   120 FPS decoupling); still require an on-OAK pass before sign-off.

## Work items

### SDK repo (branch `Head-Tracked-Holographic-Projection-System`) — CLOUD-JOB SCOPE
Reuses `ComputeOffAxis`, `HtOffAxis`/`ToMatrix`, `HeadTrackingDisplaySurface.GetCorners`,
`PMHTHeadTracker.HeadPositionWorld`, the `ApplySafety` pattern.
1. **Native**: add stateless C-API export `ht_compute_offaxis_eye(pa, pb, pc, eye, near, far, out)`
   in `TrackingAPI.{h,cpp}` wrapping `ComputeOffAxis` (no tracker handle). Update `HoloTrackHarness`
   with a check (eye offset shifts the frustum; identity/degenerate guarded).
2. **Binding**: P/Invoke line in `HoloTrackNative.cs`; new `PMHTHeadTracker.TryComputeOffAxis(bl,br,tl,
   eye, near, far, out view, out proj)` overload calling it.
3. **Generic component** `Runtime/HeadTrackedStereoController.cs` (package): fields `tracker`,
   `surface`, `leftCamera`, `rightCamera`, `ipd=0.06`, `near`, `far`, `restEye`/safety (lift
   `ApplySafety`). `LateUpdate`: `H=ApplySafety(HeadPositionWorld); GetCorners; r=(br-bl).normalized;
   eyeL=H-r*ipd/2; eyeR=H+r*ipd/2;` two `TryComputeOffAxis(...eye...)` → set each camera's
   `worldToCameraMatrix`/`projectionMatrix` + `transform.position`. Eye-order swap flag for inverted depth.
4. **Detection modes**: extend `OakDevice`/`OakOptions` + `ht_oak_options_t` with `detectionMode`;
   support a face blob path + person blob path; `FaceThenPerson` runs face, falls back to person when
   no face for N frames. Feature-gated as before; stub path unchanged.
5. **Verify** (no hardware): feature-OFF DLL builds; harness green; if a Unity editor is reachable,
   compile-check the package. Commit + push. Cloud/Linux note: the Windows DLL is NOT rebuilt in a
   Linux sandbox — commit source; the Windows `HoloTrackSDK.dll` redeploy happens on the machine.

### Game repo + Unity — DEFERRED to a machine+Unity session (cannot run headless)
6. Rig hook: add `SetEyeMatrices(bool right, Matrix4x4 view, Matrix4x4 proj)` + `ExternalEyeMatrices`
   flag to `PMSDKStereoContentRig` so `UpdateEye` skips its own shear when driven externally; expose
   the two eye cameras. Wire `HeadTrackedStereoController` to them (order it after the rig's `ApplyState`).
7. **ProBuilder holographic diorama scene** (recipe): window/zero-parallax at z=0 coincident with a
   2.0×1.2 `HeadTrackingDisplaySurface`; rest eye ≈ (0,0,+2). Recessed stage box (interior 2.0×1.2×1.2,
   spans z 0→−1.2, front face open, normals flipped inward — or 5 inward planes as the robust
   fallback); 4-bar window bezel (clean 2.0×1.2 opening); depth props at z≈−1.05/−0.55/−0.15 and a
   **pop-out hero** at z≈+0.25 kept within ≈±0.4×±0.35 so its silhouette never touches the bezel
   (a clipped pop-out breaks the illusion). `ContentCamera` at (0,0,+2) facing −Z with
   `PMSDKStereoContentRig` (`EyeSeparation 0.06`, `ZeroParallaxDistance 2.0`) + `HeadTrackedStereoController`.
   Build via `manage_probuilder`/`manage_gameobject`; delete/flip faces by querying normals (never
   hardcode face indices). Verify parallax numerically (window anchored; near/far shift opposite) +
   stereo disparity sign in play mode; watch for an eye-order flip.
