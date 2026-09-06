# Admiral Trader — Product Design Authority

Status: **FINAL / DESIGN SCOPE CLOSED**

This document is the human-readable authority for the completed Admiral Trader product-design scope. The machine-readable authority is `manifests/product-design-final.json`.

The detailed M3 manifests remain supporting evidence and implementation detail. They are **not competing design authorities** and should not trigger further design churn by default.

## Product direction

Admiral Trader is one curated trader, not a recreation of the old multi-trader ecosystem. Useful ideas from Andrudis/QuestManiac, Natalya and other trader implementations are absorbed selectively into Admiral; repetitive, redundant, grind-heavy or technically weak material is rejected.

The design principle is reuse-first: use native SPT behavior and proven maintained C# trader/quest patterns where they fit, then adapt only the minimum Admiral-specific layer required for identity, progression, quests, rewards and finite stock.

## Final M3 campaign

The final design contains **12 operations in four acts**.

### Act I — Establish the Network / Развернуть сеть

1. **Acoustic Discipline / Акустическая дисциплина** — level 15
2. **Forward Reserve / Полевой резерв** — level 15
3. **Low Profile / Низкий профиль** — level 15
4. **Mobility Doctrine / Манёвренная защита** — level 17
5. **Borrowed Access / Чужой доступ** — level 18

### Act II — Keep the Routes Open / Удержать маршруты

6. **Acoustic Contact / Акустический контакт** — level 20
7. **Route Security / Безопасный маршрут** — level 21
8. **Contractor Intercept / Перехват контрактников** — level 23

### Act III — Deny the Threat / Лишить противника инициативы

9. **Observation Window / Окно наблюдения** — level 28
10. **Heavy Assault / Тяжёлый штурм** — level 30
11. **Break the Perimeter / Прорыв периметра** — level 31

### Act IV — Operate Without Support / Работать автономно

12. **Internal Security / Внутренняя охрана** — level 37

## Original 15-operation disposition

The old post-0.1.0 list is not preserved as a quota.

| Original concept | Final disposition |
| --- | --- |
| Signal Discipline | Rewrite as Acoustic Discipline |
| Field-Expedient Supply | Rewrite as Forward Reserve |
| Expedition Loadout | Rewrite as Low Profile |
| Protection Calibration | Rewrite as Mobility Doctrine |
| Access Reconnaissance | Rewrite as Borrowed Access |
| Ballistic Head Test | Merge into Heavy Assault |
| Acoustic Contact | Keep |
| Route Security | Keep |
| Hostile Operator Intercept | Rewrite as Contractor Intercept |
| Precision Observation Window | Keep as Observation Window |
| Precision Denial | Merge into Observation Window |
| Heavy Assault Loadout | Rewrite/merge as Heavy Assault |
| Rogue Interdiction | Rewrite as Break the Perimeter |
| Endurance Circuit | Drop from M3 |
| Labs Security Disruption | Rewrite as Internal Security |

No filler quest is added to preserve the number fifteen.

## Quest quality authority

Every admitted quest must have a concrete operational **Why / What / Context / Payoff** and a materially distinct player decision.

The campaign deliberately rejects:

- map-by-map copies of the same objective;
- xN / x2N / x5N body-count ladders;
- equipment-class ladders whose only change is the required item category;
- key-collection catalogues presented as access gameplay;
- generic survive-on-several-maps checklists;
- player-facing developer language explaining anti-grind design;
- unsupported claims that SPT observed an action it cannot actually observe.

Shared maps, equipment or target types are acceptable only when the operation solves a genuinely different problem.

## EN/RU authority and Admiral voice

Player-facing English and Russian are authored independently from the same intent; Russian is not a literal translation layer.

Admiral's voice is:

- professional;
- restrained;
- operational;
- experienced and practical;
- focused on routes, logistics, access, threat control and unnecessary risk.

It is not a military parody, generic aggression, developer commentary, or repeated narration of counters.

The detailed approved copy is in `manifests/m3-campaign-editorial-copy.json`; the writing rules are in `docs/m3-quest-writing-standard.md`.

## Reward authority

The current final M3 design envelope is:

