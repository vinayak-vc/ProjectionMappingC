# On-Site Calibration UX — Design

> **Status: P1 + P2 implemented and play-mode verified (2026-07-16).**
> Files: `Runtime/Calibration/PMSDKCalibrationManager.cs` (mode/input/persistence/undo/
> mouse drag/loupe + thumbnail rendering), `PMSDKCalibrationOverlay.cs` (handles,
> identify badge, blend-zone tint per projector output), `PMSDKCalibrationHUD.cs`
> (operator console: status, loupe view, clickable thumbnails, F1 help),
> `PMSDKCalibrationData.cs` (JSON schema + atomic IO).
> `PMSDKCornerPinUI` is superseded — the manager disables any instance it finds;
> the generator and auto-configurator now add a "PMSDK Runtime Services" object
> (calibration manager + display activator) instead.
> P2 notes: mouse drag uses `Display.RelativeMouseAt` in players and falls back to the
> raw mouse position in the Editor (drag on the focused Game view; grab is
> distance-gated so wrong-view coordinates just miss). Undo is a coalesced snapshot
> stack (0.7 s groups, Ctrl+Z, depth 100). The loupe and thumbnails are rendered by
> one reusable hidden service camera into RenderTextures shown on the operator HUD.
> **Overlay canvases must never be parented under a warp surface** — the surface's
> 16:9 X-scale leaks into ScreenSpaceCamera canvas geometry and pushed the blend
> zones out of the frustum; overlays live under the manager instead.
>
> **Status: P3 (camera auto-align) implemented and simulation-verified (2026-07-16).**
> Files: `PMSDKGrayCodeDecode.cs` (managed decode, bit-compatible with native
> src/Calibration/GrayCode.cpp), `PMSDKHomography.cs` (least-squares planar homography),
> `PMSDKCalibrationCamera.cs` (`IPMSDKCalibrationCamera` + `PMSDKSimulatedCamera`),
> `PMSDKAutoAlign.cs` (orchestrator coroutine). Manager binds it to the `A` key
> (Shift+A = all surfaces). See §9 below.

Goal: a technician standing in a venue with a keyboard (maybe no mouse), projectors on
Displays 2..N, running a **standalone build with no Inspector**, must be able to align,
blend, and persist a multi-projector setup in minutes.

## 1. What professional tools do (survey)

| Tool | Calibration UX | What we steal |
|---|---|---|
| **Resolume Arena** (Advanced Output) | Dedicated output-setup mode; corner-pin + grid warp per slice; arrow keys nudge selected point (Shift = bigger step); per-output test card toggle; blend width dragged between outputs; presets saved to file | Mode-based editing, graded keyboard nudge, per-output test card, file presets |
| **MadMapper** | Surface list → select → handles on output; fullscreen test patterns; numeric nudging; magnifier loupe around dragged point; quick-save scenes | Selection model (surface → handle), loupe (later phase), quick-save |
| **Barco / Christie / Epson** (projector on-remote keystone) | No pointer at all: one button **cycles the active corner**, arrow keys move it; step size toggles coarse/fine | The whole keyboard-only flow — proven usable standing at a projector with a remote |
| **VIOSO / Scalable Display** | Camera watches projected structured-light patterns; warp + blend computed automatically; manual touch-up after | Auto-align phase 3 — our SDK already has native Gray-code structured light + direct camera capture (D-019) |
| **TouchDesigner (kantanMapper)** | Editor window per output; stores calibration to file, show reloads it | Persistence contract: calibrate once, every later boot silently reloads |

Distilled UX laws:
1. **Calibration is a mode**, one hotkey away, available in any build. Show mode = clean output.
2. **Keyboard-first.** Mouse is a bonus, never a requirement (booth reality; also multi-display mouse in Unity is flaky).
3. **See what you adjust**: the selected projector/corner is visually loud; everything else stays quiet.
4. **Graded steps**: coarse → normal → fine via modifiers, current step always visible.
5. **Persist instantly, reload silently.** First run with no calibration file boots straight into calibration mode.
6. **Operator console on the control display** (Display 1); projector outputs carry only patterns + handles + a big identify badge.

## 2. Mode flow

