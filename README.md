# SPT Mod Suite

A source repository for independent SPT 4.1.x mods. Each maintained module owns its source code, documentation, versioning, and release lifecycle under `mods/`.

> **Before development:** read [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`docs/development-workflow.md`](docs/development-workflow.md). Independent module workstreams use separate branches, PRs, and module-specific validation while under active development. Repository-wide publication is a separate controlled operation.

## Project ownership

This repository contains only the AdmiralAM SPT modules maintained as part of this suite. All rights belong exclusively to Admiral and should not be used by anyone without approval.

## Modules

Versions below reflect the durable workstream roadmap when a module has an active replacement/development line; publication channels continue to represent their last deliberately accepted runtime package until a newer candidate is accepted.

| Module | Version / state | Purpose | Install / development channel |
| --- | --- | --- | --- |
| [Admiral Tactical HUD](https://github.com/AdmiralAM/spt-1/issues/71) | `1.13.3`; **Stable Beta** playable baseline, further development later | Population and player-status HUD; kill feed is intentionally absent from Stable Beta and is the mandatory next product stage | Stable Beta baseline is pinned by exact source/release metadata; the single live implementation PR remains the later-development line |
| [Item Intelligence Admiral](mods/SPT-Item-Intelligence) | `1.2.0`; stable / runtime validated | Requirement, FIR, hideout, valuation/background, craft/barter relevance, contextual inventory cards, and optional Amands Sense guidance | `runtime-item-intelligence` |
| [Pause Admiral](mods/SPT-Pause) | `1.0.0`; stable / runtime validated | Offline-raid pause with raid-clock/time-of-day preservation and paused-input suppression | `runtime-pause` |
| [B&A&HB #2 MOD SPT](https://github.com/AdmiralAM/spt-1/issues/285) | `0.2.0`; active Pack 'n' Strap companion (`0.1.0` published stable) | HeadBand, optional Dogtag Case, and exact B&A-owned protection; standard belts, pouches and mini-containers remain owned by separately installed Pack 'n' Strap | Single live v0.2 PR discovered from GitHub; stable full-Belt v0.1.0 reserve remains on [`runtime-belt-armband`](https://github.com/AdmiralAM/spt-1/tree/runtime-belt-armband) |
| [Item Valuation MOD SPT](mods/Item-Valuation-MOD-SPT) | `1.0.0`; stable / SPT 4.1.3 runtime validated | Server-only inventory background coloring by economic value/category, with penetration tiers for ammunition | `runtime-item-valuation` |
| [Economy Admiral](https://github.com/AdmiralAM/spt-1/issues/262) | `0.1.0`; active product acceptance / published runtime baseline | Coherent Quest, Trader, Flea and Loot economy pressure with server-backed F12 controls and optional Admiral Trader compatibility | Single live implementation PR discovered from GitHub; `runtime-economy-admiral` remains the accepted publication channel |
| [Admiral Trader](https://github.com/AdmiralAM/spt-1/issues/192) | `0.1.0 + milestones`; active development | Curated successor campaign/trader for the legacy Andrudis/QuestManiac ecosystem | Single live implementation PR discovered from GitHub; CI artifacts until deliberate runtime promotion |

### Admiral Tactical HUD Stable Beta

`Admiral Tactical HUD 1.13.3 Stable Beta` is the user-accepted playable baseline. It is **not final stable**.

- Runtime code baseline: `d7d63c0196af12141aedfc8f4896f0a9a84376ed`.
- The unsuccessful experimental kill feed is intentionally absent and must not be restored into Stable Beta.
- Kill feed remains the mandatory next product stage and will be redesigned from first principles for fast, clear, low-noise combat readability without covering important UI.
- Further architecture, population/status, UX and performance polish remains later roadmap work.

Issue #71 is the durable roadmap; the live implementation PR remains the continuation line when development resumes. See [Stable Beta release notes](docs/admiral-tactical-hud-1.13.3-stable-beta.md).

`main` may still contain the older integrated `mods/SPT-Tactical-HUD` source tree. That legacy path is not a competing development authority. Tactical HUD `1.14.0`, historical PR #195 and the retired `optimize/tactical-hud-runtime` branch remain superseded evidence only.

### Item Intelligence Admiral transition

Issues #338 and PRs #341/#343 record the accepted consolidation through **v1.2.0**. `runtime-item-intelligence` is the maintained install channel. Item Valuation behavior is available as an optional Item Intelligence module; the legacy standalone source/runtime channel remains only for rollback compatibility.

## Repository channels

- `main` — authoritative integrated source, including accepted stable module source plus active integrated development.
- `stable` — source commit promoted after deliberate suite publication.
- `runtime` — current published Tactical HUD install channel; an active Admiral Tactical HUD RC does not rewrite it before deliberate acceptance.
- `runtime-item-intelligence` — install-only Item Intelligence Admiral accepted publication channel; retained compatibility branch name.
- `runtime-pause` — install-only Pause Admiral package.
- [`runtime-belt-armband`](https://github.com/AdmiralAM/spt-1/tree/runtime-belt-armband) — install-only B&A&HB Stable v0.1.0 package while v0.2 remains in development.
- `runtime-item-valuation` — install-only Item Valuation MOD SPT 1.0.0 package for SPT 4.1.3.
- `runtime-economy-admiral` — install-only Economy Admiral 0.1.0 publication channel.
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

Runtime branches provide the maintained install packages for Admiral Tactical HUD/Tactical HUD, Item Intelligence Admiral, Pause Admiral, B&A&HB, Item Valuation MOD SPT, and Economy Admiral. The old Tactical HUD `archive/v1.13.0` branch is only a temporary recovery point until final 1.13.3 cleanup.

Item Valuation MOD SPT `1.0.0` is published on `runtime-item-valuation` as a server-only install package rooted at `SPT_Runtime/user/mods/Item Valuation MOD SPT/`.

Economy Admiral `0.1.0` is published on `runtime-economy-admiral` as its maintained install-only publication channel.

## Repository policy

`main` contains maintained source and durable documentation. Active-development modules may also keep deterministic validation suites and build definitions. Once a module is deliberately promoted to a stable production source line, obsolete RC-only tests, temporary diagnostics, evidence bundles and development-only tooling are removed rather than retained as runtime baggage.

Generated binaries, package copies, build/test logs, CI run metadata, temporary diagnostics, local IDE state, dependency caches, third-party source trees, imported content archives, and large runtime assets do not belong in source history.

Temporary feature, fix, diagnostic, research, and archaeology branches are removed after their useful work is merged or explicitly superseded. Active workstream branches are preserved only while they serve current development.

Development follows [`CONTRIBUTING.md`](CONTRIBUTING.md). See also [development workflow](docs/development-workflow.md), [source/stable/runtime governance](docs/github-stable-runtime.md), and [branch hygiene](docs/branch-hygiene.md).
