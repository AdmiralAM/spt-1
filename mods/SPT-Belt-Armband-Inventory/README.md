# B&A&HB #2 MOD SPT

Development candidate **v0.2.0** for **SPT 4.1.3**.

Stable **v0.1.0** is already frozen and published separately on `runtime-belt-armband` / tag `bahb-v0.1.0`. This branch develops v0.2.0 without changing that release.

Active authority:
- Issue **#285**
- PR **#286**
- branch `feature/bahb-v0.2-compact-headband`

The v0.2.0 client reports `AssemblyVersion/FileVersion/BepInEx PluginVersion = 0.2.0`. Its physical DLL filename intentionally remains `SPT Belt Armband Inventory v0.1.0.dll` for this upgrade line so extracting the candidate over an installed stable v0.1.0 replaces the existing client file instead of leaving two DLLs with the same BepInEx GUID. CI explicitly forbids a second `...v0.2.0.dll` in the candidate package.

The BepInEx plugin GUID/name remain unchanged for in-place upgrade compatibility. Every CI artifact contains `BUILD-INFO.txt` with the exact head SHA, runtime candidate version, filename-compatibility marker and SHA-256 hashes for both runtime DLLs.

## Current v0.2 scope

### Wearable products

- **Wrist Wallet** — ArmBand host, `1x1`, currency-only, Ragman LL1, 12,500 RUB.
- **Magazine Armband** — ArmBand host, `1x2`, MAGAZINE-only, Ragman LL1, 25,000 RUB.
- **Magazine Belt** — dedicated slot15, `2x2`, MAGAZINE-only, Ragman LL2, 45,000 RUB.
- **Utility HeadBand** — dedicated slot16, Ragman LL1, 25,000 RUB.

All four v0.2 products publish explicit **EN and RU** item names, short names and descriptions. Persistent template/grid/assort identities are unchanged by localization.

### Utility HeadBand v0.2

The HeadBand keeps the existing item/slot identity but now uses **two native `1x1` grids**:

- `main` — RUB, USD, EUR, Simple Wallet and WZ Wallet;
- `cigarettes` — Apollo Soyuz, Malboro, Wilston and Strike.

The original `main` grid identity is preserved. The cigarettes grid has its own persistent identity. Existing v0.1.0 HeadBand contents are migrated through the native SPT profile-migration lifecycle before profile deserialization. Currency/wallet items remain in `main`, cigarettes move to `cigarettes`, and same-category overflow is preserved through the PMC sorting table rather than deleted.

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
- Wrist Wallet and Utility HeadBand are not reload roots.
- Existing vanilla fast-access slot order is preserved as the complete priority prefix; ArmBand and B&A&HB Belt are appended after it, so wearable magazines are lower-priority fallback sources rather than the preferred source.
- Parent traversal and item-template accessors are resolved and compiled at startup; there are no inventory-wide scans, scene scans, per-frame polling or runtime reflection discovery in the reload path.
- The existing unload-grid integration likewise appends eligible wearable grids after vanilla unload destinations.

If the exact EFT reachability boundary cannot be bound, the reload extension fails closed and the wearable containers remain valid storage rather than replacing vanilla behavior.

## Stable mechanical boundaries retained

v0.2 preserves the accepted slot15/slot16 lifecycle and protection model:

- slot16 mapping is created/recovered before native `_slotViews` enumeration;
- late `SlotView.Show` cannot add/remove/clone slot-map entries;
- persistent item, parent, grid and slot identities remain immutable;
- ArmBand/Belt/HeadBand death and insurance settings keep their existing exact-root behavior;
- Scav compatibility remains bounded and CI-owned;
- no permanent production `Update` polling or scene-wide scans are introduced.

## Development validation

CI for PR #286 owns:

- hot-path/lifecycle guard;
- reload-access fallback/order guard;
- version/build-identity and single-DLL upgrade-path guard;
- product-contract and EN/RU localization guard;
- compact-layout guard;
- deterministic regressions, including the real split-grid profile migration and reload fallback policy;
- offline profile recovery;
- client build;
- server build against the SPT 4.1.3 package set;
- one installable exact-head RC artifact with exact-head/hash manifest.

A physical runtime handoff is made only after a materially significant bundle is ready, the exact PR head is fully GREEN, and the handoff includes one working GitHub artifact link plus a numbered PASS/FAIL checklist.
