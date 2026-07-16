# Dev Setup — new machine / new agent

Everything needed to continue the project is in git. This file lists the one-time
environment setup so a fresh machine (and a fresh Claude Code / AI agent) can pick up.

## 1. Clone both repos

```
git clone <SDK repo>            # this repo -> C:/UnrealProject/ProjectionMapping (branch release-management)
git clone <Unity game repo>     # -> the nested game repo (remote: ProjectionMapping-unity)
```

The **Unity base project** (`ProjectionMapping-base-project`) is a template — obtain it
separately; do not expect our work to live there (see AGENTS.md). The nested game repo
sits at `ProjectionMapping-base-project/Assets/Games/ProjectionMapping-unity`.

## 2. Get the agent up to speed (no re-explaining needed)

In Claude Code, point at the SDK repo and:
> Read AGENTS.md, then docs/ai_handoff.md, docs/roadmap.md, docs/tasks.md, docs/decisions.md. Summarize state.

That reconstructs full context (design decisions D-001..D-023, feature status, gotchas).

## 3. Build the native SDK (C++)

Requires CMake + a C++20 toolchain + the vendored vcpkg (OpenCV builds via vcpkg; first
build is slow, subsequent are incremental).

```
cmake --preset vs2022
cmake --build build/vs2022 --config Release --target pmsdk pmsdk_tests
build/vs2022/bin/Release/pmsdk_tests.exe        # run the gtest suite
```

Output DLL: `build/vs2022/bin/Release/ProjectionMappingSDK.dll`.

## 4. Deploy the DLL into Unity

Copy the built DLL to the nested game repo's plugin folder (Unity must be CLOSED — it locks
the plugin for its session):

```
Assets/Games/ProjectionMapping-unity/Plugins/Mapping/ProjectionMappingSDK.dll
```

## 5. Unity

- Unity 6.3 LTS (6000.3.x). Open `ProjectionMapping-base-project`.
- The `com.viitorx.pmsdk` package is referenced by `file:` path to this repo's
  `bindings/unity/com.viitorx.pmsdk` — adjust the path in `Packages/manifest.json` if the
  clone location differs.
- Demo scenes: `Assets/Games/ProjectionMapping-unity/Scenes/WarpAndBlendExample.unity`,
  `ProBuilderMappingDemo.unity`. If materials show "Missing", run
  `Tools > Projection Mapping > Generate Demo Scene` to regenerate `Assets/PMSDKDemo`.

## 6. Claude Code MCP (optional, for driving Unity)

To let the agent drive the Unity editor, install the Unity MCP server (CoplayDev
`com.coplaydev.unity-mcp`, already in the base project's `Packages/manifest.json`) and
configure Claude Code's MCP client to reach it (stdio bridge, default port 6401 here).
See AGENTS.md for MCP gotchas (plugin lock, link drops, play-mode pause).

## Verification status (as of 2026-07-16)

- C++ core + calibration: gtest green.
- Unity runtime-verified: perspective corner pin, color correction, output rotation.
- Unit-verified (math): auto-blend, dense-warp, homography, Gray-code decode, blend gamma.
- NOT yet verified: real projector+camera loop (no hardware), mark-target/webcam runtime,
  per-frame warp-readback performance at scale.
