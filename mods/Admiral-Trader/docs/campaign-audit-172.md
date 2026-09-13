# Campaign audit — 172 core quests

This audit describes the published Admiral campaign at the M8 exact runtime head. It is a content and progression review, not a replacement for the final fresh-profile playthrough.

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

M8’s normalized core distribution is: median 11,950 XP, 47,800 roubles and +0.013 Admiral standing; the observed range is 2,500–28,000 XP, 11,000–135,000 roubles and +0.005–0.03 standing. There are 37 free physical-item rewards and 10 controlled storefront unlocks.

High-value containers, the armour repair kit and tank battery are no longer early free payouts. They are controlled one-per-reset purchase unlocks at actual value. The Labs finale now uses a usable access-card sample instead of an inert classified folder. The optional Icebreaker finale is separately documented as a non-repeatable exceptional reward.

## M9 editorial work derived from this audit

1. Replace every remaining player-facing English `QuestName` with a proper Russian title; retain exact IDs and graph links.
2. Make each weapon category explicit in its operational brief, including the allowed weapon pool where a category name alone is ambiguous.
3. Keep useful situation and objective context, but remove duplicate reward and requirement summaries from descriptions where the native quest panel already renders them.
4. Preserve multiline paragraphs and readable spacing instead of compressing text to one line.

No quest graph, reward value, trader identity, offer identity or runtime architecture is changed by this audit.
