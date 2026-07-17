# Unity Projection Mapping SDK - Quick Start Guide

Welcome to the Projection Mapping SDK for Unity! This package provides high-performance, multithreaded native C++ projection mapping capabilities directly inside Unity without locking the main thread.

## Installation
1. Open the Unity Package Manager (`Window > Package Manager`).
2. Click the `+` button in the top left and select `Add package from disk...`
3. Navigate to this folder (`com.viitorx.pmsdk`) and select the `package.json` file.
4. Unity will automatically import the SDK and its native binaries.

## Getting Started

### 1. The Interactive Setup Wizard
The easiest way to get started is to use the Guided Setup Wizard:
1. In the top menu bar, click `Tools > Projection Mapping > Setup Wizard`.
2. Follow the on-screen steps to configure your Projector and physical Camera.
3. The wizard will automatically generate the `DeformationField`, `WarpNode`, and `Mesh` for your scene.

### 2. Manual C# Scripting
If you want to manually interact with the SDK, you can use the C# wrappers:

```csharp
using PMSDK;

public class ProjectionMapper : MonoBehaviour {
    void Start() {
        // 1. Create a Warp Node
        IntPtr warpNode = NativeBindings.CreateWarpNode("MyProjector");

        // 2. Load a 3D Mesh
        IntPtr mesh = NativeBindings.LoadMesh("path/to/mesh.obj");

        // 3. Process the mapping (runs entirely in native C++)
        NativeBindings.ProcessMesh(warpNode, mesh);
    }
}
```

## Advanced: GrayCode Calibration
This SDK supports automatic GrayCode generation and decoding to automatically triangulate your physical projection surface using a camera!
1. Open the `Calibration` tools in the Unity menu.
2. Select your Camera (the SDK connects directly to it via C++ `cv::VideoCapture` to ensure zero lag in Unity).
3. Click `Start Calibration`. The projector will flash GrayCode patterns, and the system will generate a pixel-perfect 3D mesh of your real-world surface!
