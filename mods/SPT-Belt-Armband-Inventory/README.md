# B&A&HB #2 MOD SPT

Development candidate **v0.2.0** for **SPT 4.1.3**.

Stable **v0.1.0** is already frozen and published separately on `runtime-belt-armband` / tag `bahb-v0.1.0`. This branch develops v0.2.0 without changing that release.

Active authority:
- Issue **#285**
- PR **#286**
- branch `feature/bahb-v0.2-compact-headband`

The v0.2.0 client reports `AssemblyVersion/FileVersion/BepInEx PluginVersion = 0.2.0`. Its physical DLL filename intentionally remains `SPT Belt Armband Inventory v0.1.0.dll` for this upgrade line so extracting the candidate over an installed stable v0.1.0 replaces the existing client file instead of leaving two DLLs with the same BepInEx GUID. CI explicitly forbids a second `...v0.2.0.dll` in the candidate package.

The BepInEx plugin GUID/name remain unchanged for in-place upgrade compatibility. Every CI artifact contains `BUILD-INFO.txt` with the exact head SHA, runtime candidate version, filename-compatibility marker and SHA-256 hashes for both runtime DLLs.

## Candidate install / upgrade

The CI artifact contains one install root: `SPT_Runtime`.

1. Stop the SPT server and game completely.
2. Back up the active SPT profile before the first v0.2.0 launch. v0.2.0 includes a one-way profile migration for the Utility HeadBand split-grid shape and introduces new Dogtag Case persistent identities; the migration preserves valid HeadBand content, but a profile backup remains the rollback boundary.
3. Extract/copy the artifact's `SPT_Runtime` directory **over the existing SPT root**, preserving paths.
4. Confirm the client path is exactly `SPT_Runtime/BepInEx/plugins/SPT Belt Armband Inventory v0.1.0.dll`. The filename is intentionally legacy; its compiled/BepInEx version is v0.2.0.
5. Confirm there is **no** second `SPT Belt Armband Inventory v0.2.0.dll` (or another duplicate B&A&HB client DLL) beside it.
6. Confirm the server path is `SPT_Runtime/user/mods/B&A&HB #2 MOD SPT/SPT-Belt-Armband-Inventory.Server.dll` and that the same directory contains `BUILD-INFO.txt`.
7. For an exact candidate check, compare `BUILD-INFO.txt` `HeadSha`, `ClientSha256` and `ServerSha256` with the handoff evidence before launching SPT.

The published stable v0.1.0 runtime uses those same client/server paths, so an in-place v0.2.0 overlay replaces both runtime DLLs rather than creating a second server-mod directory.

Do not install v0.2.0 by copying only the client or only the server DLL. Client/server code and the profile migration belong to one exact candidate.

### Rollback to stable v0.1.0

Do not simply copy v0.1.0 binaries over a profile after v0.2.0 has migrated/created newer persistent data. Stop SPT, restore the pre-v0.2.0 profile backup when possible, then restore the complete stable v0.1.0 package. If no compatible backup exists, follow `profile-safety/README.md`: preserve a backup/copy and use the current cleanup contract before starting an older build that may not know newer distributed identities.

## Current v0.2 scope

### Products

- **Wrist Wallet** — ArmBand host, `1x1`, currency-only, Ragman LL1, 12,500 RUB.
- **Magazine Armband** — ArmBand host, `1x2`, MAGAZINE-only, Ragman LL1, 25,000 RUB.
- **Magazine Belt** — dedicated slot15, `2x2`, MAGAZINE-only, Ragman LL2, 45,000 RUB.
- **Utility HeadBand** — dedicated slot16, Ragman LL1, 25,000 RUB.
- **Dogtag Case** — existing vanilla Dogtag host, canonical EFT Dogtag Case geometry/filter contract, Ragman LL2, 50,000 RUB.

All five v0.2 products publish explicit **EN and RU** item names, short names and descriptions. Persistent template/grid/assort identities are immutable and parity-checked against the packaged recovery manifest.

### Utility HeadBand v0.2

The HeadBand keeps the existing item/slot identity but now uses **two native `1x1` grids**:

- `main` — RUB, USD, EUR, Simple Wallet and WZ Wallet;
- `cigarettes` — Apollo Soyuz, Malboro, Wilston and Strike.

The original `main` grid identity is preserved. The cigarettes grid has its own persistent identity. Existing v0.1.0 HeadBand contents are migrated through the native SPT profile-migration lifecycle before profile deserialization. Currency/wallet items remain in `main`, cigarettes move to `cigarettes`, and same-category overflow is preserved through the PMC sorting table rather than deleted. For Scav profiles without a sorting table, only actionable normalization is claimed; unclassifiable/overflow children are preserved rather than repeatedly reporting the same migration as pending.

### Dogtag Case v0.2

The Dogtag Case uses the **existing vanilla `Dogtag` equipment slot**; B&A&HB does not invent another equipment enum/slot for it.