```
Boot
 └─ load %persistentDataPath%/pmsdk_calibration.json
     ├─ found → apply to CornerPin/EdgeBlend components → run show
     └─ missing → enter CALIBRATION MODE automatically
Any time: F2 toggles CALIBRATION MODE  (also: -calibrate CLI flag forces it)
Exit (Esc or F2): auto-save + return to show
```

## 3. Input map (keyboard-only complete)

| Key | Action |
|---|---|
| `F2` | Toggle calibration mode |
| `F1` | Help overlay (this table, on operator display) |
| `PgUp` / `PgDn` | Select previous / next projector surface |
| `Tab` / `Shift+Tab` | Cycle corner: TL → TR → BR → BL |
| Arrows | Nudge selected corner. Base step 0.002 normalized (~4 px @1080p) |
| `Shift` + Arrows | Coarse ×5 |
| `Ctrl` + Arrows | Fine ×0.1 |
| `T` / `Shift+T` | Test pattern: selected surface / all surfaces |
| `B` | Blend-edit submode: `Tab` cycles edge (L/R/T/B), `←→` width, `↑↓` gamma, `N`/`Shift+N` black level |
| `G` | Grid-warp submode (N×M): `Tab` cycles control point, arrows/drag move it, `[`/`]` columns, `-`/`=` rows, `R` reset. For curved/irregular surfaces. |
| `M` | Mark-target submode: live camera preview on the operator console with 4 draggable corners. Place them on the physical screen edges, then `A` aligns the projection to that rectangle. `Tab` select, arrows/drag move, `R` reset, `M`/`Esc` cancel. |
| `R` / `Ctrl+R` | Reset selected corner / whole surface to identity |
| `C` | Canvas reference: end-to-end level plus + grid drawn in SHARED wall-canvas space (pre-warped through every pin) — lands straight/seam-continuous on the wall even with tilted projectors; a kink at a seam = pin residual to touch up. Level relative to the canvas; check once against a laser level for true gravity-level. |
| `Ctrl+1..4` / `Ctrl+Shift+1..4` | Load / save preset slot (named cues; files `pmsdk_preset_<name>.json`) |
| `V` | A/B compare — swap live state with the pre-preset-load snapshot |
| `Ctrl+S` | Save now (auto-save also fires on exit) |

Remote control: `PMSDKOscServer` (UDP, default port 9000) drives everything above from
QLab/TouchOSC/Chataigne — `/pmsdk/calibration`, `/surface`, `/corner(/nudge|/set)`,
`/blend(/gamma|/black)`, `/testpattern`, `/autoalign`, `/preset/(save|load)`, `/ab`.
| `Esc` | Exit calibration mode (saves) |
| Mouse drag | Move handle directly (`Display.RelativeMouseAt` routes per display; Windows standalone). `Alt` = fine drag |

