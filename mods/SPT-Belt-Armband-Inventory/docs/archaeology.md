# Archaeology and SPT 4.1 mapping

Primary references reviewed for Phase 1:

- [Trench-foot/BeltSlot](https://github.com/Trench-foot/BeltSlot) — original MIT implementation for SPT 3.11.
- [WelcomeToThursday/PackNStrap](https://github.com/WelcomeToThursday/PackNStrap) — current content pack with embedded BeltSlot 2.0.x code targeting SPT 4.0.4.
- [SPT 4.x Assembly-CSharp decompilation](https://github.com/Luna-Salamanca/assemblycsharptarkovspt4) — current `ContainersPanel` and inventory extension shapes.

Useful behavior retained:

- `ArmBand` is inserted beside Pockets only for belt/container presentation.
- A belt uses the normal container slot template instead of the dogtag-style template.
- Any compatible container item can work; the adapter does not hardcode Pack 'n' Strap item IDs.

Behavior intentionally replaced:

- No patch on `ItemView.Update`.
- No scene-name table or scene polling.
- No hardcoded obfuscated field such as `equipmentSlot_0`.
- No full replacement of `InventoryController.ReplaceInventory`.
- No manual clone/toggle map for each screen hierarchy.

SPT 4.1 keeps a single `ContainersPanel.Show(...)` route across player inventory, loot, insurance/build/profile contexts. Phase 1 temporarily supplies a belt-aware slot order for that call and restores the original field in a Harmony finalizer. The private slot factory is handled with the same transaction pattern so `ArmBand` receives the default container template without patching the method result type.
