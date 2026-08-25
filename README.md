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
| [SPT Tactical HUD](mods/SPT-Tactical-HUD) | client `1.13.2`; optional server `1.13.0` | Population, player-status, and kill-feed HUD | `runtime` |
| [Item Intelligence Admiral](mods/SPT-Item-Intelligence) | `1.0.0`; stable / maintenance-only | Requirement, FIR, valuation, craft/barter relevance, and persistent per-item markers | `runtime-item-intelligence` |
| [Pause Admiral](mods/SPT-Pause) | `1.0.0`; stable / runtime validated | Offline-raid pause with raid-clock/time-of-day preservation and paused-input suppression | `runtime-pause` |
| [SPT Belt/Armband Inventory](mods/SPT-Belt-Armband-Inventory) | `0.1.0`; active development | Additional inventory/container behavior for the `ArmBand` equipment slot | `runtime-belt-armband` |
| [SPT Quest Planner](mods/SPT-Quest-Planner) | `0.9.4`; active development / runtime gate pending | Persistent active raid planning and quest decision support | CI artifact until deliberate runtime promotion |
| [Admiral Trader](mods/Admiral-Trader) | `0.1.0`; active development | Curated successor campaign/trader for the legacy Andrudis/QuestManiac ecosystem | Development source / CI artifacts |
| [Admiral Artyom Revival](mods/Admiral-Artyom-Revival) | `3.0.0`; SPT 4.1.3 runtime validated | Maintained revival of WTT-Artem trader, 23-quest campaign, gear and clothing | `runtime-artem-revival` |

Tactical HUD `1.14.0` is retired. That build accidentally combined early Item Intelligence code with the HUD. The maintained HUD line is `1.13.2`; Item Intelligence Admiral has an independent version and release lifecycle.

## Repository channels

- `main` — authoritative development source.
- `stable` — source commit promoted after deliberate suite publication.
- `runtime` — install-only Tactical HUD package.
- `runtime-item-intelligence` — install-only Item Intelligence Admiral package; branch name retained as a compatibility identifier.
- `runtime-pause` — install-only Pause Admiral package.
- `runtime-belt-armband` — install-only Belt/Armband Inventory package.
- `runtime-artem-revival` — stable Admiral Artyom Revival publication identity; branch name retained as an established compatibility identifier.
- `archive/v1.13.0` — intentional frozen Tactical HUD `1.13.0` reserve.

Runtime branches are publication/runtime channels, not development branches. Their exact package model is documented by the owning module.

## Downloads

Runtime branches provide the maintained install packages for Tactical HUD, Item Intelligence Admiral, Pause Admiral, Belt/Armband Inventory, and the validated server identity for Admiral Artyom Revival. Tactical HUD `1.13.0` remains preserved under `archive/v1.13.0`.

Admiral Artyom Revival differs from the self-contained runtime ZIP channels: `runtime-artem-revival` pins the accepted r5 server identity while repaired authored core data and the external Unity `Bundles/` set remain governed by the module's reconstruction/update contract.

## Repository policy

`main` contains source code, tests, maintained assets, build definitions, and durable documentation. Generated binaries, package copies, build/test logs, CI run metadata, temporary diagnostics, local IDE state, and dependency caches do not belong in source history.

Temporary feature, fix, diagnostic, research, and archaeology branches are removed after their useful work is merged or explicitly superseded. Active workstream branches are preserved until that determination is made.

Development follows [`CONTRIBUTING.md`](CONTRIBUTING.md). See also [development workflow](docs/development-workflow.md), [source/stable/runtime governance](docs/github-stable-runtime.md), and [branch hygiene](docs/branch-hygiene.md).