- **12 operations**;
- **106,500 XP** total;
- **661,000 RUB** total;
- **+0.157 standing** total;
- **0 selected item rewards**;
- **0 permanent unlocks** in this design slice.

Reward rules:

- reward actual risk, difficulty and campaign contribution rather than raw counter size;
- removed or merged quests do not donate their old reward budget to surviving quests;
- Economy Admiral remains owner of global economy normalization;
- one-time thematic samples may be reviewed later only as a deliberate separate decision, with cash offset and Economy review; they are not part of this closed M3 authority.

## Progression authority

The M3 graph is intentionally non-linear.

- no sales-volume gate;
- no repeatable-task gate;
- no quest-count gate;
- no standing or loyalty requirement used as a hidden quest prerequisite;
- maximum two total direct prerequisites per operation;
- four roots: Acoustic Discipline, Forward Reserve, Low Profile and Borrowed Access;
- terminal operations: Break the Perimeter and Internal Security.

The detailed graph is in `manifests/m3-campaign-progression.json`.

## External content absorption

### Natalya

Absorb selectively:

- Exfil evacuation/logistics premise;
- route continuity across Tarkov;
- support caches and resupply narrative;
- marked transit/corridor narrative;
- escalating threats around moving people and supplies.

Do not absorb:

- a second Natalya trader requirement;
- the Pay Back map-by-map body-count chain;
- duplicate Weapons Training chains;
- wholesale quest/imported dialogue;
- broad assort/custom armor dependencies merely for fidelity.

Natalya's Exfil DNA is used as narrative glue for operations such as Forward Reserve, Borrowed Access, Route Security and Break the Perimeter rather than imported as an eleven-part checklist.

### Andrudis / QuestManiac

Use the corpus as an idea/theme inventory and provenance source. Final M3 concepts draw selectively from themes including Ultrasound, Errand Boy, Deep Pockets/Tarkov Mule, Iron Head/Juggernaut, Keys Proficiency, Scav Hunt, PMC Hunt, Sniper Life, Rogue Hunt and Raider Hunt.

Do not recreate:

- six separate traders;
- thousands of quests as a quantity target;
- repetitive count ladders;
- cosmetic map copies;
- wholesale bundles or WTT dependency merely to preserve the old architecture.

### Admiral Artyom Revival

Use as an in-repository reference for current SPT C# organization: clear load order, helper separation, data-driven trader assets/base/assort/locales and focused orchestration. Do not copy unrelated custom-item/clothing scope or WTT dependency when Admiral does not need it.

### Scorpion C# / Ref Friendly Quests C#

Use the maintained repositories as references for focused C# helper/config/model/data separation and native SPT table-oriented quest/trader integration. Add custom routers or compatibility machinery only when Admiral has the same demonstrated need.

Legacy acidphantasm Scorpion/RefChanges sources remain behavioral and provenance references, not preferred implementation over maintained C# code.

## Closed and deferred concepts

The following do not remain open M3 design tasks:

- **Field Medicine** — out of M3; do not fake treatment skill with a shopping-list handover.
- **Chemical Support** — out of M3; do not fake stimulant-use gameplay with procurement or ordinary kills.
- **Endurance Circuit** — dropped from M3; no generic cross-map extraction checklist.
- **Boss Command Strike** — no reserved slot; can return only through an explicitly reopened M4 curation decision with a unique operational purpose.
- **Cultist Night Operation** — no reserved slot; same rule as above.

## SPT compatibility policy

The current development/validation baseline is **SPT 4.1.5**. Runtime metadata remains **`~4.1.0`**.

Exact SPT patch versions are used for reproducibility and validation evidence. They are not automatic runtime refusal gates. A newer compatible `4.1.x` release should not be blocked only because the patch number changed. Narrow the compatibility range only after a demonstrated API/data incompatibility.

## Implementation boundary

This document does **not** authorize runtime implementation.

Functional implementation remains gated by:

1. M1 lifecycle acceptance;
2. M2 existing-campaign acceptance;
3. explicit user permission for new functional implementation beyond the already approved scope.

No further design churn is expected by default. Reopen this design only if:

- the user changes product direction;
- a demonstrated runtime constraint invalidates a final design choice; or
- a new external source materially improves one specific operation.

Otherwise the product-design workstream is **complete and closed**.
