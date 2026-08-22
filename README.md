# SPT Tactical HUD

HUD mod for SPT 4.1.x. Current client version: **1.13.2**. The unchanged optional server companion remains **1.13.0**.

## Download / repository channels

- **Runtime:** install-only stable package. On the first migration, delete old `SPT Tactical HUD v...` plugin folders, then copy its folders into the SPT root. Later Runtime updates overwrite the same unversioned folder. It contains no source code or Python tools.
- **Stable:** the exact source and CI output used to publish Runtime.
- **Main:** active development; do not use the repository root itself as an installation package.

Direct links: [download Runtime ZIP](https://github.com/AdmiralAM/spt-1/archive/refs/heads/runtime.zip) · [browse Runtime](https://github.com/AdmiralAM/spt-1/tree/runtime) · [browse Stable](https://github.com/AdmiralAM/spt-1/tree/stable)

## HUD clusters

- Population: PMC, Scav, Boss and Raider counts during a raid; independently selectable Horizontal/Vertical layout.
- Player Status: hydration, energy and weight state; independently selectable Horizontal/Vertical layout and optional display outside raids.
- Kill Feed: compact icon-only layouts. Minimal shows killer/victim roles; Normal adds weapon category and distance; Detailed also adds hit location. Player, bot and weapon names are never rendered.
- HUD Edit Mode: compact draggable hitboxes with saved positions.

## Runtime rules

- F9 cycles `Hidden -> Population -> Population + Status -> Hidden`.
- The selected HUD state is restored after restarting the game.
- Hideout and menu scenes are not treated as raids.
- Raid counters, subscriptions and kill entries are cleared on every raid boundary.
- The native SPT version label is hidden by default. Detection is scene-driven and limited to text-component types; there is no recurring global Unity object scan.
- The server component performs no gameplay work and emits one successful-load line in the SPT server console.
- Role and body pictograms use a cached light contrast plate with a strong category-colored rim; weapon silhouettes retain a dark plate. Hydration, energy, weight and weight-state glyphs render directly on the HUD with no circular plate or rim. No per-frame texture allocation.
- Runtime hot paths reuse refresh collections and GUI measurement content. Kill-feed icon classification is cached when a death is recorded, and mouse/edit events use cached cluster hitboxes instead of rebuilding the visual layout.

## Asset pipeline

- Approved Pictures SPT artwork is stored as normalized transparent source PNGs. The production Scav cell uses a deliberately simplified micro-glyph of the approved balaclava-and-cigarette concept so its eyes and cigarette survive the 12-20 px HUD size.
- CI composes a 512×384 atlas with 43 mapped cells and validates transparency, optical centering and micro-scale coverage.
- Weapon families cover the complete approved 24-category board; unknown weapons use the cartridge icon.

## Package layout

Copy the contents of the versioned build folder into the SPT game root. Client-only releases contain only:

- `BepInEx/plugins/SPT Tactical HUD v1.13.2` — client HUD and sprite atlas.

The CI package includes `SPT_Runtime/user/mods/SPT Tactical HUD Server` only when the server component itself changes.

Every green `main` build atomically promotes its verified commit to `stable` and regenerates the install-only `runtime` branch. Failed or superseded builds cannot replace either channel.
