# Admiral Trader post-consolidation content audit

Status: **decision report; no offers or quests removed by this audit**. Runtime snapshot: Admiral Trader 0.3.0 at `f78831376200133bb7b081f9e323b35dfad668ef`, SPT 4.1.6 with the exact SPT 4.1.5-built server DLL.

## Measured scope

The authored Admiral layer contains 172 quests and 82 finite core offers. The consolidated external layer adds 402 roots / 946 rows, 35 quests, 44 quest unlocks and 68 suits. TGC contributes 114 unique root templates, Artem 281 roots (277 root templates), Painter 7 roots (5 templates), and the retained Natalya rack 35 complete weapon presets.

The root-template comparison found 23 template overlaps and 49 involved offers. Comparing the complete item tree, rather than the root template alone, reduced this to 13 exact product-tree overlaps and 27 involved offers. Same-base weapon presets with different installed parts are therefore not classified as duplicates.

## Storefront decisions

| Product | Existing paths | Decision | Reason |
| --- | --- | --- | --- |
| Documents case | Admiral LL1 barter; Artem LL2 barter | retain both for now | The payments serve different loot routes. Audit their effective value before choosing one. |
| Labs access card | two authored Admiral quest barters; Artem LL4 cash | retain | These are three materially different acquisition paths and progression gates. |
| `.45 ACP AP` | Admiral LL1 cash; Artem LL4 cash | **removal candidate: Artem offer `66bf757f27d0b097db0acf0c`** | Identical loose ammunition; the later offer adds no role and is only marginally cheaper. |
| `5.56x45 M856A1` | Admiral LL1 premium cash; Artem LL3 cheaper cash | retain | Price and loyalty create a legitimate early-premium versus later-standard choice. |
| `7.62x51 M62` | Admiral LL1 cash; Artem LL4 cash | **removal candidate: Artem offer `66bf757f27d0b097db0acf10`** | The later route is both more expensive and less accessible. |
| Injector case | Admiral LL1 barter; Artem LL3 barter | retain for value audit | Different barter ingredients may support distinct loot decisions; neither should be removed without valuation. |
| AFAK | Admiral LL2 cash; Artem LL3 cheaper cash | retain | The later discount is a normal loyalty progression rather than grid padding. |
| `5.56x45 M855A1` | Painter quest-linked LL4; Artem LL3 cash | retain | The Painter offer carries a preserved quest-unlock identity; removal would require an authored unlock decision. |
| `7.62x51 M61` | Painter quest-linked LL4; Artem LL4 cash | **removal candidate: Artem offer `66bf757f27d0b097db0acf0a`** | Same loyalty and product; the Painter path is cheaper and tied to a preserved unlock. |
| Painter reward boxes | cash and GP-coin paths for each box | retain | The second row is a barter alternative, not an accidental duplicate. |
| Grizzly and 60-round AK magazine | Artem cash and barter paths | retain | Each pair deliberately offers money or barter acquisition. |

No Natalya/TGC weapon row is an exact assembled-tree duplicate. Shared base weapon templates are different presets and remain outside the removal set.

## Quest portfolio decisions

The 172 authored records divide into 10 access protocols, 40 paced Arsenal tasks, 4 Ground Zero introductory operations, 6 equipment-set operations, 12 cross-chain operations and 100 map-story quests. The access protocols and Arsenal lanes are repetitive by category but have distinct progression jobs; they are not deletion candidates in this pass.

### High-confidence merge or removal candidates

| Quest | Decision candidate | Graph-safe replacement direction |
| --- | --- | --- |
| `208db81b5ce195bf0c176852` — **Низкий профиль** | merge into the equipment lane | It repeats the earlier “enter in cheap rig/backpack and survive” contract without an additional action. Move any useful Interchange flavour into `Комплект: Первый выход` or its successor, then point `Манёвренная защита` to the retained equipment step. |
| `8dad0d354ac000b7bbf05b9a` — **Акустическая дисциплина** | merge with `41a41cb262ea084c1e110513` — **Акустический контакт** | Both require the same headset pool on Woods. One coherent task can combine the two visits, limited combat and successful extraction. Rewire the retained task to `Комплект: Акустический контроль`; keep `Окно наблюдения` behind the retained result. |
| `3c6e085fc02f0597efdb5d5a` — **Операция: Открытый коридор** | fold into Ground Zero introduction | It is the second plain Scav counter on the same early map and does not introduce a new decision. |
| `31ab6a69a8436df6b3834b0a` — **Операция: Спорная территория** | fold into Ground Zero introduction | Replacing Scavs with two PMCs changes target type but still forms a short isolated kill ladder beside the ten-step Ground Zero story. |
| `e520cec55b83621928e9e4ec` — **Операция: Правильный выход** | fold or retire | It returns to an unrestricted five-target counter and is the weakest final step of the four-quest ladder. |

`02c07ee31821696597ceabef` — **Операция: Первый контакт** should remain as the single combat onboarding record because the access protocol already uses it as an entry gate. Any collapse must preserve that ID and redirect dependants before the other three templates are retired.

### Retain, but rewrite or value-check

- `56813681ae0690016376f163` — **Полевой резерв** breaks up raid objectives with a small procurement task; retain it, but compare the fuel-can reward with live flea and trader values.
- The 10 access protocols remain useful map-oriented FIR key collection and key-upgrade tasks. Keep the branch; review only rarity and reward value.
- The 40 Arsenal records remain the two-choice weapon rotation requested for campaign pacing. Their category repetition is intentional; objective wording should be shortened where the client already lists the full pool.
- The 100 map-story records retain their locations and graph. Repeated visit/retrieve/place mechanics require better local story context and more varied physical rewards, not wholesale deletion.

## Approved implementation boundary

The next content change may remove only the three clearly inferior offer rows listed above and may consolidate only the five quest candidates after their successor prerequisites and rewards are explicitly mapped. Persistent IDs remain recorded as retired identities; completed profile history is not rewritten. Price and loyalty normalization must use effective item value, stock limit, quest gate and acquisition route together. No broad reward increase and no generated replacement filler are allowed.

Painter item localization is independent of those deletion decisions. All five preserved Painter templates require complete Russian name, short name and description records while retaining their IDs and English fallback.
