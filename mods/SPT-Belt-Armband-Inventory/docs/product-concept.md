# B&A&HB product concept

## Product thesis

B&A&HB is an accessory-logistics framework, not a generic way to add free inventory cells. It turns small equipment accessories into deliberately scoped load-bearing roles while preserving EFT inventory rules, persistence and native UI behavior.

The useful decision is not simply “more space”. A player chooses where a small set of supplies lives, how quickly gameplay systems can reach it, and which equipment role is consumed by that choice. Capacity, accepted item classes and runtime integrations therefore remain explicit for every accessory.

## Stable mechanical base

Stable Baseline 1 is exact head `d6336f290361b16c4aa54f9d7dddfe0e8f7f9bbf`, preserved on branch `belt-stable-baseline-1`. It physically passed first-open Items, pre-raid/insurance navigation and normal PMC lifecycle. Subsequent product work must preserve that host/slot architecture unless a concrete regression proves a mechanical change necessary.

## Product roles and progression

| Product | Category / host | Capacity | Exact role | Ragman progression |
| --- | --- | --- | --- | --- |
| Wrist Wallet | `ArmBand` | `1x1` | currency-only specialist reserve | LL1 — 12,500 RUB |
| Magazine Armband | `ArmBand` | `1x2` | compact magazine reserve | LL1 — 25,000 RUB |
| Utility HeadBand | `HeadBand` slot16 | `1x2` | micro currency/cigarette/wallet utility | LL1 — 25,000 RUB |
| Magazine Belt | `Belt` slot15 | `2x2` | larger sustained magazine reserve | LL2 — 45,000 RUB |

This progression deliberately avoids a universal accessory container. The LL1 products are narrow specialist tools; the larger Belt arrives later and costs more rather than simply replacing the ArmBand option for free.

## Category rules

### ArmBand

The vanilla ArmBand host is a proven searchable-container foundation. Concrete items remain narrow-purpose: Wrist Wallet is payment-oriented currency storage; Magazine Armband is MAGAZINE-only. A new ArmBand item does not automatically inherit every capability.

### Belt

Belt uses dedicated pseudo-slot 15 and is mechanically active. The current concrete product is Magazine Belt: `2x2`, MAGAZINE-only, Ragman LL2. Future belts, if any, must specialize by filter/capability rather than merely adding more unrestricted cells.

### HeadBand

HeadBand uses dedicated pseudo-slot 16 and is mechanically active. Utility HeadBand remains a micro utility carrier with an exact-item whitelist: RUB, USD, EUR, Apollo Soyuz, Malboro, Wilston, Strike, Simple Wallet and WZ Wallet. It is not a generic CASE, money-parent, barter-parent, secure or medical container.

Death protection is a separate F12 family policy and must not be presented as an unconditional item property.

## Balance rules

1. No universal accessory container. Each item has a narrow logistical purpose.
2. Capacity is paid for through equipment opportunity, item restrictions, progression placement, weight and price rather than invisible UI penalties.
3. Fast-access, grenade enumeration, loot placement, payment and death retention are separate capabilities. A category does not automatically inherit all integrations.
4. Native EFT inventory behavior remains authoritative. The mod extends reachability and ordering only at confirmed boundaries.
5. A declared grid is exact. UI padding or presentation must never imply hidden capacity.
6. Persistent item, parent, grid, assort and slot identities are immutable after distribution.
7. Stable Baseline 1 remains the rollback point while product/design work advances.

## Architecture lessons now treated as constraints

Physical RC work established several non-negotiable boundaries:

- slot16 map creation/recovery happens before `EquipmentTab.Show` begins native enumeration;
- late `SlotView.Show` cannot mutate that map;
- HeadBand layout cannot resize/translate the host Gear Panel or force global Canvas rebuilds;
- first-visible layout is event-driven and does not depend on tab switching;
- stale Unity objects must be checked with Unity liveness semantics, not managed-reference non-null alone;
- death retention and insurance-loss filtering consume the same exact-root protection policy;
- Scav compatibility is bounded and automated rather than a user acceptance requirement.

These are reusable implementation rules for future wearable slots.

## Deferred final HeadBand design

The current accepted HeadBand geometry is mechanically stable but not the final visual target. The deferred design direction is:

- reduce the Face equipment window roughly by half;
- use the recovered space for a compact HeadBand placement above/adjacent to Face;
- visually follow the ArmBand + Dogtag principle so no control protrudes beyond the normal equipment panel;
- preserve slot16 identity and the stable lifecycle while changing only presentation.

A second final-stage feasibility question is whether Utility HeadBand can become two independent internal `1x1` cells: one cigarettes-only and one currency/wallet-only. This must be proven against native multi-grid behavior before implementation. If it requires fragile parallel UI or compromises stable lifecycle, keep the current single `1x2` exact whitelist instead.

## Visual-content gate

Icons, bundles and 3D models should represent the actual capacity and role. They must not be used to conceal ambiguous mechanics. Presentation redesign can proceed only with Stable Baseline 1 preserved as rollback reference and the product roster protected by CI.
