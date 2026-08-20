# Multi-Skin Single Projector — Design

## Purpose

Turn one physical projector into N independent content regions ("skins"). Each skin is a
shape drawn on the projector's raster output that maps onto a specific physical face (box
front, top, right side, signage cutout, logo target). Each skin has its own content source,
its own geometry, its own color/opacity/mask, all independent.

Two-phase workflow, following the projection-mapping industry pattern (MadMapper surfaces,
Resolume slices, TouchDesigner networks):

1. **Author** — a dedicated Skin Editor scene where you draw shapes on the projector output
   and stretch them to fit physical faces. Output: a `PMSDKSkinSet` asset (Unity SO + JSON
   sidecar).
2. **Play** — any content scene loads the skin set and binds media to skins by name. One
   calibration, many scenes.

## Scope

v1:

- Single-projector only. Multi-projector-with-skins is v2.
- Shape primitives: Rectangle/Square, Ellipse/Circle, N-gon (triangle/pentagon/hexagon/star),
  Freeform polygon.
- Vertex cap: 32 for named polygons; 128 for freeform.
- Content sources: `Texture`, `RenderTexture`, `VideoClip`, `TestPattern`.
- Uniform soft-mask feathering (single scalar per skin). JSON schema accepts a per-edge array
  from v1 so v1.1's per-edge UI needs no migration.
- Skin set is scoped to one projector; JSON kept separate from `pmsdk_calibration.json`.
- Default content in the skin set (optional; loader auto-binds if present).

Not v1 (kept schema-compatible for later):

- Multi-projector skin coordination + per-skin cross-projector blend (v2).
- Per-edge soft-mask feathering **UI** (v1.1; schema supports it from v1).
- v1.1 content sources: Camera, VideoUrl, Spout, NDI, Webcam.
- Auto-align a single skin via Gray-code (v2).
- 3D-twin skin (corners pulled from a mesh face — a v2 alternative for well-modelled objects).

## Terminology

| Term | Meaning |
|---|---|
| **Skin** | UX name for one content region on a projector. |
| `PMSDKSkin` | Unity component that owns one skin. |
| **Skin set** | Named collection of skins for one projector. Persisted as `PMSDKSkinSet` SO + JSON sidecar. |
| **Skin Editor scene** | Dedicated Unity scene shipped in the package that lets you draw and edit skins on live projector output. |
| **Skin loader** | Runtime component that instantiates skins from a skin set into a content scene. |

Skin ≠ the older "warp surface" language: a skin IS implemented as a raster-space warp mesh.
The "one warp surface per projector" invariant becomes "N warp meshes per projector", each
being one skin.

## Architecture

### Runtime components (new)

**`PMSDKSkin`** — one skin. Lives in the projector's raster space (child of the projector's
warp root). Owns:

- `PMSDKSkinShape` — shape kind + vertex list + mask config.
- Rebuildable Unity mesh (MeshFilter/MeshRenderer using `PMSDK/UnlitWarp`).
- A warp path chosen by shape kind (see next section).
- `PMSDKSkinContent` — the content source binding.
- Optional `PMSDKColorCorrection` (existing component reused).

**`PMSDKSkinShape`** — pure data + geometry regeneration:

- `Kind`: `Rectangle | Ellipse | Polygon | Freeform` (Rectangle/Ellipse short-circuit to the
  quad+shader-mask path; Polygon/Freeform share a code path).
- `Vertices`: raster-space `Vector2` list in `[0..1]`, top-left origin (matches the existing
  calibration coordinate convention).
- `Mask`: `{ kind: None|Ellipse|CustomAlpha, feather: float | float[] }`. Rectangle uses
  `None`; Ellipse uses `Ellipse`; polygons use `None` (their shape IS the geometry).
- Rebuilds the MeshFilter's mesh when vertices change (ear-clip triangulation for concave
  polygons; UVs from axis-aligned bounding box).

**`PMSDKSkinContent`** — the content binding:

