# Phase 1 runtime contract

Target: SPT 4.1.x client UI.

## Included

- Detect any equipped `ArmBand` item that exposes inventory containers; no Pack 'n' Strap type dependency.
- Add one belt row beside Pockets only while a container armband is rendered.
- Use the normal container slot template instead of the dogtag/armband template.
- Cover every screen that delegates container rendering to EFT's `ContainersPanel` (player inventory, loot, insurance, equipment builds and profile views).
- Configure the row above or below Pockets in F12; restart required.
- Discover the SPT 4.1 slot-order field structurally, not by its obfuscated name.
- Restore every temporarily changed EFT field in a Harmony finalizer, including when the original UI method throws.
- Perform no `Update`, per-frame polling, scene scan, inventory replacement or profile mutation.
- Refuse to double-patch when legacy `Trenchfoot-BeltSlot` is already loaded.

## Deliberately deferred

- Adding custom belt items or changing server database filters.
- Ctrl-click/quick-move priority into belt grids.
- Live refresh when a belt is equipped while the same inventory screen remains open; close and reopen the screen in Phase 1.
- Physical validation against the user's exact SPT 4.1.2 install and mod stack.

## Acceptance checks

1. Empty/plain armband: no duplicate container row.
2. Container armband: one row in the configured position.
3. Closing the panel or an exception restores the original static slot order and dogtag template.
4. With legacy BeltSlot present, this plugin logs a conflict and installs no Harmony patches.
5. Automated state/order tests pass and the client project builds without an `Assembly-CSharp` compile dependency.
