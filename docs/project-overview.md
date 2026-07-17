# Project Overview — ProjectionMappingSDK

## What

A standalone, engine-agnostic C++20 SDK providing the core computation used by professional
projection mapping software (Resolume Arena, MadMapper, TouchDesigner, HeavyM, Disguise class
of tools): geometry, warping, edge blending, calibration, and project serialization.

## What it is NOT

- Not a Unity project, not an Unreal plugin — those consume the SDK via thin wrappers.
- No rendering: no DirectX/OpenGL/Vulkan, no GUI, no engine includes. Computation only.
  Everything graphical belongs to the host application.

## Documents

- [Architecture (`architecture.md`)](architecture.md) - C++ Core constraints, module layering, build targets.
- [Unity Architecture (`unity-architecture.md`)](unity-architecture.md) - Unity Editor integration rules, layer isolation, SDK constraints, and Editor Utilities.
- [Tasks (`tasks.md`)](tasks.md) - Living checklist. Current focus: Unity on-site calibration + pro-feature parity (Milestone 19 Plugin SDK still open).
- [Roadmap (`roadmap.md`)](roadmap.md) - Milestones 1-20 (all done except M19) plus the post-1.0 Unity/pro-feature track.
- [Calibration UX (`calibration-ux-design.md`)](calibration-ux-design.md) - Runtime on-site calibration design, key map, camera auto-align (P1-P3).
- [Decisions (`decisions.md`)](decisions.md) - Architecture decision records (e.g., C-API over P/Invoke; D-020..D-022 for the Unity rig, homography auto-align, projective corner pin).
- [AI Handoff (`ai_handoff.md`)](ai_handoff.md) - State capture for the next agent session.
- [Dev Setup (`dev-setup.md`)](dev-setup.md) - New machine / new agent: clone, build, deploy, MCP.

## Deliverables

- Windows DLL / Linux `.so` / macOS `.dylib` / static library.
- Stable C API (opaque handles, status codes) for Unity P/Invoke, Unreal, Godot, Qt,
  native C++, C#, Rust, Java, Python (future).

## Core capabilities (target)

| Area | Contents |
|---|---|
| Core | lifecycle, versioning, logging, errors, configuration, plugin registration |
| Math | Vector2/3/4, Quaternion, Matrix4, Transform, BoundingBox, Ray, Plane |
| Geometry | Mesh + builder + subdivision, KD-tree, BVH, ray intersection, Bezier/spline, UV mapping |
| Projection | projection mesh/surface/camera, projection matrix, homography |
| Warp | grid (N×M bilinear), Bezier, **perspective corner pin (homography)**, mesh warp, vertex editing |
| Blend | blend regions, blend curve, gamma, black level, LUT + mask generation |
| Calibration | camera/projector calibration, chessboard, structured light (Gray code) with **robust inverse-pattern decode + shadow mask**, homography solver, triangulation, pose estimation |
| Serialization | JSON + binary project files, versioning, compression-ready |
| Utilities | file IO, thread pool, profiling, timer, memory pool |

## Quality bar

Commercial-grade: unit tests (90% coverage target), Doxygen on all public functions,
thread-safe subsystems, no crashes across the API boundary, ABI stability via the C API.

## Consumers and boundaries

- Unity package = C# P/Invoke wrapper + prebuilt DLL only; no algorithms in C#.
- Unreal plugin = C++ wrapper + Blueprint library only; computation stays in the SDK.
