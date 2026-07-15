# Unity Integration Architecture

This document details the architectural decisions, constraints, and best practices for using the Projection Mapping SDK natively inside Unity.

## 1. Native SDK vs. Third Party Applications (TPAs)
Traditionally, projection mapping workflows involve:
1. Running Unity on a primary display.
2. Using Nvidia Surround or AMD Eyefinity to stitch multiple projectors into one giant "virtual display".
3. Capturing the Unity output (via Spout/Syphon) and sending it to a TPA (like Resolume or MadMapper).
4. Warping the 2D video feed in the TPA.

**Why the Native SDK is Superior:**
- **Zero Latency:** By hooking into Unity's Mesh rendering pipeline, warping is executed in real-time on the GPU via native C++ pointers, eliminating the frame delay caused by Spout/Syphon captures. This is critical for highly interactive installations (Kinect, Lidar tracking).
- **True 3D Mapping:** TPAs just warp a flat 2D video. The SDK maps projection onto true 3D objects in the Unity scene (Planes, Cylinders, Pyramids, custom architecture).
- **No Screen Tunneling Hacks:** By utilizing Unity's native `Camera.targetDisplay` routing, Windows simply sees standard displays. This prevents Nvidia Surround crashes when HDMI cables are unplugged.

## 2. The "Content Camera" Architecture
To properly map a 3D game onto projectors without visual glitches, the scene MUST be structured using the Content Camera architecture.

1. **Content Camera:** A camera that renders the actual 3D game world. It does *not* output to a display. Instead, it outputs to a large `RenderTexture`.
2. **Screen Meshes (Planes):** 3D objects (usually primitive Planes) placed in the scene. Their material is driven by the `RenderTexture`.
3. **Projector Cameras:** Cameras that are placed physically in front of the Screen Meshes. They look strictly at the meshes and output directly to the physical projector ports (Display 2, Display 3).

### Layer Isolation (Fixing "Layer Bleed")
If all objects share the `Default` layer, the Projector Cameras will see both the warped Screen Meshes *and* the raw 3D game geometry floating in the background, causing a "ghosting" or double-vision effect.
**The Fix:**
- The 3D game content MUST be placed on an isolated layer (e.g., `Water`).
- The Content Camera MUST be set to only render the Content layer.
- The Screen Meshes MUST be placed on a separate isolated layer (e.g., `TransparentFX`).
- The Projector Cameras MUST be set to only render the Screen Meshes layer and the UI layer.

### Camera Clear Flags
Projector Cameras must have `CameraClearFlags.SolidColor` set to pure black (`Color.black`). If set to `Skybox`, the projectors will blast the Unity skybox into the physical room, ruining the immersion and edge blending.

### Mesh Orientations (Backface Culling)
Unity's primitive Plane has backface culling enabled by default. If a Plane is rotated `-90` on the X-axis, it faces `+Z`. A Projector Camera placed at `-Z` looking forward will be staring at the *invisible back* of the Plane, resulting in a pure black output. 
**The Fix:** Always rotate Planes `+90` on the X-axis so their normal faces `-Z` toward the cameras.

## 3. SDK Component Hard Constraints (Foolproofing)
To prevent runtime crashes and silent failures, the SDK enforces strict component dependencies (`[RequireComponent]`) and validation:

- **`PMSDKMeshWarp`**: 
  - Requires `MeshFilter` and `MeshRenderer`. 
  - Validates that a Mesh is actually assigned to the MeshFilter before attempting to clone it or pass it to the C++ engine. Prints a warning if missing.
  - Safely idles and prints a warning if no `PMSDKProjector` is assigned.
- **`PMSDKCornerPin`**: 
  - Requires `PMSDKMeshWarp`. (It previously incorrectly required a Projector, which caused silent failures because the Projector lives on the Camera).
- **`PMSDKEdgeBlend`**: 
  - Requires `PMSDKMeshWarp`. Modifies vertices based on UVs.
- **`PMSDKProjector`**: 
  - Requires `Camera`. 
- **`PMSDKCornerPinUI`**: 
  - Intelligently waits in the background during `Update()` until a valid Projector Camera is linked to the MeshWarp, at which point it dynamically spawns the interactive UI Canvas on the Projector output.

## 4. The Auto-Configurator Utility
To simplify the complex architecture described above, the SDK includes a custom Editor Utility (`PMSDKMeshWarpEditor.cs`).
When a user adds `PMSDKMeshWarp` to any GameObject, the Unity Inspector presents a **"Auto-Configure Full SDK Dependencies"** button.

Clicking this button completely automates the architectural setup:
1. Validates if the object has a Mesh. If empty, it creates and assigns a standard Unity primitive Plane.
2. Creates a `PMSDKProjector` Camera, links it, and sets its culling masks and clear flags properly.
3. Automatically attaches all required components (`PMSDKTestPattern`, `PMSDKCornerPin`, `PMSDKEdgeBlend`, `PMSDKCornerPinUI`).
