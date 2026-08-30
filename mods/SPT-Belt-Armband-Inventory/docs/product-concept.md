# B&A&HB product concept

## Product thesis

B&A&HB is an accessory-logistics framework, not a generic way to add free inventory cells. Each accessory has a narrow role, explicit capacity/filter rules and a concrete equipment opportunity cost while retaining native EFT inventory behavior.

Stable **v0.1.0** is the accepted mechanical base and rollback release. Development candidate **v0.2.0** keeps those persistent/lifecycle boundaries while refining HeadBand presentation/storage, making the two magazine-specific wearables operational reload fallback sources, and adding one exact-purpose Dogtag-slot container without inventing another equipment slot.

## v0.2 roster

| Product | Host | Capacity | Role | Ragman |
| --- | --- | --- | --- | --- |
| Wrist Wallet | ArmBand | `1x1` | currency-only reserve/payment source | LL1 — 12,500 RUB |
| Magazine Armband | ArmBand | `1x2` | compact magazine storage + low-priority reload fallback | LL1 — 25,000 RUB |
| Utility HeadBand | slot16 | two native `1x1` grids | micro money/wallet + cigarette utility | LL1 — 25,000 RUB |
| Magazine Belt | slot15 | `2x2` | larger magazine storage + low-priority reload fallback | LL2 — 45,000 RUB |
| Dogtag Case | vanilla `Dogtag` | canonical EFT Dogtag Case geometry/filter groups | dogtag-only carried container; ordinary personal dogtag remains the vanilla alternative | LL2 — 50,000 RUB |

There is no universal accessory container. Wrist Wallet, Utility HeadBand and Dogtag Case deliberately do not inherit magazine reload semantics.

All five products publish explicit EN/RU item localization in v0.2. Persistent template/grid/assort identities do not change with localization.

## Utility HeadBand v0.2

Utility HeadBand keeps its persistent item identity and dedicated slot16 host. Its old single `1x2` utility grid is split into two native `1x1` grids:

- `main`: RUB, USD, EUR, Simple Wallet, WZ Wallet;
- `cigarettes`: Apollo Soyuz, Malboro, Wilston, Strike.

The original `main` grid identity remains persistent. The cigarette grid uses a new dedicated persistent ID. Existing v0.1.0 profile contents migrate through SPT's profile-migration lifecycle: compatible money/wallet contents stay in `main`, cigarettes move to `cigarettes`, and excess same-category roots are preserved through the PMC sorting table rather than deleted.

## Dogtag Case v0.2

Dogtag Case is intentionally **not** a fourth custom wearable host. It uses the existing vanilla `Dogtag` equipment slot and extends that host only with the exact B&A&HB Dogtag Case template while preserving every pre-existing vanilla acceptance entry.

- Source item: canonical EFT/SPT Dogtag Case `5c093e3486f77430cb02e593`.
- Internal grid geometry and include/exclude filters are copied from that exact source contract; no broad item-category fallback is permitted.
- The container receives its own immutable B&A&HB template/grid/assort IDs for recovery/provenance, but no new slot ID.
- It remains outside B&A&HB wearable death-retention, insurance-suppression, fast-access and build capabilities.
- Offline uninstall cleanup still owns the exact serialized Dogtag Case root and its descendants/references wherever SPT persisted them, because an older build without the template must not inherit invalid profile data.

## Compact presentation

The visual redesign is deliberately local:

- preserve the original FaceCover outer footprint;
- reduce FaceCover height;
- place the compact HeadBand above FaceCover within that same footprint;
- do not resize/translate Gear Panel;
- do not move unrelated native equipment slots;
- do not require tab switching, Canvas force-refresh, coroutine retries or idle polling;
- retain the accepted stable presentation as fail-safe fallback if the compact owner cannot install.

## Magazine reload role

Magazine Armband and Magazine Belt extend EFT reload reachability narrowly; they do not replace vanilla magazine selection.

- Ordinary EFT reachable magazine locations remain the complete priority prefix/default source.
- The scoped Belt reload bridge leaves the vanilla candidate sequence intact and appends only exact Magazine Belt descendants during `Reload()` / `QuickReload()` enumeration; Magazine Armband remains on the native ArmBand path.
- Only an otherwise-unreachable `Magazine` whose ancestor chain contains the exact Magazine Armband or Magazine Belt template may be promoted to reachable.
- Wrist Wallet, Utility HeadBand and Dogtag Case are not reload roots.
- Parent traversal and template identity readers are bound once at startup; no inventory-wide scans, scene scans, per-frame polling or runtime reflection discovery are allowed in the reload path.
- If an exact EFT reload/reachability boundary cannot bind, the affected feature fails closed to reserve-only storage rather than altering broader inventory behavior.

## Balance and implementation constraints

1. Persistent template/slot/grid/assort identities are immutable once distributed.
2. Product filters remain exact and narrow; generic CASE/medical/barter parents are not accepted as convenience fallbacks.
3. slot16 map creation/recovery stays pre-enumeration; late `SlotView.Show` cannot mutate the active map.
4. ArmBand/Belt/HeadBand death retention and insurance-loss filtering remain one exact-root wearable policy; Dogtag Case explicitly stays outside it.
5. Scav compatibility stays bounded and CI-owned.
6. Declared grid capacity must match visible/native capacity exactly.
7. Vanilla reload reachability/order stays authoritative; magazine wearables are appended fallback only.
8. The vanilla Dogtag host remains authoritative; B&A&HB may append only the exact Dogtag Case product and may not normalize or replace existing accepted dogtags.
9. Stable v0.1.0 remains the rollback release while v0.2.0 is under development.
10. Candidate package/runtime identity is v0.2.0 while the client physical filename remains the legacy v0.1.0 path solely for safe in-place overwrite; BUILD-INFO records the exact head and binary hashes.

## Current gate

The v0.2 development package is eligible for one combined physical gate only after exact-head CI is fully GREEN. The gate covers first-open compact layout, two independent HeadBand cells and filters, existing-profile migration sanity, all five products/EN-RU localization and host isolation, vanilla-first reload selection, wearable reload fallback, Dogtag Case contents/persistence, and one normal PMC lifecycle. No repeated death/insurance matrix is required unless new evidence points to that subsystem.
