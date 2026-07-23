# ViitorX HoloTrack SDK (Unity package)

Head-tracked holographic projection for Unity, driven by the standalone `HoloTrackSDK.dll`
(namespace `holotrack`). **Independent of** `com.viitorx.pmsdk` — install either or both. This
package is the thin C# binding + MonoBehaviours; all computation lives in the native DLL.

## Install the native plugin

`HoloTrackSDK.dll` is **not** bundled in this package (mirrors the PMSDK convention where the
DLL is deployed into the consumer project). Copy the built DLL into your Unity project's
plugins folder, e.g. `Assets/Plugins/HoloTrack/HoloTrackSDK.dll`, and mark it as a native
plugin for the target platform in the importer. Build it with:

```
cmake --preset vs2022
cmake --build build/vs2022 --config Release --target holotrack
# → build/vs2022/bin/Release/HoloTrackSDK.dll
```

## Scene wiring

1. **Head Tracker** — add `PMHTHeadTracker` to a GameObject. Assign a `HeadTrackingConfig`
   asset (Create ▸ ViitorX ▸ HoloTrack ▸ Head Tracking Config) and a source component.
2. **Source** — for development without hardware, add `PMHTSimulatedSource` and assign a
   `headProxy` transform; drag it around to simulate the viewer. (The OAK device source lands
   in a later milestone.)
3. **Display Surface** — add `HeadTrackingDisplaySurface` to an object placed/sized to match
   the real projection surface (metres).
4. **Camera** — add `HeadTrackedCameraController` to your content Camera; assign the tracker and
   the surface. It sets the off-axis view/projection each frame. Feed this camera's output into
   your projection-mapping pipeline (e.g. the PMSDK content camera) as usual.
5. Optional: `HeadTrackingDiagnostics` (overlay + gizmos) and `HeadTrackingRecorder` (CSV).

## Composition with projection mapping

HoloTrack only positions the content camera correctly for the viewer. Warping/blending onto
projectors is the projection-mapping package's job downstream — the two SDKs never link; they
meet at the content camera in your scene (D-029).

## Notes

- The off-axis mapping to Unity's `worldToCameraMatrix` / `projectionMatrix` follows the OpenGL
  convention the DLL emits. If depth/parallax reads inverted on real hardware, verify the view
  handedness (a known place to check, analogous to the stereo eye-swap note in PMSDK).
- `ht_tracker_compute_offaxis` currently marshals two `float[16]` per call; if per-frame GC
  matters at scale, switch `HtOffAxis` to a blittable unsafe layout.
