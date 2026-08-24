# GitHub source / stable / runtime model

## Purpose

The repository is a multi-mod source repository. `main` is the authoritative development source tree. Generated build products, transient validation logs and CI run metadata are not source and should not be persisted in `main`.

## Branch roles

| Branch | Role |
| --- | --- |
| `main` | Active source, tests, maintained assets and documentation for independent mods |
| `stable` | CI-green source commit promoted by the suite publication workflow |
| `runtime` | Install-only SPT Tactical HUD channel |
| `runtime-item-intelligence` | Install-only SPT Item Intelligence channel |
| `runtime-pause` | Install-only SPT Pause channel |
| `runtime-belt-armband` | Install-only SPT Belt/Armband Inventory channel |
| `archive/v1.13.0` | Frozen Tactical HUD 1.13.0 reserve |

Feature, fix, diagnostic and archaeology branches are temporary development branches. They are not release channels and should be removed after their useful changes are merged, superseded or intentionally archived.

## Current module set

The current source tree contains these independent long-term modules under `mods/`:

- `SPT-Tactical-HUD`
- `SPT-Item-Intelligence`
- `SPT-Pause`
- `SPT-Belt-Armband-Inventory`
- `SPT-Quest-Planner`

The root `README.md` is the canonical human-readable index for current versions and module scope. Module-specific READMEs own implementation and development details.

## Repository hygiene

`main` should contain only material that is useful for development, review, maintenance or reproducible publication.

Keep in `main`:

- source code;
- project/build definitions;
- tests and deterministic validation scripts;
- maintained runtime assets that are part of a module's source package;
- documentation;
- GitHub workflow definitions.

Do not persist in `main`:

- `bin/`, `obj/`, IDE state or local dependency caches;
- transient build/test logs;
- CI run IDs or generated validation-status files;
- temporary diagnostic dumps;
- install ZIPs or duplicate compiled packages when the same output is already published as a CI artifact/runtime channel.

The root `.gitignore` is the baseline guardrail. Workflows must also avoid force-adding ignored/generated material back into source history.

## Build and publication model

CI may create working directories such as `build-output/` and `build-status/` during a run. These are CI workspace data, not source-of-truth directories.

Successful package outputs belong in GitHub Actions artifacts and, for maintained install channels, the corresponding runtime branch. A successful suite publication advances `stable` to the validated source commit and regenerates the relevant runtime branches from validated build output.

The same compiled package should not also be committed to `main` merely as build evidence. GitHub Actions already records the workflow result and artifact provenance.

## Promotion rule

A source commit may be promoted only after the validations required for the affected maintained modules succeed. Promotion must never depend on a follow-up commit whose only purpose is storing generated logs, package copies or CI metadata.

`stable` therefore represents validated source, while runtime branches and Actions artifacts represent validated installable output.

## Historical note

Tactical HUD `1.14.0` remains retired because it mixed early Item Intelligence code into the HUD assembly. The maintained Tactical HUD line returned to the independent `1.13.x` source/runtime model. Historical release details belong in the affected module documentation rather than in this repository-governance document.
