# Admiral Trader post-consolidation content audit

Status: **implemented content audit; no offers or quests removed by this audit**. No persistent quest IDs were retired. Runtime baseline: Admiral Trader 0.3.0, exact SPT 4.1.5 data and the current SPT 4.1.6 integration set.

## Measured scope

The authored Admiral layer contains 172 quests and 82 finite core offers. The consolidated external layer adds 402 roots / 946 rows, 35 quests, 44 quest unlocks and 68 suits. TGC contributes 114 unique root templates, Artem 281 roots (277 root templates), Painter 7 roots (5 templates), and the retained Natalya rack 35 complete weapon presets.

The root-template comparison found 23 template overlaps and 49 involved offers. Comparing the complete item tree, rather than the root template alone, reduced this to 13 exact product-tree overlaps and 27 involved offers. Same-base weapon presets with different installed parts are therefore not classified as duplicates.

## Storefront decisions

| Product | Existing paths | Decision | Reason |
| --- | --- | --- | --- |
| Documents case | Admiral LL1 barter; Artem LL2 barter | retain both for now | The payments serve different loot routes. Audit their effective value before choosing one. |
| Labs access card | two authored Admiral quest barters; Artem LL4 cash | retain | These are three materially different acquisition paths and progression gates. |
| `.45 ACP AP` | Admiral quest-unlocked offer `67d5501fb925a7836b99f112`; Artem LL4 cash | retain | The Admiral row is unlocked by Arsenal A-12 rather than being an unrestricted LL1 sale. Artem provides the ordinary late-loyalty route; the two acquisition paths have distinct roles. |
| `5.56x45 M856A1` | Admiral LL1 premium cash; Artem LL3 cheaper cash | retain | Price and loyalty create a legitimate early-premium versus later-standard choice. |
| `7.62x51 M62` | Admiral LL1 cash; Artem LL4 cash | retain | The Artem row is a persistent quest-assort unlock and reward target. Its higher price is secondary to preserving the authored quest result. |
| Injector case | Admiral LL1 barter; Artem LL3 barter | retain for value audit | Different barter ingredients may support distinct loot decisions; neither should be removed without valuation. |
| AFAK | Admiral LL2 cash; Artem LL3 cheaper cash | retain | The later discount is a normal loyalty progression rather than grid padding. |
| `5.56x45 M855A1` | Painter quest-linked LL4; Artem LL3 cash | retain | The Painter offer carries a preserved quest-unlock identity; removal would require an authored unlock decision. |
| `7.62x51 M61` | Painter quest-linked LL4; Artem LL4 cash | retain | Both rows are referenced by preserved quest-assort/reward identities. Consolidation must not silently erase either quest result. |
| Painter reward boxes | cash and GP-coin paths for each box | retain | The second row is a barter alternative, not an accidental duplicate. |
| Grizzly and 60-round AK magazine | Artem cash and barter paths | retain | Each pair deliberately offers money or barter acquisition. |

No Natalya/TGC weapon row is an exact assembled-tree duplicate. Shared base weapon templates are different presets and remain outside the removal set.

### Full value, stock and progression pass

The machine-readable [offer audit](storefront-value-audit.csv) covers all 546 possible roots: 47 Admiral core, 35 Natalya presets, 281 Artem, 7 Painter, 114 TGC runtime and 62 optional WTT roots. Every row records its source, persistent offer and template IDs, loyalty level, stock, purchase limit, cash/barter route, quest gate, resolved rouble cost, complete-tree handbook value where possible, and explicit flags. The generator uses exact SPT 4.1.5 handbook/prices and accepts TGC as an explicit runtime input; it does not mutate the store.

