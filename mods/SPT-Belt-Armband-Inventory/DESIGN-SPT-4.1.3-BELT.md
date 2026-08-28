# B&A&HB — SPT 4.1.3 wearable runtime design

## Status

This note records the **current mechanical architecture** of PR #64. Earlier revisions that treated Belt as only an `ArmBand` projection or described HeadBand as a `1x1` medical container are historical and superseded by the implementation below.

Gameplay sizing/balance is not declared final here. The final product-design pass for ArmBand, Belt and HeadBand remains intentionally separate from mechanical stabilization.

## Persistent equipment model

B&A&HB now owns three wearable families:

1. **ArmBand** — vanilla `ArmBand` equipment host. Current RC searchable container is `1x2` magazine-only; Wrist Wallet is a separate persistent ArmBand-family item.
2. **Belt** — dedicated SPT/EFT pseudo-enum equipment value **15**, semantic identity `BAndHBBelt`, wire slot ID `15`. Current Magazine Belt is `2x2` magazine-only.
3. **HeadBand** — dedicated pseudo-enum equipment value **16**, semantic identity `BAndHBHeadBand`, wire slot ID `16`. Current Utility HeadBand is `1x2` and exact-item whitelist only.

All distributed template, parent, slot, grid and assort IDs are persistent contracts recorded in `profile-safety/persistent-identities.json`. Existing IDs must not be renamed, recycled or silently removed.

SPT 4.1.x constructs `InventoryEquipment` through the closed `EquipmentSlot` enum. Dedicated Belt/HeadBand locations therefore use collision-checked numeric values 15/16 on the wire while retaining human-readable semantic identities inside B&A&HB. Server registration does not reorder vanilla slots; client presentation owns visible placement.

## Native container path

The successful ArmBand proof established the important rendering rule: B&A&HB searchable containers use EFT's native generated-grid path. No unsupported `GridLayoutComponent` or external custom-layout prefab is required.

The dedicated Belt and HeadBand items retain the same principle:

- server-backed grid metadata is authoritative;
- custom runtime item/template identity is registered before item construction where required;
- `GridWindow`/generated grids render the actual declared dimensions;
- bounded exact-fit correction may settle late EFT layout writes after a window opens;
- there is no permanent `ItemView.Update` or `MonoBehaviour.Update` polling loop.

Current calibrated exact-fit targets are derived from declared cell geometry: `1x2` = `73x158`, `2x2` = `136x158`.

## Dedicated slot filters

The two added equipment slots are exact-template scoped:

- slot 15 accepts only the current dedicated Magazine Belt template;
- slot 16 accepts only the current Utility HeadBand template.

Shared searchable/container parent identities never make ArmBand/Wrist Wallet equipable in those dedicated locations.

HeadBand's internal grid is also exact-item scoped. Current whitelist:

- RUB;
- USD;
- EUR;
- Apollo Soyuz cigarettes;
- Malboro;
- Wilston;
- Strike;
- small vanilla Wallet.

The Apollo Soyuz TPL is explicitly regression-guarded against the historical accidental golden-neck-chain TPL. Broad medical or generic loot parents are not accepted.

## Presentation boundary

Dedicated UI is lifecycle-owned, not polled:

- Belt visual anchor: between Pockets and Backpack;
- HeadBand visual anchor: immediately above Headwear;
- client slot registration maps numeric 15/16 into the dedicated runtime locations;
- `SlotView.Show` owns HeadBand visual creation;
- exact caption normalization prevents visible raw numeric IDs;
- optional late UI repairs are bounded to the concrete factory-created row/window rather than scanning scenes or inventories.

## Scav boundary

SPT 4.1.3 Scav `ReplaceInventory` does not guarantee the old property-only shape. B&A&HB resolves the required members once at startup:

- `Inventory`;
- `Equipment`;
- `ContainedItem`;
- `Deleted`;
- `StringTemplateId`.

Each may bind as a compatible property or field. Dynamic delegates are cached after discovery. The `ReplaceInventory` postfix inspects only ArmBand, Belt15 and HeadBand16 and restores only descriptors with the explicit Scav-host restoration capability. No reflection scan occurs per replacement and no global inventory polling is introduced.

## Death and insurance boundary

F12 exposes independent `Protected` / `LostOnDeath` settings for ArmBand, Belt and HeadBand, default `Protected`.

Protection is defined by exact `(slot, template)` roots and expands through the full descendant inventory tree. Arbitrary vanilla/third-party items in a wearable-looking slot are not protected. `LostOnDeath` simply removes that family from B&A&HB special retention and leaves normal SPT semantics in control.

The server publishes active roots as an immutable atomic snapshot. The exact SPT 4.1 death-retention and insurance-loss patches are DI-managed and installed as one atomic feature: if either fails to enable, any already-enabled half is rolled back. This prevents death retention and lost-insured processing from diverging.

## Profile safety

Uninstall/recovery is ownership-scoped. Cleanup follows persistent B&A&HB roots, descendants and direct references and must preserve unrelated vanilla/third-party profile data. Backup-first recovery tooling and deterministic fixtures are packaged with the RC.

## Performance contract

Forbidden production behavior:

- permanent `ItemView.Update` polling;
- permanent generic `MonoBehaviour.Update` polling;
- scene-wide object/hierarchy scans;
- repeated reflection scanning in inventory/raid hot paths;
- global inventory scans per frame/update.

Allowed mechanisms are startup discovery, cached delegates, bounded lifecycle hooks, exact Harmony patches and short deferred work that drains to zero.

## Remaining release boundary

Automated gates cover hot-path policy, deterministic lifecycle/identity/filter/death/insurance behavior, offline profile recovery, client/server compilation and install-tree packaging. Once one exact head is GREEN, the remaining combined SPT 4.1.3 physical gate is defined in `docs/RC1-runtime-checklist.md`.

Only after mechanical runtime acceptance does the final design pass decide the long-term gameplay role, dimensions, item variants, balance and visual concept for each of the three wearable families.
