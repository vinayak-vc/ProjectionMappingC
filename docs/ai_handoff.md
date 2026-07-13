# AI Agent Handoff Document

## Current State (2026-07-13)

**Last Completed Milestone**: Milestone 17 (GrayCode Decoder & Triangulation)
**Current Task**: Ready for Milestone 18 (Performance optimization).

## What was just done
- **Milestone 16 Completed**: Added Advanced OpenCV Calibration Wrappers. Enabled the SDK to perform physical camera intrinsic/extrinsic calibration and calculate reprojection errors directly inside C++.
- **Milestone 17 Completed**: Built the `GrayCodeDecoder` core which automatically un-gray-codes captured images to find exact projector pixel mappings and then triangulates them using OpenCV. Added ability for the SDK to perform direct camera captures (`cv::VideoCapture`) to bypass Unity's main thread entirely.
- Both features were exposed via the C-API and Unity bindings (`NativeBindings.cs`, `Calibration.cs`).

## Next Recommended Task (Milestone 18)
1. Proceed with **Milestone 18**: Performance optimization (SIMD, multithreading, or GPU compute for the Warp mesh deformations).
2. Or tackle **Milestone 19**: Plugin SDK.

## Project Structure Notes
- The DLL is completely self-contained.
- Unity wrapper expects `ProjectionMappingSDK.dll` in its plugins directory.
- Unreal wrapper uses the standard ThirdParty plugin structure to load the DLL.

## Commands
Build C++ core: `cmake --preset vs2022` then `cmake --build build/vs2022 --config Release`
