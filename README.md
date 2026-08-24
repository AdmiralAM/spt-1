# SPT Mod Suite

A source repository for independent SPT 4.1.x mods. Each maintained module owns its source code, tests, documentation, versioning, and release lifecycle under `mods/`.

> **Before development:** read [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`docs/development-workflow.md`](docs/development-workflow.md). Independent module workstreams use separate branches, PRs, and module-specific CI. Repository-wide publication is a separate controlled operation.

## Modules

| Module | Current state | Purpose | Install channel |
| --- | --- | --- | --- |
| [SPT Tactical HUD](mods/SPT-Tactical-HUD) | Client `1.13.2`; optional server `1.13.0` | Population, player-status, and kill-feed HUD | [`runtime`](https://github.com/AdmiralAM/spt-1/tree/runtime) |
| [SPT Item Intelligence](mods/SPT-Item-Intelligence) | `0.10.1`; active development | Item requirement and value intelligence with persistent per-item markers | [`runtime-item-intelligence`](https://github.com/AdmiralAM/spt-1/tree/runtime-item-intelligence) |
| [SPT Pause](mods/SPT-Pause) | `0.1.1`; validation pending | Offline-raid pause with raid-clock and time-of-day preservation | [`runtime-pause`](https://github.com/AdmiralAM/spt-1/tree/runtime-pause) |
| [SPT Belt/Armband Inventory](mods/SPT-Belt-Armband-Inventory) | `0.1.0`; active development | Additional inventory/container behavior for the `ArmBand` equipment slot | [`runtime-belt-armband`](https://github.com/AdmiralAM/spt-1/tree/runtime-belt-armband) |
| [SPT Quest Planner](mods/SPT-Quest-Planner) | `0.9.0`; active development | Quest topology, requirements, progression state, and raid planning | Development source / CI artifacts |

Tactical HUD `1.14.0` is retired. That build accidentally combined early Item Intelligence code with the HUD. The maintained HUD line is `1.13.2`; Item Intelligence has an independent version and release lifecycle.

## Repository channels

- `main` — authoritative development source.
- `stable` — source commit promoted after deliberate suite publication.
- `runtime` — install-only Tactical HUD package.
- `runtime-item-intelligence` — install-only Item Intelligence package.
- `runtime-pause` — install-only Pause package.
- `runtime-belt-armband` — install-only Belt/Armband Inventory package.
- `archive/v1.13.0` — intentional frozen Tactical HUD `1.13.0` reserve.

Runtime branches contain only installable files for their named module. They are generated from validated source and are not development branches.

## Downloads

[Tactical HUD](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime.zip) · [Item Intelligence](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime-item-intelligence.zip) · [Pause](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime-pause.zip) · [Belt/Armband Inventory](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime-belt-armband.zip) · [Tactical HUD 1.13.0 archive](https://github.com/AdmiralAM/spt-1/archive/refs/heads/archive/v1.13.0.zip)

Copy only the runtime package for the mod you want into the SPT root. Runtime plugin directories are version-independent so an update can replace the previous installation cleanly.

## Repository policy

`main` contains source code, tests, maintained assets, build definitions, and durable documentation. Generated binaries, package copies, build/test logs, CI run metadata, temporary diagnostics, local IDE state, and dependency caches do not belong in source history.

Temporary feature, fix, diagnostic, and archaeology branches are removed after their useful work is merged or explicitly superseded. Active workstream branches are preserved until that determination is made.

Development follows [`CONTRIBUTING.md`](CONTRIBUTING.md). See also [development workflow](docs/development-workflow.md), [source/stable/runtime governance](docs/github-stable-runtime.md), and [branch hygiene](docs/branch-hygiene.md).
