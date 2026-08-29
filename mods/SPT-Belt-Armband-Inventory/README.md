# B&A&HB #2 MOD SPT

Post-stable development line for **SPT 4.1.3**.

Stable **v0.1.0** is already frozen and published separately on `runtime-belt-armband` / tag `bahb-v0.1.0`. This branch develops the next B&A&HB revision without changing that release.

Active authority:
- Issue **#285**
- PR **#286**
- branch `feature/bahb-v0.2-compact-headband`

## Current v0.2 scope

### Wearable products

- **Wrist Wallet** — ArmBand host, `1x1`, currency-only, Ragman LL1, 12,500 RUB.
- **Magazine Armband** — ArmBand host, `1x2`, MAGAZINE-only, Ragman LL1, 25,000 RUB.
- **Magazine Belt** — dedicated slot15, `2x2`, MAGAZINE-only, Ragman LL2, 45,000 RUB.
- **Utility HeadBand** — dedicated slot16, Ragman LL1, 25,000 RUB.

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
- product-contract guard;
- compact-layout guard;
- deterministic regressions, including the real split-grid profile migration;
- offline profile recovery;
- client build;
- server build against the SPT 4.1.3 package set;
- one installable exact-head RC artifact.

A physical runtime handoff is made only after the exact PR head is fully GREEN and includes one working GitHub artifact link plus a numbered PASS/FAIL checklist.
