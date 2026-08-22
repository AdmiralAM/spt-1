# GitHub stable/runtime model

## Branch roles

| Branch | Purpose | Intended consumer |
| --- | --- | --- |
| `main` | Active source, asset pipeline, QA tools and CI-generated build evidence | Development |
| `stable` | Exact commit that passed the complete CI gate and produced the published runtime | Audit / rollback |
| `runtime` | Minimal ready-to-copy SPT directory tree | Player installation |

`runtime` is generated from scratch after a successful build. It cannot accumulate obsolete DLLs, old versioned plugin folders, source code, Python tools, previews or build logs.

## Runtime layout

The client is normalized to a version-independent path:

```text
BepInEx/
  plugins/
    SPT Tactical HUD/
      SPT Tactical HUD.dll
      assets/
        hud-sprites.png
runtime-manifest.json
README.md
```

This removes the need to create a new plugin directory for every client patch. The assembly and manifest still retain the real semantic version.

The first migration to this channel requires deleting any legacy `BepInEx/plugins/SPT Tactical HUD v...` directories. Afterwards the Runtime ZIP can overwrite the same unversioned plugin directory directly.

`SPT_Runtime/` is added only when the server component changed in the promoted source commit. Client-only releases explicitly leave the installed server companion untouched.

## Promotion gate

A commit reaches `stable` and `runtime` only after:

1. HUD asset generation and optical checks pass;
2. the hot-path regression guard passes;
3. client and server projects compile successfully;
4. the versioned build package exists;
5. the workflow artifact upload succeeds.

The workflow uses one cancellable concurrency group. A newer `main` commit supersedes an older in-progress build, preventing stale output from being promoted over newer source.