- `SourceMode`: `None | Texture | RenderTexture | VideoClip | TestPattern` (v1).
- Source-specific fields.
- Owns a per-skin RT when needed (VideoPlayer target).
- Transform: `CropRect`, `Rotation` (0/90/180/270), `MirrorX`, `MirrorY`.
- Playback: `Play`/`Pause`/`Loop`/`TimeOffset`/`Rate`/`Mute` (for video sources).
- Opacity.
- Pushes uniforms via `MaterialPropertyBlock` (per-skin state is cheap; no per-skin material
  instance needed).

**`PMSDKSkinLoader`** — runtime component on a projector in a content scene. Assigned a
`PMSDKSkinSet` reference (or a JSON path). At `Awake`:

- Reads the skin set.
- Instantiates one `PMSDKSkin` per saved skin as a child of the projector's warp root.
- Applies the saved geometry, shape kind, mask, color, opacity, z-order.
- If a skin has `defaultContent`, applies it directly to `PMSDKSkinContent` (unless a
  `PMSDKSkinBindings` overrides).

**`PMSDKSkinBindings`** — optional runtime component next to `PMSDKSkinLoader`. Lets the
content scene bind arbitrary sources per skin ID. Bindings override `defaultContent`.

### Native SDK

**No native changes for v1.** Three warp paths, all fed by existing native calls:

| Shape kind | Warp path | Native call | Notes |
|---|---|---|---|
| Rectangle, Square | `PMSDKMeshWarp` + `PMSDKCornerPin` | `PerspectiveWarp` (existing) | Homography — projective corner-pin. |
| Ellipse, Circle | `PMSDKMeshWarp` + `PMSDKCornerPin` + shader mask | `PerspectiveWarp` + shader `_ShapeMask=Ellipse` | Same quad geometry; mask cuts the ellipse. |
| Polygon, Freeform | Regenerated Unity mesh, no native warp | (none) | Vertex positions ARE the geometry. Content sampled via bounding-box UVs. |

A skin can be **promoted to a grid warp** for curved faces via `PMSDKGridWarp` (existing).
Same UX flow, different underlying warp. Deferred until a real curved case appears.

### Shader

`PMSDK/UnlitWarp` gains uniforms (backward-compatible defaults):

- `_ShapeMask` (int enum: 0=None, 1=Ellipse) — 0 is identity; 1 samples a circular alpha
  mask over the mesh's UV space with feathered edges.
- `_MaskFeather` (float) — feather width in UV units.
- `_Opacity` (float, default 1.0) — per-skin opacity multiplier applied at the alpha stage.

Existing per-projector edge-blend (vertex alpha) and color-correction paths still apply
per-skin — each skin has its own MPB carrying its uniforms.

## The Skin Editor scene

A dedicated Unity scene shipped in the package. Provides a drawing canvas that IS the
projector output.

### Distribution (both channels)

- **UPM sample**: `com.viitorx.pmsdk/Samples~/SkinEditor/SkinEditor.unity` — imported via
  Unity Package Manager > Samples > "Skin Editor". Copied into `Assets/Samples/...` on
  import; user-editable.
- **Local generator**: `Tools > Projection Mapping > Generate Skin Editor Scene` — creates a
  local copy at `Assets/PMSDKDemo/SkinEditor.unity`, same rig, editable in place. Same
  pattern as the existing `PMSDKDemoSceneGenerator`.

### Layout

- Projector output (Display 2 by default) shows an empty raster-space canvas.
- Operator console (Display 1) shows: shape toolbar (top), skin list (left), skin inspector
  (right), status/help (bottom).
- The scene hosts a `PMSDKSkinEditorController` (new) that owns the drawing logic and the
  save/export workflow.

### Shape toolbar

Buttons + hotkeys: **[R]** Rectangle, **[E]** Ellipse, **[P]** Polygon, **[F]** Freeform.
Selecting a tool arms drawing on the next mouse-drag on the projector canvas.

### Key map

