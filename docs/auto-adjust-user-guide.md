# Auto Adjust - user guide (camera-assisted mapping with the OAK-D)

Goal: you draw the skins; the camera makes the content inside each skin **equal-sized on the real surface** (straight
lines, equal cells, no stretching). Background and decisions: D-049 .. D-055 in `decisions.md`.

## 1. Hardware set-up (once per installation)

1. **Projector**: fixed; do not change zoom, focus or position afterwards.
2. **OAK-D**: about **1 m to the side of the projector** (left or right), aimed so the surfaces you map are well inside
   its image (image edges are the least accurate). Mount it **rigidly**. Not on top of the projector (too close to the
   lens: depth ~10x worse). Its stand must not shadow the projector beam.
3. Close OAK Viewer (only one program can use the camera).

## 2. The normal flow (what you will use)

The inspector section **Auto adjust (camera)** shows only: Calibrate all skins / Calibrate selected skin, Calibrate
projector / Clear selected skin, Show calibration grid. Everything else is under **Show advanced settings** (D-058).

0. **Once per projector (D-058):** under **Show advanced settings > Projector lens**, enter the projector's focal and
   centre y (office projector: **923** and **1103**). Then every projector calibration only finds where the camera is,
   which is reliable from any camera position. Leave both empty only to let the software measure the lens (needs a
   camera ~1 m to the side and a pattern that hits several surfaces; it failed from the 2026-11-18 positions).
1. Unity: open the `SkinEditor` scene, press **Play**, then **Tools > Projection Mapping > Fullscreen Previews >
   Open On Primary** (projector shows only the output; stays on top; close with **Ctrl+Shift+-** or the same menu
   **> Close**).
2. **Draw your skins** as usual - Rectangle or **Polygon** - and drag the corners / points onto the surfaces. The
   camera never moves your points.
3. **Camera moved since last time?** Click **Calibrate projector** (~30 s). The status line shows the baseline (camera
   to projector distance): it should match a tape measurement within ~5 %. Nothing has to be typed in.
4. Click **Calibrate all skins** (or **Calibrate selected skin**).
   - First time (or after the camera/projector moved) it calibrates the projector (~30 s, full-screen stripes - they
     must also hit something besides your surfaces, e.g. the ceiling).
   - Then per skin (~40 s each): stripes through the skin, the camera measures the surface, a warp map spreads the
     content evenly inside the skin, then it measures itself.
   - The status line lists each skin with its error in mm and cell size.
5. Check: **Show calibration grid** (numbered grid on the selected skin) and look at it, or **Measure on surface
   (depth)** for a report in `%USERPROFILE%\AppData\LocalLow\ViitorX\ViitorX Projection Skin\SurfaceReports`.

If you move a skin's points later, click **Calibrate selected skin** again. If the camera was bumped, the next calibrate
notices it ("moved since the projector calibration") and re-calibrates the projector by itself, once.

## 3. Advanced tools (not needed for the normal flow)

| Button | What it does | When |
|---|---|---|
| Calibrate projector | Only the projector/camera calibration | Rarely; the flow above does it |
| Build warp map (+ Physical spacing tick) | One skin's map | Same as Calibrate selected skin, manual |
| Auto adjust / Adjust all / Snap | Older checker-based corner refinement (camera-perspective) | Flat rectangles only (greyed out on Polygon skins - expected) |
| Find surfaces (depth) | **Creates new skins** from depth | Do not use for your drawn skins |
| Camera photo / Map marked surface | **Creates a new skin** from a marked camera photo | Only for "make the skin from a photo" |
| Clear | Removes the selected skin's correction and warp map | Start over on one skin |

## 4. What to expect

- Lines straight on each face; equal cells measured on the surface. From an angle, far cells still *look* smaller
  (perspective) - that is not stretching.
- Accuracy on the office soffit (~2 m): about 3-8 mm on ~200 mm cells.
- Thin side faces next to a skin (e.g. below a fold) are left dark instead of showing broken grid pieces.

## 5. Troubleshooting

| Symptom | Fix |
|---|---|
| Nothing decodes / "Only N lit camera pixels" | Projection covered by another window: reopen the fullscreen preview; check exposure |
| "single flat surface" during projector calibration | Let the full-screen pattern also hit the ceiling or a wall |
| "moved since the projector calibration" twice | The camera is still moving: fix the mount, then calibrate again |
| A skin you did not draw appeared ("Surface 1", "Surface probe") | Created by Find surfaces / an older build's leftover probe: delete it in the skin list (the probe is no longer saved) |

## 6. Where things are stored

`%USERPROFILE%\AppData\LocalLow\ViitorX\ViitorX Projection Skin\`: `SkinSets` (skins), `SkinMasks` (warp maps,
masks, camera regions), `ProjectorCalibration`, `SurfaceReports`, `CameraMarks` (photo flow only).

## 7. Hardware checklist for the next test

1. OAK back at ~1 m beside the projector (the previous position B was better than the current one), mount tight.
2. Use a fresh skin set; delete "Surface 1" / "Surface probe" from the POP set if you keep using it.
3. Draw 2-3 skins (one Rectangle on a flat face, one Polygon over the folded band).
4. **Calibrate all skins**; note the status lines.
5. **Show calibration grid** per skin; take a phone photo from the room (lines straight, cells even).
6. Tape-measure two 4-cell spans per skin (one horizontal, one vertical) at both ends.
7. Bump test (optional): move the camera 5 cm, **Calibrate selected skin** - it must re-calibrate by itself.
