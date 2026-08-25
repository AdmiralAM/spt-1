# SPT Mod Suite

A source repository for independent SPT 4.1.x mods. Each maintained module owns its source code, tests, documentation, versioning, and release lifecycle under `mods/`.

> **Before development:** read [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`docs/development-workflow.md`](docs/development-workflow.md). Independent module workstreams use separate branches, PRs, and module-specific CI. Repository-wide publication is a separate controlled operation.

## Project authorship

This repository and the active SPT Mod Suite development are maintained by **AdmiralAM**. Modules authored as part of this suite are developed and maintained here under AdmiralAM's project ownership unless a module explicitly documents different upstream authorship or provenance.

`Artem Revival MOD SPT` is the important exception: it is a revival/compatibility workstream based on the pre-existing WTT Artem mod and its upstream content. Inclusion and maintenance of the revival in this repository do not claim original authorship of WTT Artem or its upstream assets/content. Module-specific documentation records that provenance where relevant.

This section is an authorship/provenance statement, not a software license grant.

## Modules

| Module | Current state | Purpose | Install channel |
| --- | --- | --- | --- |
| [SPT Tactical HUD](mods/SPT-Tactical-HUD) | Client `1.13.2`; optional server `1.13.0` | Population, player-status, and kill-feed HUD | [`runtime`](https://github.com/AdmiralAM/spt-1/tree/runtime) |
| [SPT Item Intelligence](mods/SPT-Item-Intelligence) | `0.12.0`; stable / maintenance-only | Requirement, FIR, valuation, craft/barter relevance, and persistent per-item markers | [`runtime-item-intelligence`](https://github.com/AdmiralAM/spt-1/tree/runtime-item-intelligence) |
| [Pause Admiral](mods/SPT-Pause) | `v1.0.0`; stable / runtime validated | Offline-raid pause with raid-clock/time-of-day preservation and paused-input suppression | [`runtime-pause`](https://github.com/AdmiralAM/spt-1/tree/runtime-pause) |
| [SPT Belt/Armband Inventory](mods/SPT-Belt-Armband-Inventory) | `0.1.0`; active development | Additional inventory/container behavior for the `ArmBand` equipment slot | [`runtime-belt-armband`](https://github.com/AdmiralAM/spt-1/tree/runtime-belt-armband) |
| [SPT Quest Planner](mods/SPT-Quest-Planner) | `0.9.4`; active UX/polish development | Persistent active raid planning plus quest progression recommendations | Development source / CI artifacts |
| [Artem Revival MOD SPT](mods/WTT-Artem-Revival) | `3.0.0`; SPT 4.1.3 runtime validated | Revived Artem trader, 23-quest campaign, gear and clothing | [`runtime-artem-revival`](https://github.com/AdmiralAM/spt-1/tree/runtime-artem-revival) |

Tactical HUD `1.14.0` is retired. That build accidentally combined early Item Intelligence code with the HUD. The maintained HUD line is `1.13.2`; Item Intelligence has an independent version and release lifecycle.

## Repository channels

- `main` — authoritative development source.
- `stable` — source commit promoted after deliberate suite publication.
- `runtime` — install-only Tactical HUD package.
- `runtime-item-intelligence` — install-only Item Intelligence package.
- `runtime-pause` — install-only Pause Admiral package.
- `runtime-belt-armband` — install-only Belt/Armband Inventory package.
- `runtime-artem-revival` — stable Artem runtime identity containing the validated r5 server DLL/runtime manifest; authored Artem core data and the large Unity `Bundles/` payload remain external/reproducible from the module source contract.
- `archive/v1.13.0` — intentional frozen Tactical HUD `1.13.0` reserve.

Runtime branches are publication/runtime channels, not development branches. Their exact package model is documented by the owning module.

## Downloads

[Tactical HUD](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime.zip) · [Item Intelligence](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime-item-intelligence.zip) · [Pause Admiral](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime-pause.zip) · [Belt/Armband Inventory](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime-belt-armband.zip) · [Tactical HUD 1.13.0 archive](https://github.com/AdmiralAM/spt-1/archive/refs/heads/archive/v1.13.0.zip)

Artem Revival is different from the self-contained runtime ZIP channels above: `runtime-artem-revival` pins the accepted r5 DLL/runtime manifest, while the repaired authored core data and already-assembled external `Bundles/` directory remain in the installed Artem folder. See the Artem module README for reconstruction/update details.

## Repository policy

`main` contains source code, tests, maintained assets, build definitions, and durable documentation. Generated binaries, package copies, build/test logs, CI run metadata, temporary diagnostics, local IDE state, and dependency caches do not belong in source history.

Temporary feature, fix, diagnostic, and archaeology branches are removed after their useful work is merged or explicitly superseded. Active workstream branches are preserved until that determination is made.

Development follows [`CONTRIBUTING.md`](CONTRIBUTING.md). See also [development workflow](docs/development-workflow.md), [source/stable/runtime governance](docs/github-stable-runtime.md), and [branch hygiene](docs/branch-hygiene.md).
