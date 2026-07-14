# Unreal Engine Projection Mapping SDK - Quick Start Guide

Welcome to the Projection Mapping SDK for Unreal Engine 5! This plugin leverages our high-performance, multithreaded native C++ SDK to provide blazing fast projection mapping right inside Unreal.

## Installation
1. Copy this entire `ProjectionMapping` folder into your Unreal project's `Plugins/` directory (create the directory if it doesn't exist).
2. The folder structure should look like: `YourProject/Plugins/ProjectionMapping/ProjectionMapping.uplugin`
3. Launch your Unreal Project.
4. When prompted, click `Yes` to rebuild the plugin modules if necessary.
5. Ensure the plugin is enabled under `Edit > Plugins > Projection Mapping`.

## Getting Started

### Using Blueprints
The SDK is fully exposed to Blueprints! 
1. Right click in any Blueprint Event Graph and search for `Projection Mapping`.
2. Use the `Create Warp Node` node to initialize a new projector.
3. Use the `Apply Deformation Field` node to warp a Static Mesh or Procedural Mesh dynamically.

### Using C++
To use the SDK from your own C++ game modules:
1. Open your project's `Build.cs` file (e.g. `YourProject.Build.cs`).
2. Add `"ProjectionMappingSDK"` to your `PublicDependencyModuleNames`.
3. In your code, you can include the C-API headers:

```cpp
#include "PMSDK/C_API/WarpAPI.h"
#include "PMSDK/C_API/GeometryAPI.h"

void AMyActor::StartMapping() {
    // Call the native C-API directly!
    pmsdk_warp_node_h warpNode = pmsdk_warp_node_create("MyProjector");
    
    // ... setup and projection logic ...
}
```

## Troubleshooting
If you encounter linker errors regarding `ProjectionMappingSDK.dll`, ensure that you copied the `bin/` folder correctly when you packaged the release. The Unreal plugin dynamically loads the native DLL from `Source/ThirdParty/ProjectionMappingSDK/bin/Release/ProjectionMappingSDK.dll`.
