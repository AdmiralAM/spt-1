# Campaign audit — 172 core quests

This audit describes the published Admiral campaign at the closed M7 content scope. It is a content and progression review, not a replacement for the final fresh-profile playthrough.

## Scope

- 172 core Admiral records; 10 additional Icebreaker records are published only when that map mod is present.
- The core campaign has eight authored entry points. It uses native quest ownership, explicit accept/complete, finite rewards and the persistent Admiral trader ID.
- The complete installed campaign is 182 Admiral records when Icebreaker is present.

## Campaign composition

| Role | Records | Purpose |
| --- | ---: | --- |
| Access protocols | 10 | Practical early-to-late map access and route reconnaissance. |
| Original Arsenal protocol | 21 | Weapon-category qualifications, fieldwork and ammunition access. |
| Operations | 12 | Standalone tactical work: reconnaissance, routes, contacts and high-risk endpoints. |
| Staged Arsenal rotation | 19 | Two paced weapon lanes; normally a small choice instead of a wall of parallel kills. |
| Loadout operations | 10 | Vary armour, rig, headset, backpack and raid preparation decisions. |
| Story investigations | 100 | Ten map-centred, ten-part operational chains with pickup, marking, reconnaissance and combat beats. |

The 100-story set is the main narrative backbone, rather than a collection of generic kill counters. Natalya is represented as an authored specialist within that backbone; no Natalya trader, ID, item pack or external runtime dependency exists.

## Objective and map coverage

The 172 records contain 60 elimination, 56 exploration, 55 pickup and one completion quest. Finish conditions comprise 194 counters, 58 FIR-find requirements, 56 handovers and 35 beacon placements. Thirty-five quests deliberately require a single successful raid; 45 include a truthful FIR handover or retrieval.

Every normal map has recurring work through the campaign: Ground Zero 27, Customs 29, Woods 23, Shoreline 25, Interchange 28, Factory day 19 and night 17, Reserve 19, Lighthouse 17, Streets 20, and Labs 16 location-condition appearances. A weapon is not assigned to one permanent map: early tasks use Ground Zero and nearby maps, then map pools widen with level and operation context.

Weapon work is split between 40 core Arsenal assignments: the established 21-category protocol and 19 sequential rotation entries. The active rotation intentionally covers service pistols, compact AKs, shotguns, SMGs/PDWs, starter rifles, carbines, battle rifles, precision weapons and specialist platforms. Early pools include the practical models previously requested as examples — AUG, MP7, pistols and short AK variants — without making those examples the whole pool. WTT alternatives remain optional additions to a native route.

## Progression and storefront

The core shop is already a staged 82-offer storefront: 30 LL1, 21 LL2, 21 LL3 and 10 LL4 finite offers. Nineteen LL1 offers are immediately available; eleven are deliberately released through completed access/capability work. With the installed WTT Armory and Content Backport, the optional post-load assortment adds 62 finite offers, making 144 without making either mod a dependency.

The 35 complete native presets recovered from Natalya are all present as Admiral finite stock. The remaining Natalya roots were not copied as a commodity dump: they are mostly ammunition-box variants, generic medicine, maps, protected containers, duplicate supplies or items that require Natalya-only definitions. This preserves the useful weapon identity while avoiding broken items, container bypasses and a redundant supermarket.

## Reward review

The pre-field-support normalized core distribution was: median 11,950 XP, 47,800 roubles and +0.013 Admiral standing; the observed range was 2,500–28,000 XP, 11,000–135,000 roubles and +0.005–0.03 standing. M7 then converted bounded portions of 20 payouts into progressive native field equipment, alongside four early complete weapons, ten signature finale presets and conditional B&A&HB equipment/container trades. Ten controlled storefront unlocks remain finite purchases rather than free rewards.

The 20 native trades progress from IFAK/GSSH/Salewa and an early Day Pack through surgery kits, headsets, armour, rigs and backpacks to late combat stimulants. They are disjoint from the early-weapon, Natalya-finale, WTT-substitution and B&A&HB layers, so no later registration pass overwrites another reward. Every cash trade leaves a positive rouble payout.

Five raw reward rows sit above one campaign review threshold. Their effective rewards and objective risk were reviewed individually:

