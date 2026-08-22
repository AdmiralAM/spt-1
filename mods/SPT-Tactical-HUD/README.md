# SPT Tactical HUD

Stable HUD mod for SPT 4.1.x.

| Component | Version |
| --- | --- |
| Client HUD | `1.13.2` |
| Optional server companion | `1.13.0` |

The HUD contains only Population, Player Status, Kill Feed, HUD Edit Mode and their asset/optimization pipeline. Item Intelligence and all future non-HUD systems are separate mods.

Runtime behavior and visuals are identical to the previously verified Tactical HUD `1.13.2`. The corrected repository structure does not add any new HUD feature.

## Source layout

- `client/` — BepInEx HUD source and assets;
- `server/` — optional SPT server load companion;
- `tools/` — HUD asset, optics and hot-path checks;
- `docs/` — Tactical HUD-specific backlog and notes;
- `build-status/` — CI evidence generated for this mod.

The approved 2048×1536 source board is stored losslessly as 43 named 256×256 cells under `client/assets/source/approved-cells/`. This avoids a single fragile multi-megabyte PNG while preserving the exact approved pixels used by the atlas generator.

Install the package from the repository's `runtime` channel. Its unversioned `BepInEx/plugins/SPT Tactical HUD/` directory is safe to overwrite on later HUD updates.
