# B&A&HB — SPT 4.1.3 wearable runtime design

## Authority

Stable v0.1.0 is already frozen/published and remains the rollback release. Active development candidate is **v0.2.0** under Issue #285 / PR #286, with magazine reload integration tracked by Issue #287.

## Persistent equipment model

B&A&HB owns three wearable families plus one vanilla-hosted container product:

1. **ArmBand** — vanilla ArmBand host. Wrist Wallet (`1x1`, currency-only) and Magazine Armband (`1x2`, MAGAZINE-only).
2. **Belt** — dedicated pseudo-enum equipment value **15**, semantic identity `BAndHBBelt`, wire slot `15`. Magazine Belt is `2x2`, MAGAZINE-only.
3. **HeadBand** — dedicated pseudo-enum equipment value **16**, semantic identity `BAndHBHeadBand`, wire slot `16`. Utility HeadBand v0.2 uses two native `1x1` grids.
4. **Dogtag Case** — separate persistent container item hosted by vanilla `EquipmentSlot.Dogtag`; it does not create another equipment enum value and does not replace ordinary personal dogtags.

All distributed template, parent, slot, grid and assort IDs are persistent contracts. Existing identities must not be renamed, recycled, silently removed or overwritten through partial metadata collisions.

## v0.2 product contract

| Product | Host | Grid/filter | Operational role | Ragman |
| --- | --- | --- | --- | --- |
| Wrist Wallet | ArmBand | `1x1`, currency-only | reserve/payment source | LL1 — 12,500 RUB |
| Magazine Armband | ArmBand | `1x2`, MAGAZINE-only | magazine storage + vanilla-first reload fallback | LL1 — 25,000 RUB |
| Utility HeadBand | slot16 | two native `1x1` grids | money/wallet + cigarette utility | LL1 — 25,000 RUB |
| Magazine Belt | slot15 | `2x2`, MAGAZINE-only | magazine storage + vanilla-first reload fallback | LL2 — 45,000 RUB |
| Dogtag Case | vanilla Dogtag | canonical EFT/SPT Dogtag Case-derived dogtag-only grid | wearable dogtag container | LL2 — 50,000 RUB |

Utility HeadBand grids:

- `main` — RUB, USD, EUR, Simple Wallet `5783c43d2459774bbe137486`, WZ Wallet `60b0f6c058e0b0481a09ad11`;
- `cigarettes` — Apollo Soyuz, Malboro, Wilston, Strike.

Broad medical, barter, money-container and generic CASE parents are not accepted. Dogtag Case instead clones the canonical EFT/SPT Dogtag Case geometry/filter authority and rejects positive admission of every B&A&HB-owned product, including itself. All five products publish explicit EN/RU item localization without changing persistent identities.

## Split-grid profile migration

v0.1.0 profiles may contain Utility HeadBand children in the old single `main` `1x2` grid. v0.2 uses SPT's native raw-profile migration lifecycle before profile deserialization.

Migration rules:

- preserve the original `main` grid identity for currency/wallet;
- move one cigarette child to the new persistent `cigarettes` grid;
- normalize retained 1x1 children to grid origin;
- never delete same-category overflow; on PMC move overflow root to sorting table and preserve its descendant subtree;
- Scav profiles without a sorting table claim only actionable normalization; unknown/overflow children are preserved and do not leave the same migration permanently pending on every profile load;
- migration is regression-tested for idempotence and compiled against the SPT 4.1.3 server package set.

## Server registration ownership / collision boundary

Persistent server mutations are deliberately single-owner and prepare-before-commit:

- `WearableTaxonomyRegistration` at `Preload` is the only owner allowed to create the three persistent parent nodes. All existing nodes are validated and all missing nodes are prepared before the first `TemplateTable` addition.
- Later item registration validates the taxonomy; it does not recreate it.
- Magazine Armband extends the vanilla ArmBand filter only when exactly one ArmBand slot and exactly one filter group are proven.
- dedicated slot15/slot16 registration validates/prepares both slot contracts before mutating the canonical inventory slot list, preventing Belt-only/HeadBand-only partial installation after a collision.
- Dogtag Case derives its root/grid/filter contract from canonical EFT/SPT Dogtag Case `5c093e3486f77430cb02e593`. Canonical root properties, Grids/Filters wrappers, grid/filter-group objects and include/exclude sets are value-checked and exact-reference pinned around source identity before cloning.
- vanilla Dogtag host mutation preserves ordinary BEAR/USEC personal dogtags plus compatible captured foreign baseline entries and admits only the exact B&A&HB Dogtag Case from the B&A&HB family.
- Dogtag preload and trader publication re-prove the complete live `DefaultInventory -> Properties -> Slots -> Dogtag slot -> Properties -> Filters -> group -> HashSet` ownership chain before accepting the host contract.
- persistent Ragman offer creation requires the assort item, barter metadata and loyalty metadata to be simultaneously absent. If only part of a persistent ID is already owned, registration fails rather than overwriting an existing dictionary entry.
- Dogtag Ragman publication additionally captures `Trader.Assort -> Items -> BarterScheme -> LoyalLevelItems`; retained/new tuple validation and final reproof execute only against those captured wrappers, while wrapper identity is re-proven through publication. Rollback removes only exact reference-owned item/barter state and removes value-only loyalty metadata only while both reference-owned tuple components remain ours.

