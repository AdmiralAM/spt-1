# B&A&HB product concept

## Product thesis

B&A&HB is an accessory-logistics framework, not a generic way to add free inventory cells. Each wearable has a narrow role, explicit capacity/filter rules and a concrete equipment opportunity cost while retaining native EFT inventory behavior.

Stable **v0.1.0** is the accepted mechanical base and rollback release. Development candidate **v0.2.0** keeps those persistent/lifecycle boundaries while refining HeadBand presentation/storage and making the two magazine-specific wearables operational reload fallback sources.

## v0.2 roster

| Product | Host | Capacity | Role | Ragman |
| --- | --- | --- | --- | --- |
| Wrist Wallet | ArmBand | `1x1` | currency-only reserve/payment source | LL1 — 12,500 RUB |
| Magazine Armband | ArmBand | `1x2` | compact magazine storage + low-priority reload fallback | LL1 — 25,000 RUB |
| Utility HeadBand | slot16 | two native `1x1` grids | micro money/wallet + cigarette utility | LL1 — 25,000 RUB |
| Magazine Belt | slot15 | `2x2` | larger magazine storage + low-priority reload fallback | LL2 — 45,000 RUB |

There is no universal accessory container. Wrist Wallet and Utility HeadBand deliberately do not inherit magazine reload semantics.

All four products publish explicit EN/RU item localization in v0.2. Persistent template/grid/assort identities do not change with localization.

## Utility HeadBand v0.2

Utility HeadBand keeps its persistent item identity and dedicated slot16 host. Its old single `1x2` utility grid is split into two native `1x1` grids:

- `main`: RUB, USD, EUR, Simple Wallet, WZ Wallet;
- `cigarettes`: Apollo Soyuz, Malboro, Wilston, Strike.

The original `main` grid identity remains persistent. The cigarette grid uses a new dedicated persistent ID. Existing v0.1.0 profile contents migrate through SPT's profile-migration lifecycle: compatible money/wallet contents stay in `main`, cigarettes move to `cigarettes`, and excess same-category roots are preserved through the PMC sorting table rather than deleted.

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
- B&A&HB ArmBand/Belt locations are appended after the vanilla fast-access order.
- Only an otherwise-unreachable `Magazine` whose ancestor chain contains the exact Magazine Armband or Magazine Belt template may be promoted to reachable.
- Wrist Wallet and Utility HeadBand are not reload roots.
- Parent traversal and template identity readers are bound once at startup; no inventory-wide scans, scene scans, per-frame polling or runtime reflection discovery are allowed in the reload path.
- If the exact EFT reachability boundary cannot bind, the feature fails closed to reserve-only storage rather than altering broader inventory behavior.

## Balance and implementation constraints

1. Persistent template/slot/grid identities are immutable once distributed.
2. Product filters remain exact and narrow; generic CASE/medical/barter parents are not accepted by HeadBand.
3. slot16 map creation/recovery stays pre-enumeration; late `SlotView.Show` cannot mutate the active map.
4. Death retention and insurance-loss filtering remain one exact-root policy and are not reopened by presentation/product work.
5. Scav compatibility stays bounded and CI-owned.
6. Declared grid capacity must match visible capacity exactly.
7. Vanilla reload reachability/order stays authoritative; magazine wearables are appended fallback only.
8. Stable v0.1.0 remains the rollback release while v0.2.0 is under development.
9. Candidate package/runtime identity is v0.2.0 while the client physical filename remains the legacy v0.1.0 path solely for safe in-place overwrite; BUILD-INFO records the exact head and binary hashes.

## Current gate

The v0.2 development package is eligible for one combined physical gate only after exact-head CI is fully GREEN. The gate covers first-open compact layout, two independent HeadBand cells and filters, existing-profile migration sanity, product roster/EN-RU localization, vanilla-first reload selection, wearable reload fallback and one normal PMC lifecycle. No repeated death/insurance matrix is required unless new evidence points to that subsystem.
