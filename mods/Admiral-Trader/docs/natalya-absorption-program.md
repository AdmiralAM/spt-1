# Natalya absorption program

## Authority

Natalya is being retired from the target SPT installation. Her useful gameplay and storefront material therefore belongs in the existing Admiral runtime. This is a continuation from the tagged `0.2.0` stable baseline, not a resurrection of a second trader.

Scorpion and Ref remain installed. Their content is not copied. Native traders plus those two are the comparison set used to reject duplicate offers and duplicate quest roles.

Andrudis/QuestManiac is already disabled. Its repository is a source library only; it must not be re-enabled or become a runtime dependency.

## Verified source inventory

The pinned Natalya source contains 36 quest records and 203 root assortment offers:

| Family | Records | What it contributes | Direct import decision |
| --- | ---: | --- | --- |
| Pay Back I–XIV | 14 | Map-route opening, escalating threat and hand-off framing | Re-author selected beats; do not copy the linear body-count ladder. |
| Weapons Training | 10 | A useful category sequence from revolvers through bolt-actions | Fold the roles into Admiral's staged weapon rotation; preserve no source IDs or unlock flood. |
| Exfil Part 1–11 | 12 | Ground Zero start, supplies, route security and evacuation continuity | Re-author as native objectives; source custom zones and beacon placement are not a dependency. |

Natalya also carries 48 custom item definitions, 26 custom zones and four armour presets. Nineteen storefront roots depend on those custom definitions; none is safe to copy as a prerequisite for a working Admiral installation. Their gameplay role may be represented only with exact SPT 4.1.5-native items and conditions.

## Storefront handover

Natalya's source assortment has 35 native weapon roots. A first comparison against the exact 4.1.5 native assortments and the installed Scorpion static assort identifies useful source candidates including M700, T-5000, VPO-215, SR-25, G28, M1A, RPDN, M60E6, PKP, Mk17, SA-58, SAG AK-545, RFB, Vector, MPX, MP5, MP-155, M870, MR-133, USP, Five-seveN and the Rhino variants.

All 35 complete native weapon presets passed the intake gate and are now materialized. The remaining 168 source roots were reviewed rather than copied blindly: 19 require Natalya-only templates, while the native remainder is dominated by 98 ammunition-box rows plus ordinary medicine, grenades, maps and protected containers. Those rows duplicate retained supply roles, expose progression-sensitive containers, or add grid volume without a distinct Admiral role. Broken custom-armour rows and detached parts are excluded.

The result transfers Natalya's useful ready-to-raid weapon identity without recreating her commodity warehouse. Every accepted preset has a complete native item tree, finite stock of two, a one-per-reset purchase limit, and an authored loyalty tier. The four offer IDs published by the earlier intake remain unchanged.

## Materialized delivery

1. All 35 complete native weapon presets are finite Admiral offers.
2. Four Ground Zero operations provide the opening route and gate the old key-access chain behind the first completed operation.
3. Nineteen new weapon assignments complete two staged lanes with 40 Arsenal assignments across the full campaign; six equipment assignments rotate rigs, headsets, helmets and armor.
4. Fifty verified WTT weapons are optional alternatives inside matching pools. Removing WTT leaves every quest and prerequisite playable through native weapons.
5. Existing quest IDs and the frozen 0.1.0 identity remain valid. The expanded runtime contains 172 quests and 82 finite offers.
6. Natalya appears as an authored specialist in 18 story beats spanning Ground Zero, Customs, Interchange, Shoreline, Lighthouse, Streets and The Lab; she remains inside Admiral's engine and never becomes a second trader.

## Explicit exclusions

- no Natalya trader, WTT requirement, custom-zone router, custom-item pack or imported profile state;
- no Andrudis runtime activation or wholesale quest import;
- no migration of Scorpion or Ref inventory or quests;
- no empty weapon roots, incompatible armour assemblies or duplicate commodity grid padding.
