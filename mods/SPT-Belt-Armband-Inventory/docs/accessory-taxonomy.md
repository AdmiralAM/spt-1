# B&A&HB accessory taxonomy and UI contract

This document is the foundation for the three planned accessory categories. It is
deliberately independent from model meshes and from any particular trader item.

## Categories

| Category | Logical role | Equipment host | Container use | Presentation rule |
|---|---|---|---|---|
| `ArmBand` | forearm-mounted compact utility | EFT `ArmBand` | optional | keep the native ArmBand host; expose the container only when the equipped item is actually container-capable |
| `Belt` | waist-mounted general utility | future validated belt host | expected | use the same searchable compound-item contract, with capacity driven by its real grid definition |
| `HeadBand` | head-mounted compact utility | future validated head/face host | optional | use the same item contract, but keep capacity and interaction deliberately smaller than a Belt |

The category is a gameplay/UI concept. It must not be inferred from a mesh name,
trader ID, or a single vanilla parent template. Runtime behavior is selected by
the registered client item type plus the validated equipment host.

## Shared runtime contract

Every container accessory must satisfy the same minimum contract:

1. The server item has an explicit parent/type identity and a real `Grids`
   definition.
2. The client constructs a searchable compound item, not a plain cosmetic host
   item.
3. `IsContainer`, the searchable contract, and the client-visible grid dimensions
   agree at runtime.
4. The ordinary EFT `GeneratedGridsView` is used whenever the category does not
   ship a deliberately registered custom layout prefab.
5. Contents persist through equip, unequip, profile reload and the existing death
   policy for that category.

The current ArmBand Belt RC is the reference implementation for this contract.
Its `1x2` grid is rendered by the native generated-grid path. A custom
`GridLayoutComponent` must not be added merely to expose a layout name: that
component selects an asset by layout identifier and returns no view when the
matching client prefab is absent.

## Grid and window geometry

Grid dimensions are authoritative. The UI must never present a larger empty
canvas than the grid requires.

- `1x1`: one visible cell; compact window.
- `1x2`: one column and two rows; the window height may grow for two cells, but
  must not reserve a second column or a 2x2 area.
- `2x2`: two columns and two rows; the window may expand in both directions.
- `2xN`: preserve the two-column width and grow only in rows.

The item view and its window are one presentation contract: the generated grid,
cell size, spacing, caption and padding must be measured from the same template.
Do not solve a wrong window size by adding invisible cells or by changing the
server grid dimensions.

## Capacity bands

Capacity is categorized by function, not by visual size alone:

- `HeadBand`: micro utility; normally `1x1` or `1x2`, narrow item filters.
- `ArmBand`: compact utility; normally `1x2` or `2x2`, focused filters.
- `Belt`: expandable utility; normally `2xN` or several intentionally separated
  grids, with the broadest filter set.

These are design defaults, not hardcoded limits. A concrete item must declare its
grid and filters explicitly, and the UI must render that declaration exactly.

## Implementation order

1. Keep and clean the proven ArmBand runtime/container path.
2. Extract category-neutral policies from ArmBand-specific reflection and slot
   code without changing the working runtime behavior.
3. Add Belt as a second host only after its actual EFT slot boundary is proven.
4. Add HeadBand only after the shared contract works for both a compact and an
   expanded accessory.
5. Add models, icons and visual polish last; the category and UI contract must
   remain valid without them.

## Explicit non-goals

- no 3D model implementation in this phase;
- no invented EFT equipment enum values;
- no custom layout prefab registration until a real prefab and asset key exist;
- no per-frame UI polling;
- no copy of PackNStrap features that are not required by the shared contract.