- The product clones canonical EFT/SPT Dogtag Case `5c093e3486f77430cb02e593`.
- The vanilla Dogtag host must resolve uniquely with its existing non-empty filter contract before mutation.
- Existing host acceptance is preserved and only the exact B&A&HB Dogtag Case template is appended.
- The internal grid copies canonical Dogtag Case geometry plus include/exclude filter groups exactly; there is no broad generic-item fallback.
- The case stays outside B&A&HB wearable capabilities: no custom death retention, insurance-loss suppression, fast-access or build behavior is granted.
- Its template/grid/assort IDs are nevertheless B&A&HB-owned persistent recovery identities. Offline cleanup removes serialized owned roots plus descendants/direct references from equipment/stash, mail, insurance and build records without crossing into unrelated profile data.

## Compact Face + HeadBand presentation

The v0.2 presentation is local to the **existing FaceCover footprint**:

- FaceCover keeps its original width and is reduced in height;
- HeadBand is placed above FaceCover inside the same outer footprint;
- the host Gear Panel is not resized or translated;
- unrelated native equipment slots are not moved;
- no global Canvas refresh, coroutine retry or idle polling is used;
- the accepted v0.1.0 HeadBand presentation remains the fallback if the compact owner cannot install safely.

## Magazine operational integration

Magazine Armband and Magazine Belt are no longer reserve-only storage in v0.2.

- EFT's native `InventoryController.IsAtReachablePlace(Item)` remains authoritative.
- A vanilla-reachable magazine is never modified by B&A&HB.
- A magazine that is otherwise unreachable may become reachable only when its ancestor chain contains the exact B&A&HB Magazine Armband or Magazine Belt template.
- Wrist Wallet, Utility HeadBand and Dogtag Case are not reload roots.
- Existing vanilla fast-access candidates/order remain the complete priority prefix; the scoped Belt bridge only appends exact Magazine Belt descendants during `Reload()` / `QuickReload()` enumeration, so wearable magazines remain lower-priority fallback sources rather than the preferred source.
- Parent traversal and item-template accessors are resolved and compiled at startup; there are no inventory-wide scans, scene scans, per-frame polling or runtime reflection discovery in the reload path.
- The existing unload-grid integration likewise appends eligible wearable grids after vanilla unload destinations.

If the exact EFT reachability or scoped reload-enumeration boundary cannot be bound, the affected extension fails closed and the wearable containers remain valid storage rather than replacing vanilla behavior.

## Compatibility and fail-safe boundaries

- The persistent taxonomy has one server mutation owner. All three parent nodes are validated/prepared before any `TemplateTable` addition.
- Dedicated slot15/slot16 contracts are likewise validated/prepared together before the canonical inventory slot list is mutated; a collision cannot leave a half-installed Belt-only/HeadBand-only state.
- The ArmBand host must resolve to exactly one vanilla slot with exactly one filter group before its accepted parent is extended.
- The vanilla Dogtag host must likewise resolve uniquely; its existing filter entries remain intact and only the exact Dogtag Case template may be appended.
- New persistent Ragman assort IDs are created only when the item, barter metadata and loyalty metadata are all absent; partial pre-existing ownership is treated as an ID collision rather than overwritten. This applies to all five current offers.
- Historical Trenchfoot-BeltSlot GUID variants are declared as BepInEx soft dependencies so they load before B&A&HB conflict inspection. A confirmed legacy BeltSlot blocks B&A client runtime patching; an unreadable/unknown `Chainloader.PluginInfos` state fails closed rather than assuming no conflict.
- ArmBand/Belt/HeadBand protection defaults server-side to Protected. Client F12 values are reported as synchronized only after the server returns an exact acknowledgement of the applied three-family snapshot; an unacknowledged transport attempt does not become a false success log.

## Stable mechanical boundaries retained

v0.2 preserves the accepted slot15/slot16 lifecycle and protection model:

- slot16 mapping is created/recovered before native `_slotViews` enumeration;
- late `SlotView.Show` cannot add/remove/clone slot-map entries;
- persistent item, parent, grid, assort and dedicated-slot identities remain immutable;
- ArmBand/Belt/HeadBand death and insurance settings keep their existing exact-root behavior;
- Dogtag Case remains deliberately outside that wearable protection/build/fast-access surface;
- Scav compatibility remains bounded and CI-owned;
- no permanent production `Update` polling or scene-wide scans are introduced.

## Development validation

CI for PR #286 owns:

- hot-path/lifecycle guard;
- reload-access fallback/order guard;
- version/build-identity and single-DLL upgrade-path guard;
- atomic persistent taxonomy/dedicated-slot and unique ArmBand/Dogtag host-boundary guards;
- legacy BeltSlot load-order/conflict and acknowledged protection-sync guards;
- persistent assort collision-safety guard for all five offers;
- documentation-authority and persistent recovery-identity guards;
- product-contract and EN/RU localization guard;
- compact-layout guard;
- deterministic regressions, including the real split-grid profile migration, non-repeating Scav migration edge, protection wire acknowledgement, reload fallback policy and Dogtag Case negative capability/recovery ownership;
- offline profile recovery, including Dogtag Case equipment/mail/insurance/build cleanup ownership;
- client build;
- server build against the SPT 4.1.3 package set;
- compiled client/server/SPT server-mod version checks and root/installed BUILD-INFO provenance;
- one installable exact-head RC artifact with exact-head/hash manifest.

A physical runtime handoff is made only after a materially significant bundle is ready, the exact PR head is fully GREEN, and the handoff includes one working GitHub artifact link plus a numbered PASS/FAIL checklist.
