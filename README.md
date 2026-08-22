# SPT Tactical HUD

Client-side BepInEx HUD plugin for SPT 4.x. Current development version: **1.11.0**.

## HUD clusters

- Population: PMC, Scav, Boss and Raider counts during a raid.
- Player Status: hydration, energy and weight state; optional profile-backed display outside raids.
- Kill Feed: Minimal, Normal and Detailed layouts with role, self/player, left/right hit location, distance and weapon-category data.
- HUD Edit Mode: compact draggable hitboxes with saved positions.

## Runtime rules

- F9 cycles `Hidden -> Population -> Population + Status -> Hidden`.
- The selected HUD state is restored after restarting the game.
- Hideout and menu scenes are not treated as raids.
- Raid counters, subscriptions and kill entries are cleared on every raid boundary.
- The native SPT version label can be hidden without modifying unrelated UI text.
- In Detailed Kill Feed mode the weapon name is placed after distance.

## Asset pipeline

- Approved Pictures SPT artwork is stored as normalized transparent source PNGs.
- CI composes a 512×384 atlas with 43 mapped cells and validates transparency, optical centering and micro-scale coverage.
- Weapon families cover the complete approved 24-category board; unknown weapons use the cartridge icon.