| Key | Action |
|---|---|
| `R` / `E` / `P` / `F` | Rectangle / Ellipse / Polygon / Freeform tool |
| Left-drag on empty canvas | Draw new shape |
| Left-drag on a corner | Move that vertex |
| Left-drag on an edge midpoint (rect/polygon) | Move both adjacent vertices together |
| `N` | Add default-sized shape of current tool at canvas center |
| `Enter` (during polygon draw) | Close the polygon |
| `+` / `-` (with selected polygon edge) | Insert / remove a vertex on that edge |
| `Ctrl+D` | Duplicate selected skin |
| `Del` | Delete selected skin |
| `PgUp`/`PgDn` | Cycle skins |
| `Tab` | Cycle corner within selected skin |
| Arrows | Nudge selected corner (Shift = fine, Ctrl = coarse) |
| `X` / `Shift+X` | Solo selected skin / unmute all |
| `Y` / `Shift+Y` | Cycle test pattern on selected skin |
| `[` / `]` | Send backward / bring forward in z-order |
| `Ctrl+S` | Save skin set (SO + JSON sidecar) |
| `Ctrl+Shift+E` | Export JSON to a file picker location |
| `Ctrl+Shift+I` | Import JSON |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo (coalesced groups) |
| `F1` | Help overlay |

### Save / export

- `Ctrl+S` writes the current state to a `PMSDKSkinSet` SO in the project (default path
  `Assets/PMSDKDemo/Skins/<name>.asset`) + a JSON sidecar (`<name>.json`) at the same path.
  Round-trip fidelity is a hard requirement — JSON is the portable format.
- `Ctrl+Shift+E` exports JSON to any path (portable across machines/projects).
- `Ctrl+Shift+I` imports JSON, overwriting the current in-memory state.

## Playback

A content scene:

1. Has a projector GameObject (same rig as today: ortho camera + display activator +
   optional calibration services).
2. Attaches `PMSDKSkinLoader` to the projector and assigns a `PMSDKSkinSet`.
3. Optional: attaches `PMSDKSkinBindings` to override or supply media per skin ID.
4. Play → the loader instantiates one `PMSDKSkin` per saved skin; the bindings/defaults
   drive content.

The existing F2 calibration mode still works — a `PMSDKCalibrationManager` in the content
scene can nudge skins post-load. Saved changes update the skin set atomically (same pattern
as the existing calibration JSON write).

## Persistence — `PMSDKSkinSet`

### Storage

- ScriptableObject `PMSDKSkinSet.asset` in the project (Unity-native reference,
  versionable).
- JSON sidecar at the same path (`<name>.json`) — the portable, canonical serialization.
  The SO reads the JSON; the JSON is authoritative.
- Runtime overrides via `-skinset <path>` CLI flag or
  `PMSDKSkinLoader.OverrideJsonPath` (matches the existing `-calibfile` pattern).

Skin set JSON files are kept **separate from `pmsdk_calibration.json`** — the calibration
file continues to hold per-projector corner-pin + blend as today; skin sets are their own
files (`pmsdk_skinset_<name>.json`). Back-compat by construction: existing scenes without
skin sets behave unchanged.

### Schema

```json
{
  "version": 1,
  "name": "BoxDemo",
  "projector": {
    "id": "Projector_1",
    "resolution": [1920, 1080],
    "displayIndex": 2
  },
  "skins": [{
    "id": "front",
    "name": "Box front",
    "shape": "rectangle",
    "vertices": [[0.10,0.10],[0.50,0.10],[0.50,0.60],[0.10,0.60]],
    "warp": "projective",
    "mask": { "kind": "none", "feather": 0.0 },
    "transform": { "crop": [0,0,1,1], "rotation": 0, "mirrorX": false, "mirrorY": false },
    "color":     { "gain": [1,1,1], "offset": [0,0,0], "gamma": [1,1,1] },
    "opacity": 1.0,
    "softMask": 0.0,
    "zOrder": 0,
    "defaultContent": {
      "mode": "VideoClip",
      "path": "Assets/Media/front.mp4",
      "loop": true, "mute": false, "startTime": 0.0
    }
  }, {
    "id": "logo",
    "shape": "ellipse",
    "vertices": [[0.55,0.10],[0.90,0.10],[0.90,0.45],[0.55,0.45]],
    "warp": "projective",
    "mask": { "kind": "ellipse", "feather": 0.02 },
    "opacity": 1.0,
    "softMask": [0.02, 0.02, 0.02, 0.02],
    "zOrder": 1
  }, {
    "id": "sign",
    "shape": "polygon",
    "vertices": [[0.60,0.55],[0.85,0.55],[0.95,0.70],[0.85,0.90],[0.60,0.90],[0.50,0.70]],
    "warp": "polygon",
    "opacity": 1.0,
    "zOrder": 2
  }]
}
```

