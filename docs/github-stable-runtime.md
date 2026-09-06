# Source, stable, and runtime model

## Purpose

This is a multi-mod source repository. `main` is the authoritative integrated tree. Generated packages, transient validation logs, and CI run metadata are not source and must not be persisted in `main`.

## Branch roles

| Branch | Role |
| --- | --- |
| `main` | Active integrated source, tests, maintained assets, workflows, and durable documentation |
| `stable` | Source commit promoted after deliberate suite validation/publication |
| `runtime` | Install-only Admiral Tactical HUD / historical Tactical HUD publication channel |
| `runtime-item-intelligence` | Install-only Item Intelligence Admiral channel; retained compatibility branch name |
| `runtime-pause` | Install-only Pause Admiral channel |
| `runtime-belt-armband` | Install-only Belt/Armband Inventory channel |
| `runtime-economy-admiral` | Install-only Economy Admiral 0.1.0 channel for SPT 4.1.4 |
| `runtime-artem-revival` | Stable Admiral Artyom Revival publication identity; retained compatibility branch name |
| `archive/v1.13.0` | Temporary Tactical HUD recovery reserve until final Admiral Tactical HUD 1.13.3 cleanup; never active development authority |

Feature, fix, diagnostic, research, build, and archaeology branches are temporary unless explicitly documented otherwise. They are not release channels and should be removed after their useful work is merged or superseded.

## Current modules

Long-term source modules currently integrated under `mods/` include:

- `SPT-Tactical-HUD` — previously accepted integrated Tactical HUD source while the `Admiral Tactical HUD 1.13.3` replacement remains unmerged in its single active workstream PR;
- `SPT-Item-Intelligence`;
- `SPT-Pause`;
- `SPT-Belt-Armband-Inventory`;
- `Item-Valuation-MOD-SPT`;
- `Economy-Admiral`;
- `Admiral-Trader`;
- `Admiral-Artyom-Revival`.

### Admiral Tactical HUD transition

The canonical product identity is **Admiral Tactical HUD**. Issue #71 owns its milestone roadmap and the canonical workstream identifies the single live implementation PR.

Before final integration, `main` intentionally still contains the last accepted legacy source at `mods/SPT-Tactical-HUD`. The active `1.13.3` replacement uses `mods/Admiral-Tactical-HUD` in its live PR. These are **not two active HUD products**: the old `main` path is the integrated baseline, and the live PR is the only development authority.

Ordinary M1→M2→… milestone transitions remain in that single live HUD line by default. A milestone PASS does not create a frozen successor branch or a second implementation authority.

The `runtime` branch remains the current published install channel until a candidate is deliberately promoted. An unmerged RC, CI artifact, or development PR must not silently rewrite it.

`archive/v1.13.0` exists only as a temporary recovery point while the 1.13.3 replacement is unfinished. Once the canonical 1.13.3 line reaches deliberate stable cleanup, the archive should be removed so old versions do not remain as competing repository surfaces, unless the user explicitly asks to retain it.

`Economy-Admiral` is integrated source with physically accepted SPT 4.1.4 Economy Beta behavior and module-specific CI. Its maintained install-only publication channel is `runtime-economy-admiral`; the runtime manifest pins version `0.1.0`, SPT `4.1.4`, the publication source commit, and install root `SPT_Runtime/user/mods/Economy Admiral`.

`Item-Valuation-MOD-SPT` remains a development/CI-artifact module until its SPT 4.1.4 physical runtime gate passes; it has no permanent runtime branch yet.

The root `README.md` is the human-readable module index. Each module README owns its current product name, version, scope, architecture, installation channel, and validation status. Project/package metadata is the machine-readable version authority. Detailed phase/revision documents are supporting history, not a second source of truth for current status.

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

Validated packages belong in GitHub Actions artifacts and, for maintained install channels, the corresponding runtime branch. The suite publication workflow advances `stable` to the validated source commit and rebuilds its managed self-contained runtime branches from package output produced during that run. Economy Admiral uses its own isolated publication workflow so publishing `runtime-economy-admiral` does not advance suite `stable` or republish unrelated runtime channels.

`runtime-artem-revival` is an explicit module-specific compatibility identifier for **Admiral Artyom Revival**. The accepted runtime consists of the validated SPT 4.1.4 server build plus repaired authored upstream data/assets and approximately 1.5 GB of Unity Bundles. The authored core and Bundles originate from the external archived WTT-Artem source set and are reproducibly transformed by deterministic importer/repair/localization tooling in `main`; duplicating those binaries in Git would be inappropriate. The permanent runtime branch therefore pins the validated server identity and immutable manifest for the accepted `r5-RU-compat` candidate. The module README documents how that identity relates to the persistent installed core/Bundles set.

Promotion of `runtime-artem-revival` must use a candidate that passed Admiral Artyom Revival module CI and required user runtime validation. The branch is a publication identity, not a development workspace or a claim that its Git archive is a standalone full installation.

A generated asset may remain tracked only when it is an intentional maintained source/runtime asset and deterministic validation depends on the repository copy. Build logs and package copies are never evidence that needs a source commit; the Actions run already provides provenance.

## Promotion rule

A commit/runtime candidate may be promoted only after the validations required for the affected maintained module succeed. Promotion must never depend on a follow-up commit whose only purpose is storing generated logs, package copies, trigger markers, or CI metadata.

`stable` represents validated suite source. Runtime branches and Actions artifacts represent validated runtime/publication state according to each module's documented package model.

## Documentation and version rule

Current-state documentation must describe what the repository contains now. Historical phase/revision notes may be retained when they explain design decisions or regression intent, but they must be clearly treated as history when later implementation has superseded them.

Current product names and versions must agree between the root module index, canonical workstream, active module documentation, build/package metadata and maintained runtime manifest, allowing an explicitly documented RC transition where accepted `main`/runtime state intentionally trails the active development line. Historical/upstream names or compatibility identifiers may remain only where their retained role is explicit. See `docs/development-workflow.md` for the complete naming/version contract.

Avoid status text that becomes false as soon as development advances: prefer explicit version metadata, validation state, and links to current source/tests over prose such as "current phase" scattered across multiple files.

## Historical note

Tactical HUD `1.14.0` is retired because it mixed early Item Intelligence code into the HUD assembly. The independent HUD returned to the `1.13.x` model; the current canonical product name is **Admiral Tactical HUD**. Detailed historical release information belongs with the affected module rather than in repository-governance documentation.
