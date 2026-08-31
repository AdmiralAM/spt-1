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

SPT 4.1 keeps a single `ContainersPanel.Show(...)` route across the player inventory, loot, transfer, build and profile-style container contexts reviewed above. The runtime adapter temporarily supplies a belt-aware slot order for that call and restores the original field in a Harmony finalizer. The private slot factory is handled with the same transaction pattern so `ArmBand` receives the default container template without patching the method result type.

Porting rule: **do not add a screen-specific patch merely because old BeltSlot had one.** Add one only when a current SPT 4.1 screen is proven to bypass `ContainersPanel.Show(...)` and that bypass prevents belt inventory functionality.

## R15 reload pseudo-slot enumeration contract

The post-physical ArmBand PASS / Belt FAIL diagnosis is now anchored to the exact SPT 4.x decompilation snapshot `Luna-Salamanca/assemblycsharptarkovspt4@5566499af1ba6d9e85cc89c72c79ded5757cafec`, rather than to the earlier broad hypothesis that an undeclared enum value is automatically rejected.

`Inventory.GetItemsInSlots(IEnumerable<EquipmentSlot>)` resolves every supplied value through `InventoryEquipment.GetSlot`, materializes the equipped roots in caller order, and then concatenates descendants of compound roots. `InventoryEquipment.GetSlot` itself is an integer-indexed lookup into `_cachedSlots`; its constructor sizes that cache from the live equipment `Slots` array and populates each entry by parsing the slot ID back to `EquipmentSlot`.

Consequences for the dedicated Magazine Belt reload boundary:

- pseudo-value `15` is not rejected merely because vanilla `Enum.GetValues(EquipmentSlot)` ends at `ArmBand=14`;
- slot15 enumeration is viable when the live equipment instance actually owns cache index 15, which is the condition created by the server-side dedicated-slot registration;
- if the live cache lacks index 15, `GetItemsInSlots(slot15)` fails inside the existing scoped bridge exception boundary and the complete vanilla reload result is retained;
- the current bridge therefore remains correctly placed at `Inventory.GetItemsInSlots`: it preserves the vanilla result/order and performs one bounded slot15 query only while Reload/QuickReload scope and the exact retained/installed FastAccess or BindAvailable slot-array reference are active;
- if a future physical candidate still fails, the next diagnostic split is no longer “enum 14 vs pseudo 15”. It is: (a) runtime equipment cache does not contain slot15, (b) Reload/QuickReload does not call `GetItemsInSlots` with one of the exact recognized slot-array references, or (c) the returned slot15 compound tree differs from the decompiled SPT 4.x contract. Do not widen to structural-array matching or broad inventory scans without evidence for one of those boundaries.

`ReloadPseudoSlotEnumerationContractRegression` mirrors the exact root-prefix/compound-descendant ordering and the missing-cache-index failure boundary so this architecture assumption cannot silently regress while runtime acceptance remains batched.