| Check | Result | Decision |
| --- | --- | --- |
| Loyalty distribution | Every retained source spans its authored tiers; core has 21/12/12/2 roots at LL1–4, Natalya 9/9/9/8, Artem 84/103/67/27, TGC 8/30/49/27, optional WTT 23/25/13/1. | Retain. Moving offers merely to make tiers visually uniform would destroy authored progression. |
| Acquisition routes | 516 cash and 12 barter roots before quest gating; 62 offers are gated by a completed quest (18 Admiral, 41 Artem, 3 Painter). | Retain. Quest and barter alternatives remain materially distinct acquisition paths. |
| Stock and limits | Admiral and Natalya use finite global stock. Artem preserves 115 globally unlimited roots from its source data, but 107 still have a per-player purchase cap. The remaining 8 are basic magazines or ammunition routes. | Retain. Global replenishment is not unlimited player purchasing when a per-player cap exists; the eight uncapped basics are intentional utility supply rather than rare equipment or presets. |
| Price comparison | 24 fully priced rows exceed 3× SPT handbook value. Most are deliberate premium access items, signals, quest goods, high-grade ammunition/magazines or late containers; no fully priced row falls below 0.35× handbook value. | Do not bulk-normalize. Handbook price is not sufficient evidence for access cards, signals or quest-gated scarcity. Review only named rows against vanilla trader/flea availability before changing balance. |
| Modded valuation | 307 roots contain at least one custom template absent from the vanilla SPT handbook: all TGC/WTT roots and part of Artem/Painter. | Keep them fail-safe and source-priced. A fabricated vanilla value would be less truthful than the explicit `partial-product-value` flag. |

The pass therefore authorizes no blind deletion, global multiplier or blanket stock cap. The apparent Artem stock concern is resolved by separating global supply from the existing player purchase limits; no rare equipment or weapon preset is both globally unlimited and player-uncapped.

## Quest portfolio decisions

The 172 authored records divide into 10 access protocols, 40 paced Arsenal tasks, 4 Ground Zero introductory operations, 6 equipment-set operations, 12 cross-chain operations and 100 map-story quests. The access protocols and Arsenal lanes are repetitive by category but have distinct progression jobs; they are not deletion candidates in this pass.

### Audited candidate resolutions

| Quest | Implemented decision | Runtime distinction |
| --- | --- | --- |
| `208db81b5ce195bf0c176852` — **Низкий профиль** | rewritten inside the existing ID | One Interchange raid in Scav Vest + Transformer Bag now requires the KOSTIN service area, first warehouse sector and survived extraction in the same equipment. It is a route discipline task rather than an empty loadout check. |
| `8dad0d354ac000b7bbf05b9a` — **Акустическая дисциплина** | retained | This is the non-combat headset reconnaissance route through the abandoned convoy and USEC camp. `Акустический контакт` remains its later combat application. |
| `3c6e085fc02f0597efdb5d5a` — **Операция: Открытый коридор** | rewritten inside the existing ID | Four Scavs from at least 30 metres plus survived extraction in one Ground Zero raid. This introduces spacing and raid completion instead of another plain counter. |
| `31ab6a69a8436df6b3834b0a` — **Операция: Спорная территория** | retained | Its early PMC target is distinct from the Scav clearance before it and the mixed-target extraction test after it. |
| `e520cec55b83621928e9e4ec` — **Операция: Правильный выход** | rewritten inside the existing ID | Five mixed targets and survived extraction must now occur in one Ground Zero raid; combat progress without returning no longer completes it. |

`02c07ee31821696597ceabef` — **Операция: Первый контакт** remains the combat onboarding record because the access protocol uses it as an entry gate. The revised three-step continuation preserves all IDs, prerequisites and early physical rewards, so existing profile history and graph references remain valid.

### Retain, but rewrite or value-check

- `56813681ae0690016376f163` — **Полевой резерв** breaks up raid objectives with a small procurement task; retain it, but compare the fuel-can reward with live flea and trader values.
- The 10 access protocols remain useful map-oriented FIR key collection and key-upgrade tasks. Keep the branch; review only rarity and reward value.
- The 40 Arsenal records remain the two-choice weapon rotation requested for campaign pacing. Their category repetition is intentional; objective wording should be shortened where the client already lists the full pool.
- The 100 map-story records retain their locations and graph. Repeated visit/retrieve/place mechanics require better local story context and more varied physical rewards, not wholesale deletion.

## Approved implementation boundary

The exact-tree duplicate and full storefront passes authorize **no offer deletion**. The apparently redundant ammunition rows are separated by quest unlocks or loyalty progression; M61/M62 are explicitly protected by preserved Artem unlocks, and the Admiral `.45 ACP AP` row is protected by Arsenal A-12. All five quest candidates now have an explicit keep/rewrite decision without retiring an ID, changing the graph or removing an early reward. No broad reward increase and no generated replacement filler are authorized by this audit.

Painter item localization is independent of those deletion decisions. All five preserved Painter templates require complete Russian name, short name and description records while retaining their IDs and English fallback.
