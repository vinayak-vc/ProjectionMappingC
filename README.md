# ProjectionMappingSDK

Engine-agnostic projection mapping SDK in modern C++20. Provides the core computation used by
professional projection mapping tools (warping, edge blending, calibration, mesh/geometry math)
as a standalone library with a stable C API — no rendering, no engine code.

Consumers: Unity (P/Invoke), Unreal, Godot, Qt, native C++, C#, Rust, Python (planned).

## Status

Milestone 1 complete: repository skeleton, CMake + vcpkg + presets, CI, versioned core library.
See [docs/roadmap.md](docs/roadmap.md) and [docs/architecture.md](docs/architecture.md).

## Requirements

- CMake ≥ 3.25
- A C++20 compiler: MSVC 19.3x+ (VS 2022+), Clang 15+, GCC 12+
- Ninja (or Visual Studio generator)
- vcpkg is vendored as a git submodule — no global install needed

## Building

```bash
git clone --recurse-submodules <repo-url>
cd ProjectionMapping

# one-time vcpkg bootstrap
./third_party/vcpkg/bootstrap-vcpkg.sh      # Windows: .\third_party\vcpkg\bootstrap-vcpkg.bat

cmake --preset release        # or: debug, clang-release, gcc-release, vs2022
cmake --build --preset release
ctest --preset release
```

On Windows with the Ninja presets, run from a *Developer Command Prompt / PowerShell* so `cl` is
on PATH.

### CMake options

| Option | Default | Meaning |
|---|---|---|
| `BUILD_SHARED_LIBS` | `ON` | Build as DLL/shared library |
| `PMSDK_BUILD_TESTS` | `ON` | Build GoogleTest suite (pulls `gtest` via vcpkg feature) |
| `PMSDK_BUILD_EXAMPLES` | `ON` | Build example apps |
| `PMSDK_WARNINGS_AS_ERRORS` | `OFF` | `/WX` / `-Werror` (CI turns this on) |

## Layout

```
include/PMSDK/   public headers (Core, Math, Geometry, Projection, Warp, Blend,
                 Calibration, Serialization, Utilities, CAPI)
src/             implementation
tests/           GoogleTest suites
examples/        consumer examples (native C++ now; Unity/Unreal wrappers later)
docs/            project docs (overview, architecture, roadmap, tasks, decisions, handoff)
cmake/           CMake modules
third_party/     vcpkg submodule
```

## Documentation

- [docs/project-overview.md](docs/project-overview.md)
- [docs/architecture.md](docs/architecture.md)
- [docs/roadmap.md](docs/roadmap.md)
- [docs/decisions.md](docs/decisions.md)
