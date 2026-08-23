# SPT Mod Suite

This repository contains independent SPT mods. Each long-term product owns its source, documentation, tests and release version under `mods/`.

| Mod | Version | Scope | Install channel |
| --- | --- | --- | --- |
| [SPT Tactical HUD](mods/SPT-Tactical-HUD) | Client `1.13.2`; optional server `1.13.0` | Population, status and kill-feed HUD only | [`runtime`](https://github.com/AdmiralAM/spt-1/tree/runtime) |
| [SPT Item Intelligence](mods/SPT-Item-Intelligence) | `0.9.0` | Persistent requirement markers with live stack-aware Value and ₽/slot, Phase 17 | [`runtime-item-intelligence`](https://github.com/AdmiralAM/spt-1/tree/runtime-item-intelligence) |
| [SPT Pause](mods/SPT-Pause) | `0.1.1` | Offline raid pause with timer/time-of-day preservation, Phase 1 validation hardening | [`runtime-pause`](https://github.com/AdmiralAM/spt-1/tree/runtime-pause) |
| [SPT Belt/Armband Inventory](mods/SPT-Belt-Armband-Inventory) | `0.1.0` | Event-driven container row for belts equipped in ArmBand, Phase 1 | [`runtime-belt-armband`](https://github.com/AdmiralAM/spt-1/tree/runtime-belt-armband) |

`SPT Tactical HUD v1.14.0` is retired: that number was created when Item Intelligence was mistakenly compiled into the HUD. The corrected current HUD is the complete stable **v1.13.2**, while the extracted Item Intelligence code started its own lifecycle at **v0.1.0** and now advances independently at **v0.9.0**.

## Repository channels

- `main` — active multi-mod development;
- `stable` — exact CI-green source and build evidence for all current mods;
- `runtime` — install-only **SPT Tactical HUD** package; it never contains Item Intelligence;
- `runtime-item-intelligence` — install-only **SPT Item Intelligence** package; it never contains Tactical HUD;
- `runtime-pause` — install-only **SPT Pause** package; it contains neither Tactical HUD nor Item Intelligence;
- `runtime-belt-armband` — install-only **SPT Belt/Armband Inventory** package; it contains no other suite module;
- `archive/v1.13.0` — frozen full Tactical HUD 1.13.0 reserve.

Downloads: [Tactical HUD ZIP](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime.zip) · [Item Intelligence ZIP](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime-item-intelligence.zip) · [Pause ZIP](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime-pause.zip) · [Belt/Armband Inventory ZIP](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime-belt-armband.zip) · [Tactical HUD 1.13.0 reserve](https://github.com/AdmiralAM/spt-1/archive/refs/heads/archive/v1.13.0.zip)

All runtime branches use stable, version-independent plugin directories. Copy only the branch for the mod you want into the SPT root.
