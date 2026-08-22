# SPT Tactical HUD

Client-side BepInEx HUD plugin for SPT 4.x. Current development version: **1.10.4**.

## HUD clusters

- Population: PMC, Scav, Boss and Raider counts during a raid.
- Player Status: hydration, energy and weight state; optional profile-backed display outside raids.
- Kill Feed: Minimal, Normal and Detailed layouts with role, hit location, distance and weapon data.
- HUD Edit Mode: compact draggable hitboxes with saved positions.

## Runtime rules

- F9 cycles `Hidden -> Population -> Population + Status -> Hidden`.
- The selected HUD state is restored after restarting the game.
- Hideout and menu scenes are not treated as raids.
- Raid counters, subscriptions and kill entries are cleared on every raid boundary.
- The native SPT version label can be hidden without modifying unrelated UI text.
- In Detailed Kill Feed mode the weapon name is placed after distance.

## Pending visual integration

Weapon-category icons remain temporary. Replace them only after the approved Kill Feed asset set is complete.
