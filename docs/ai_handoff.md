# AI Agent Handoff Document

## Current State (2026-07-11)

**Last Completed Milestone**: Milestone 13 (Unity wrapper)
**Current Task**: Ready for Milestone 14 (Unreal wrapper) or other backlog tasks.

## What was just done
- Created a complete Unity package at `bindings/unity/com.viitorx.pmsdk`.
- Implemented C# P/Invoke bindings (`NativeBindings.cs`) targeting `ProjectionMappingSDK.dll`.
- Created object-oriented wrappers (`Mesh.cs`, `Projector.cs`, `WarpNode.cs`) in the `vxpmsdk.Core` namespace using `IDisposable`.
- Added extension methods to convert `UnityEngine` math types to native `pmsdk` types.

## Next Recommended Task (Milestone 14 or Backlog)
1. Proceed with **Milestone 14**: Unreal Engine wrapper (creating an Unreal Plugin).
2. Or tackle **Milestone 15**: Sample applications.
3. Or implement the remaining C APIs (`BlendConfig`, `DeformationField`) and update the Unity wrapper accordingly.

## Project Structure Notes
- The Unity wrapper expects `ProjectionMappingSDK.dll` to be present in the Unity project's plugins directory.
- `IDisposable` pattern is strictly followed to prevent memory leaks from unmanaged SDK handles.

## Commands
Build C++ core: `cmake --build --preset debug`
Test C++ core: `ctest --preset debug`
