# AGENTS.md

## Documentation

Maintain:

- docs/project-overview.md
- docs/architecture.md
- docs/roadmap.md
- docs/tasks.md
- docs/decisions.md
- docs/ai_handoff.md

## Before Coding

- Read architecture.md
- Read roadmap.md
- Read ai_handoff.md
- Summarize understanding before implementation

## During Coding

- Follow existing patterns
- Update tasks.md
- Update decisions.md for major decisions

## After Coding

- Update ai_handoff.md
- Record modified files
- Record next recommended task

## Architecture

- Prefer consistency over novelty
- Document architectural changes before introducing them

## Repositories & commit policy

Three git repos are in play:

1. **SDK repo** — this repo (`C:/UnrealProject/ProjectionMapping`), branch `release-management`.
   Holds all C++ core + the Unity package source (`bindings/unity/com.viitorx.pmsdk`) + docs.
   Commit here normally; this is where ~all engineering lives.
2. **Unity base project** — `C:/Unity/ProjectionMapping-base-project`. A reusable **template**;
   **never commit to it**. It references the SDK package via a `file:` path in
   `Packages/manifest.json`. Its branch may change (Unity-version-named branches) — expected.
3. **Nested game repo** — `.../ProjectionMapping-base-project/Assets/Games/ProjectionMapping-unity`
   (its own `.git`, remote `ProjectionMapping-unity`). Holds scenes, scripts, and the deployed
   `Plugins/Mapping/ProjectionMappingSDK.dll`. **Always commit and push this one** after
   Unity-side changes.

Generated demo assets (`Assets/PMSDKDemo/`) are regenerable via
`Tools > Projection Mapping > Generate Demo Scene`; if they go missing, regenerate.

## Environment gotchas (Unity MCP)

- **Native plugin lock**: Unity locks `ProjectionMappingSDK.dll` for its whole session. To
  redeploy a rebuilt DLL you must close Unity, copy, reopen. Restarting Unity also reconnects
  a dropped MCP link.
- **MCP link drops** after a forced recompile / domain reload and may not re-handshake for the
  session (Unity itself stays healthy). Fallback to confirm compilation:
  `%LOCALAPPDATA%/Unity/Editor/Editor.log`, grep `error CS`.
- **Play-mode pause**: `manage_editor(play)` sessions can end up paused; if live state looks
  frozen, clear `EditorApplication.isPaused` before debugging.

## Handoff

Assume another AI agent may continue the project tomorrow, possibly on a different machine.
Optimize for agent portability: everything needed to continue is in git (this repo + the
nested game repo). A new agent should read this file, then `docs/ai_handoff.md`,
`docs/roadmap.md`, `docs/tasks.md`, `docs/decisions.md`. Machine setup steps are in
`docs/dev-setup.md`.