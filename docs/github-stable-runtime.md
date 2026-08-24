# Source, stable, and runtime model

## Purpose

This is a multi-mod source repository. `main` is the authoritative development tree. Generated packages, transient validation logs, and CI run metadata are not source and must not be persisted in `main`.

## Branch roles

| Branch | Role |
| --- | --- |
| `main` | Active source, tests, maintained assets, workflows, and durable documentation |
| `stable` | Source commit promoted after the suite validation/publish workflow succeeds |
| `runtime` | Install-only Tactical HUD channel |
| `runtime-item-intelligence` | Install-only Item Intelligence channel |
| `runtime-pause` | Install-only Pause channel |
| `runtime-belt-armband` | Install-only Belt/Armband Inventory channel |
| `archive/v1.13.0` | Intentional frozen Tactical HUD `1.13.0` reserve |

Feature, fix, diagnostic, build, and archaeology branches are temporary unless explicitly documented otherwise. They are not release channels and should be removed after their useful work is merged or superseded.

## Current modules

Long-term source modules under `mods/`:

- `SPT-Tactical-HUD`
- `SPT-Item-Intelligence`
- `SPT-Pause`
- `SPT-Belt-Armband-Inventory`
- `SPT-Quest-Planner`

The root `README.md` is the human-readable module index. Each module README owns its current scope, architecture, installation channel, and validation status. Detailed phase/revision documents are supporting history, not a second source of truth for current status.

## What belongs in `main`

Keep material required for development, review, maintenance, or reproducible publication:

- source code and project/build definitions;
- tests and deterministic validation tools;
- maintained runtime/source assets;
- GitHub workflow definitions;
- durable architecture, compatibility, and maintenance documentation.

Do not persist:

- `bin/`, `obj/`, IDE state, dependency caches, or local environment files;
- transient build/test logs and generated status directories;
- CI run IDs or one-off trigger/evidence marker files;
- temporary diagnostic dumps;
- install ZIPs or duplicate compiled packages already represented by CI artifacts/runtime channels.

The root `.gitignore` is the baseline guardrail. Workflows must not force-add ignored/generated material back into source history.

## Build and publication

CI may create `build-output/`, `build-status/`, dependency caches, previews, and other temporary files inside the runner workspace. Those paths are disposable CI state.

Validated packages belong in GitHub Actions artifacts and, for maintained install channels, the corresponding runtime branch. The suite publication workflow advances `stable` to the validated source commit and rebuilds runtime branches from the package output produced during that run.

A generated asset may remain tracked only when it is an intentional maintained source/runtime asset and deterministic validation depends on the repository copy. Build logs and package copies are never evidence that needs a source commit; the Actions run already provides provenance.

## Promotion rule

A commit may be promoted only after the validations required for the affected maintained modules succeed. Promotion must never depend on a follow-up commit whose only purpose is storing generated logs, package copies, trigger markers, or CI metadata.

`stable` represents validated source. Runtime branches and Actions artifacts represent validated installable output.

## Documentation rule

Current-state documentation must describe what the repository contains now. Historical phase/revision notes may be retained when they explain design decisions or regression intent, but they must be clearly treated as history when later implementation has superseded them.

Avoid status text that becomes false as soon as development advances: prefer version numbers from project metadata, explicit validation state, and links to current source/tests over prose such as "current phase" scattered across multiple files.

## Historical note

Tactical HUD `1.14.0` is retired because it mixed early Item Intelligence code into the HUD assembly. The maintained Tactical HUD line returned to the independent `1.13.x` model. Detailed historical release information belongs with the affected module rather than in repository-governance documentation.
