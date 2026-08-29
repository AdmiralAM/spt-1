# B&A&HB product concept

## Product thesis

B&A&HB is an accessory-logistics framework, not a generic way to add free inventory cells. Each wearable has a narrow role, explicit capacity/filter rules and a concrete equipment opportunity cost while retaining native EFT inventory behavior.

Stable **v0.1.0** is the accepted mechanical base. v0.2 changes the HeadBand product/presentation only within the boundaries proven by that release.

## v0.2 roster

| Product | Host | Capacity | Role | Ragman |
| --- | --- | --- | --- | --- |
| Wrist Wallet | ArmBand | `1x1` | currency-only reserve | LL1 — 12,500 RUB |
| Magazine Armband | ArmBand | `1x2` | compact magazine reserve | LL1 — 25,000 RUB |
| Utility HeadBand | slot16 | two native `1x1` grids | micro money/wallet + cigarette utility | LL1 — 25,000 RUB |
| Magazine Belt | slot15 | `2x2` | larger magazine reserve | LL2 — 45,000 RUB |

There is no universal accessory container.

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

## Balance and implementation constraints

1. Persistent template/slot/grid identities are immutable once distributed.
2. Product filters remain exact and narrow; generic CASE/medical/barter parents are not accepted by HeadBand.
3. slot16 map creation/recovery stays pre-enumeration; late `SlotView.Show` cannot mutate the active map.
4. Death retention and insurance-loss filtering remain one exact-root policy and are not reopened by presentation/product work.
5. Scav compatibility stays bounded and CI-owned.
6. Declared grid capacity must match visible capacity exactly.
7. Stable v0.1.0 remains the rollback release while v0.2 is under development.

## Current gate

The v0.2 development package is complete enough for one combined physical gate only after exact-head CI is fully GREEN: first-open compact layout, two independent HeadBand cells and filters, existing-profile migration sanity, product roster, and one normal PMC lifecycle. No repeated death/insurance matrix is required unless new evidence points to that subsystem.
