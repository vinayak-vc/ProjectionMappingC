# Roadmap — ProjectionMappingSDK

One milestone at a time. Each milestone must compile, pass tests, and keep examples building
before the next begins.

| # | Milestone | Status |
|---|-----------|--------|
| 1 | Repository setup, CMake, vcpkg, CI, folder structure | ✅ done (2026-07-10) |
| 2 | Core module (logging, errors, versioning, handles) | ✅ done (2026-07-10) |
| 3 | Math library | ✅ done (2026-07-10) |
| 4 | Geometry library | ✅ done (2026-07-10) |
| 5 | Mesh data structures | ✅ done (2026-07-10) |
| 6 | Warp engine | ✅ done (2026-07-10) |
| 7 | Blend engine | ✅ done (2026-07-10) |
| 8 | Serialization | ✅ done (2026-07-10) |
| 9 | Calibration (OpenCV) | ✅ done (2026-07-10) |
| 10 | Public C API | ✅ done (2026-07-10) |
| 11 | Unit tests (full sweep to 90% coverage) | ✅ done (2026-07-10) |
| 12 | Documentation (Doxygen) | ✅ done (2026-07-10) |
| 13 | Unity wrapper | ✅ done (2026-07-11) |
| 14 | Unreal wrapper | ✅ done (2026-07-13) |
| 15 | Sample applications | ✅ done (2026-07-13) |
| 16 | Advanced OpenCV calibration wrappers | ✅ done (2026-07-13) |
| 17 | GrayCode Decoder & Triangulation | ✅ done (2026-07-13) |
| 18 | Performance optimization | pending |
| 19 | Plugin SDK | pending |
| 20 | Release packaging | pending |

## Dependency plan (vcpkg manifest features)

| Milestone | Dependency added |
|---|---|
| 1 | `gtest` (feature `tests`) |
| 3 | `glm` (internal SIMD-friendly math backend, optional) |
| 8 | `nlohmann-json` |
| 9 | `opencv4` (feature `calibration`) |
| later | `spdlog` — deferred; M2 logging is a zero-dependency callback facade (see D-008) |
