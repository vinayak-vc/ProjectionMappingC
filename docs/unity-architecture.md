# Unity Integration Architecture

This document details the architectural decisions, constraints, and best practices for using the Projection Mapping SDK natively inside Unity.

## 1. Native SDK vs. Third Party Applications (TPAs)
Traditionally, projection mapping workflows involve:
1. Running Unity on a primary display.
2. Using Nvidia Surround or AMD Eyefinity to stitch multiple projectors into one giant "virtual display".
3. Capturing the Unity output (via Spout/Syphon) and sending it to a TPA (like Resolume or MadMapper).
4. Warping the 2D video feed in the TPA.

**Why the Native SDK is Superior:**
- **Zero Latency:** Warping is executed by the native C++ engine (`pmsdk_warpnode_process_mesh`) and applied straight to Unity meshes, eliminating the frame delay caused by Spout/Syphon captures. This is critical for highly interactive installations (Kinect, Lidar tracking).
- **No Screen Tunneling Hacks:** By utilizing Unity's native `Camera.targetDisplay` routing, Windows simply sees standard displays. This prevents Nvidia Surround crashes when HDMI cables are unplugged.

## 2. The Two-Space Architecture (Content Space + Raster Space)

The scene is split into two independent "spaces" that never see each other (enforced with layers):

### Content space (layer `Water`)
- The actual 3D game world: geometry, physics, lights, animation.
- A **Content Camera** renders it. It does *not* output to a display — it renders
  into a `RenderTexture` asset (`Projection_RT`).
- Its culling mask is the content layer only.

### Raster space (layer `TransparentFX`)
- One **warp surface** per physical projector. This is a mesh whose vertices the
  native engine rewrites every frame.
- **Native contract:** `GridWarp::ApplyDeformation` sets
  `position = Evaluate(uv)` — the output mesh always lives in **normalized
  projector raster space**: a `[0,1]×[0,1]` quad on the local XY plane (z = 0),
  regardless of the input mesh's 3D shape. The input mesh only contributes its
  UV layout and tessellation density (a default Unity Plane = 11×11 vertices).
- Consequence: a warp surface must have **identity rotation** and is stretched
  to raster aspect with `localScale = (16/9, 1, 1)`. Do NOT rotate warp
  surfaces to "face" anything — their 3D orientation is meaningless after the
  native engine rewrites the vertices. (An earlier revision of this document
  recommended ±90° X rotations for backface culling; that advice was wrong
  twice over — it applied to the *pre-warp* plane only, and the sign was
  inverted. It is obsolete under the raster-space rig, and the
  `PMSDK/UnlitWarp` shader is `Cull Off` anyway.)
- Each surface's material is a **split-slice material**: it samples a horizontal
  slice of `Projection_RT` via texture tiling/offset (e.g. left projector
  `scale=(0.55,1) offset=(0,0)`, right projector `scale=(0.55,1) offset=(0.45,0)`
  for a 10% overlap band used by edge blending).

### Projector cameras
- One **orthographic** camera per projector, framing its unit quad exactly:
  `orthographicSize = 0.5 × surface.localScale.y`, positioned at the quad
  center (local `(0.5, 0.5, 0)`) pulled back along −Z.
- Culling mask = raster layer + UI. `CameraClearFlags.SolidColor`, pure black —
  `Skybox` clear would blast the skybox into the physical room and ruin edge
  blending.
- `targetDisplay` = 1, 2, … (Display 2, Display 3, …).

### Multi-display output
Unity standalone builds only render to the primary display unless every extra
display is explicitly activated. Every scene must contain a
**`PMSDKDisplayActivator`** (calls `Display.displays[i].Activate()` on Start).
Inside the Editor, preview via the Game view's Display dropdown instead.

## 3. Shading and Edge Blending

- The native blend stage (`pmsdk_blendconfig_apply_to_mesh`) writes its falloff
  ramp into the **vertex color alpha channel only**.
