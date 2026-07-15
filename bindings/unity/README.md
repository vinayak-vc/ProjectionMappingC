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

## Multi-Projector Setup Guide (Step-by-Step)
Follow these steps to set up a seamless, multi-projector overlapping display natively in Unity.

### Step 1: Physical Hardware & Windows Setup
1. Turn on your PC and projectors, and connect them to your GPU.
2. Open Windows **Display Settings**.
3. Under "Multiple displays", select **Extend these displays** (do *not* duplicate or use Nvidia Surround).
4. Click "Identify" to note your display numbers (e.g., Display 1 = Monitor, Display 2 = Left Projector, Display 3 = Right Projector).

### Step 2: Unity Scene Setup
1. In your Unity scene, create two 3D Planes (`GameObject > 3D Object > Plane`) side-by-side representing your screens.
2. Select the Left Plane. From the top menu, click **Tools > Projection Mapping > Setup Wizard**, then click **Setup Projector & Mesh Warp**.
3. Select the Right Plane and do the same. This generates Projector Cameras pointing at your planes and attaches the `PMSDKMeshWarp` script.

### Step 3: Routing Cameras to Projectors
1. Select the Projector Camera for the Left screen. In the Inspector, change the **Target Display** dropdown to **Display 2**.
2. Select the Projector Camera for the Right screen and change its **Target Display** to **Display 3**.
3. Press **Play** in Unity. Unity will automatically output to both physical projectors via your GPU!

### Step 4: Corner Pinning (Physical Alignment)
1. Select both Plane objects and click `Add Component`. Add the **PMSDKTestPattern** script. This renders a high-visibility calibration checkerboard with borders, crosshairs, and aspect ratio circles.
2. Click `Add Component` again and add **PMSDKCornerPin**.
3. Look at your physical wall. In the Unity Inspector, adjust the X and Y values for the Top-Left, Top-Right, Bottom-Left, and Bottom-Right corners of both projectors. 
4. Drag these virtual corners until the red borders form a perfect, squared-off rectangle on the wall and the center crosshairs align.

### Step 5: Edge Blending
1. Select both Plane objects and add the **PMSDKEdgeBlend** component.
2. For the Left projector, increase the **Right Edge** slider (e.g., 0.1).
3. For the Right projector, increase the **Left Edge** slider (e.g., 0.1).
4. The C++ SDK will instantly blend the overlapping pixels seamlessly. You can adjust the **Gamma** slider to match your projector's luminance curve.
5. Disable the **PMSDKTestPattern** component to restore your original video/game content!

---

## Advanced: GrayCode Calibration
This SDK supports automatic GrayCode generation and decoding to automatically triangulate your physical projection surface using a camera!
1. Open the `Tools > Projection Mapping > GrayCode Calibration` window.
2. Select your Camera Index and click `Open Camera` (the SDK connects directly via C++ `cv::VideoCapture` to ensure zero lag in Unity).
3. Capture frames and decode. The projector will flash GrayCode patterns, and the C++ engine will automatically generate a pixel-perfect 3D mesh of your real-world surface!
