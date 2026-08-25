# B&A&HB — SPT 4.1.3 Belt Runtime Design Note

## Status

PR #64 is a diagnostic/prototype branch, not a merge candidate. Physical Gate A failed even after the known Harmony installation blockers were resolved.

Observed runtime state on the exact-SHA artifact:

- the RC belt item exists;
- it equips into `EquipmentSlot.ArmBand`;
- server-side `1x2` MAGAZINE grid metadata exists;
- `ContainersPanel` projection patches install;
- `Slot.MergeContainerWithChildren` postfix installs and returns `EParentMergeType.InheritFromItem` for `ArmBand`;
- no separate BELT row appears;
- the equipped belt is not usable/openable as an equipment container.

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

This is materially different from PR #64's RC item, which is currently cloned from a vanilla armband template and only receives server-side `Grids` data. The prototype therefore has container-looking data without evidence that the EFT client instantiates it as the searchable/container-capable item runtime type expected by normal container UI.

## #64 comparison

### What #64 has now

- server clone from vanilla armband parent/template;
- `1x2` MAGAZINE `Grids` metadata;
- ArmBand acceptance/equipment behavior;
- `ContainersPanel` ArmBand projection;
- event/deferred refresh rather than `ItemView.Update` polling;
- inventory reachability / priority compatibility patches;
- `MergeContainerWithChildren` getter result override.

### What #64 does not currently prove

- that the RC belt deserializes/constructs as an EFT `SearchableItemItemClass`-equivalent runtime object;
- that its template implements the client layout contract used to create a `GridLayoutComponent`;
- that the client custom item type is registered before template/item creation;
- that `ContainedItem.IsContainer` is true for the RC belt at the point `ContainersPanel` builds the row;
- that the item exposes the same container/searchable interfaces/components as native rigs/backpacks/PackNStrap belts.

The failed physical Gate A is consistent with this missing item-runtime-type boundary: slot projection can be correctly installed yet never produce a usable container row if the equipped object is still instantiated as an armband-style item rather than a searchable grid-bearing item.

## Design decision

The next implementation should not continue extending the old projection patch chain first.

The next narrow proof should establish the item runtime type boundary.

### Recommended architecture

1. Keep `ArmBand` as the real equipment host. Do not add a new EFT equipment enum slot.
2. Introduce a minimal B&A&HB client belt item class equivalent in role to PackNStrap's `CustomBeltItemClass`:
   - derive from the SPT 4.1.3 searchable/container item base actually used by the current client;
   - construct the grid/layout component from the item's template;
   - no extra PackNStrap features.
3. Introduce/register only the minimum corresponding template class/layout contract required by SPT 4.1.3.
4. Register that custom item type through the current SPT 4.1.3 item-type registration boundary before item construction/deserialization.
5. Make the RC server item identify/parent itself in the minimal way required for that client class to be selected. Do not otherwise change grid/filter/trader semantics.
6. Retain the proven `MergeContainerWithChildren` result override for `ArmBand`.
7. Re-evaluate how much of the current `ContainersPanel` projection is still required after the equipped item is a genuine client container. Prefer native `ContainersPanel` behavior; keep only the smallest ArmBand-specific presentation interception that remains necessary.

## Required proof before another Gate A artifact

Before issuing another physical-test artifact, add diagnostics/tests that prove, for the RC belt at runtime:

- concrete client item type name;
- `IsContainer` value;
- searchable/container base/interface membership;
- number/type of item components, specifically presence of a grid/layout component;
- template concrete type and layout name/identifier;
- client-visible grid count and dimensions;
- ArmBand slot `ContainedItem` is that same custom belt instance.

If these facts cannot be established, stop rather than adding more UI patches.

## Explicit non-goals

- no full PackNStrap port;
- no `ItemView.Update` polling;
- no old SPT 4.0.x field/layout assumptions without 4.1.3 proof;
- no Phase 2;
- no new belt features;
- no change to current `1x2` MAGAZINE filter/trader behavior except the minimum template/type identity required to instantiate the correct client runtime class.

## Gate for the next implementation

Only after the runtime-type proof succeeds should a new Gate A artifact be produced. Gate A remains:

1. equip RC belt into ArmBand;
2. native inventory shows a BELT/container row;
3. existing contents are visible;
4. magazine can move into and out of the `1x2` grid;
5. remove belt -> row disappears;
6. re-equip -> row returns with contents intact.
