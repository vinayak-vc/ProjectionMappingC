# AI Agent Handoff Document

## Current State (2026-07-10)

**Last Completed Milestone**: Milestone 12 (Documentation/Doxygen)
**Current Task**: Ready for Milestone 13 (Unity wrapper) or other backlog tasks.

## What was just done
- Created a `Doxyfile` and `docs/api.md` overview.
- Added comprehensive standard Doxygen blocks (`@brief`, `@param`, `@return`) to the core C++ headers (`Mesh`, `WarpNode`, `Projector`, `DeformationField`, `BlendConfig`, `EdgeBlend`, `Serialization`).
- Added Doxygen blocks to the public C-API headers (`Types.h`, `GeometryAPI.h`, `WarpAPI.h`).
- Created a GitHub Actions workflow (`.github/workflows/doxygen.yml`) to automatically generate and deploy HTML documentation to GitHub Pages.

## Next Recommended Task (Milestone 13 or Backlog)
1. Proceed with **Milestone 13**: Creating a Unity package wrapper using `DllImport` to consume the C-API.
2. Alternatively, address backlog items like version header generation via CMake (`configure_file`).

## Project Structure Notes
- The C-API uses `_create()` and `_destroy()` semantics. Clients must manage handle lifetimes.
- Internal details (e.g., OpenCV types in Calibration) are tightly scoped to implementation files and not exported in headers.
- Doxygen handles all `pmsdk::` namespaces and `pmsdk_*` C-API functions.

## Commands
Build: `cmake --build --preset debug`
Test: `ctest --preset debug`