- Stock `Unlit/Texture` ignores vertex colors entirely — blending is invisible
  with it. Warp surfaces must use the package shader **`PMSDK/UnlitWarp`**,
  which multiplies `rgb × vertexColor.rgb × vertexColor.a` and outputs
  opaque alpha ("a projector blends to black").
- Edge blend sizes are expressed as a fraction of the projector raster: with a
  10% content overlap and 0.55-wide slices, each inner edge blend is
  `0.10 / 0.55 ≈ 0.18`. Gamma ≈ 2.2 for gamma-space projects.

## 4. Asset Persistence Rules (hard-learned)

- **Never reference in-memory objects from a scene.** RenderTextures, materials,
  and physics materials created with `new` and assigned to scene objects become
  missing references after a scene reload. The demo generator saves everything
  under `Assets/PMSDKDemo/` via `AssetDatabase.CreateAsset` first.
- `PhysicsMaterial` assets must use the `.asset` extension —
  `AssetDatabase.CreateAsset` rejects `.physicsMaterial`.
- `Rigidbody.maxLinearVelocity` is runtime-only PhysX state (not serialized);
  set it from a component (`PMSDKVelocityCap`), never from the Inspector/editor
  scripts.
- `PMSDKTestPattern` serializes its captured `originalMaterial` and its runtime
  pattern material/texture are `HideFlags.DontSave`, so toggling it while the
  scene is saved can no longer permanently clobber the screen material.

## 4b. Warp modes (corner pin vs. grid)

A warp surface has one active deformation, driven by whichever component is enabled:

- **`PMSDKCornerPin`** — 4-corner **projective** pin. Pushes the four corners into the
  native `PerspectiveWarp` (deformation type 3, a homography). Perspective-correct
  foreshortening; the default for flat surfaces. (Superseded the old 2×2 bilinear grid
  path, which sheared on keystoned quads — D-022.)
- **`PMSDKGridWarp`** — N×M **bilinear** grid for curved/irregular surfaces. Disabled by
  default; when enabled it takes over the warp node (deformation type 2) and the corner
  pin yields. Control points are normalized raster coords (row-major, y up), serialized
  with the scene and in the calibration file.

In calibration mode, `G` toggles grid editing on the selected surface: `Tab` cycles the
control point, arrows/drag move it, `[` `]` change columns, `-` `=` change rows, `R`
resets. The overlay draws the lattice (handles + lines) on the projector output.

## 5. SDK Component Hard Constraints (Foolproofing)
To prevent runtime crashes and silent failures, the SDK enforces strict component dependencies (`[RequireComponent]`) and validation:

- **`PMSDKMeshWarp`**:
  - Requires `MeshFilter` and `MeshRenderer`.
  - Validates that a Mesh is actually assigned to the MeshFilter before attempting to clone it or pass it to the C++ engine. Prints a warning if missing.
  - Safely idles and prints a warning if no `PMSDKProjector` is assigned.
- **`PMSDKCornerPin`**:
  - Requires `PMSDKMeshWarp`. Pushes a 2×2 grid (normalized corners) into the native GridWarp.
- **`PMSDKEdgeBlend`**:
  - Requires `PMSDKMeshWarp`. Configures the native blend ramps (applied to vertex alpha).
- **`PMSDKProjector`**:
  - Requires `Camera`. Owns the native Projector + WarpNode.
- **`PMSDKDisplayActivator`**: activates Displays 2+ in standalone builds.
- **`PMSDKVelocityCap`**: applies `Rigidbody.maxLinearVelocity` at runtime (demo content).
- **`PMSDKCalibrationManager`** (+ `PMSDKCalibrationOverlay`, `PMSDKCalibrationHUD`):
  runtime on-site calibration — F2 toggles calibration mode; corner-pin (mouse + keyboard),
  edge-blend (`B`), N×M grid warp (`G`), camera auto-align (`A`), and mark-target-rectangle
  (`M`) submodes; JSON persistence in `persistentDataPath`, auto-enters on first run.
  Camera capture abstracted via `IPMSDKCalibrationCamera` (`PMSDKSimulatedCamera` /
  `PMSDKNativeWebcamCamera`). Full design + key map: docs/calibration-ux-design.md.
  Scenes carry one "PMSDK Runtime Services" object (manager + display activator).
