# B&A&HB product concept

## Product thesis

B&A&HB is an accessory-logistics framework, not a generic way to add free
inventory cells. It turns small equipment accessories into deliberately scoped
load-bearing roles while preserving EFT inventory rules, persistence and native
UI behavior.

The useful decision is not simply “more space”. A player chooses where a small
set of supplies lives, how quickly gameplay systems can reach it, and which
equipment role is consumed by that choice. Capacity, accepted item classes and
runtime integrations must therefore be explicit for every accessory.

## Category roles

| Category | Intended decision | Capacity direction | Filter direction | Runtime state |
|---|---|---|---|---|
| `ArmBand` | carry a compact specialist reserve without replacing a main rig | compact | one narrow purpose per item | validated SPT 4.1.3 host |
| `Belt` | organize sustained combat logistics outside the vest's primary layout | expanded, but bounded | combat supply groups chosen per concrete belt | concept only until a real host is proven |
| `HeadBand` | expose a micro head-worn utility role rather than general storage | micro | deliberately restrictive utility set | concept only until a real host is proven |

These roles do not prescribe a mesh, trader item or exact final grid. A concrete
item still owns its dimensions and filters. The category supplies policy and
expectations; it never fabricates a container or an EFT slot by itself.

## Balance rules

1. No universal accessory container. Each item has a narrow logistical purpose.
2. Capacity is paid for through equipment opportunity, item restrictions,
   progression placement, weight and price rather than invisible UI penalties.
3. Fast-access, grenade enumeration, loot placement, payment and death retention
   are separate capabilities. A future category does not automatically inherit
   all current ArmBand integrations.
4. Native EFT inventory behavior remains authoritative. The mod extends
   reachability and ordering only at confirmed boundaries.
5. A declared `1x2` grid is exactly two visible cells. UI padding or a custom
   prefab must never create the impression of a hidden `2x2` capacity.

## What is distinct

Pack 'n' Strap proves that grid-backed wearable accessories are useful, but
B&A&HB's target is a smaller and stricter SPT 4.1.3 architecture:

- one shared category and identity contract across client and server;
- native `GeneratedGridsView` unless a real custom prefab is shipped;
- event-driven lifecycle without `ItemView.Update` polling;
- ownership-safe compatibility patches that fail per feature;
- explicit integration with loot, unload, pickup, payment, fast access, builds,
  Scav handling and death persistence;
- future categories gated by proven hosts instead of invented enum values.

The result should feel like part of EFT's inventory system, not a parallel bag
UI attached to cosmetic items.

## Development gates

### Current ArmBand reference

The current RC remains the proof fixture: custom searchable runtime type,
`MAGAZINE` filter and native one-column/two-row grid. It establishes technical
behavior; its placeholder economy and content are not the final category roster.

### Belt activation gate

Before Belt leaves `ConceptOnly`, static evidence must identify a real equipment
host, serialization path, inventory merge behavior, screen lifecycle and native
presentation boundary. Only then should concrete capacity/filter profiles be
selected.

### HeadBand activation gate

HeadBand additionally needs proof that its chosen host does not conflict with
armor, face cover, eyewear or headset behavior. Its utility purpose must be
valuable with micro capacity; otherwise the category should remain conceptual.

### Visual-content gate

Icons, bundles and 3D models start only after the category role, grid, filters,
host and progression are stable. Visual assets must represent the actual capacity
and cannot be used to compensate for an unclear gameplay purpose.

## Candidate future policies (not yet runtime features)

- category-specific capability profiles instead of granting every integration;
- progression tiers that change specialization, not merely cell count;
- filter presets for emergency magazines, medical reserve or narrow utility;
- compatibility reporting that identifies which optional integration failed;
- exact-grid UI verification as an automated contract once a renderable test
  boundary is available.

Every candidate must first answer three questions: what player decision it adds,
which existing system owns adjacent behavior, and how it can fail closed.
