# Unity Projection Mapping SDK - Quick Start Guide

Welcome to the Projection Mapping SDK for Unity! This package provides high-performance, multithreaded native C++ projection mapping capabilities directly inside Unity without locking the main thread. 

**This SDK handles all warping and edge-blending natively, meaning you do NOT need third-party software (like Resolume or MadMapper), nor do you need to use Nvidia Surround/AMD Eyefinity to stitch displays.**

## Why Native Unity SDK vs. Third Party Applications (TPAs)?
Traditionally, projection mapping requires running a game in Unity, using Nvidia Surround to stitch projectors into one massive virtual monitor, and running a Third Party Application (like Resolume or MadMapper) to warp the screen via Spout/Syphon. 
Our native SDK is superior for interactive installations because:
1. **Zero Latency**: By living inside Unity's rendering pipeline, warping happens directly on the GPU in real-time, which is critical for interactive tracking (Kinect, Lidar).
2. **True 3D Mapping**: TPAs just warp a flat 2D video feed. The SDK maps natively onto 3D objects in the Unity scene.
3. **No Screen Tunneling Hacks**: By utilizing Unity's native `Target Display` routing, you bypass the instability of Nvidia Surround. Windows sees normal, stable displays.
4. **No Expensive Licenses**: Professional TPAs cost thousands of dollars; you are running an enterprise-grade solution natively in the engine.

## Installation
1. Open the Unity Package Manager (`Window > Package Manager`).
2. Click the `+` button in the top left and select `Add package from disk...`
3. Navigate to this folder (`com.viitorx.pmsdk`) and select the `package.json` file.
4. Unity will automatically import the SDK and its native binaries.

---

## How the rig works (two-space architecture)

Content and projector output live in two isolated layers (details:
`docs/unity-architecture.md`):

1. **Content camera** renders your 3D scene (Water layer) into a `Projection_RT`
   RenderTexture — it never drives a display.
2. **Warp surfaces** are unit quads (identity rotation, 16:9 scale) that sample a slice
   of that RT. The native engine rewrites their vertices every frame into normalized
   projector raster space; a warp surface is NOT a 3D plane you tilt.
3. **Projector cameras** are **orthographic**, frame their warp surface exactly, clear to
   black, and route to Display 2/3 via `targetDisplay`.
4. A **PMSDK Runtime Services** object (`PMSDKDisplayActivator` + `PMSDKCalibrationManager`)
   activates the extra displays in builds and runs on-site calibration.

Build the whole rig in one click: **Tools > Projection Mapping > Generate Demo Scene**.
Or add `PMSDKMeshWarp` to a surface and press **Auto-Configure Full SDK Dependencies** in
its inspector. Example scenes: `WarpAndBlendExample`, `ProBuilderMappingDemo`, and
`ObjectMappingDemo` (object mapping / spatial AR — see below).

### Two modes: screen warp vs object mapping
- **Screen warp** (WarpAndBlend / ProBuilder demos): content renders to a RenderTexture,
  which is warped/blended onto flat projection surfaces. For walls, screens, panoramas.
- **Object mapping** (`ObjectMappingDemo`): a projector camera at the projector's pose
  renders a **virtual twin** of a physical 3D object directly to its display, so the image
  wraps the object's real faces. For mapping onto sculptures, buildings, props — the thing
  flat capture-based tools (Resolume/MadMapper via Spout) cannot do. Register the twin to
  the physical object with corner-pin/grid warp + camera calibration.

## Multi-Projector Setup (Step-by-Step)

### Step 1: Physical hardware & Windows
1. Turn on the PC and projectors; connect each to a GPU output.
2. Windows **Display Settings** → **Extend these displays** (never Duplicate/Nvidia Surround).
3. "Identify" to note display numbers (Display 1 = control monitor, Display 2/3 = projectors).

### Step 2: Generate the rig
1. **Tools > Projection Mapping > Generate Demo Scene** (or Auto-Configure your own surfaces).
2. Confirm each projector camera's **Target Display** (Left = Display 2, Right = Display 3).
3. Press **Play** — content routes to the projectors; the display activator lights the extra outputs.

### Step 3: On-site calibration (runtime, keyboard-driven, works in builds)

All calibration is a runtime mode — no Inspector needed, so it works in a shipped build.
On first run with no saved calibration it enters automatically; otherwise press **F2**.
The operator console appears on Display 1; handles + an "identify" badge appear on each
projector output. Calibration is saved to `Application.persistentDataPath/pmsdk_calibration.json`
and reloaded silently on every later boot. Full key map: `docs/calibration-ux-design.md`.

| Key | Action |
|---|---|
| `F2` | enter/exit calibration (auto-saves on exit) |
| `PgUp`/`PgDn` | select projector |
| `Tab` + arrows / mouse | move the selected corner (perspective corner pin) |
| `Shift`/`Ctrl` | coarse / fine step |
| `B` | edge-blend mode (edge width, gamma, black level) |
| `G` | N×M grid warp for curved/irregular surfaces |
| `A` / `Shift+A` | camera auto-align: selected / all projectors |
| `M` | mark the target rectangle in the camera view, then `A` |
| `Ctrl+Z` / `Ctrl+S` | undo / save |

### Step 4: Manual corner pin + blend
- Select a projector, drag its 4 corners (or arrow-nudge) until the image squares up on
  the wall. The corner pin is a true **perspective** map (foreshortens correctly).
- `B` for edge blend: raise the overlapping edge on each projector (~0.1) until the seam
  disappears; tune **gamma** to your projector's luminance curve.
- `G` if the surface is curved: subdivide (`[` `]` columns, `-` `=` rows) and drag interior
  grid points.

---

## Camera auto-align (structured light)

A **plain 2D webcam is enough — no depth camera needed.** Auto-align projects Gray-code
patterns, decodes what the camera sees, and solves a camera→projector homography.

1. On `PMSDKCalibrationManager`, set **UseNativeWebcam = true** and **WebcamIndex**
   (the SDK opens the camera directly via C++ `cv::VideoCapture`; frames are buffer-flushed
   so each capture matches the displayed pattern).
2. Dim the room (structured light needs contrast; low-contrast pixels are masked out).
3. `F2` to calibrate, then:
   - `M` → drag the 4 preview corners onto the physical screen edges (optional; skip to
     fill the whole projection).
   - `A` (or `Shift+A` for all) → the projector flashes white/black + Gray patterns (and
     inverses), the camera captures, and the corner pin snaps to the solution.
4. Touch up by hand, `Ctrl+S` to save.

Best results: lock the webcam's focus/exposure, 1080p, positioned to see the whole
projection. The decode is robust to surface albedo and ambient light (per-pixel
pattern-vs-inverse comparison + shadow mask). The native metric-triangulation path (full
3D point cloud) remains available but needs a calibrated camera+projector and is not used
for on-site corner alignment.
