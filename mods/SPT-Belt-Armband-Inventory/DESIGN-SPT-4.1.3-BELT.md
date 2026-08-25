# B&A&HB — SPT 4.1.3 Belt Runtime Design Note

## Status

PR #64 has passed the native GridWindow container-rendering gate. The RC belt now opens through EFT's default `GeneratedGridsView` and displays its persisted `1x2` contents.

The decisive runtime dump showed that the item, item context and server-backed grid were already correct while `GridWindow._containedGrids` remained `null`. `ContainedGridsView.CreateGrids` selected a custom-layout asset path because the prototype added `GridLayoutComponent` with layout name `B&A&HB-RC-1x2`; no matching client prefab existed. Removing that unnecessary custom-layout component restored the native generated-grid path.

Verified runtime state on commit `4621b5e`:

- the RC belt item exists;
- it equips into `EquipmentSlot.ArmBand`;
- server-side `1x2` MAGAZINE grid metadata exists;
- `ContainersPanel` projection patches install;
- `Slot.MergeContainerWithChildren` postfix installs and returns `EParentMergeType.InheritFromItem` for `ArmBand`;
- no separate BELT row appears;
- the equipped belt opens as a native GridWindow container;
- separate belt instances display their own empty or populated `1x2` grids;
- runtime type, searchable/container contract and grid dimensions all pass.

Therefore the missing behavior is no longer explained by the previously known Harmony installation failures.

## PackNStrap decomposition

PackNStrap has two distinct client layers that matter here.

### 1. Belt-slot presentation layer

`Trenchfoot-BeltSlot` patches `ContainersPanel.method_0` so `EquipmentSlot.ArmBand` receives a cloned default `SlotView`, inserts `ArmBand` into the container-panel equipment-slot order, and refreshes belt/armband visibility based on whether `slot.ContainedItem.IsContainer` is true.

This layer is presentation/inventory-slot plumbing. Its historical implementation also polls `ItemView.Update`, which is explicitly not acceptable for B&A&HB.

### 2. Custom item runtime type layer

Current PackNStrap also defines and registers a dedicated belt item runtime class:

- `CustomBeltItemClass : SearchableItemItemClass`;
- constructor receives `CustomContainerTemplateClass`;
- when the template has a `LayoutName`, it adds `GridLayoutComponent`;
- the class is bound through `[CustomParent(...)]` metadata;
- `RegisterCustomItemTypesPatch` adds `CustomBeltItemClass` to the game's custom item-type list during `GClass3381.Init`.

The custom template derives from `SearchableItemTemplateClass`, implements the layout interface used by the client, and exposes `LayoutName`.

PR #64 now keeps PackNStrap's useful custom searchable-item identity but intentionally does not copy its custom-layout component. PackNStrap owns a matching client layout prefab; B&A&HB does not need one because its single grid is rendered correctly by EFT's default generated-grid template.

## #64 comparison

### What #64 has now

- server clone from vanilla armband parent/template;
- `1x2` MAGAZINE `Grids` metadata;
- ArmBand acceptance/equipment behavior;
- `ContainersPanel` ArmBand projection;
- event/deferred refresh rather than `ItemView.Update` polling;
- inventory reachability / priority compatibility patches;
- `MergeContainerWithChildren` getter result override.

### What #64 now proves

- the RC belt constructs as `CustomBeltSearchableContainer`;
- the client registers the custom template and item mappings before item creation;
- `IsContainer`, searchable behavior and the `1x2` client grid are present;
- `GridWindow` receives the correct belt and item context;
- EFT's default `GeneratedGridsView` renders persisted contents without a custom prefab.

The remaining unproven presentation feature is a separate inline BELT row in `ContainersPanel`. It is independent of the now-working native GridWindow container path.

## Design decision

Preserve the working native GridWindow path. Treat the separate inline BELT row as an optional presentation slice and do not let it gate container functionality.

### Recommended architecture

1. Keep `ArmBand` as the real equipment host. Do not add a new EFT equipment enum slot.
2. Introduce a minimal B&A&HB client belt item class equivalent in role to PackNStrap's `CustomBeltItemClass`:
   - derive from the SPT 4.1.3 searchable/container item base actually used by the current client;
   - expose the server-backed `Grids` through the searchable compound-item base;
   - no extra PackNStrap features.
3. Register only the minimum corresponding template class required by SPT 4.1.3; do not add `GridLayoutComponent` unless a matching client prefab is shipped.
4. Register that custom item type through the current SPT 4.1.3 item-type registration boundary before item construction/deserialization.
5. Make the RC server item identify/parent itself in the minimal way required for that client class to be selected. Do not otherwise change grid/filter/trader semantics.
6. Retain the proven `MergeContainerWithChildren` result override for `ArmBand`.
7. Re-evaluate how much of the current `ContainersPanel` projection is still required after the equipped item is a genuine client container. Prefer native `ContainersPanel` behavior; keep only the smallest ArmBand-specific presentation interception that remains necessary.

## Verified runtime proof

- concrete client item type name;
- `IsContainer` value;
- searchable/container base/interface membership;
- absence of an unsupported custom `GridLayoutComponent`;
- template concrete type and default generated-grid renderer selection;
- client-visible grid count and dimensions;
- ArmBand slot `ContainedItem` is that same custom belt instance.

## Explicit non-goals

- no full PackNStrap port;
- no `ItemView.Update` polling;
- no old SPT 4.0.x field/layout assumptions without 4.1.3 proof;
- no Phase 2;
- no new belt features;
- no change to current `1x2` MAGAZINE filter/trader behavior except the minimum template/type identity required to instantiate the correct client runtime class.

## Remaining physical checks

Container rendering and existing contents are proven. Remaining checks are magazine insertion/removal, remove/re-equip persistence, raid transitions, and the optional separate inline BELT row.
