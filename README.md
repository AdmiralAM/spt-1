# SPT Mod Suite

A source repository for independent SPT 4.1.x mods. Each maintained module owns its source code, documentation, versioning, and release lifecycle under `mods/`.

> **Before development:** read [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`docs/development-workflow.md`](docs/development-workflow.md). Independent module workstreams use separate branches, PRs, and module-specific validation while under active development. Repository-wide publication is a separate controlled operation.

## Project authorship

This repository and the active SPT Mod Suite development are maintained by **AdmiralAM**. Modules authored as part of this suite are developed and maintained here under AdmiralAM's project ownership unless a module explicitly documents different upstream authorship or provenance.

**Admiral Artyom Revival** is the important provenance exception: it is the maintained revival/compatibility product based on the pre-existing WTT-Artem mod and its upstream content. Inclusion and maintenance of the revival do not claim original authorship of WTT-Artem or its upstream assets/content.

## Modules

Versions below are the module versions declared by current accepted source metadata or, for an explicitly recorded active replacement line, the canonical workstream state.

| Module | Version / state | Purpose | Install channel |
| --- | --- | --- | --- |
| [Admiral Tactical HUD](https://github.com/AdmiralAM/spt-1/issues/71) | `1.13.3 RC2`; **M1 stabilization** | Population, player-status, and kill-feed HUD | Development: [PR #322](https://github.com/AdmiralAM/spt-1/pull/322); published `runtime` remains unchanged until deliberate acceptance |
| [Item Intelligence Admiral](mods/SPT-Item-Intelligence) | `1.0.0`; stable / maintenance-only | Requirement, FIR, valuation, craft/barter relevance, and persistent per-item markers | `runtime-item-intelligence` |
| [Pause Admiral](mods/SPT-Pause) | `1.0.0`; stable / runtime validated | Offline-raid pause with raid-clock/time-of-day preservation and paused-input suppression | `runtime-pause` |
| [B&A&HB #2 MOD SPT](mods/SPT-Belt-Armband-Inventory) | **`0.1.0 STABLE` / SPT 4.1.3** | ArmBand inventory plus dedicated Belt and HeadBand equipment slots, with configurable loss-on-death behavior | **[download ZIP](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime-belt-armband.zip)** |
| [Item Valuation MOD SPT](mods/Item-Valuation-MOD-SPT) | `1.0.0`; stable / SPT 4.1.3 runtime validated | Server-only inventory background coloring by economic value/category, with penetration tiers for ammunition | `runtime-item-valuation` |
| [Economy Admiral](mods/Economy-Admiral) | `0.1.0`; Economy Beta / SPT 4.1.3 runtime validated | Provenance-safe bounded economy/reward normalization with source-pressure, health, and explicit Admiral Trader compatibility evidence | `runtime-economy-admiral` |
| [Admiral Trader](mods/Admiral-Trader) | `0.1.0`; active development | Curated successor campaign/trader for the legacy Andrudis/QuestManiac ecosystem | Development source / CI artifacts |
| [Admiral Artyom Revival](mods/Admiral-Artyom-Revival) | `3.0.0`; SPT 4.1.3 runtime validated | Maintained revival of WTT-Artem trader, 23-quest campaign, gear and clothing | `runtime-artem-revival` |

### Admiral Tactical HUD transition

`Admiral Tactical HUD` is the canonical product identity and Issue #71 is its single milestone roadmap. The current active implementation authority is the one live HUD PR recorded by the canonical workstream; at present that is Draft PR #322 on `recovery/tactical-hud-rc2-clean`.

Until M1 receives physical runtime acceptance and the active line is deliberately integrated, `main` still contains the previously accepted legacy source tree at `mods/SPT-Tactical-HUD` (`1.13.2`). That legacy path is **not** a competing development authority and must not be used to start new HUD work. The target canonical source path is `mods/Admiral-Tactical-HUD`.

Tactical HUD `1.14.0` is retired. Historical PR #195 and branch `optimize/tactical-hud-runtime` are superseded evidence only. `archive/v1.13.0` is a temporary recovery reserve while the 1.13.3 line is not yet final; it is not a maintained product version and should be removed during the final 1.13.3 stable cleanup unless the user explicitly decides otherwise.

## Repository channels

- `main` — authoritative integrated source, including accepted stable module source plus active integrated development.
- `stable` — source commit promoted after deliberate suite publication.
- `runtime` — current published Tactical HUD install channel; an active Admiral Tactical HUD RC does not rewrite it before deliberate acceptance.
- `runtime-item-intelligence` — install-only Item Intelligence Admiral package; branch name retained as a compatibility identifier.
- `runtime-pause` — install-only Pause Admiral package.
- [`runtime-belt-armband`](https://github.com/AdmiralAM/spt-1/tree/runtime-belt-armband) — **install-only B&A&HB Stable v0.1.0 package**.
- `runtime-item-valuation` — install-only Item Valuation MOD SPT 1.0.0 package for SPT 4.1.3.
- `runtime-economy-admiral` — install-only Economy Admiral 0.1.0 package for SPT 4.1.3.
- `runtime-artem-revival` — stable Admiral Artyom Revival publication identity; branch name retained as an established compatibility identifier.
- `archive/v1.13.0` — temporary Tactical HUD recovery reserve pending final Admiral Tactical HUD 1.13.3 stable cleanup; never development authority.

Runtime branches are publication/runtime channels, not development branches. Their exact package model is documented by the owning module.

## Downloads

### B&A&HB #2 MOD SPT — Stable v0.1.0

- **Download:** [B&A&HB Stable v0.1.0 ZIP](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime-belt-armband.zip)
- **Package contents:** [`runtime-belt-armband`](https://github.com/AdmiralAM/spt-1/tree/runtime-belt-armband)
- **Stable source tag:** [`bahb-v0.1.0`](https://github.com/AdmiralAM/spt-1/tree/bahb-v0.1.0)
- **Version:** 0.1.0
- **Target:** SPT 4.1.3
- **Extra dependencies:** none

Installation: close SPT, download and unpack the ZIP, open the included `SPT_Runtime` directory, then copy its **contents** into the existing `SPT_Runtime` directory of the game. The package already contains both required parts: the BepInEx client DLL and the server mod DLL. Remove the obsolete `Trenchfoot-BeltSlot.dll` first if it is installed.

Runtime branches provide the maintained install packages for Admiral Tactical HUD/Tactical HUD, Item Intelligence Admiral, Pause Admiral, B&A&HB, Item Valuation MOD SPT, Economy Admiral, and the validated server identity for Admiral Artyom Revival. The old Tactical HUD `archive/v1.13.0` branch is only a temporary recovery point until final 1.13.3 cleanup.

Item Valuation MOD SPT `1.0.0` is published on `runtime-item-valuation` as a server-only install package rooted at `SPT_Runtime/user/mods/Item Valuation MOD SPT/`.

Economy Admiral `0.1.0` is published on `runtime-economy-admiral` as a server-only install package rooted at `SPT_Runtime/user/mods/Economy Admiral/`.

Admiral Artyom Revival differs from the self-contained runtime ZIP channels: `runtime-artem-revival` pins the accepted r5 server identity while repaired authored core data and the external Unity `Bundles/` set remain governed by the module's reconstruction/update contract.

## Repository policy

`main` contains maintained source and durable documentation. Active-development modules may also keep deterministic validation suites and build definitions. Once a module is deliberately promoted to a stable production source line, obsolete RC-only tests, temporary diagnostics, evidence bundles and development-only tooling are removed rather than retained as runtime baggage.

Generated binaries, package copies, build/test logs, CI run metadata, temporary diagnostics, local IDE state, and dependency caches do not belong in source history.

Temporary feature, fix, diagnostic, research, and archaeology branches are removed after their useful work is merged or explicitly superseded. Active workstream branches are preserved only while they serve current development.

Development follows [`CONTRIBUTING.md`](CONTRIBUTING.md). See also [development workflow](docs/development-workflow.md), [source/stable/runtime governance](docs/github-stable-runtime.md), and [branch hygiene](docs/branch-hygiene.md).