## Dedicated slot lifecycle

The accepted v0.1.0 slot lifecycle is unchanged:

- slot15/slot16 are exact-template scoped;
- slot16 is inserted/recovered in the `EquipmentTab.Show` prefix before native `_slotViews` enumeration;
- live slot16 mappings are preserved; only stale Unity-null mappings are replaced pre-enumeration;
- `SlotView.Show` binds the already-mapped slot16 and cannot Add/Remove/clone the active map;
- exact captions prevent visible raw numeric IDs;
- no permanent scene/inventory polling is introduced.

Dogtag Case intentionally does not participate in the dedicated-slot UI lifecycle; it uses the native Dogtag host.

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

## Magazine reload reachability and candidate bridge

v0.2 extends the existing fast-access owner rather than adding a parallel reload subsystem.

- `Inventory.FastAccessSlots` and `BindAvailableSlotsExtended` preserve the complete vanilla sequence and append ArmBand plus dedicated Belt afterward.
- Exact `InventoryController.IsAtReachablePlace(Item)` is the narrow eligibility boundary. A vanilla `true` result is never changed.
- Only an otherwise-unreachable `Magazine` with an exact B&A&HB `FastAccess` ancestor may be promoted; the registered roots are Magazine Armband and Magazine Belt.
- Wrist Wallet, Utility HeadBand and Dogtag Case do not own `FastAccess` and cannot become reload roots.
- Reload/QuickReload keep the complete vanilla candidate sequence as strict priority prefix. Magazine Belt descendants are appended only through one scoped pseudo-slot15 `GetItemsInSlots` query; Magazine Armband remains on the native ArmBand path.
- The pinned SPT 4.1.x query boundary is exactly `IEnumerable<Item> GetItemsInSlots(IEnumerable<EquipmentSlot>)`. Array/non-generic/lookalike return or parameter shapes fail closed.
- Candidate bridge activation accepts only the exact retained/installed `FastAccessSlots` and `BindAvailableSlotsExtended` array references, with install-time content snapshots re-proved at four bounded execution stages.
- `GetItemsInSlots`, the one-value pseudo-slot15 argument, `ItemType`, `MagazineType`, declared `ReturnType`, `GetAllParentItems` and `ReadTemplateId` are captured once at bridge entry. All lazy vanilla/Belt processing uses those locals only; static identities are re-proved at contract entry, pre-query, post-query/pre-Belt-enumeration and post-lazy-Belt-enumeration/pre-publication.
- The reflective fallback invokes only the captured `MethodInfo` with the captured pseudo-slot argument. There is exactly one fallback query and no redirect/retry after drift.
- Candidate append accepts only runtime magazines with exact Magazine Belt ancestry. Reference dedup suppresses the same object twice while preserving distinct same-template magazine objects.
- Any unsupported shape, mutable authority drift, ambiguous ancestry/query contract or exception returns the exact incoming vanilla result object.
- `GetAllParentItems` and the item template-ID reader are discovered/bound once during installation and executed through cached compiled delegates; there is no runtime reflection discovery in the reload hot path.
- No inventory-wide scan, scene scan, per-frame polling or replacement reload-selection subsystem is introduced.

This design remains vanilla-first: existing reachable sources and their order stay authoritative; wearable locations are fallback eligibility/candidate extensions, never preferred replacements.

## Dogtag Case lifecycle / profile safety

Dogtag Case is intentionally isolated from the three wearable protection families:

