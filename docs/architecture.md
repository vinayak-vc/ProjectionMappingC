# Architecture — ProjectionMappingSDK

## Layering (dependencies point downward only)

```
            +---------------------------+
            |          CAPI             |  stable C ABI, opaque handles
            +---------------------------+
            |       Serialization       |  project files (JSON/binary)
            +---------------------------+
            | Projection | Warp | Blend | Calibration
            +---------------------------+
            |         Geometry          |  mesh, spatial structures, curves
            +---------------------------+
            |           Math            |  vectors, matrices, transforms
            +---------------------------+
            |           Core            |  errors, logging, version, config
            +---------------------------+
   Utilities (cross-cutting; depends only on Core)
```

Rules:

- Core never depends on higher modules. No circular dependencies, ever.
- Feature modules (Projection/Warp/Blend/Calibration) may use Geometry, Math, Core,
  Utilities — never each other (coordination happens above, in Serialization/CAPI layer
  or the host app).
- OpenCV is confined to Calibration. GLM/spdlog/nlohmann-json usage stays internal —
  never in public headers (headers expose only std types and PMSDK types).

## ABI / API strategy

- The **C API (`include/PMSDK/CAPI/`) is the only stable ABI boundary**: opaque handles,
  status-code returns, `PM_GetLastError()` for details. No C++ types, no exceptions cross it.
- C++ headers are a convenience for native consumers; ABI stability is not promised for them.
- Version handshake: `pmsdk::GetVersion()` (runtime) vs `pmsdk::kHeaderVersion` (compile time);
  the C API will expose `PM_GetVersion()`.
- Export macro: `PMSDK_API` in `include/PMSDK/Core/Export.h` (the only permitted macro family
  besides build-injected version constants).

## Error handling

- Internal code may use exceptions, but they never cross the DLL boundary.
- Every public API call returns a status; detailed messages retrievable per-thread.
- Never crash: all inputs validated at the API boundary.

## Design rules (from spec)

PImpl where appropriate; no globals/singletons/static mutable state; prefer `constexpr`,
`std::span`, smart pointers, RAII; thread-safe subsystems; low-allocation, cache-friendly,
SIMD-ready data layouts.

## Build architecture

- Single library target `pmsdk` (output name `ProjectionMappingSDK`), shared by default,
  static via `BUILD_SHARED_LIBS=OFF` (consumers then get `PMSDK_STATIC` automatically).
- Hidden symbol visibility on non-Windows; only `PMSDK_API` symbols exported.
- vcpkg manifest with per-milestone features (`tests` → gtest; later `calibration` → OpenCV)
  so consumers don't pay for dependencies they don't use.
- Presets: Ninja debug/release (default, Clang, GCC variants) + `vs2022` generator preset.

## Directory map

```
include/PMSDK/<Module>/   public headers, namespace pmsdk
src/<Module>/             implementation (+ internal headers, not installed)
tests/<Module>/           GoogleTest suites mirroring module layout
examples/                 NativeExample now; Unity/Unreal wrappers at M13/M14
cmake/                    PMSDKCompilerOptions.cmake, package config template
```
