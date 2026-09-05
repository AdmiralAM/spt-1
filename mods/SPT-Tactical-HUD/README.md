# SPT Tactical HUD

> **Legacy integrated baseline — not active development authority.**
>
> This `mods/SPT-Tactical-HUD` tree is the previously accepted `1.13.2` source still present in `main` while **Admiral Tactical HUD 1.13.3** is developed through the single canonical HUD workstream. New HUD work belongs to [Issue #71](https://github.com/AdmiralAM/spt-1/issues/71) and the single live HUD implementation PR (currently [PR #322](https://github.com/AdmiralAM/spt-1/pull/322)). Do not fork, resume, or “repair” this legacy path as a separate product line.

Stable historical HUD module for SPT 4.1.x.

| Component | Version |
| --- | --- |
| Client | `1.13.2` |
| Optional server companion | `1.13.0` |

## Scope

Tactical HUD owns only HUD functionality:

- population display;
- player status display;
- kill feed;
- HUD edit mode;
- HUD assets and their validation/optimization pipeline.

Item Intelligence, Pause, Belt/Armband Inventory, Quest Planner, and other non-HUD systems are independent modules.

The maintained historical `1.13.2` client preserves the verified Tactical HUD behavior after the accidental `1.14.0` HUD/Item-Intelligence combination was retired. It is retained in `main` only as the accepted baseline until the Admiral Tactical HUD replacement line is deliberately integrated.

## Source layout

- `client/` — BepInEx client source and maintained HUD assets.
- `server/` — optional SPT server companion.
- `tools/` — deterministic asset, optics, and hot-path validation tools.
- `docs/` — Tactical HUD-specific design and maintenance notes when present.

CI may create transient `build-status/` data while validating the module. That directory is generated workspace output and is not part of the source tree.

The approved 2048×1536 source board is preserved losslessly as 43 named 256×256 cells under `client/assets/source/approved-cells/`. The atlas generator uses those cells to reproduce the approved HUD sprites without keeping a single fragile multi-megabyte source image.

## Installation

Use the install-only [`runtime`](https://github.com/AdmiralAM/spt-1/tree/runtime) channel for the currently published HUD package. Development candidates from the Admiral Tactical HUD workstream are not silently promoted to that channel before deliberate acceptance.
