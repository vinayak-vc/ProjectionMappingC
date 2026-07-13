# AI Agent Handoff Document

## Current State (2026-07-13)

**Last Completed Milestone**: Milestone 15 (Sample applications)
**Current Task**: Ready for Milestone 14 (Unreal wrapper) or other backlog tasks.

## What was just done
- **Milestone 15 Completed**: Created a fully functional interactive Projection Mapping scene in Unity (`WarpAndBlendExample.unity`).
- Extended the C-API to expose `BlendConfig` and `EdgeBlend` configurations.
- Extended the C-API to allow reading warped geometry back via `pmsdk_mesh_get_vertices` and `pmsdk_mesh_get_indices`.
- Updated the Unity Wrapper (`com.viitorx.pmsdk`) to support the new `BlendConfig` and Mesh getter functionality.
- Implemented `GuidedProjectionDemo.cs` featuring an interactive Wizard UI that statically lives in the Unity Scene, allowing users to step through and visualize Warping and Edge Blending dynamically.

## Next Recommended Task (Milestone 14 or Backlog)
1. Proceed with **Milestone 14**: Unreal Engine wrapper (creating an Unreal Plugin).
2. Or tackle **Milestone 16**: Performance optimization.
3. Or implement the remaining C APIs (`DeformationField`) and update the wrappers accordingly.

## Project Structure Notes
- The Unity wrapper expects `ProjectionMappingSDK.dll` to be present in the Unity project's plugins directory.
- `IDisposable` pattern is strictly followed to prevent memory leaks from unmanaged SDK handles.
- The Sample Scene UI is statically built inside the Scene file (`.unity`), avoiding runtime Canvas creation for easier user editing.

## Commands
Build C++ core: `cmake --build --preset debug`
Test C++ core: `ctest --preset debug`
