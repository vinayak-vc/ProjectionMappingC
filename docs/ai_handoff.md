# AI Agent Handoff Document

## Multi-Skin single projector — M-Sk4 v1.7 (preset catalog +Hexagon) COMPLETE (2026-08-21)

**M-Sk4 v1.7 polish — hexagon preset added, 207/207 EditMode tests green** (2 new + 205
existing). First regular n-gon in the catalog. Uses a new `RegularNGon(cx, cy, r, n,
startAngleDeg)` factory in `PMSDKSkinPresets` so future regular n-gon presets
(pentagon, octagon) reuse the same helper — currently invoked for the six-vertex
hexagon centered at raster (0.5, 0.5) with radius 0.30, pointy-top (first vertex at
90°). Kind = Polygon → routed through the regenerated-mesh path (native SDK still
untouched). Tests: `Preset_Hexagon_IsPolygonWithSixVertices` and
`Preset_Hexagon_HasRoughlyEqualEdgeLengths` (regular-polygon edge-length equality
within 1e-3). Menu item at priority 360 as "11. Hexagon".

## Multi-Skin single projector — M-Sk4 v1.6 (preset catalog: 10 iconic targets) COMPLETE (2026-08-20)

**M-Sk4 v1.6 polish — preset catalog done, 205/205 EditMode tests green** (18 new + 187
existing). Ten iconic projection-mapping targets ship as one-click presets so users
learn the plugin AND accelerate common installations. The flagship (Preset #2, "Box: 3
Faces Isometric") is exactly the shape the user asked to draw from the reference image.

**Presets (`Tools > Projection Mapping > Skins > Presets > *`)**:
1. Single Rectangle — beginner starter.
2. **Box: 3 Faces Isometric ★** — cube from a corner view (front + right + top,
   parallelogram/foreshortened layout). *The user's ask.*
3. Box: 2 Faces (Top+Front) — box slightly from above.
4. Column / Cylinder (3 strips) — vertical column as 3 rectangles; bend sides to match.
5. Triangular Prism (2 faces) — polygon×2.
6. Facade Grid 3×2 — regular window/panel grid.
7. Arch / Tunnel — 11-vertex polygon tracing an arch.
8. Dome / Sphere — Ellipse skin (shader mask).
9. Star (5-point) — 10-vertex polygon with alternating radii.
10. Diorama Stage — outer frame + inner stage.

**API**:
- `PMSDKSkinPresets.All` — the ten `Preset` records (Id, DisplayName, Description,
  SkinDef[]). Each `SkinDef` = (Name, Kind, Vertices in raster space).
- `PMSDKSkinPresets.TryFind(id, out preset)`, `PMSDKSkinPresets.DisplayNames()`.
- `PMSDKSkinEditorController.ApplyPreset(preset, replaceExisting = true)` —
  spawns one PMSDKSkin per SkinDef. Records ONE undo entry ("preset"), records so
  Undo restores the pre-preset state. Z-order stack: last spawned = topmost.
- `PMSDKSkinEditorController.ApplyPresetById(id, replaceExisting = true)` —
  string-key overload.

**Editor menu**: 10 items under `Tools > Projection Mapping > Skins > Presets`.
Prompts before destroying existing skins (Replace / Add alongside / Cancel).

**Tests** (`PMSDKSkinPresetsTests`, 18/18):
- Catalog integrity: 10 presets, unique ids, every preset has skins, every skin has
  a valid vertex count for its kind (rect/ellipse = 4, polygon 3..32, freeform 3..128),
  every vertex in raster [0..1].
- Apply flow: ApplyPreset spawns the right skin count, replaceExisting default is true,
  add-alongside keeps existing skins, z-order stacks last-on-top, ApplyPresetById
  returns false on unknown id, one undo entry per preset, Undo restores prior state.
- Per-preset sanity: Dome is Ellipse, Star has 10 verts, Facade has 6 rectangles,
  Arch has ≥7 verts (semi-arc coverage), Diorama has "frame" + "stage" skin names.
- Geometry validity: every polygon has positive signed area (no degenerates).

## Multi-Skin single projector — M-Sk4 v1.5 (UGUI Canvas list + drag-reorder) COMPLETE (2026-08-20)

**M-Sk4 v1.5 polish — UGUI skin list with drag-reorder done, 187/187 EditMode tests
green** (8 new + 179 existing). The skin list on Display 1 is now a proper UGUI Canvas
panel; rows implement IBeginDrag/IDrag/IEndDrag for reorder. All originally-listed
M-Sk4 polish items are now shipped.

**Deliverables**:
- `Runtime/Skin/PMSDKSkinEditorCanvasList.cs` (new) — builds a left-side Canvas panel
  with a `VerticalLayoutGroup` of rows. Rebuilds rows lazily when the skin list changes
  (hashed signature = count + skin IDs + z-orders). Coexists with the v1.4 Canvas
  inspector on the same display (both use `sortingOrder = 100`, own Canvas).
- `PMSDKSkinCanvasRow` (in the same file) — one row per skin. Implements
  `IBeginDragHandler` / `IDragHandler` / `IEndDragHandler`. Drag moves the row in the
  hierarchy live (layout group shifts siblings out of the way), and on end-drag calls
  `PMSDKSkinEditorController.MoveSkinToIndex(skin, newIndex)`.
- `PMSDKSkinEditorController.MoveSkinToIndex(skin, targetIndex)` (new, in the Phase D
  partial) — rewrites every skin's z-order so the resulting sort places `skin` at
  `targetIndex`. Records an Undo entry ("reorder"); no-ops on same-index moves.
- `PMSDKSkinEditorHUD` — new `bool DrawList` field (matches the `DrawInspector` pattern
  from v1.4). Canvas list flips it off while active, restores on disable.
- Editor scene generator: attaches `PMSDKSkinEditorCanvasList` alongside the other
  editor rig components.

**Tests** (`PMSDKSkinEditorCanvasListTests`, 8/8):
- Canvas built after `RebuildForTests`.
- IMGUI HUD's `DrawList` flips off with canvas active.
- Rows populate one-per-skin; row visuals reflect the skin's DisplayName.
- `MoveSkinToIndex` reorders the visual stack, clamps out-of-range targets,
  no-ops on same-index moves (undo depth unchanged), and undo restores the prior
  order. Drag-event simulation is skipped — the pointer-event dispatch trips
  ShouldRunBehaviour in EditMode; the reorder LOGIC is what matters and is
  covered directly.

**All M-Sk4 polish rounds now complete**:
- v1.0 core (snap + multi-select + visibility)
- v1.2 per-edge feather (Ellipse shader)
- v1.3 drag-drop file assignment (Editor)
- v1.4 UGUI Canvas inspector (right panel)
- **v1.5 UGUI Canvas list with drag-reorder (left panel)** ← this pass

## Multi-Skin single projector — M-Sk4 v1.4 (UGUI Canvas inspector) COMPLETE (2026-08-20)

**M-Sk4 v1.4 polish — UGUI Canvas inspector done, 179/179 EditMode tests green** (11 new
+ 168 existing). The skin inspector on Display 1 is now a proper UGUI Canvas panel
(Text/InputField/Slider/Toggle/Button) instead of IMGUI immediate-mode. Rest of the HUD
(toolbar, skin list, status/help) stays IMGUI — non-blocker.

**Component**: `PMSDKSkinEditorCanvasInspector`.
- Builds the entire Canvas hierarchy in code on `OnEnable` (no prefab assets needed).
- Right-aligned panel with `VerticalLayoutGroup` + `ContentSizeFitter`; each row is a
  `HorizontalLayoutGroup` with label + control.
- Fields: name `InputField`, kind label, verts label, opacity `Slider` with numeric
  readout, feather `Slider`, per-edge `Toggle` + four T/R/B/L sliders (visible only on
  Ellipse skins with PerEdgeFeatherEnabled), z-order back/forward buttons, action row
  (Duplicate / Solo / Pattern / Delete).
- Legacy `Text` + `InputField` — no TMP dependency.
- Auto-creates an `EventSystem` + `StandaloneInputModule` if the scene lacks one.
- **Auto-hides the sibling IMGUI HUD's inspector**: on enable, sets
  `PMSDKSkinEditorHUD.DrawInspector = false`; on disable, restores the prior value.
  New `DrawInspector` flag added to the IMGUI HUD for this.
- `RebuildForTests()` — public convenience to force a deterministic build after
  Controller assignment (EditMode's enable/disable timing around AddComponent is
  finicky).

**Scene generator**: adds `PMSDKSkinEditorCanvasInspector` alongside the IMGUI HUD in
the generated scene. Users can disable one or the other to switch back.

**Tests** (`PMSDKSkinEditorCanvasInspectorTests`, 11/11):
- Canvas + EventSystem built; NameField / OpacitySlider / FeatherSlider / PerEdgeToggle
  exist.
- IMGUI HUD's DrawInspector flips off with canvas active; OnDisable restores it.
- Selection change: RebindFieldsFromSkin updates the text + slider to the new skin's
  state; per-edge toggle visibility gates on Ellipse.
- Handlers write back: OnNameChanged / OnOpacityChanged / OnFeatherChanged /
  OnPerEdgeToggled / OnPerEdgeChanged all mutate the selected skin (invoked via
  reflection to bypass UGUI's own dispatch, which trips internal
  `ShouldRunBehaviour` assertions in EditMode).

**Deferred v1.5**: drag-reorder in the skin list (UGUI drag events on rows). Explicit
up/down buttons already exist in the IMGUI list; a UGUI list rewrite plus drag is a
follow-up refinement.

## Multi-Skin single projector — M-Sk4 v1.3 (drag-drop file assignment) COMPLETE (2026-08-20)

**M-Sk4 v1.3 polish — drag-drop file assignment on the HUD done, 168/168 EditMode tests
green** (10 new + 158 existing). Editor-only (guarded by `#if UNITY_EDITOR`) since
`UnityEditor.DragAndDrop` doesn't exist in standalone builds — a standalone play-mode
file-drop path would need a native plugin and is documented as deferred.

**Where it works**:
- Each skin list row is a drop target.
- Inspector has a labeled drop zone ("Drop image / video / asset here").
- Both accept OS-file drops from Explorer AND asset drops from the Project window.

**Payload routing**:
- **OS files** (via `DragAndDrop.paths`):
  - `.png / .jpg / .jpeg / .tga / .exr / .psd / .bmp` → `Mode=Texture`, `Texture2D.LoadImage(bytes)`.
  - `.mp4 / .mov / .webm / .mkv / .avi / .m4v` → `Mode=VideoUrl` with `file://` URI.
  - `.json` → routed to `controller.LoadFrom(path)` (whole-set replace).
- **Unity asset drops** (via `DragAndDrop.objectReferences`, Editor-only path):
  - `Texture` / `Texture2D` / `Sprite` → Texture mode.
  - `RenderTexture` → RenderTexture mode.
  - `VideoClip` → VideoClip mode.
  - `PMSDKSkinSet` → whole-set replace via `PMSDKSkinSetJson.Apply(controller, set.Data)`.

**Utility** (`PMSDKSkinDragDrop`):
- `KindForPath(path)` returns `PMSDKSkinDropKind`.
- `ApplyPath(content, path, out jsonPathOut)` — pure; runtime-safe. JSON drops set
  `jsonPathOut` so the caller (HUD) forwards to the controller.
- `ApplyObject(content, obj, out skinSetOut)` — Editor-only; `PMSDKSkinSet` drops set
  `skinSetOut` for controller-side apply.
- Utility lives in Runtime with `#if UNITY_EDITOR` around the asset-object path;
  Editor and Runtime tests both compile against it.

**HUD** (`PMSDKSkinEditorHUD`):
- `HandleRowDrop(rect, skin)` — Editor-only helper that intercepts `DragUpdated`
  / `DragPerform` on any rect. Called for each skin list row + the inspector drop zone.
- F1 help panel updated with a "Drag-drop (Editor only)" section.

**Tests** (`PMSDKSkinDragDropTests`, 10/10):
- Extension classification for image / video / json / unknown.
- Image path: encode a real PNG via `EncodeToPNG`, write to disk, apply, verify
  `Mode=Texture` and dimensions.
- Missing file: warns + returns false.
- Video path: converts to `file://` URI.
- JSON path: signals `jsonPathOut` to caller without mutating content.
- Unknown extension: no-op.
- Editor-only: `Texture` / `RenderTexture` / `VideoClip` / `PMSDKSkinSet` asset drops
  route to the right modes; `PMSDKSkinSet` sets `skinSetOut` instead of touching content;
  unknown types (`GameObject`) return false.

## Multi-Skin single projector — M-Sk4 v1.2 (per-edge feather) COMPLETE (2026-08-20)

**M-Sk4 v1.2 polish — per-edge feather done, 158/158 EditMode tests green** (6 new + 152
existing). Ellipse skins can now feather each edge independently: T/R/B/L each with its
own softness value. When disabled, the scalar `SoftMask` continues to work exactly as
before (back-compat by construction — the Skin pushes `(SoftMask, SoftMask, SoftMask,
SoftMask)` into `_MaskFeather4`).

**Shader**:
- New uniform `_MaskFeather4 (Top, Right, Bottom, Left)`.
- Per-pixel weighted feather: `fY = d.y>=0 ? f4.x : f4.z; fX = d.x>=0 ? f4.y : f4.w;
  feather = (fX*|dx| + fY*|dy|) / (|dx| + |dy|)`. Smooth transition around the ellipse.
- Scalar fallback when all four channels are zero (defensive; PMSDKSkin always pushes
  something so this branch is rare).

**Component**: `PMSDKSkin.PerEdgeFeatherEnabled` (bool) + `EdgeFeather` (Vector4 TRBL).
When enabled, `PushMaterialProperties` clamps negative values to zero then pushes
`_MaskFeather4` from EdgeFeather. When disabled, pushes the scalar tiled — perfect
back-compat.

**JSON schema**: `mask.perEdgeFeather` (List&lt;float&gt; of length 4) is populated when
enabled, empty/absent otherwise. Round-trip restores `PerEdgeFeatherEnabled` +
`EdgeFeather`. Legacy JSON without the field lands as `PerEdgeFeatherEnabled = false`.

**HUD**: an "Ellipse selected?" gate reveals the toggle + four T/R/B/L sliders when the
selected skin is an Ellipse. Sliders live in the skin inspector under the scalar Feather
slider.

**Tests** (`PMSDKSkinPerEdgeFeatherTests`, 6/6): default pushes uniform scalar, enabled
pushes per-edge, negative values clamp to zero, JSON serializes when enabled, scalar-only
skin leaves `perEdgeFeather` absent, full JSON round-trip restores per-edge state.

## Multi-Skin single projector — M-Sk4 COMPLETE (2026-08-20)

**M-Sk4 polish — done, 152/152 EditMode tests green**. All five multi-skin milestones now
shipped. Skin Editor gains snap-to-grid, alignment guides between skins, multi-select
(Ctrl/Shift+click), group nudge, and per-skin visibility toggle + z-order arrows in the
HUD list rows.

**Deliverables**:
- `Runtime/Skin/PMSDKSkinEditorSnap.cs` (new) — pure static: `SnapToGrid`, `SnapToVertices`,
  `CollectVertices` (excludes the dragged skin). `PMSDKSkinEditorSnapGuides` struct
  tracks live guide lines for the overlay.
- `Runtime/Skin/PMSDKSkinEditorControllerPhaseD.cs` (new partial file) — snap config
  (`SnapGridStep`, `SnapGridTolerance`, `SnapAlignTolerance`), multi-select
  (`MultiSelectedIndices`, `AddToSelection`, `ToggleInSelection`, `ClearMultiSelection`,
  `IsInSelectionGroup`, `SelectionGroupCount`), group nudge (`NudgeSelectedGroup` —
  falls back to single-corner when set is empty), visibility (`ToggleVisibilitySelected`,
  `SetSkinVisibility`, `IsVisible`), internal `ApplySnaps(target, dragged)` /
  `ClearSnapGuides()` called from Phase A's corner-drag path + OnPointerUp.
- `Runtime/Skin/PMSDKSkinEditorController.cs` — added public `HitTestSkinAt(raster)` so
  Input can route modifier-clicks; wired `ApplySnaps` into `OnPointerDrag`'s DraggingCorner
  branch; `ClearSnapGuides` in `OnPointerUp`.
- `Runtime/Skin/PMSDKSkinEditorInput.cs` — Ctrl/Shift+click in Select mode toggles the hit
  skin in multi-select instead of running the default select-or-draw flow. Arrow-key nudge
  now always calls `NudgeSelectedGroup` (which falls through to single-corner when the
  group is empty).
- `Runtime/Skin/PMSDKSkinEditorOverlay.cs` — multi-selected skins render in cyan
  (`MultiSelectedColor`); snap guides drawn as dashed magenta lines spanning the raster
  canvas while a corner drag is snapping.
- `Runtime/Skin/PMSDKSkinEditorHUD.cs` — snap toggle button in the toolbar (cycles
  OFF → 0.05 → 0.10 → OFF), multi-select + snap badges in the status hint, per-row
  z-order up/down arrows + visibility toggle (o/-) in the skin list, extended F1 help.

**Tests (M-Sk4)**: `PMSDKSkinEditorPhaseDTests` — 16/16:
- `PMSDKSkinEditorSnap` unit math: SnapToGrid within/outside tolerance, step=0 pass-through,
  SnapToVertices sets X-only / both-axis guides, CollectVertices excludes the dragged skin.
- `ApplySnaps` during corner drag snaps to the grid; alignment snap exposes the guide
  state and clears on pointer-up.
- Multi-select: ToggleInSelection adds/removes/promotes when the primary is toggled,
  ClearMultiSelection empties the set.
- Group nudge: moves every selected skin's same-index corner; empty group falls back to
  single-corner nudge.
- Visibility: `SetSkinVisibility` toggles the MeshRenderer; `ToggleVisibilitySelected`
  toggles the primary.

**Bug caught**: same "skin-index instability at equal ZOrder" issue as M-Sk2.5 —
`ToggleInSelection_PrimaryClick_PromotesOrClears` captured indices too early. Fixed by
capturing skin references and re-resolving indices after all spawns.

**Deferred with notes** (documented in tasks.md):
- **Per-edge feather UI + shader** — schema stores `perEdgeFeather` but visual effect
  requires shader work; better as a v1.2 pass.
- **Drag-drop file assignment onto skins in the HUD** — IMGUI `DragAndDrop` is Editor-only;
  play-mode file drops are a separate integration.
- **Canvas-based inspector** — IMGUI works; Canvas rewrite is non-blocking.

**Full multi-skin roadmap status**: **M-Sk1..M-Sk4 all done**.

## Multi-Skin single projector — M-Sk3 COMPLETE (2026-08-20)

**M-Sk3 v1.1 content sources — done, 136/136 EditMode tests green**. Skins can now source
their pixels from a Unity Camera, a streaming URL, a webcam, an arbitrary external
texture (Spout / NDI / user code) — on top of the M-Sk1 set (Texture / RenderTexture /
VideoClip / TestPattern).

**Deliverables**:
- `Runtime/Skin/PMSDKSkinContent.cs` — 4 new modes:
  - **Camera**: assign a `Camera` reference; content owns a per-skin RT, sets it as
    that camera's `targetTexture`, and restores the camera's prior target on mode
    switch / disable (matched to `ownedCameraRT` so a user reassignment isn't overwritten).
  - **VideoUrl**: extends the existing VideoPlayer pipeline with `source = Url`; reuses
    the owned RT + Loop/Mute/StartTime/PlaybackRate.
  - **Webcam**: owns a `WebCamTexture` (device name, WxH, FPS). Only calls `Play()` in
    Play mode — EditMode allocates but skips activation to avoid keeping the device open
    across domain reloads.
  - **ExternalTexture**: exposes a public `Texture ExternalTexture` field read each
    frame — adapter components (see below) push their frame here.
- `Runtime/Interop/Spout/PMSDKSkinSpoutIn.cs` — per-skin `Klak.Spout.SpoutReceiver`
  adapter; pushes `receivedTexture` into `PMSDKSkinContent.ExternalTexture` each frame,
  forces `Mode = ExternalTexture` while enabled (restores prior Mode on disable).
  Compile-gated on `PMSDK_KLAK_SPOUT` — inherits the same `versionDefines` as the
  existing scene-wide `PMSDKSpoutIn`.
- `Runtime/Interop/Ndi/PMSDKSkinNdiIn.cs` — same for `Klak.Ndi.NdiReceiver`.
- `Runtime/Skin/PMSDKSkinSetJson.cs` — schema extended: `defaultContent` gains
  `url` (VideoUrl) and `webcamDeviceName` (Webcam) fields. Camera/ExternalTexture
  preserve only their mode (scene refs must be re-wired at load time).
- `Runtime/Skin/PMSDKSkinBindings.cs` — `Binding` gains `VideoUrl`, `SourceCamera`,
  `WebcamDeviceName`, `ExternalTexture` fields; `Apply` switch extended to the four
  new modes.
- `Runtime/Skin/PMSDKSkinLoader.cs` — `PMSDKSkinSetJsonExt.ApplyDefaultContent` switch
  extended to handle VideoUrl/Webcam on load.

**Tests** (M-Sk3): `PMSDKSkinContentV11Tests` — 13/13:
- Camera mode allocates the owned RT, swaps `Camera.targetTexture`, restores the prior
  target on mode switch (assertion order-fixed with `cam.targetTexture = null` before
  release in the finally block).
- Camera mode with null camera → null CurrentTexture.
- VideoUrl mode with empty URL → null. With a URL → `VideoSource.Url` + `vp.url` set.
- Webcam mode allocates the texture but doesn't `.Play()` in EditMode.
- ExternalTexture mode exposes the assigned texture and tracks field changes across
  frames.
- JSON schema serializes VideoUrl.url and Webcam.deviceName.
- `Bindings.Apply` correctly wires each of VideoUrl, Camera, Webcam, ExternalTexture.

**Two bugs caught by the new tests**:
1. `TeardownAll` released the owned camera RT BEFORE restoring the camera's prior
   `targetTexture`, which meant the RT was still bound to the camera at Release time →
   Unity logs "Releasing render texture that is set as Camera.targetTexture!". Fixed
   by reordering: `RestoreTrackedCamera()` runs first.
2. `OnDestroy` called `DestroyImmediate(videoPlayer)` on an attached component, but
   Unity's own GameObject-destroy cleanup runs after OnDestroy and destroys the same
   component → "Destroying object multiple times." Fixed by removing OnDestroy entirely
   and moving the VideoPlayer teardown into `TeardownAll` (which just stops + detaches;
   Unity handles the final destroy).

**How to use the new sources**:
- Camera: set `PMSDKSkinContent.Mode = Camera`, `SourceCamera = <your camera>`.
- Video URL: `Mode = VideoUrl`, `VideoUrl = "https://..."`, Play/Loop/Mute as usual.
- Webcam: `Mode = Webcam`, `WebcamDeviceName = ""` (default) or a specific device name.
- Spout: add `PMSDKSkinSpoutIn` component to the same GameObject as `PMSDKSkinContent`
  — the adapter auto-configures a `SpoutReceiver` and streams frames into the skin.
  Same pattern for NDI via `PMSDKSkinNdiIn`.

## Multi-Skin single projector — M-Sk2.5 COMPLETE (2026-08-20)

**M-Sk2.5 (author once, use in any scene) — done, 123/123 EditMode tests green** on Unity
MCP (port 6402). The multi-skin feature now has its "portable artifact" surface: a
`PMSDKSkinSet` ScriptableObject with a JSON sidecar, and a `PMSDKSkinLoader` that reads it
back into any content scene.

**Deliverables**:
- `Runtime/Skin/PMSDKSkinSet.cs` — ScriptableObject wrapping `PMSDKSkinSetJson.Set`. Methods:
  `CaptureFrom(controller)`, `ToJson()`, `FromJson(str)`, `SaveJsonTo(path)` (atomic .tmp+move),
  `LoadJsonFrom(path)`, `ReloadFromSidecar()`, `GetSidecarPath()`. Editor menu creates via
  `Create > PMSDK > Skin Set` or via `Tools > Projection Mapping > Skins > New Empty Skin Set…`.
- `Runtime/Skin/PMSDKSkinLoader.cs` — runtime component; at Start, spawns one PMSDKSkin per
  data entry (idempotent, `DespawnAll` for cleanup, `SpawnInEditMode` opt-in). Load path
  precedence: (1) `-skinset <path>` CLI flag, (2) `OverrideJsonPath` on the component,
  (3) SkinSet's JSON sidecar (if `ReloadFromSidecar` is on and file exists), (4) SkinSet's
  in-memory `Data`.
- `Runtime/Skin/PMSDKSkinBindings.cs` — scene-level content override per SkinId. Modes:
  Texture / RenderTexture / VideoClip + playback fields (Loop/Mute/StartTime/PlaybackRate/Opacity).
  Bindings ALWAYS beat the SkinSet's `defaultContent`.
- `Runtime/Skin/PMSDKSkinSetJson.cs` — extended DTO with the design doc's `warp`, `mask`,
  `transform`, `color`, `defaultContent` fields. JsonUtility-friendly (Vector2 stays as
  `{x,y}` — schema note in D-037; canonical tuple form is a v3 migration behind a version
  bump). ContentModes / TestPatternTypes serialize as strings.
- `Editor/PMSDKSkinSetMenu.cs` — `Tools > Projection Mapping > Skins > Save Skin Set…`
  (writes `.asset` + `.json` sidecar next to it, reuses existing GUID on overwrite),
  `Load Skin Set…` (accepts either `.asset` or `.json`), `New Empty Skin Set Asset…`.
- `Tests/Editor/PMSDKSkinSetTests.cs` — 12/12: capture, JSON round-trip, save+load via
  filesystem, TestPattern fields serialize, Loader spawns per data entry, Loader default
  content applied, Loader idempotent, Loader DespawnAll clears, OverrideJsonPath takes
  precedence over the SO, Bindings TryGet, Bindings Apply, Bindings override beats
  defaultContent.

**Bug caught**: `PMSDKColorCorrection` exposes the gamma vector as `OutputGamma` (not
`Gamma`) — first draft of `PMSDKSkinSetJson.CaptureColor` referenced the wrong name and
would have CS1061'd. Fixed before compile.

**Content authors' workflow now works end-to-end**:
1. Author in the Skin Editor scene, `Ctrl+S` for the persistentDataPath quick-save,
   `Tools > Projection Mapping > Skins > Save Skin Set…` for the portable SO + JSON sidecar.
2. In any content scene: add `PMSDKSkinLoader` to (or a child of) the projector, assign the
   `PMSDKSkinSet` asset. Optionally add `PMSDKSkinBindings` to point specific skins at
   scene-local video clips / RTs.
3. Press Play — geometry loads from the set, content flows from bindings or defaults.

**Not in this pass**: canonical tuple-format JSON (v3), M-Sk3 content sources (Camera,
VideoUrl, Spout, NDI, Webcam), M-Sk4 polish (drag-drop file assignment, per-edge mask,
snap/guides, multi-select, canvas-based inspector).

## Multi-Skin single projector — M-Sk2 Phase A+B+C COMPLETE (2026-08-20)

**All of M-Sk2 shipped in this session — 111/111 EditMode tests green** (24 Phase B/C +
24 Phase A + 25 M-Sk1 + 38 pre-existing). Drawing, editing, undo/redo, and JSON save/load
all working. Only M-Sk2.5 (portable `PMSDKSkinSet` ScriptableObject) and M-Sk3/4 (v1.1
content sources + polish) remain.

**Phase B (drawing polish + editor UI) — done**:
- Polygon click-to-draw: `P` tool + click adds a vertex, click near start (or `Enter`) closes.
- Freeform stroke-to-draw: `F` tool + drag traces points at `FreeformSampleSpacing`, release closes.
- Insert / remove polygon vertex (`+` / `-`) on the selected corner's edge / at that corner.
- `Ctrl+D` duplicate (offsets by +0.02); `[` / `]` z-order back / forward.
- `X` solo (mutes every other skin's MeshRenderer); `Shift+X` unmute all.
- `Y` / `Shift+Y` cycles the selected skin's test pattern (routes through `PMSDKSkinContent`,
  auto-sets Mode = TestPattern).
- Extended IMGUI HUD: tool toolbar with active-highlight, skin list (click to select, muted
  glyph), skin inspector (name field, opacity/feather sliders, z-order + duplicate + solo +
  pattern-cycle + delete buttons), extended F1 help.

**Phase C (undo/redo + JSON save/load) — done**:
- `PMSDKSkinEditorUndo`: coalesced snapshot stack (0.7s window, depth cap 100, same feel
  as F2 calibration). Nudge bursts collapse to one entry.
- All mutating operations record before mutating: draw-rect, corner-drag, edge-drag,
  add-default, delete, duplicate, poly-start, poly close, freeform-start, vertex-insert,
  vertex-remove, zorder, solo/unmute, pattern-cycle, nudge, load.
- `Ctrl+Z` / `Ctrl+Y` undo/redo — restores state by JSON round-trip via `PMSDKSkinSetJson.Apply`
  which reconciles scene skins on `SkinId` (in-place update, spawn missing, destroy removed).
- `PMSDKSkinSetJson`: JsonUtility-backed round-trip. Atomic write (write to `<path>.tmp`,
  move-over). Default save path: `Application.persistentDataPath/pmsdk_skinset_default.json`.
  Interim schema — matching the canonical JSON in the design doc becomes M-Sk2.5's job.
- `Ctrl+S` save / `Ctrl+L` load bindings on the input, plus toolbar buttons.

**Bug caught by Phase B/C tests**:
- `BeginFreeformStroke`, `ContinueFreeformStroke`, `EndFreeformStroke` were declared `internal`,
  so the Tests/Editor asmdef (a separate assembly) couldn't call them → CS1061. Bumped to
  `public`. Also added `using UnityEngine.TestTools;` for `LogAssert`.

**Files added / modified in Phase B/C** (`bindings/unity/com.viitorx.pmsdk/`):
- `Runtime/Skin/PMSDKSkinEditorControllerPhaseBC.cs` (new) — partial class with all
  Phase B/C methods + state.
- `Runtime/Skin/PMSDKSkinEditorUndo.cs` (new) — coalesced undo stack.
- `Runtime/Skin/PMSDKSkinSetJson.cs` (new) — JSON round-trip + snapshot capture / apply.
- `Runtime/Skin/PMSDKSkinEditorController.cs` — marked `partial`; added Undo.Record hooks
  in NudgeSelectedCorner, DeleteSelected, AddDefaultShape, BeginHandleDrag, OnPointerDown
  (draw-rect branch).
- `Runtime/Skin/PMSDKSkinEditorInput.cs` — added polygon click-to-draw routing, freeform
  stroke routing, Enter close, +/- vertex, [/] z-order, X/Shift+X solo, Y/Shift+Y pattern,
  Ctrl+D duplicate, Ctrl+Z/Y undo/redo, Ctrl+S/L save/load, Esc cancels polygon draw.
- `Runtime/Skin/PMSDKSkinEditorHUD.cs` — extended IMGUI with toolbar + skin list + inspector.
- `Tests/Editor/PMSDKSkinEditorPhaseBCTests.cs` (new) — 24 tests covering polygon draw,
  freeform, insert/remove vertex, duplicate, z-order, solo/unmute, pattern cycle,
  undo/redo (incl. coalesce), JSON round-trip, save/load, missing-file handling.

**Not in this pass**: UPM `Samples~/SkinEditor/` — the local generator (`Tools > Projection
Mapping > Skins > Generate Skin Editor Scene`) is the primary distribution, and a UPM
sample would be redundant with the generator's output. Marked deferred in tasks.md.

## Multi-Skin single projector — M-Sk2 Phase A COMPLETE (2026-08-20)

**M-Sk2 Phase A (drawing engine + overlay + scene generator) — done, 87/87 EditMode tests
green** on Unity MCP (port 6402). Ships the "draw a shape and drag its corners" experience
in a dedicated `Assets/PMSDKDemo/SkinEditor.unity` scene generated on demand.

New Unity files in `bindings/unity/com.viitorx.pmsdk/`:
- `Runtime/Skin/PMSDKSkinEditorController.cs` — state machine + public API (tool switch,
  pointer down/drag/up, cycle skin/corner, add default shape, delete). No input polling —
  pure state, unit-testable.
- `Runtime/Skin/PMSDKSkinEditorInput.cs` — reads keyboard + mouse each frame, converts
  mouse to raster space via the same `Display.RelativeMouseAt` pattern as
  `PMSDKCalibrationManager.TryGetPointerViewport`, dispatches to the controller.
- `Runtime/Skin/PMSDKSkinEditorOverlay.cs` — GL immediate-mode overlay drawing outlines +
  handles on the projector camera in `OnRenderObject`. Selected skin pulses yellow;
  selected corner highlighted white; unselected dim gray.
- `Runtime/Skin/PMSDKSkinEditorGeometry.cs` — pure static helpers (pick corner /
  edge-midpoint, point-in-polygon / -ellipse, bounding box, build-rect-from-drag,
  clamp-to-raster). All unit-tested.
- `Runtime/Skin/PMSDKSkinEditorHUD.cs` — IMGUI status line + F1 help overlay on
  Display 1 (Canvas HUD is deferred to Phase B).
- `Runtime/Shaders/SkinEditorOverlay.shader` — trivial per-vertex-color line/quad
  shader (ZTest Always, alpha blend) for the overlay's GL draws.
- `Editor/PMSDKSkinEditorSceneGenerator.cs` — `Tools > Projection Mapping > Skins >
  Generate Skin Editor Scene` — creates a self-contained scene with runtime services +
  ortho projector on Display 2 + editor rig, idempotent.

**Tests** (M-Sk2 Phase A):
- `PMSDKSkinEditorGeometryTests` — 11/11 (pick corner/edge, point-in-polygon,
  point-in-ellipse, build-rect-from-drag, clamp, bounds).
- `PMSDKSkinEditorControllerTests` — 13/13 (draw-rectangle drag flow, corner drag,
  edge-midpoint drag moving both adjacent corners, click-select, delete, cycle, nudge
  with clamp). Directly drives `OnPointerDown/Drag/Up` — no Play mode needed.

**Bug caught by controller tests**: `PMSDKSkinMeshBuilder.BuildUnitQuadMesh` and
`BuildPolygonMesh` weren't setting mesh **normals**, but `PMSDKMeshWarp.InitializeNativeMeshes`
reads `normals[i]` unconditionally — would have `IndexOutOfRangeException` at runtime for
any Rectangle/Ellipse skin. Fixed to set explicit `-Z` normals per vertex.

**Key map (Phase A)**:
- `R / E / P / F` pick tool, `Esc` select tool.
- Drag on empty canvas with `R/E` — draw new rectangle/ellipse.
- Drag corner or edge midpoint — reshape any existing skin.
- `N` add default shape, `Del`/`Backspace` delete.
- `PgUp/PgDn` cycle skins, `Tab`/`Shift+Tab` cycle corners.
- Arrows nudge (Shift = fine, Ctrl = coarse).
- `F1` help overlay.
- Not in Phase A: polygon click-to-draw, freeform stroke, solo/mute, Ctrl+D duplicate,
  z-order `[`/`]`, Ctrl+S save, Ctrl+Z undo — deferred to Phase B/C.

**Playback of a saved skin set** is still M-Sk2.5 (PMSDKSkinSet SO + JSON sidecar +
PMSDKSkinLoader + PMSDKSkinBindings). The Skin Editor scene lives in memory only at Phase A
— no persistence yet.

## Multi-Skin single projector — M-Sk1 CODE COMPLETE (2026-08-20)

Single projector hosts N independent content regions ("skins"), each a shape
(Rectangle/Ellipse/Polygon/Freeform) drawn on the projector's raster output and mapped to a
physical face. Design: `docs/multi-skin-design.md`. Decisions: D-036 (multi-skin
architecture — quad shapes reuse `PerspectiveWarp`; polygon shapes are regenerated Unity
meshes; native SDK unchanged for v1), D-037 (dedicated Skin Editor scene + `PMSDKSkinSet`
as the authoring artifact).

**M-Sk1 (Foundation) — done, Unity-compile-verified, MCP test-run deferred.** Files added
in `bindings/unity/com.viitorx.pmsdk/`:
- `Runtime/Skin/PMSDKSkin.cs` — the skin component. `Kind ∈ {Rectangle, Ellipse, Polygon,
  Freeform}`; `Vertices : List<Vector2>` in raster-space [0..1] y-UP; auto-manages the
  underlying warp path (Rectangle/Ellipse → `PMSDKMeshWarp`+`PMSDKCornerPin` on the same
  GameObject; Polygon/Freeform → disables those and owns a regenerated Unity mesh).
  Pushes `_ShapeMask`, `_MaskFeather`, `_Opacity` and `_MainTex` via MPB. Z-order maps to
  small localPosition.z offsets.
- `Runtime/Skin/PMSDKSkinShape` — folded into `PMSDKSkin` as its `Kind`/`Vertices`/`SoftMask`
  fields (kept the design-doc term inline in code comments; no separate MonoBehaviour to
  reduce inspector noise).
- `Runtime/Skin/PMSDKSkinContent.cs` — content-source binding.
  `Mode ∈ {None, Texture, RenderTexture, VideoClip, TestPattern}`; owns per-skin
  `VideoPlayer`+`RenderTexture` only in VideoClip mode and a `Texture2D` in TestPattern
  mode; tears them down on mode switch. Playback controls (Play/Loop/Mute/Rate/StartTime)
  wire to `VideoPlayer`.
- `Runtime/Skin/PMSDKSkinMeshBuilder.cs` — pure static utility. Unit-quad, polygon
  triangulation (ear-clip, O(n²), handles CW input by internal reversal), bounding-box UV
  mapping. Vertex caps `MaxPolygonVertices = 32` / `MaxFreeformVertices = 128`.
- `Runtime/Shaders/UnlitWarp.shader` — three new uniforms with no-op defaults:
  `_ShapeMask` (0=None, 1=Ellipse), `_MaskFeather`, `_Opacity`. Ellipse mask uses raw mesh
  UV (`i.uv`, not the content-transformed `uv`) so `PMSDKOutputTransform` rotations don't
  rotate the mask along with content. Existing pipeline (edge blend → luminance gain →
  black level → color correct) unchanged.
- `Editor/PMSDKSkinMenu.cs` — `Tools > Projection Mapping > Skins > Add Skin > {Rectangle,
  Ellipse, Polygon (Hexagon), Freeform (empty)}`. Creates a fully-configured GameObject
  under the selected projector (matches existing warp-surface transform convention: identity
  rotation, `localScale=(16/9,1,1)`, `PMSDK/UnlitWarp` material). Quad kinds get
  `PMSDKMeshWarp`+`PMSDKCornerPin` companions; polygon kinds do not. `Convert Projector to
  Multi-Skin` is a stub (v1: projectors already tolerate multiple sibling skins; M-Sk2 will
  strip the classic split-slice material).
- `Tests/Editor/PMSDKSkinMeshBuilderTests.cs` — pure-logic unit tests: unit-quad
  winding/UVs, signed area, CW/CCW normalization, concave ear-clip, degenerate rejection,
  BuildPolygon bounding-box UV, cap-sized (128-gon) triangulation.
- `Tests/Editor/PMSDKSkinTests.cs` — integration tests: kind-driven mesh selection, MPB
  uniform push, z-order transform, `OnValidate` clamping/padding, content lifecycle
  (None/Texture/TestPattern mode switching, owned-texture release).

**Verification state**:
- Unity **compile** clean — 0 `error CS`, 0 errors in Unity console, .meta files
  auto-generated on save.
- **EditMode tests 63/63 green** on Unity MCP (port 6402):
  - New M-Sk1 tests: **25/25** (13 in `PMSDKSkinMeshBuilderTests`, 12 in `PMSDKSkinTests`).
  - Pre-existing: **38/38** (Calibration + Math + Osc + TestPattern) — no regressions.
- Test discovery unblocked by removing the `UNITY_INCLUDE_TESTS` define constraint from
  `Tests/Editor/vxpmsdk.Tests.Editor.asmdef` — `includePlatforms: ["Editor"]` already keeps
  tests Editor-only, so the constraint was redundant belt-and-suspenders. The Multi Projector
  template didn't have `UNITY_INCLUDE_TESTS` in its scripting defines and is ReadOnly per
  the machine setup, so this fix lives in the SDK package (the right layer).
- MCP `run_tests` filter API gotcha: `assembly_names` filter returns `total: 0`. Use
  `test_names` with class fixture names (e.g. `"vxpmsdk.Tests.PMSDKSkinTests"`) or full test
  paths. Bake this into the next agent's memory.
- **Runtime path** (playing a scene with a skin) — deferred; needs a scene actually holding
  a `PMSDKSkin`. Recommended smoke: `Tools > Projection Mapping > Generate Demo Scene`,
  then use the new `Add Skin > Ellipse` menu to drop a skin onto the projector, hit play,
  confirm the ellipse mask cuts a circular region and the corner-pin drag still works.

**Convention decisions locked during implementation** (worth carrying forward):
- `PMSDKSkin.Vertices` and all skin geometry are **y-UP normalized `[0..1]`** raster
  coordinates (matches `PMSDKCornerPin`'s corner fields and Unity's world convention). The
  design doc's earlier "top-left origin" note was inconsistent; the actual on-disk
  convention (both in-memory and future JSON persistence) is y-UP.
- **Kind swap at runtime**: `PMSDKSkin` DOES auto-enable/disable its
  `PMSDKMeshWarp`+`PMSDKCornerPin` companions when `Kind` changes between quad and polygon
  families. Users can flip kind mid-session without deleting the skin (not a documented
  workflow, but robust to inspector edits).
- **Shape mask discards** outside pixels (rather than blending with alpha), so skins can
  safely overlap without one skin's black falloff writing over another. Alpha blending
  would require a queue change; discard keeps the queue at `Geometry` with `ZWrite On`.

**Next**: **M-Sk2** — Skin Editor scene (drawing tools: draw with mouse, corner/edge drag,
insert/remove polygon vertex, solo/mute, undo/redo). Ship in `Samples~/SkinEditor/` and
add the `Tools > Projection Mapping > Generate Skin Editor Scene` menu that writes a copy
to `Assets/PMSDKDemo/SkinEditor.unity`. **Before M-Sk2** it's worth spending 10 min to fix
the MCP test discovery so the M-Sk1 tests can gate M-Sk2 work.

## HoloTrackSDK — NEW independent product (2026-07-21)

Branch `Head-Tracked-Holographic-Projection-System` (off `main`). Building the **Head-Tracked
Holographic Projection System** as a **separate DLL** — `HoloTrackSDK.dll`, namespace
`holotrack`, no runtime dependency on `ProjectionMappingSDK` (D-029). OAK-D-PRO-W-97 →
single-viewer head tracking → off-axis generalized-perspective render (D-030) that feeds a
content camera; projection mapping is composed downstream in the consumer scene, not by linking
the two DLLs. Design: `docs/holotrack-architecture.md`; decisions D-029..D-032; plan/status in
`docs/roadmap.md` + `docs/tasks.md` (HoloTrack sections).

**Done (H1–H3), verified with no camera:**
- C++ core in `include/HoloTrack/**` + `holotrack/src/**`: filters (Exp/OneEuro/Kalman),
  Searching→Tracking→Prediction→Lost state machine, viewer selector (nearest/largest-box +
  depth gate + switch hysteresis + stable id + no viewer-hop on transient loss), head estimator
  (pose keypoints → bbox+depth+body-height fallback), OAK→world transform, Kooima off-axis
  projection, and the `Tracker` orchestrator (velocity + prediction extrapolation).
- C-API `include/HoloTrack/C_API/**` → `HoloTrackSDK.dll` with 10 `ht_*` exports (dumpbin-
  confirmed). Independent CMake target wired via top-level `add_subdirectory(holotrack)`
  (`HOLOTRACK_BUILD`, default ON).
- Verification: `holotrack_harness` (dependency-free assertion exe) **161/161, exit 0**. A
  GoogleTest suite is also authored but gated behind `HOLOTRACK_BUILD_GTESTS` (default OFF):
  the prebuilt vcpkg gtest was built against a newer MSVC STL than the local link toolset, so
  it needs `__std_rotate` which only resolves when a heavy lib (OpenCV) drags in the newer CRT —
  the pure test target must not depend on that, hence the harness is the primary verifier.
  Enable the gtest suite once the build/gtest toolsets are aligned.

**H5 Unity package AUTHORED** (`bindings/unity/com.viitorx.holotrack`, asmdef `vxholotrack`):
`HoloTrackNative` (P/Invoke, lib `HoloTrackSDK`), `PMHTHeadTracker` (owns handle, pumps
`IHeadTrackingSource`, zero per-frame alloc), `PMHTSimulatedSource` (proxy transform — no
hardware), `HeadTrackingConfig` SO, `HeadTrackingDisplaySurface`, `HeadTrackedCameraController`
(off-axis + safety layer), `HeadTrackingDiagnostics`, `HeadTrackingRecorder`, README. NOT
compiled in Unity yet (no editor here) — that is H6.

**H6 DONE — consumer sample verified live** (nested game repo `main`, commit `dc18b10`): base
project `Packages/manifest.json` references `com.viitorx.holotrack` (file:), `HoloTrackSDK.dll`
in `Plugins/HoloTrack/` (force-added past the global `*.dll` ignore). Scene
`HoloTrackDemo` (display surface + off-axis camera + sim source + head proxy + 3 depth cubes +
diagnostics). Package compiled clean. Play-mode parallax verified numerically: head +0.8 x →
on-plane surface stays NDC.x 0.000 (window anchored) while near cube 0.600→0.200 and far cube
0.000→0.400 move OPPOSITE ways = correct off-axis hologram. No view-handedness flip needed.
Gotchas: MCP drops on domain reload (reconnects); play mode started PAUSED (clear
`EditorApplication.isPaused`); MCP screenshot renders white for this scene (built-in-pipeline
capture quirk, reproduces with the default camera — unrelated to HoloTrack; verify numerically).

**H4 CODE done — live test deferred until DepthAI C++ install + blobs are available.** `holotrack::OakDevice` (PImpl
`IDetectionSource`, DepthAI ColorCamera+MonoL/R→StereoDepth→`MobileNetSpatialDetectionNetwork`
on a bg thread, spatial→metres +Y up) compiled only under `HOLOTRACK_HAVE_DEPTHAI`, else an inert
stub → default DLL stays camera-free. Device C-API `HoloTrack/C_API/DeviceAPI.*` (8 `ht_oak_*`
exports) + Unity `PMHTOakSource` (`IHeadTrackingSource` over the same push path; warns + no-ops
without DepthAI). CMake `HOLOTRACK_WITH_DEPTHAI` (OFF default) now resolves an external
`depthai` CMake package by default; vcpkg resolution is opt-in with
`HOLOTRACK_DEPTHAI_USE_VCPKG=ON` only for machines with a custom/updated port (D-033). Verified:
feature-OFF DLL builds, harness 180/180, Unity C# compiles clean.

To go live: install Luxonis DepthAI C++ (`depthaiConfig.cmake` exporting `depthai::core`), configure
with `-DHOLOTRACK_WITH_DEPTHAI=ON -DCMAKE_PREFIX_PATH=<depthai-install>`, build, **close Unity**
(plugin lock) and redeploy the new DLL to `Plugins/HoloTrack/`, drop person/face `.blob` files in
StreamingAssets or use absolute paths, close OAK Viewer for exclusive device access, swap the demo's
`PMHTSimulatedSource` for a `PMHTOakSource`, attach the OAK-D-PRO-W-97, smoke-test with
`holotrack_oak_smoke` first and Unity second. Targets depthai-core v2.x — verify node API
(`MobileNetSpatialDetectionNetwork`, board sockets) against the installed version.

**H7 head-tracked SBS-3D integration — SDK + rig code DONE (2026-07-22).** Design:
`docs/holotrack-stereo-integration.md`. Added: stateless `ht_compute_offaxis_eye` C-API (arbitrary
eye) + `PMHTHeadTracker.TryComputeOffAxis(...,eye,...)` overload; generic
`HeadTrackedStereoController` (head ± IPD/2 → two off-axis frusta on two eye cameras; `swapEyes`,
safety layer); OAK detection modes `Person/Face/FaceThenPerson` (`OakDevice`/`OakOptions`/
`ht_oak_options_t`/`PMHTOakSource` — face box emitted as a nose keypoint so the estimator uses it
directly; person fallback after N faceless frames; feature-gated, untested without hardware);
game-side `PMSDKStereoContentRig.ExternalEyeMatrices` + eye-camera getters + `EnsureEyeCameras()`.
Build feature-OFF green, **harness 180/180**. Nested rig change committed to the game repo.

**H7 diorama scene DONE + play-mode verified (2026-07-22).** Built `HoloStereoDioramaDemo`
(nested repo `Scenes/`) per brief §C: display surface (2.0×1.2 @ z=0) + rest eye (0,0,+2),
recessed box as 5 inward walls (robust fallback vs ProBuilder face-flipping), 4-bar bezel,
3 depth props (z −1.05/−0.55/−0.15), pop-out hero (z +0.25). Wiring added: clean
`HeadTrackedStereoController.SetEyeCameras(l,r)` (SDK) + game-side `PMSDKStereoHeadTrackBinder`
(sets rig `SceneCameras`+`ExternalEyeMatrices`+`EnsureEyeCameras`, binds the rig's on-demand eye
pair into the controller; `[DefaultExecutionOrder(50)]`, after the rig). Verified numerically:
window disparity 0 (anchored), far>near positive behind, hero negative/crossed in front (no
`swapEyes` flip); head→(0.8,0,2) leaves the window anchored (Δ0) while behind props shift right
(far ≫ near) and the pop-out shifts opposite; eye offset exactly the 0.06 IPD. Console clean.

**Root cause fixed along the way:** the deployed `Plugins/HoloTrack/HoloTrackSDK.dll` was the
PRE-H7 build (10 `ht_` exports, missing `ht_compute_offaxis_eye`) → the controller threw
`EntryPointNotFound` and never moved the eyes. Rebuilt the DLL **feature-OFF standalone** (a
throwaway minimal CMake over the 4 holotrack sources + header-only `PMSDK/Math` — NO vcpkg/OpenCV,
~1 min at a short path to dodge MAX_PATH) → 19 `ht_` exports incl. the H7 arbitrary-eye + H4
`ht_oak_*`; redeployed (Unity closed for the plugin lock). Feature-OFF is enough for head-tracked
stereo with the sim source; the real OAK path is now blocked on installing DepthAI C++ + blobs, not
on this repo's vcpkg baseline.

**Manifest note:** `com.viitorx.holotrack` was added to `Multi Projector/Packages/manifest.json`
(file:) so the package loads — but that manifest is in the TEMPLATE project (never committed), so
on a fresh machine it must be re-added (same as the pmsdk reference).

**All of H1–H7 SDK/rig code + the stereo diorama scene are complete and play-mode verified. The
only remaining HoloTrack work is the H4 real-OAK path (requires DepthAI C++ package + model blobs +
exclusive camera access).**

**Unified full-stack scene DONE + play-mode verified (2026-07-23).** `HoloProjectionStereoDemo`
(nested repo `Scenes/`) is the first scene combining ALL THREE: **projection mapping + SBS 3D +
head-tracking**. Head-tracked off-axis stereo content (HoloTrack drives the rig's eye cameras)
renders into the rig's eye textures and flows through the two-projector `PMSDKStereoComposer`
warp/blend path, packed SBS for 3D projectors. Composition rule confirmed (per
`holotrack-architecture.md`): HoloTrack decides *what* each eye sees, projection mapping decides
*where* it lands. Built by grafting `StereoMappingDemo`'s projection+SBS rig onto
`HoloStereoDioramaDemo`'s head-track layer (framed diorama on the content layer, simulated head
source; OAK is a one-flag swap). Key enabler: `PMSDKStereoHeadTrackBinder.DirectScreenSbs` is now a
serialized option — `true` = direct-to-display SBS (the diorama demo, unchanged default), `false` =
route eye textures through the composer (this scene). `DirectScreenSbs=true` and the composer path
are mutually exclusive (true skips creating eye textures → composers idle). Verified: IsStereoReady
+ both composers active, 0.06 IPD; projector captures show split-sliced/warped/blended/SBS-packed
head-tracked content; window-anchored parallax, depth-ordered + crossed stereo disparity. Nested
commit `c09b4a0`.

**H4 live unblocking pass (2026-07-22, current machine):** user-provided OAK Viewer logs confirm
the connected camera: `OAK-D-PRO-W-97`, MXID `14442C10F143D3D200`, platform RVC2, calibrated,
fully compatible. The logs also show OAK Viewer repeatedly polling/running the device and printing
DepthAI `X_LINK_BOOTED ... X_LINK_ERROR` warnings for a booted device name `2.8`; close OAK Viewer
before any Unity/native smoke test so HoloTrack has exclusive device access.

Repo change: removed the hard dependency on a nonexistent vcpkg `depthai` port. With
`HOLOTRACK_WITH_DEPTHAI=ON`, CMake now expects an externally installed Luxonis DepthAI C++ package
discoverable by `find_package(depthai CONFIG REQUIRED)` (pass
`-DCMAKE_PREFIX_PATH=<depthai-install>`). Use `HOLOTRACK_DEPTHAI_USE_VCPKG=ON` only if a machine
has a custom/updated vcpkg registry/overlay providing `depthai`. Added
`holotrack_oak_smoke` target for hardware verification:
`holotrack_oak_smoke --mode person --person-blob <blob> --seconds 10` or
`--mode both --person-blob <blob> --face-blob <blob> --seconds 10`.
Feature-OFF build remains green and `holotrack_harness` is 180/180; the smoke tool correctly
reports "built without DepthAI support" against a feature-OFF DLL. Live H4 remains pending until
DepthAI C++ headers/libs and model `.blob` files are installed.

**Recommended next live-test order (advised 2026-07-22):**
1. Close OAK Viewer first; it is currently the process touching the OAK camera.
2. Install/build Luxonis DepthAI C++ to an install prefix that contains `depthaiConfig.cmake`
   and exports `depthai::core` (for example `C:/SDKs/depthai-install`).
3. Start with **person-only** mode, not `FaceThenPerson`: it needs only one MobileNet-SSD 300x300
   `.blob` and proves the camera, DepthAI DLL, model, P/Invoke, and tracker path with the fewest
   variables. Add the face blob/mode after person mode works.
4. Configure/build:
   `cmake -S . -B build/vs2022-depthai -G "Visual Studio 17 2022" -A x64 -DHOLOTRACK_WITH_DEPTHAI=ON -DCMAKE_PREFIX_PATH=C:/SDKs/depthai-install`
   then build `holotrack holotrack_oak_smoke`.
5. Run native smoke before Unity:
   `build/vs2022-depthai/bin/Release/holotrack_oak_smoke.exe --mode person --person-blob <blob> --seconds 10`.
   It must show frames/detections before any Unity test is meaningful.
6. Close Unity, copy the DepthAI-enabled `HoloTrackSDK.dll` (and any DepthAI runtime DLLs produced
   by the build) into the nested repo's `Plugins/HoloTrack/`, then reopen Unity.
7. In `HoloStereoDioramaDemo`, disable `PMHTSimulatedSource`, add/configure `PMHTOakSource`
   (`Detection Mode = Person`, blob path set), and assign it to `PMHTHeadTracker`.
8. In Play Mode, verify there is no "DLL built without DepthAI" or "OAK start failed"; diagnostics
   should move from `Searching` to `Tracking`, and head motion should keep the window plane anchored
   while near/far content shifts.

Build/verify: `cmake --preset vs2022 && cmake --build build/vs2022 --config Release --target
holotrack holotrack_harness`, then `build/vs2022/bin/Release/holotrack_harness.exe`.

## Current State (2026-07-16)

New machine / new agent: see `docs/dev-setup.md` for environment setup; repo layout and
gotchas are in `AGENTS.md`. Everything needed to continue is in git (this repo + the nested
game repo).

**Last Completed**: Milestones 1-18 + 20; Unity raster-space rig; on-site calibration
P1-P3; robust structured-light decode + webcam path; **pro-feature gap list 8/12** (all
High + all Medium: perspective corner pin, N×M grid UI, auto-blend, blend-gamma fix, color
correction, output rotation/mirror, dense auto-warp) + mark-target UI.
**Runtime-verified** (play mode): perspective corner pin, color correction, output rotation.
**Unit/ math-verified**: grid, auto-blend, dense-warp, blend-gamma, Gray-code decode.
**NOT verified**: mark-target runtime, per-frame warp-readback perf at scale.
**HARDWARE-VERIFIED (2026-07-17)**: the real projector+webcam loop — two UST projectors
blended on one wall, RMS 0.55/0.57 px, editor-driven, calibration persisted. See
"Real-hardware run" section below for the workflow and the four real-world gotchas.
**BUILT, EDITOR-VERIFIED, glasses-test pending (2026-07-17)**: blended stereo (SBS 3D) on
the two-projector wall + a `ContentUI` screen-space canvas that reaches the wall. Both are
game-side on the nested repo's `SBS` branch (unmerged). See "Stereo (SBS 3D)" and
"Content UI on the wall" sections below.
**Current Task**: Milestone 19 (Plugin SDK = UE5 bindings) is the main open track (user:
"UE will do later"). Latest batch (2026-07-17): fixed the long-red triangulation test
(real SDK bug — CV_32F read as double), Unity Test Framework suite 21/21 green, named
presets + A/B (live-verified), OSC remote (loopback-verified 5/5), ObjectMappingDemo
second projector (two-angle coverage). Native suite 133/133; Unity suite 27/27.
**Pro-feature gap list 12/12 implemented** (2026-07-17: #12 pattern set —
focus/convergence/SMPTE-bars/ramp/solids, Y cycles, pixel-tested + bars verified on
output; #11 interop — PMSDKExternalContent verified live, Klak Spout/NDI adapters
compile-gated via versionDefines but NOT tested against the real packages here).
Remaining elsewhere: Klak adapter loopback once a host installs KlakSpout, GPU warp path
for extreme scale, and upstreaming the D-025 hardware findings
(RANSAC consensus fit into `PMSDKAutoAlign`, time-based settle, C-API exposure lock).
**Per-region black-level compensation — IMPLEMENTED 2026-07-21 (D-028).** The fix for the
bright yellow band over the overlap on DARK content (doubled projector-black; a multiply
can't touch it). Additive twin of D-027: sweep's all-black captures
(`PMSDKAutoAlign.Result.Black`) → `PMSDKBlackLevelCompensation.Compute` (pure/testable,
harness-validated) → per-projector `_BlackLiftTex` raising non-overlap black to the overlap
floor, raster UV1, at the black-level stage of `PMSDK/UnlitWarp`. Opt-in
`AutoBlackLevelAfterAlignAll` (default OFF); persisted (`PMSDKGainCodec`); revert by
disabling `PMSDKBlackLevelLift`. Residual floor TINT still manual (per-projector
`PMSDKColorCorrection`) — measured per-channel tint needs an RGB capture. Verify on real
hardware next (code + unit tests only so far).
**Camera-measured luminance compensation — IMPLEMENTED 2026-07-21 (D-027).** Per-projector
wall luminance maps from the sweep's all-white captures (`PMSDKAutoAlign.Result.White`) →
`PMSDKLuminanceCompensation.Compute` (pure/testable) → per-projector `_GainTex` multiplied
in `PMSDK/UnlitWarp` in raster space (UV1), so the blend seam disappears on bright content.
Opt-in: `PMSDKCalibrationManager.AutoLuminanceAfterAlignAll` (default OFF) runs it after
Shift+A; persisted quantized+base64 in the calibration JSON (`PMSDKGainCodec`); revert by
disabling a surface's `PMSDKLuminanceGain`. EditMode tests in PMSDKCalibrationLogicTests.
Verify on real hardware next (was validated only in code + unit tests). Companion fix for
dark content shipped as D-028 (per-region black-level, above). Still open: D-025 upstreaming
and N-projector wall-canvas generalization — natural for the same branch.

## Machine note (2026-07-17) — current layout on this machine

Paths in older sections and AGENTS.md refer to the previous machine. Current layout:

- SDK repo: `C:/Unity/ProjectionMappingC`, branch `main` (PR #5 merged `release-management`).
- Unity project: `C:/Unity/Multi Projector` (template — never commit).
- Nested game repo: `C:/Unity/Multi Projector/Assets/Games/ProjectionMapping-unity`, branch `main`.
- Package reference: `Packages/manifest.json` now points `com.viitorx.pmsdk` at THIS
  repo's source (`file:C:/Unity/ProjectionMappingC/bindings/unity/com.viitorx.pmsdk`) —
  package edits reach Unity directly. (It briefly pointed at the v1.0.3 release archive
  right after the machine move; that gotcha is resolved.)
- vcpkg submodule arrived broken after the copy (missing `.git/modules`, dangling gitdir;
  `git status` failed). Fixed 2026-07-17: re-init in place, shallow fetch of the pinned
  `97b19ca`, then `git submodule absorbgitdirs`.
- **Demo assets**: the canonical `PMSDKDemo/` now lives inside the nested repo with the
  original GUIDs (the demo scenes reference them) and the newer UnlitWarp material
  properties. A stale duplicate at root `Assets/PMSDKDemo` had caused Unity to regenerate
  GUIDs on the nested copy (meta churn); it was deleted. Re-running the demo generator
  recreates root `Assets/PMSDKDemo` with fresh GUIDs — harmless, but never commit that
  copy into the nested repo over the canonical one.
- `ProBuilderMappingDemo` scene: commit `02176ef` had accidentally regenerated the scene
  WITHOUT the ProBuilder stage; restored 2026-07-17 from `3512932` (PB_Stage +
  5 embedded pb_Meshes) — nested commit `1d57868`.

## Real-hardware run (2026-07-17) — two-projector wall blend, editor-driven

First physical validation of the whole calibration stack. Setup: two ultra-short-throw
1080p projectors on the floor aiming at one wall (~15% physical overlap, Windows
extended desktop, both forced to 1920×1080 — at their native 1920×1200 the 16:9 raster
gets edge-cropped), Logitech C270 as observer ~4 m back facing the wall, room dark.
No build needed: `Tools > Projection Mapping > Fullscreen Previews > Open` puts a
borderless Game view on each projector (`GameView.showToolbar=false` internally — do
NOT try y-offset tricks, the window manager clamps popups back onto the screen).

**Result**: one continuous canvas, corner-pin RMS 0.55 px (left) / 0.57 px (right) on
the 128-px raster, blend bands coincident by construction, calibration saved to
`%USERPROFILE%/AppData/LocalLow/ViitorCloud/Projection mapping/pmsdk_calibration.json`.

**Operator workflow (repeatable without an agent)**: play `ProBuilderMappingDemo`,
open the fullscreen previews, place the webcam so it sees BOTH full projections with
margin, lights off, press **F4** (`PMSDKWallCanvasAlign` on "PMSDK Runtime Services",
nested repo). Two Gray-code sweeps (~1–2 min, other projector auto-blanked), then the
canvas mapping + blend widths apply and save automatically. `A`/`Shift+A` (package
auto-align) only RECOVERS each projector's own footprint — it does not build the
shared canvas; use F4 for the wall workflow. F2 = manual touch-up, Ctrl+S saves.

**Why the first attempts failed — the four real-world gotchas (also D-025)**:
1. Floor/ceiling **light spill decodes as valid correspondences** on other planes →
   plain least-squares fit poisoned (reproj 100–2700 px). RANSAC consensus fit fixes
   it (~half the points rejected as spill). Upstream into `PMSDKAutoAlign` eventually.
2. **Settle time**: projector input lag + webcam exposure + USB buffering ≈ hundreds
   of ms; the default `SettleFrames=2` captures stale patterns. Use `SettleFrames≈45`,
   `flushFrames≈5` (frame-based — fps-dependent; time-based settle would be sturdier).
3. **Webcam auto-exposure** partially equalizes the white/black reference frames →
   shadow mask starves (47 valid px with lights on). Lights off is mandatory; a C-API
   exposure lock (`pmsdk_decoder_set_property`) would remove the caveat.
4. During a sweep, **blank the other projector, freeze animated content, hide overlay
   badges** — all three otherwise contaminate the decode (animated content in the
   overlap is the worst).

Also learned: OpenCV device indices ≠ Unity `WebCamTexture` order (Meta Quest virtual
cameras don't consume OpenCV indices — the C270 was index 0); `pmsdk_decoder_create`
runs fine but `open_camera` fails while any other app holds the camera.

## Stereo (SBS 3D) on the blended wall (2026-07-17) — nested repo, `SBS` branch

Blended stereoscopic 3D across the two-projector wall, for glasses + DLP-Link projectors in
3D SBS mode. DLP-Link sync was verified stable across BOTH projectors in the overlap zone
before any code (the go/no-go gate — free-running projectors can ghost in the overlap; this
hardware did not). Architecture + rationale: D-026. Files (nested repo `Scripts/`,
`Shaders/`):

- `PMSDKStereoContentRig` (on the Content Camera): produces two eye textures. `SceneCameras`
  mode renders a parallel L/R pair with asymmetric off-axis frusta, zero-parallax at the
  wall plane (`ZeroParallaxDistance`, default 6 m; nearer = pops out, farther = recedes);
  `SbsTexture` mode uses the two halves of a side-by-side texture (e.g. a VideoPlayer RT) as
  the eyes. `EyeSeparation` (default 0.06) tunes depth strength. **F6** toggles stereo,
  **F7** flips source. When stereo turns on it also re-parks screen-space content canvases
  to the zero-parallax plane (world space) so flat UI renders correctly in both eyes at wall
  depth, and restores them on toggle-off.
- `PMSDKStereoComposer` (on each projector camera): renders the projector's EXISTING warp
  surface once per eye (swaps only `_MainTex` via a MaterialPropertyBlock, with the eye's
  slice scale/offset composed in), then packs both warped images into an SBS frame via
  `PMSDK/StereoPack`. Calibration mode → full mono passthrough; a material takeover
  (test pattern / Gray-code sweep) → the pattern packed identically into both halves, so F4
  works with the projectors left in 3D SBS mode.
- `PMSDK/StereoPack` shader: samples `_LeftTex`/`_RightTex` into the left/right output
  halves; `_Mono=1` packs left into both (calibration/2D passthrough).

**Demo scene**: `Scenes/StereoMappingDemo.unity` — a clone of the calibrated
ProBuilderMappingDemo (reads the SAME calibration JSON, so the wall alignment carries over),
plus a VideoPlayer → `PMSDKDemo/SBS_Video_RT` (assign your SBS clip to the VideoPlayer's
Video Clip; slot intentionally empty in git) and three depth showcase objects at ~3 m
(pops out), ~4.5 m, ~9 m (recedes). Title bar retitled.

**On-wall procedure**: projectors → 3D SBS mode; play the scene; open fullscreen previews;
**F6** (stereo); glasses on; **F7** flips scene-objects ↔ video. Tuning knobs on the rig:
`EyeSeparation` (depth strength), `ZeroParallaxDistance` (what sits at wall depth). If depth
reads INVERTED (some projectors swap eye order), swap `_LeftTex`/`_RightTex` in the composer
— one-line fix. **Editor-verified** (0.00 px disparity at zero-parallax, −18.7 px crossed at
2 m, both eyes warped+blended+packed, clean mono round-trip); **NOT yet verified on the wall
with glasses** — that is the remaining gate before this merges to main / upstreams.

## Content UI on the wall (2026-07-17) — nested repo, `SBS` branch

Screen-Space **Overlay** canvases never reach the projectors (Unity composites Overlay onto
the display backbuffer after all cameras; the wall only shows what lands in `Projection_RT`).
To put audience UI on the wall: a **Screen Space – Camera** canvas rendered by the Content
Camera, on a dedicated `ContentUI` layer added to the Content Camera's culling mask ONLY.
The projector cameras must NOT see that layer or they draw the UI a second time, unwarped —
they cull `UI | TransparentFX` (calibration overlays), so keep audience UI off `UI`.
Implemented in ProBuilderMappingDemo (a spanning title bar) + StereoMappingDemo. Operator UI
(calibration HUD/loupe) deliberately stays Overlay on Display 1 — never projected. Caveat:
the `ContentUI` layer NAME lives in the template project's `ProjectSettings/TagManager.asset`
(not committed); scene/camera store the layer by index (bit 8), so it works on a fresh
machine but shows unnamed until re-added. A straight-edged UI element is also a great
residual-warp detector — the title bar is what exposed a stale grid warp on Left_Screen.

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
  (D-021). Design + key map: docs/calibration-ux-design.md §9.
- **Calibration hardening "item 1" (2026-07-16)**: native robust decode
  (pattern-vs-inverse bits + white/black shadow mask, `DecodeRobust`), capture-buffer
  flushing, frame readback, `pmsdk_decoder_decode_robust` C API; Unity
  `PMSDKNativeWebcamCamera` + manager `UseNativeWebcam`; `PMSDKAutoAlign` projects
  inverse patterns by default. 6 new native tests green; DLL redeployed to the Unity
  plugin folder. Simulated loop verified; **physical projector+webcam smoke test still
  pending** (no hardware). Pre-existing failure `DecoderTests.DecodeAndTriangulate`
  fails at HEAD before these changes — separate investigation.
  Gotcha: never parent ScreenSpaceCamera overlay canvases under a warp surface — its
  16:9 X-scale leaks into canvas geometry (blend zones rendered outside the frustum).

## Next Recommended Task
- **Milestone 19: Plugin SDK**: Create plugin bindings for UE5 or wrap the C API in C# for advanced Unity rendering hooks.
- Calibration auto-align real-hardware path: add `pmsdk_decoder_get_frame` (grayscale
  readback) to the C API, implement an `IPMSDKCalibrationCamera` over the native webcam
  decoder, then run a physical projector+camera smoke test.
- Unity backlog: expose `pmsdk_gridwarp` grids beyond 2×2 in `PMSDKCornerPin` (bezier/grid
  UI), per-projector color correction, and a Game-view-independent preview window.

- **Pro-feature High items (2026-07-16)**: (1) perspective corner pin — native
  `PerspectiveWarp` (Heckbert homography, `DeformationType::Perspective`, C API
  `pmsdk_perspectivewarp_set_corners`); `PMSDKCornerPin` now projective, not 2×2 bilinear
  (D-022); 4 unit tests green. (2) N×M grid warp UI — `PMSDKGridWarp` component +
  calibration grid mode (`G` key, drag/nudge control points, `[]`/`-=` subdivision),
  persisted in the calibration JSON. (3) mark-target-rectangle UI — calibration `M`
  submode: live camera preview on the operator console with 4 draggable corners →
  `AlignSelectedWithTarget`, so the projection can be mapped onto a specific physical
  screen instead of the full throw; persisted in the JSON. (4) auto-blend from camera
  overlap — `PMSDKAutoBlend` derives edge-blend widths from the Gray-code overlap between
  projectors (coverage-histogram scan), applied after `Shift+A` align-all; core verified
  by edit-mode tests. All **compile + unit-tested but not yet runtime-verified**: the new
  DLL couldn't be deployed because the open Unity editor holds the native-plugin lock.
  Pro-feature gap list now 4/12 (all High + auto-blend).
- **Medium batch (2026-07-16)**: (#6) blend-gamma fix — `PMSDKEdgeBlend` sends ramp
  exponent 1/projectorGamma, fixing the reported dark overlap seam (D-023), + uniform
  black-level floor + per-channel output gamma in `PMSDK/UnlitWarp`; (#7)
  `PMSDKColorCorrection` gain/offset/gamma; (#8) `PMSDKOutputTransform` rotate/mirror;
  (#5) `PMSDKDenseWarp.FitGrid` dense N×M grid from correspondence (off by default via
  `DenseAutoWarpN`). Compiled clean (Editor.log 0 CS errors, shader imported); blend-gamma
  math-proven; dense-warp numeric test NOT run (MCP client link dropped after a domain
  reload — Unity healthy, reconnect to run it). Gap list now **8/12**.
- **Runtime verification (2026-07-16, after Unity restart, MCP reconnected)**: deployed
  DLL == fresh build (has perspective exports). Play-mode confirmed on ProBuilderMappingDemo:
  perspective corner pin (corners exact + mid-edge perspective, not bilinear; visually
  foreshortens correctly), per-projector color correction (red-gain tint), output rotation
  (180° flip). Restored `Assets/PMSDKDemo` resolved the missing-material refs. Grid /
  auto-blend / dense-warp remain unit-test-verified; blend-gamma math-proven; mark-target +
  webcam + real-hardware still unverified. Found + fixed: `PerspectiveWarp` left the source
  mesh's z instead of flattening to 0 (harmless under the ortho projector cam, wrong under a
  perspective one) — fixed + retested; **DLL redeploy of the z-fix deferred** (Unity holds
  the plugin lock; current deployed DLL renders the ortho rig correctly).
- **Env note**: the Unity MCP client transport can drop after a forced recompile/domain
  reload and not re-handshake for the session; Unity stays healthy. Editor.log
  (`%LOCALAPPDATA%/Unity/Editor/Editor.log`, grep `error CS`) is the fallback to confirm
  compilation when MCP is down. Deploy step: close Unity → copy
  build/vs2022/bin/Release/ProjectionMappingSDK.dll into the nested repo's
  Plugins/Mapping/ → reopen → hit play. Until then, entering play mode with the OLD DLL
  throws EntryPointNotFound in PMSDKCornerPin (perspective entry points missing).

## Gotchas for the next agent
- Unity MCP `manage_editor(play)` sessions can end up PAUSED (`EditorApplication.isPaused`)
  — if play-mode state looks frozen (Update not running, time stuck), check/clear pause
  before debugging phantom bugs.
- Native plugin DLL is process-locked while the Unity editor is open (loaded via
  DllImport, never unloaded on domain reload). To update ProjectionMappingSDK.dll you must
  close Unity, copy, then reopen — there is no hot-swap. Deploy DLL changes while Unity is
  closed.

## Project Structure Notes
- The DLL is completely self-contained.
- Unity wrapper expects `ProjectionMappingSDK.dll` in its plugins directory.
- Unreal wrapper uses the standard ThirdParty plugin structure to load the DLL.

## Commands
Build C++ core: `cmake --preset vs2022` then `cmake --build build/vs2022 --config Release`
