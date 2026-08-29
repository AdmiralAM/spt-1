# Archaeology and SPT 4.1 mapping

Primary references reviewed for Phase 1 / PackNStrap compatibility:

- [Trench-foot/BeltSlot](https://github.com/Trench-foot/BeltSlot) — original MIT implementation for SPT 3.11.
- [WelcomeToThursday/PackNStrap](https://github.com/WelcomeToThursday/PackNStrap) — current content pack with embedded BeltSlot 2.0.x code targeting SPT 4.0.4.
- [SPT 4.x Assembly-CSharp decompilation](https://github.com/Luna-Salamanca/assemblycsharptarkovspt4) — current `ContainersPanel` and inventory extension shapes.

Useful behavior retained:

- `ArmBand` is inserted beside Pockets only for belt/container presentation.
- A belt uses the normal container slot template instead of the dogtag-style template.
- Any compatible container item can work; the adapter does not hardcode Pack 'n' Strap item IDs.
- Belt containers participate in `GetPrioritizedContainersForLoot` while preserving the current vanilla result as the base ordering.
- Belt grids participate in `GetPrioritizedGridsForUnloadedObject` while preserving the current vanilla result as the base sequence.

Behavior intentionally replaced or rejected:

- No patch on `ItemView.Update`.
- No scene-name table or scene polling.
- No hardcoded obfuscated field such as `equipmentSlot_0`.
- No full replacement of `InventoryController.ReplaceInventory`.
- No manual clone/toggle map for each screen hierarchy.
- No PackNStrap-specific item class dependency or content-ID allowlist.
- No PackNStrap mag-dump-pouch special case; that belongs to the content pack rather than the generic belt contract.

## R4 screen coverage contract

The old BeltSlot 2.0.x screen-specific patches are not part of the SPT 4.1 port unless runtime evidence proves a missing route.

- `EquipmentTabPatch` is an empty postfix and provides no behavior to port.
- `EquipmentBuildsScreenPatch` only calls the old `SetBuildsArmbandSlot()` manual UI refresh path.
- `ItemUiContext.ShowPlayerEquipmentWindow` patch only calls the old `SetDeployArmbandSlot()` manual UI refresh path.
- Those refresh paths exist to clone/toggle per-screen armband/belt GameObjects in the old implementation; they are not inventory mechanics.

SPT 4.1 routes the relevant container presentations through `ContainersPanel.Show(...)` directly:

- `EquipmentBuildsScreen` owns a `ContainersPanel` and presents build equipment through it.
- `PlayerEquipmentWindow.Show(...)` closes and then shows its `ContainersPanel` with the viewed player's `InventoryEquipment`.
- `ComplexStashPanel.Show(...)` shows its `ContainersPanel` for loot/corpse equipment.
- `TransferItemsInRaidScreen.Show(...)` shows its `ContainersPanel` for transfer inventory.

Therefore the generic `ContainersPanel.Show(...)` patch is the single presentation boundary for these contexts. Adding the old screen-specific patches would duplicate state, depend on obfuscated/private screen methods, and reintroduce manual UI hierarchy manipulation without adding belt functionality.

Porting rule: **do not add a screen-specific patch merely because old BeltSlot had one.** Add one only when a current SPT 4.1 screen is proven to bypass `ContainersPanel.Show(...)` and that bypass prevents belt inventory functionality.

SPT 4.1 keeps a single `ContainersPanel.Show(...)` route across the player inventory, loot, transfer, build and profile-style container contexts reviewed above. The runtime adapter temporarily supplies a belt-aware slot order for that call and restores the original field in a Harmony finalizer. The private slot factory is handled with the same transaction pattern so `ArmBand` receives the default container template without patching the method result type.