| Quest | Effective reason | Decision |
| --- | --- | --- |
| Протокол доступа: Допуск | Level-35 finale of the ten-step access chain; the rare-card check is possession-only and the payout represents the completed access progression. | Retain 28,000 XP / ₽120,000 / +0.030. |
| Последний сигнал | Four Lighthouse Rogues plus survival in one raid creates concentrated combat and reset risk. | Retain 22,000 XP / ₽135,000 / +0.030. |
| Подмена | Level-44 deep-Labs placement in step eight of the final chain carries access cost and survival exposure. | Retain 25,750 XP / ₽99,000 / +0.018. |
| Последний контейнер | Level-46 Labs quest-item retrieval, handover and same-raid survival immediately precedes the finale. | Retain 27,000 XP / ₽104,000 / +0.019. |
| Решение Адмирала | Level-47 campaign finale after ten complete story chains; its preset and finite Labs-card unlock are non-repeatable endgame rewards. | Retain 27,750 XP / ₽108,000 / +0.019. |

No reward is reduced by the audit. The apparent outliers are bounded late-game milestones, remain below the vanilla p90 cash ceiling, and create no repeatable faucet. The exact reviewed ID set is now enforced so a future unreviewed outlier cannot enter silently.

High-value containers, the armour repair kit and tank battery are no longer early free payouts. They are controlled one-per-reset purchase unlocks at actual value. The Labs finale now uses a usable access-card sample instead of an inert classified folder. The optional Icebreaker finale is separately documented as a non-repeatable exceptional reward.

B&A&HB publishes five stable wearable product templates plus its owned small-container catalogue. Admiral defines 32 conditional reward trades: nine are placed by level 10 and 24 by level 20. The current installed runtime resolves all 32, while a setup without B&A&HB skips them and preserves the unreduced cash reward. Admiral neither fabricates nor clones Belt-owned containers.

The final balance pass confirms that the four early complete-weapon rewards arrive at levels 1, 3, 5 and 7. Every equipment substitution leaves at least ₽10,000 of the original cash payout, and the early-weapon, native field-support, B&A&HB and Natalya-finale layers use disjoint quest IDs. This keeps early rewards useful without stacking several premium reward classes on one task.

## Graph and availability audit

- All 172 core IDs and ten optional Icebreaker IDs are unique; every prerequisite resolves and no graph cycle is present.
- The core campaign has eight entry points: four at level 1, one at level 6 and three at level 15. The level-1 choice consists of the opening Ground Zero operation, both paced Arsenal lanes and the first Ground Zero story.
- The largest level band is level 15 with twelve records, but most remain behind chain prerequisites. Raw level counts therefore do not represent twelve simultaneous offers.
- Quest types remain 60 Elimination, 56 Exploration, 55 PickUp and one Completion. The story set supplies the reconnaissance, retrieval, placement and survival variety; weapon elimination stays in the two paced Arsenal lanes.
- All Russian quest names exist and contain Cyrillic. Every quest has required name, description, started and success locale records. No description still embeds duplicate `Требования:` or `Награды:` blocks.

## Storefront audit

The core assortment is structurally valid: 47 base roots plus 35 complete Natalya weapon presets produce 82 finite offers. Every root has barter and loyalty metadata, every child points to an owned parent and no offer is unlimited. The combined loyalty distribution is LL1 30, LL2 21, LL3 21 and LL4 10; 18 base offers are quest-gated.

All 35 Natalya roots are assembled presets with two to nine child parts. The complex core armour roots retain their required child armour/plate trees. The red background visible on some equipment is therefore not evidence of a missing child by itself; no orphaned or structurally incomplete offer was found in the committed assortment.

With the installed WTT modules, 19 Content Backport and 43 WTT Armory roots pass the post-load template gate and register as 62 additional finite offers. Their template IDs do not exist in the vanilla database by design; the exact combined server start proves they are published before Admiral's optional-content pass. Without WTT, all 82 core offers and the full quest graph remain available.

## Audit decisions

1. Preserve the 172+10 graph, eight entry points, 40-quest weapon rotation and current storefront counts.
2. Preserve the five individually reviewed high raw reward rows; deterministic validation rejects any new unreviewed threshold outlier.
3. Keep all 32 B&A&HB rewards conditional on Belt-owned template publication; never manufacture or clone those templates inside Trader.
4. Treat Russian weapon-model wording, long allowed-pool presentation and remaining voice consistency as the M9 editorial package after the content-order gates.
5. Treat actual offer usefulness, insurance speed and reward satisfaction as one final fresh-profile acceptance session after repository-side stabilization.

## M9 editorial work derived from this audit

1. Replace every remaining player-facing English `QuestName` with a proper Russian title; retain exact IDs and graph links.
2. Make each weapon category explicit in its operational brief, including the allowed weapon pool where a category name alone is ambiguous.
3. Keep useful situation and objective context, but remove duplicate reward and requirement summaries from descriptions where the native quest panel already renders them.
4. Preserve multiline paragraphs and readable spacing instead of compressing text to one line.

No quest graph, reward value, trader identity, offer identity or runtime architecture is changed by this audit.