Selected handle: yellow, 2× size, slow pulse. Unselected: green, small. Selected surface:
its projector output shows a large "PROJECTOR 2 — Left_Screen" identify badge (like
Resolume's output identify) so the tech knows which physical beam they're editing.

## 4. Operator console (Display 1)

Screen-space-overlay canvas on the control monitor, built entirely at runtime (no scene
authoring, no Inspector):

```
┌─ PMSDK Calibration ────────────────────────────────┐
│ Projector:  [2/3] Left_Screen  (Display 2)         │
│ Corner: TL   pos 0.0132, 0.9871   step 0.002 (~4px)│
│ Blend: L 0.00  R 0.18  γ 2.2  black 0.00           │
│ ● unsaved changes      Ctrl+S save    F1 help      │
└────────────────────────────────────────────────────┘
```

Plus per-projector thumbnails (the warped output RTs) in a row for orientation.

## 5. Persistence

`Application.persistentDataPath/pmsdk_calibration.json` (override: `-calibfile <path>`):

```json
{
  "version": 1,
  "savedAtUtc": "2026-07-15T18:30:00Z",
  "surfaces": [
    {
      "id": "Left_Screen",
      "targetDisplay": 1,
      "corners": { "tl": [0.013, 0.987], "tr": [0.998, 1.0], "bl": [0.0, 0.0], "br": [1.0, 0.01] },
      "blend": { "left": 0, "right": 0.18, "top": 0, "bottom": 0, "gamma": 2.2, "blackLevel": 0 }
    }
  ]
}
```

- `id` = GameObject name; mismatch on load → warn + skip (scene changed).
- JSON via Unity `JsonUtility` (no dependency). Native SDK serialization (Milestone 8)
  stays an option for cross-engine project files later.
- Save is atomic: write `.tmp`, then replace — a power cut mid-save can't corrupt the
  last good calibration.

## 6. Component architecture

```
PMSDKCalibrationManager   (one per scene; runtime-only logic, zero Inspector setup)
 ├─ discovers all PMSDKMeshWarp surfaces on enable
 ├─ owns: mode state, selection, input handling, undo snapshot, save/load
 ├─ writes: PMSDKCornerPin corners, PMSDKEdgeBlend edges/gamma/black
 └─ spawns:
     ├─ PMSDKCalibrationOverlay  (per surface → its projector display)
     │    handles, selection visuals, identify badge
     │    (supersedes PMSDKCornerPinUI; that component becomes obsolete)
     └─ PMSDKCalibrationHUD     (operator console, Display 1)
```

- Input: legacy `Input` API (project's Active Input Handling = legacy only — verified).
- Generator + Auto-Configurator add `PMSDKCalibrationManager` to every rig automatically.
- Undo (phase 2): manager snapshots surface state on selection change; `Ctrl+Z` pops.

## 7. Why this works in a build (no Inspector)

- Everything is runtime UI + hotkeys; nothing needs the Editor.
- First run: no file → boots into calibration mode by itself.
- `F2` re-enters any time; `-calibrate` flag forces it (kiosk recovery).
- Every later boot: silent load + apply, show runs untouched.

## 8. Phasing

- **P1 (core, ship first)**: manager + keyboard flow + JSON persistence + test-pattern
  toggle + identify badge + HUD text row. Keyboard-only is fully sufficient to calibrate.
- **P2 (comfort)**: mouse drag via `RelativeMouseAt`, undo stack, loupe magnifier,
  per-projector thumbnails on console, blend-zone tint.
- **P3 (pro / differentiator)**: camera-assisted auto-align — SDK already ships Gray-code
  structured light + direct `cv::VideoCapture` capture (D-019, Milestones 16/17). One key
  ("A") → project patterns per projector → decode → solve corners → manual touch-up.
  This is the VIOSO/Scalable feature, and our native core already has the hard parts.

## 9. Camera-assisted auto-align (P3)

One key (`A`, or `Shift+A` for all surfaces) aligns a projector to what a camera sees —
no metric calibration, no manual corner dragging to start from.

### Pipeline (per surface)
1. **Take over the surface**: identity corner-pin, edge blend off, swap in an unlit
   material the orchestrator drives with pattern textures.
2. **Display + capture**: a white reference, a black reference, then the Gray-code
   column/row patterns (`colBits + rowBits`, e.g. 14 at 128²). Each is shown for a few
   frames and captured through an `IPMSDKCalibrationCamera`.
3. **Decode**: `PMSDKGrayCodeDecode` thresholds each camera pixel against the per-pixel
   white/black midpoint, reads the Gray bits MSB-first, converts Gray→binary → the
   projector-raster pixel that lit each camera pixel. `minContrast` rejects pixels this
   projector never lit (ambient / other projectors).
4. **Fit homography**: `PMSDKHomography` least-squares-fits a camera→projector planar
   homography over every valid correspondence (subsampled to ~4 000 points).
5. **Solve corners**: map the four **target** corners (where the projection should land,
   in camera space) through the homography → projector-raster coords → write into
   `PMSDKCornerPin`. Operator fine-tunes with P1/P2 controls, `Ctrl+S` saves.

Target corners come from the caller (`AlignSelectedWithTarget`): the physical screen
bounds as seen by the camera, or an operator-marked quad. With no target
(`StartAutoAlign`) it uses the bounding box of the lit region — an identity check that
reproduces the current projection.

**Marking the target (`M`).** To map the projection onto a specific physical rectangle
(a real screen inside the projector's throw), press `M`: the operator console shows a
live camera preview with four draggable corners. Drag them onto the screen's edges,
then `A` runs the sweep and maps the projector→camera homography so the output fills
that rectangle. Marker positions are UI-normalized (bottom-left); the manager flips them
to the camera-normalized top-left space `AlignSelectedWithTarget` expects, and persists
them in the calibration file (camera-placement-dependent, but saved so re-aligning an
unmoved camera needs no re-marking). The preview source is the same
`IPMSDKCalibrationCamera` as auto-align (simulated observer or native webcam), so it is
freed before the sweep opens the camera.

### Auto-blend from overlap (`Shift+A`)
Aligning all projectors (`Shift+A`) additionally runs `PMSDKAutoBlend`: a camera pixel lit
by two projectors is in their overlap, so mapping those pixels back into each projector's
raster gives the overlap band per edge. A coverage-histogram scan (a band must span most of
an edge's length, so corner blobs are rejected) yields the four edge-blend widths, applied
to each `PMSDKEdgeBlend` automatically. Requires one camera that sees every projector
(`AutoBlendAfterAlignAll`, default on). Core verified by edit-mode tests (2-projector
symmetric, 3-in-a-row middle gets both edges, no-overlap → zero).

### Why homography, not the native stereo triangulation
`pmsdk_decoder_decode_and_triangulate` produces a metric 3D point cloud but needs a
fully calibrated camera **and** projector (intrinsics + extrinsics). That is not
something an on-site tech can supply in minutes. For flat/quasi-flat mapping surfaces a
camera→projector homography is sufficient, needs zero calibration, and is exactly the
model corner-pin already uses (D-021). The native triangulation path stays available for
future true-3D calibration onto complex geometry.

### Capture sources
- `PMSDKSimulatedCamera` — renders an in-scene Unity `Camera` to a texture. No hardware;
  used for testing and for virtual / LED-volume rigs. Assign `ObserverCamera` on the
  manager, or add a scene object named `PMSDK Calibration Observer` with a `Camera`.
- `PMSDKNativeWebcamCamera` — physical webcam through the native OpenCV decoder.
  Enable with `UseNativeWebcam` (+ `WebcamIndex`) on the calibration manager. Captures
  flush the VideoCapture buffer first (`pmsdk_decoder_capture_frame_flushed`) — without
  flushing, the frame after a pattern change still shows the *previous* pattern and
  corrupts the decode — then read back via `pmsdk_decoder_get_last_frame`.

### Robustness hardening (2026-07-16, "item 1")
Professional structured-light practice (OpenCV `structured_light`, VIOSO, academic
standard) never decodes with a global threshold. The SDK now matches it:
- **Native**: `GrayCode::GenerateRobustPattern` emits `[white, black, p0, p0inv, …]`
  (2 + 2·N frames); `GrayCodeDecoder::DecodeRobust` decides each bit by
  pattern-vs-inverse per pixel and rejects pixels below a white/black contrast gate
  (shadow mask). New C API: `pmsdk_graycode_get_robust_pattern_count` /
  `generate_robust_pattern`, `pmsdk_decoder_capture_frame_flushed`, `add_image_memory`,
  `get_last_frame`, `get_image_count`, `clear_images`, and `pmsdk_decoder_decode_robust`
  (returns raw camera→projector correspondences for homography solves — no metric
  calibration needed). Unit-tested: identity roundtrip, albedo+ambient survival
  (25–100% albedo gradient + ambient offset decodes perfectly), shadow-mask rejection.
- **Managed**: `PMSDKGrayCodeDecode.Decode` gained the same inverse-pattern mode;
  `PMSDKAutoAlign.UseInversePatterns` (default ON) projects each pattern's inverse and
  decodes pattern-vs-inverse. The legacy midpoint mode remains for tests.
- The old single-threshold `Decode(threshold)` and `decode_and_triangulate` remain for
  compatibility but should not be used with real cameras.

### Verification (2026-07-16, no hardware)
- **Numeric**: Gray encode→decode roundtrip 16 384/16 384 pixels, 0 mismatches;
  homography recovers a known perspective transform to 1.9e-6 reprojection error.
- **Simulated integration**: observer Unity camera views `Left_Screen`; corner-pin
  deliberately skewed to `{(.15,.85),(.9,.95),(.95,.1),(.05,.2)}`; `A` → auto-align
  restored identity corners to a total error of **0.008** across all four corners.
  Explicit centered-half target mapped the corner-pin to `{(.25,.75),(.75,.75),(.75,.25),
  (.25,.25)}` exactly. Mean reprojection error sub-pixel.
- **Not yet verified**: real webcam + physical projector loop (no hardware available).
  Requires the C-API frame-readback addition above, then a physical smoke test.
