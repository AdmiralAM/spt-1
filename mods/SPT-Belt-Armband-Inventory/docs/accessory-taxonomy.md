# B&A&HB accessory taxonomy and UI contract

This document defines the three shipped wearable categories independently from model meshes and trader presentation.

## Categories

| Category | Logical role | Equipment host | Current host state | Container use | Presentation rule |
|---|---|---|---|---|---|
| `ArmBand` | forearm-mounted compact utility | EFT native `ArmBand` | `Validated` | item-specific | keep the native ArmBand host; expose storage only for exact container-capable B&A&HB items |
| `Belt` | waist-mounted tactical utility | persistent dedicated pseudo-enum value `15`, semantic `BAndHBBelt`, wire slot `15` | `Validated` | expected | dedicated Belt presentation/storage; capacity/filter come from exact item grid contract |
| `HeadBand` | head-mounted micro utility | persistent dedicated pseudo-enum value `16`, semantic `BAndHBHeadBand`, wire slot `16` | `RuntimeCandidate` | exact item-specific | compact dedicated HeadBand presentation; do not mark validated until the bundled v0.2 physical gate passes |

Category support, host implementation and physical validation are separate states. ArmBand and Belt have accepted runtime host evidence. HeadBand has an implemented/persistent host and automated lifecycle coverage, but remains `RuntimeCandidate` until the current v0.2 combined physical gate is accepted.

The category is a gameplay/UI concept. It must not be inferred from a mesh name, trader ID, or a single vanilla parent template. Runtime behavior is selected by exact registered B&A&HB item identity/capabilities plus the appropriate equipment host.

## Current products and capability separation

| Product | Category/host | Storage | Operational capabilities |
|---|---|---|---|
| Wrist Wallet | ArmBand | `1x1`, RUB/USD/EUR | payment source; no magazine reload |
| Magazine Armband | ArmBand | `1x2`, MAGAZINE-only | fast-access/reload fallback |
| Magazine Belt | Belt/slot15 | `2x2`, MAGAZINE-only | loot/unload integration + fast-access/reload fallback |
| Utility HeadBand | HeadBand/slot16 | `main` 1x1 + `cigarettes` 1x1 | micro utility only; no payment/reload/grenade inheritance |

Capabilities are exact-item properties, not automatic category inheritance. In particular, another ArmBand container must not become a reload root merely because Magazine Armband is one, and HeadBand must not inherit Belt tactical semantics.

## Shared runtime contract

Every container accessory must satisfy the same minimum contract:

1. The server item has an explicit parent/type identity and real `Grids` definitions.
2. The client constructs a searchable compound item rather than a plain cosmetic host.
3. `IsContainer`, searchable behavior and client-visible grid dimensions agree at runtime.
4. Ordinary EFT generated-grid / `GridWindow` behavior is retained unless an exact bounded presentation owner is required.
5. Contents persist through equip, unequip, profile reload and the category death/protection policy.
6. Persistent item/parent/grid/slot/assort identities are immutable once distributed.
7. Optional operational capabilities (payment, reload, unload, loot, Scav restoration) are exact-item scoped and must fail closed when their native EFT boundary cannot bind.

## Grid and window geometry

Grid dimensions are authoritative. The UI must never present a larger empty canvas than the declared grid requires.

- `1x1`: one visible cell; compact window.
- `1x2`: one column and two rows; no fake second column/2x2 reserve area.
- `2x2`: two columns and two rows.
- Multiple grids: each grid keeps its own native identity/filter and is rendered independently rather than simulated by invisible cells.

Utility HeadBand v0.2 is the current multiple-grid example: `main` and `cigarettes` are two persistent native 1x1 grids, not a visually split fake 1x2.

The item view and its window are one presentation contract: generated grid, cell size, spacing, caption and padding must follow the same real grid declaration. Do not solve window geometry by changing server capacity or adding invisible cells.

## Capacity bands

Capacity is categorized by function, not visual size alone:

- `HeadBand`: micro utility, narrow filters and separated micro grids where appropriate.
- `ArmBand`: compact utility, normally 1x1/1x2 with focused filters.
- `Belt`: expanded/tactical utility, currently 2x2 MAGAZINE-only rather than generic free storage.

These are design defaults, not permission for broad filters. Every concrete product declares its exact grids and accepted templates/classes.

## Host lifecycle contract

- slot15 and slot16 identities are persistent and must not be renumbered/recycled.
- slot16 map creation/recovery happens before native `EquipmentTab.Show` slot-map enumeration.
- late `SlotView.Show` may bind/present the existing dedicated slot but may not add/remove/clone active map entries.
- compact HeadBand v0.2 stays inside the original FaceCover footprint and does not resize/translate Gear Panel or move unrelated native slots.
- no permanent scene/inventory polling or generic per-frame UI patch is allowed.
- host-state promotion requires physical runtime evidence; CI implementation alone does not promote HeadBand from `RuntimeCandidate` to `Validated`.

## Magazine reachability contract

Magazine Armband and Magazine Belt are appended reload fallback sources only:

- vanilla reachable source/order remains authoritative;
- exact B&A&HB fast-access locations are appended after vanilla fast-access locations;
- only a `Magazine` under an exact registered fast-access wearable ancestor may gain reachability;
- Wrist Wallet and Utility HeadBand are not reload roots;
- parent/template access is startup-bound; no broad scans or runtime reflection discovery in the reload path.

## Implementation / maintenance order

The historical host-discovery phase is complete for ArmBand/Belt and implemented for HeadBand. Ongoing changes follow this order:

1. preserve stable/persistent identities and accepted ArmBand/Belt mechanics;
2. keep HeadBand changes bounded to its runtime-candidate host until physical acceptance;
3. add capabilities only at a native EFT boundary with exact-item scoping and deterministic regressions;
4. update profile migration/recovery before changing distributed storage identity/shape;
5. freeze performance/hot-path and upgrade/package contracts in CI before runtime handoff;
6. add models/icons/visual polish only when they do not redefine the inventory contract.

## Explicit non-goals

- no generic free-inventory accessory family;
- no renumbering/recycling of persistent pseudo-enum/slot identities 15/16;
- no custom layout prefab registration merely to obtain a layout name;
- no per-frame UI/inventory polling or scene-wide scans;
- no broad reload/payment capability inheritance by category;
- no copy of unrelated PackNStrap functionality.
