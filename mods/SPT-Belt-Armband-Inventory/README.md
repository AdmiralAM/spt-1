# B&A&HB #2 MOD SPT

Stable **v0.3.0**, compatible with **SPT 4.1.x** and validated against **SPT 4.1.5**.

## Optional compatibility API

The client assembly exposes `SPTBeltArmbandInventory.BeltAccessApi` contract version `1` for optional compatibility suites. Consumers first check `IsAvailable`, then call `TryEnumerateBeltSources(object inventory, out object[] sources)` with the live EFT `Inventory` instance. A successful call returns a bounded identity-preserving snapshot of the equipped slot15 Belt root and its contents; `false` always returns an empty array. The API is published only after Belt's exact reload/access binding succeeds and is revoked with that owner. Consumers must not reproduce Belt reflection, patch reload, or infer slot ownership when the API is unavailable.

Use Items Anywhere list adaptation is Suite-owned. B&A&HB no longer detects or mutates that foreign plugin's configuration; the external idempotent adapter keeps slot15 following ArmBand.

TGC 3.0.0 and Pack 'n' Strap 2.1.1 discovery/adapter ownership is also Suite-owned through `ExternalCompatibilityApi` contract version `1`. The Suite claims the exact integration with `TryClaimTgc300(1, ownerToken)` or `TryClaimPackNStrap211(1, ownerToken)` before Belt initialization. Missing Suite, contract mismatch and a second owner fail closed without enabling foreign integration. See [`docs/external-compatibility-audit.md`](docs/external-compatibility-audit.md) for the active/historical seam inventory.

The private runtime direction imports Pack 'n' Strap belts, containers, models
and layouts into B&A&HB while keeping all third-party assets and item databases
out of this repository. The public source remains buildable without Pack 'n'
Strap or WTT CommonLib. `tools/Import-PackNStrapLocal.ps1` builds an opt-in
private variant from an existing local source checkout and installation, routes
the imported belt parent to dedicated slot15, deploys B&A&HB, and preserves then
disables the original Pack 'n' Strap runtime outside active mod/plugin paths.
The imported template IDs remain unchanged for the later profile-safety gate.
See
[`docs/packnstrap-compatibility.md`](docs/packnstrap-compatibility.md) for the
pinned audit, conflict matrix, ownership boundary and next implementation stage.

No Pack 'n' Strap source, JSON, model, icon or bundle is committed or packaged by
this repository. The local importer consumes the user's own copy only. Imported
foreign roots never enter the Admiral protection allowlist.

Stable v0.3.0 includes the v0.2 product line plus randomized functional ArmBands, wallet logistics, the crimson Utility HeadBand visual, and native always-visible HeadBand/ArmBand container panels. Previously published identities remain immutable.

### Tactical Gear Component 3.0.0 compatibility

B&A&HB owns the equipment integration boundary when TGC 3.0.0 is present. Five exact TGC combat-belt templates are removed from vanilla `ArmBand` admission and admitted through dedicated Belt slot15 while retaining their original TGC template IDs, grids, bundles and trader ownership. Existing profile roots found in `ArmBand` move to slot15 when free; a displaced PMC root is preserved through the sorting table rather than deleted.

TGC's broad `PouchesInSecureContainer` mutation is treated as disabled regardless of the foreign configuration value. B&A&HB first removes TGC container additions from the secure-container family, then explicitly admits only TGC Ammo Pouch `672e2e758808bacbb9d5abc4` and TGC First Aid container `672e2e7526ba61dbb88be7ff` into the supported Gamma family. TGC Tool Box `672e2e75b0ab4fcbbf7dc471` remains excluded. TGC belts receive Belt access, pickup, build and Scav-host behavior, but never inherit Admiral death/insurance protection.

This contract was audited against the unmodified upstream `TGC_3.0.0.7z` release asset with SHA-256 `932AAAA34D7E7F21770E7249E4F29227751938883E21A7151C684775359D2726`. B&A&HB does not copy TGC assets, change TGC identities, or take ownership of Painter/Artem trader and quest content.

The cross-module handoff is owned by Admiral TGC Integration PR #362; its live exact head is the integration authority for a combined candidate. That module publishes the unchanged TGC templates while deliberately omitting stock `TGC-NG.dll` filter mutation; B&A&HB remains the sole authority for the five slot15 belts, two Gamma admissions and Tool Box denial. Absence of TGC is a no-op, while a partial recognized TGC template family fails closed before host-filter mutation.

Release authority: Issue **#351** and PR **#357**.

The v0.3.0 client reports `AssemblyVersion/FileVersion/BepInEx PluginVersion = 0.3.0`. Its physical DLL filename intentionally remains `SPT Belt Armband Inventory v0.1.0.dll` so an in-place upgrade replaces the existing client instead of leaving duplicate BepInEx GUIDs. CI forbids additional versioned client DLLs.

The BepInEx plugin GUID/name remain unchanged for in-place upgrade compatibility. Every stable artifact contains `BUILD-INFO.txt` with the exact source SHA, runtime version, filename-compatibility marker and SHA-256 hashes for both runtime DLLs.

## Stable install / upgrade

The CI artifact contains one install root: `SPT_Runtime`.

1. Stop the SPT server and game completely.
2. Back up the active SPT profile before the first v0.3.0 launch. The inherited v0.2 migration and expanded v0.3 identity manifest preserve valid content, but a profile backup remains the rollback boundary.
3. Extract/copy the artifact's `SPT_Runtime` directory **over the existing SPT root**, preserving paths.
4. Confirm the client path is exactly `SPT_Runtime/BepInEx/plugins/SPT Belt Armband Inventory v0.1.0.dll`. The filename is intentionally legacy; its compiled/BepInEx version is v0.3.0.
5. Confirm there is no second versioned B&A&HB client DLL beside it.
6. Confirm the server path is `SPT_Runtime/user/mods/B&A&HB #2 MOD SPT/SPT-Belt-Armband-Inventory.Server.dll` and that the same directory contains `BUILD-INFO.txt`.
7. Compare `BUILD-INFO.txt` `HeadSha`, `ClientSha256` and `ServerSha256` with the release evidence before launching SPT.

Earlier stable releases use those same client/server paths, so an in-place v0.3.0 overlay replaces both runtime DLLs rather than creating a second server-mod directory.

Install the client and server parts from the same v0.3.0 package.

### Rollback to stable v0.1.0

Do not simply copy v0.1.0 binaries over a profile after v0.2.0 has migrated/created newer persistent data. Stop SPT, restore the pre-v0.2.0 profile backup when possible, then restore the complete stable v0.1.0 package. If no compatible backup exists, follow `profile-safety/README.md`: preserve a backup/copy and use the current cleanup contract before starting an older build that may not know newer distributed identities.

## Current v0.2 scope

The sections below describe the full implementation reserve at PR #286 head
`28bb0e8f90d86d2570d37a498effc868a29580cf`. They are retained for migration,
recovery and reproducibility; they are not the promised active surface of the
planned Pack 'n' Strap add-on mode.

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

Until its separate combined-runtime milestone passes, a foreign extension of
the canonical dogtag contract disables only this optional product and offer.
The server continues without changing the foreign filter; HeadBand and exact
Admiral-owned protection remain active.

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
