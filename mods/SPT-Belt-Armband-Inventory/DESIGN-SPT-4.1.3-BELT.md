# B&A&HB — SPT 4.1.3 wearable runtime design

## Authority

Stable v0.1.0 is already frozen/published and remains the rollback release. Active v0.2 development is Issue #285 / PR #286.

## Persistent equipment model

B&A&HB owns three wearable families:

1. **ArmBand** — vanilla ArmBand host. Wrist Wallet (`1x1`, currency-only) and Magazine Armband (`1x2`, MAGAZINE-only).
2. **Belt** — dedicated pseudo-enum equipment value **15**, semantic identity `BAndHBBelt`, wire slot `15`. Magazine Belt is `2x2`, MAGAZINE-only.
3. **HeadBand** — dedicated pseudo-enum equipment value **16**, semantic identity `BAndHBHeadBand`, wire slot `16`. Utility HeadBand v0.2 uses two native `1x1` grids.

All distributed template, parent, slot, grid and assort IDs are persistent contracts. Existing identities must not be renamed, recycled or silently removed.

## v0.2 product contract

| Product | Host | Grid/filter | Ragman |
| --- | --- | --- | --- |
| Wrist Wallet | ArmBand | `1x1`, currency-only | LL1 — 12,500 RUB |
| Magazine Armband | ArmBand | `1x2`, MAGAZINE-only | LL1 — 25,000 RUB |
| Utility HeadBand | slot16 | two native `1x1` grids | LL1 — 25,000 RUB |
| Magazine Belt | slot15 | `2x2`, MAGAZINE-only | LL2 — 45,000 RUB |

Utility HeadBand grids:

- `main` — RUB, USD, EUR, Simple Wallet `5783c43d2459774bbe137486`, WZ Wallet `60b0f6c058e0b0481a09ad11`;
- `cigarettes` — Apollo Soyuz, Malboro, Wilston, Strike.

Broad medical, barter, money-container and generic CASE parents are not accepted.

## Split-grid profile migration

v0.1.0 profiles may contain Utility HeadBand children in the old single `main` `1x2` grid. v0.2 uses SPT's native raw-profile migration lifecycle before profile deserialization.

Migration rules:

- preserve the original `main` grid identity for currency/wallet;
- move one cigarette child to the new persistent `cigarettes` grid;
- normalize retained 1x1 children to grid origin;
- never delete same-category overflow; on PMC move overflow root to sorting table and preserve its descendant subtree;
- preserve unknown Scav children rather than corrupting profiles where no sorting table exists;
- migration is regression-tested for idempotence and compiled against the SPT 4.1.3 server package set.

## Dedicated slot lifecycle

The accepted v0.1.0 slot lifecycle is unchanged:

- slot15/slot16 are exact-template scoped;
- slot16 is inserted/recovered in the `EquipmentTab.Show` prefix before native `_slotViews` enumeration;
- live slot16 mappings are preserved; only stale Unity-null mappings are replaced pre-enumeration;
- `SlotView.Show` binds the already-mapped slot16 and cannot Add/Remove/clone the active map;
- exact captions prevent visible raw numeric IDs;
- no permanent scene/inventory polling is introduced.

## Compact Face + HeadBand presentation

v0.2 installs one exact `EquipmentTab.Show` postfix as the compact presentation owner. The old stable HeadBand presentation remains compiled as fail-safe fallback and is suppressed only after the compact patch installs successfully.

Final geometry contract:

- preserve the original FaceCover **outer footprint**;
- FaceCover keeps original width and is reduced to roughly half-height;
- HeadBand keeps `44 px` height with a `4 px` local gap and sits above FaceCover inside the same footprint;
- no Gear Panel resize/translation;
- no unrelated native equipment slot movement;
- no `LayoutElement.preferredHeight`, Canvas force-refresh, coroutine retry or idle polling;
- if compact ownership cannot install safely, accepted stable presentation remains active.

## Death / insurance boundary

Independent F12 `Protected` / `LostOnDeath` settings for ArmBand, Belt and HeadBand remain unchanged. Protection is exact-root scoped and expands through full descendant trees. Death retention and insurance-loss filtering consume the same immutable root policy and are not reopened by v0.2 presentation/product work.

The historical `DEFAULT_VALUE` insurer incident belongs to Admiral Trader and is not handled inside B&A&HB.

## Scav / performance boundary

Scav `ReplaceInventory` compatibility remains bounded and CI-owned. Runtime member discovery is startup-only with cached delegates. Forbidden production behavior remains: permanent `ItemView.Update`/generic `MonoBehaviour.Update` polling, scene-wide scans, global per-frame inventory scans, repeated reflection in guarded hot paths, host-panel resize and global Canvas rebuilds.

## Release gate

CI owns hot-path/lifecycle guard, product contract, compact-layout ownership, split-grid migration regression, offline recovery, client/server builds and exact-head packaging. Physical runtime acceptance is one combined gate from `docs/RC1-runtime-checklist.md` only after the exact PR head is fully GREEN.