- **`PMSDKGridWarp`**: N×M bilinear grid warp component (curved surfaces); disabled by
  default, takes over the warp node when enabled (see §4b).
- **`PMSDKProjectorPoseCalibrator`** (+ `PMSDKPoseSolver`, `PMSDKPoseCalibratorOverlay`):
  object-mapping projector-pose calibration. Place anchor transforms on the virtual twin's
  features, mark each in the projector output (F3 mode — crosshair driven by arrows or
  mouse, Enter captures, Space solves), then it solves the projector camera's pose+FOV so
  the twin overlays the physical object. Managed reprojection solve against Unity's camera
  — no native/OpenCV conversion (D-024). The overlay draws the crosshair + captured dots on
  the projector output and an operator console (Display 1) with status + a live twin
  preview that rings the anchor currently being marked. Fine registration afterward uses
  corner-pin/grid warp.
- **`PMSDKColorCorrection`**: per-projector RGB gain/offset + per-channel output gamma
  (luminance/white-balance matching between projectors); drives `PMSDK/UnlitWarp`.
- **`PMSDKOutputTransform`**: content rotation (0/90/180/270) + mirror X/Y for portrait,
  ceiling-mounted, or rear projection; UV transform in the shader (warp/corner-pin
  unaffected).

### Blend, black level, color — the `PMSDK/UnlitWarp` fragment pipeline
UV transform → sample → edge-blend (vertex-color ramp) → black-level floor → color
correct (gain/offset + per-channel gamma). The **edge-blend ramp exponent is
1/projectorGamma** so two projectors' light sums to full across the overlap; setting
`PMSDKEdgeBlend.Gamma` to the projector's gamma (~2.2) removes the dark seam (D-023).
`_BlackLevel` is a uniform floor (true per-region black-level compensation is future).
- **`PMSDKCornerPinUI`**: superseded by the calibration system; the manager disables any
  instance it finds at startup. Kept only for source compatibility.

## 6. The Auto-Configurator and Demo Generator

- **`PMSDKMeshWarpEditor`** — adding `PMSDKMeshWarp` to any GameObject shows an
  **"Auto-Configure Full SDK Dependencies"** button: normalizes the transform for
  raster space (identity rotation, 16:9 scale), creates a correctly-framed
  orthographic projector camera, assigns a default Plane mesh if missing, and
  attaches the remaining components (test pattern added disabled).
- **`Tools/Projection Mapping/Generate Demo Scene`** (`PMSDKDemoSceneGenerator`)
  — builds the full two-space rig from scratch: content world (bouncing cube +
  floor), RT + split-slice materials as assets, two warp surfaces with edge
  blends, two orthographic projector cameras on Displays 2/3, and the display
  activator.

## 7. Demo Scenes (Unity project `ProjectionMapping-base-project`)

- `WarpAndBlendExample` — minimal two-projector rig (content: bouncing cube).
- `ProBuilderMappingDemo` — same rig with a ProBuilder-built content stage
  (back wall, pillars, arch, stairs) to demonstrate real content mapping.
- `ObjectMappingDemo` — **object mapping (spatial AR)**: instead of the
  content-RT → flat-screen rig, a projector camera at the projector's pose
  renders a virtual twin of a physical object (ProBuilder monument) directly to
  its display, so the projected image wraps the real object's faces. This is the
  "true 3D mapping" case flat capture-based tools can't do. `PMSDKProjector`
  carries the projector lens/throw; corner-pin/grid warp + camera calibration
  register the virtual twin to the physical object. Mapping content is animated
  by `PMSDKMappingContentAnimator` (hue wave + brightness pulse + reveal sweep).
  Runs in a dim scene (emissive content dominates, as in a real dark venue).
