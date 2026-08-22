# SPT Tactical HUD

Combined client/server HUD mod for SPT 4.1.x. Current development version: **1.12.1**.

## HUD clusters

- Population: PMC, Scav, Boss and Raider counts during a raid.
- Player Status: hydration, energy and weight state; optional profile-backed display outside raids.
- Kill Feed: compact icon-only layouts. Minimal shows killer/victim roles; Normal adds weapon category and distance; Detailed also adds hit location. Player, bot and weapon names are never rendered.
- HUD Edit Mode: compact draggable hitboxes with saved positions.

## Runtime rules

- F9 cycles `Hidden -> Population -> Population + Status -> Hidden`.
- The selected HUD state is restored after restarting the game.
- Hideout and menu scenes are not treated as raids.
- Raid counters, subscriptions and kill entries are cleared on every raid boundary.
- The native SPT version label is hidden by default. Detection is scene-driven and limited to text-component types; there is no recurring global Unity object scan.
- The server component performs no gameplay work and emits one successful-load line in the SPT server console.
- Every HUD pictogram uses a cached dark medallion plate and a stronger category-colored rim for stable contrast without per-frame texture allocation.

## Asset pipeline

- Approved Pictures SPT artwork is stored as normalized transparent source PNGs. Scav uses the approved red-skull mark; no procedural fallback is allowed.
- CI composes a 512×384 atlas with 43 mapped cells and validates transparency, optical centering and micro-scale coverage.
- Weapon families cover the complete approved 24-category board; unknown weapons use the cartridge icon.

## Package layout

Copy the contents of the versioned build folder into the SPT game root. It contains:

- `BepInEx/plugins/SPT Tactical HUD v1.12.1` — client HUD and sprite atlas.
- `SPT_Runtime/user/mods/SPT Tactical HUD Server` — one-shot server load notice.