Conventions:

- Vertices are normalized `[0..1]` in the projector's raster (top-left origin — matches
  `PMSDKCalibrationCamera` / decoder pixel conventions).
- `softMask` is scalar in v1 UI. The schema accepts an array `[e0..eN-1]` for per-edge
  feathering from v1; scalar continues to work in v1.1+.
- `defaultContent.path` is Unity-project-relative. The loader tries multiple resolution
  paths (project asset, `Resources`, `StreamingAssets`, absolute).

## Milestones

### M-Sk1 — Foundation
- `PMSDKSkin`, `PMSDKSkinShape`, `PMSDKSkinContent` components.
- Rectangle + Ellipse (quad + shader mask) using existing `PMSDKCornerPin`.
- Polygon (Unity mesh regen, ear-clip triangulation, bounding-box UVs).
- `PMSDK/UnlitWarp` extensions (`_ShapeMask`, `_MaskFeather`, `_Opacity`).
- Content sources: `Texture`, `RenderTexture`, `VideoClip`, `TestPattern`.
- Editor menu + auto-configurator ("Convert Projector to Multi-Skin").
- Unit tests: shape geometry, polygon triangulation, UV assignment, content-source lifecycle.

### M-Sk2 — Skin Editor scene
- `PMSDKSkinEditorController` — drawing engine (draw, select, move corner/edge, insert/
  remove vertex, solo, test pattern).
- Shape toolbar + skin list + skin inspector on the operator console.
- Undo/redo, coalesced groups (reuse the F2 undo pattern).
- Ship in `Samples~/SkinEditor/` and add `Tools > Projection Mapping > Generate Skin Editor
  Scene` (writes to `Assets/PMSDKDemo/SkinEditor.unity`).

### M-Sk2.5 — Persistence + playback
- `PMSDKSkinSet` ScriptableObject + JSON round-trip (`pmsdk_skinset_<name>.json`).
- `PMSDKSkinLoader` + `PMSDKSkinBindings`.
- `defaultContent` auto-binding fallback.
- JSON import/export from the editor; version field for future schema evolution.

### M-Sk3 — Content sources v1.1
- `Camera` source (per-skin Unity camera → RT).
- `VideoUrl` streaming source.
- Per-skin `Spout`/`NDI` via existing `PMSDKSpoutIn`/`PMSDKNdiIn` and
  `PMSDKExternalContent`.
- `Webcam` source (`WebCamTexture`).

### M-Sk4 — Polish
- Duplicate + reorder (drag in the skin list; `Ctrl+D`; `[`/`]` z-order).
- Drag-and-drop file assignment onto a skin in the HUD.
- Per-edge soft mask (`softMask` array UI).
- Snap-to-grid, alignment guides.
- Multi-select + group nudge.

## Non-goals for v1 (deferred but schema-compatible)

- **Cross-projector skin blending.** If a box is lit by two projectors and one face is
  covered by both, blending between projectors on a per-skin overlap requires cross-skin
  ownership. v2.
- **Auto-align per skin.** Gray-code auto-align aligns whole projectors (`A` key) or the
  shared wall canvas (`F4`). Per-skin auto-align would need each face marked out with an
  identifiable pattern. v2.
- **3D-twin skin.** Pull skin corners from a mesh face of a virtual twin. Useful when the
  physical target has an accurate 3D model. v2 (existing `PMSDKProjectorPoseCalibrator`
  covers the whole-object path today).

## Related decisions

- D-020 (raster-space rig) — skins live in raster space, same rules apply.
- D-022 (projective corner pin vs bilinear grid) — quad skins use projective; polygon skins
  use identity mesh.
- D-036 (this feature) — multi-skin architecture.
- D-037 (this feature) — Skin Editor as dedicated scene + `PMSDKSkinSet` asset.