- it is not a B&A&HB death-retention root;
- it is not removed from insurance loss through the ArmBand/Belt/HeadBand protection policy;
- it is not a build-container or fast-access root;
- ordinary personal BEAR/USEC dogtags remain valid native Dogtag-slot contents and are not targeted by B&A&HB profile cleanup;
- recovery owns only exact persistent B&A&HB IDs, removes transitive descendants/references of removed owned roots, is cardinality guarded and idempotent;
- RuntimeIdentity constants, compiled manifest and shipped recovery JSON remain parity-checked so a distributed persistent identity cannot silently diverge across runtime/recovery surfaces.

## Legacy BeltSlot compatibility boundary

BepInEx `Chainloader.PluginInfos` contains already-loaded plugins only. Therefore B&A declares both historically recognized BeltSlot GUID forms (`com.trenchfoot.beltslot` and `BeltSlot`) as soft dependencies so a present legacy plugin is ordered before B&A conflict inspection.

Conflict policy:

- inspect the public `Chainloader.PluginInfos` API after dependency ordering;
- match the known GUID keys and metadata fallback names;
- confirmed BeltSlot => B&A installs no wearable client runtime patches for the session;
- unreadable/exceptional PluginInfos => fail closed and install no wearable client runtime patches;
- only a successfully inspected, conflict-free dictionary permits B&A runtime patch installation.

## Death / insurance and F12 sync boundary

Independent F12 `Protected` / `LostOnDeath` settings for ArmBand, Belt and HeadBand retain one exact-root policy shared by death retention and insurance-loss filtering. Protection is exact-root scoped and expands through full descendant trees.

Server defaults are Protected. Client synchronization is acknowledgement-based: the server applies the three-family snapshot and returns the same deterministic wire-contract JSON; the client reports success only when that response exactly acknowledges the requested snapshot. A thrown, empty or mismatched response is not logged as a successful settings sync; the server's current/default policy remains authoritative.

The historical `DEFAULT_VALUE` insurer incident belongs to Admiral Trader and is not handled inside B&A&HB.

## Scav / performance boundary

Scav `ReplaceInventory` compatibility remains bounded and CI-owned. Runtime member discovery is startup-only with cached delegates. Forbidden production behavior remains: permanent `ItemView.Update`/generic `MonoBehaviour.Update` polling, scene-wide scans, global per-frame inventory scans, repeated reflection in guarded hot paths, host-panel resize and global Canvas rebuilds.

## Candidate identity / upgrade boundary

Runtime identity advances to **v0.2.0** in client/server assembly metadata, SPT server `IModMetadata.Version` and BepInEx `PluginVersion`; plugin GUID/name stay unchanged. The client physical filename intentionally remains `SPT Belt Armband Inventory v0.1.0.dll` for this upgrade line so extraction over stable v0.1.0 overwrites the same path rather than leaving two DLLs with one BepInEx GUID.

The published stable v0.1.0 runtime was independently checked and uses the same client path plus `SPT_Runtime/user/mods/B&A&HB #2 MOD SPT/SPT-Belt-Armband-Inventory.Server.dll`, so a v0.2 overlay replaces both runtime binaries in place.

CI forbids a second `...v0.2.0.dll`, verifies compiled client/server `FileVersion` values are `0.2.0.0`, compiles/regresses server `ModMetadata.Version = 0.2.0`, hashes both DLLs, and stamps exact head/branch/PR/version/hash data into `BUILD-INFO.txt` at both artifact root and installed server-mod path.

## Release gate

CI owns:

- hot-path/lifecycle guard;
- reload-access eligibility/order/execution-snapshot guard;
- v0.2 version/upgrade/provenance guard;
- atomic taxonomy/dedicated-slot, unique ArmBand/Dogtag host and persistent assort collision guards;
- BeltSlot dependency-order/conflict guard;
- acknowledged protection-sync guard;
- documentation-authority guard;
- product/localization contract;
- compact-layout ownership;
- split-grid migration and deterministic regressions;
- Dogtag canonical/host/trader/profile lifecycle regressions;
- offline recovery;
- client/server builds against SPT 4.1.3;
- compiled binary/server-mod version verification;
- exact-head packaging with SHA-256/provenance.

Physical runtime acceptance is one combined gate from `docs/RC1-runtime-checklist.md` only after the exact PR head is fully GREEN. It covers compact first render, split cells/migration, roster/localization, vanilla-first reload, wearable reload fallback, Dogtag Case host/container behavior and one PMC lifecycle; the accepted v0.1.0 death/insurance matrix is not repeated without concrete regression evidence.
