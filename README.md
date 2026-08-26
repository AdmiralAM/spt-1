# SPT Mod Suite

A source repository for independent SPT 4.1.x mods. Each maintained module owns its source code, tests, documentation, versioning, and release lifecycle under `mods/`.

> **Before development:** read [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`docs/development-workflow.md`](docs/development-workflow.md). Independent module workstreams use separate branches, PRs, and module-specific CI. Repository-wide publication is a separate controlled operation.

## Project authorship

This repository and the active SPT Mod Suite development are maintained by **AdmiralAM**. Modules authored as part of this suite are developed and maintained here under AdmiralAM's project ownership unless a module explicitly documents different upstream authorship or provenance.

**Admiral Artyom Revival** is the important provenance exception: it is the maintained revival/compatibility product based on the pre-existing WTT-Artem mod and its upstream content. Inclusion and maintenance of the revival do not claim original authorship of WTT-Artem or its upstream assets/content.

## Modules

Versions below are the module versions declared by current source metadata. A leading `v` is reserved for release/tag presentation and is not part of the semantic version itself.

| Module | Version / state | Purpose | Install channel |
| --- | --- | --- | --- |
| [Admiral Tactical HUD](mods/Admiral-Tactical-HUD) | `1.13.3`; stable-final candidate | Population, player-status, compact weapon-name kill feed | final CI package / runtime promotion |
| [Item Intelligence Admiral](mods/SPT-Item-Intelligence) | `1.0.0`; stable / maintenance-only | Requirement, FIR, valuation, craft/barter relevance, and persistent per-item markers | `runtime-item-intelligence` |
| [Pause Admiral](mods/SPT-Pause) | `1.0.0`; stable / runtime validated | Offline-raid pause with raid-clock/time-of-day preservation and paused-input suppression | `runtime-pause` |
| [SPT Belt/Armband Inventory](mods/SPT-Belt-Armband-Inventory) | `0.1.0`; active development | Additional inventory/container behavior for the `ArmBand` equipment slot | `runtime-belt-armband` |
| [Admiral Trader](mods/Admiral-Trader) | `0.1.0`; active development | Curated successor campaign/trader for the legacy Andrudis/QuestManiac ecosystem | Development source / CI artifacts |
| [Admiral Artyom Revival](mods/Admiral-Artyom-Revival) | `3.0.0`; SPT 4.1.3 runtime validated | Maintained revival of WTT-Artem trader, 23-quest campaign, gear and clothing | `runtime-artem-revival` |

Admiral Tactical HUD has one maintained line: **1.13.3**. Older Tactical HUD versions and the retired combined HUD/Item-Intelligence experiment are not maintained products and must not be used as runtime sources.

## Repository channels

- `main` — authoritative development source.
- `stable` — source commit promoted after deliberate suite publication.
- `runtime` — install-only Admiral Tactical HUD publication channel after final acceptance.
- `runtime-item-intelligence` — install-only Item Intelligence Admiral package; branch name retained as a compatibility identifier.
- `runtime-pause` — install-only Pause Admiral package.
- `runtime-belt-armband` — install-only Belt/Armband Inventory package.
- `runtime-artem-revival` — stable Admiral Artyom Revival publication identity.

Runtime branches are publication/runtime channels, not development branches.

## Repository policy

`main` contains source code, tests, maintained assets, build definitions, and durable documentation. Generated binaries, package copies, build/test logs, CI run metadata, temporary diagnostics, local IDE state, and dependency caches do not belong in source history.

Temporary feature, fix, diagnostic, research, and archaeology branches are removed after their useful work is merged or explicitly superseded. Active workstream branches are preserved until that determination is made.

Development follows [`CONTRIBUTING.md`](CONTRIBUTING.md). See also [development workflow](docs/development-workflow.md), [source/stable/runtime governance](docs/github-stable-runtime.md), and [branch hygiene](docs/branch-hygiene.md).
