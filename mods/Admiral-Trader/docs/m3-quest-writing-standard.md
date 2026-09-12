# Admiral Trader — M3 Quest Writing Standard

Status: **product/editorial authority only**. This document does not authorize runtime materialization and does not change the M1 -> M2 -> M3 milestone order in PR #328.

Runtime target: SPT 4.1.5. Historical SPT 4.1.3 proofs from PR #297 are research evidence only and must be revalidated where runtime semantics matter.

## Product objective

Admiral quests must feel like authored operations issued by one competent character, not generated counters with military wording. Quest count is not a KPI. A weak or duplicated operation is merged, rewritten or removed; it is never replaced with filler solely to preserve a target count.

Every admitted quest must answer four player-facing questions without exposing design machinery:

1. **Why** is this a problem for Admiral or his network?
2. **What** does the player have to accomplish?
3. **Context**: why this location, equipment, target or route?
4. **Payoff**: what changed after the operation succeeded?

## Admiral voice

Admiral is experienced, restrained, practical and operationally literate. He values preparation, logistics, intelligence, controlled violence and knowing when an operation is finished.

He is not:

- Prapor with different vocabulary;
- a sadist who invents body-count quotas;
- a lore encyclopedia;
- a military-parody generator;
- a developer explaining anti-grind rules to the player.

Preferred voice characteristics:

- short causal explanations;
- concrete operational stakes;
- confidence without theatrical bravado;
- criticism aimed at bad decisions, not at the player for its own sake;
- violence described as a means to restore access, deny a threat or protect a route;
- success text describes the result, not the fact that a counter reached its target.

Representative doctrine line:

> If the job is finished after four shots, the fifth is no longer part of the operation.

## Player-facing language vs internal constraints

Internal design constraints must never leak into quest copy.

Do not show phrases such as:

- bounded contact;
- proven equipment/category;
- exact allowlist;
- anti-grind;
- no x2/x5/x10 ladder;
- body-count ledger;
- do not farm;
- semantic overlap;
- materialization proof;
- final selected target before selection is actually final.

The player gets natural instructions. The manifest, validator and tests keep the exact technical rules.

Example:

- Internal: `maximumTargetCount=2`, exact headset TPL allowlist, same-raid extraction.
- Player EN: `Use a headset, check the route, deal with the immediate threat if necessary, and get out.`
- Player RU: `Возьми гарнитуру, проверь маршрут, при необходимости сними непосредственную угрозу и выходи.`

## EN/RU editorial policy

English and Russian are authored from the same scene and intent, not translated line-by-line.

Russian text must sound like native Russian operational speech. Avoid calques such as:

- `ограниченный контакт` for bounded contact;
- `подтвержденная категория` for proven category;
- `дисциплина сигнала` for acoustic awareness;
- `точный запрет` for precision denial.

Stable terminology:

| English | Russian |
| --- | --- |
| Scav | Дикий |
| PMC | ЧВК |
| Rogue | Отступник |
| Raider | Рейдер |
| Lighthouse | Маяк |
| Customs | Таможня |
| Reserve | Резерв |
| The Lab / Labs | Лаборатория |
| extraction | выход / эвакуация, by context |
| loadout | выкладка / комплект, by context |
| route | маршрут |
| perimeter | периметр |
| reconnaissance | разведка |

Do not translate `interdiction` mechanically. Use `перехват`, `подавление`, `срыв`, or another context-specific word.

## Uniqueness gate

A quest is rejected or merged if its defining player experience is already present in another Admiral operation after removing cosmetic differences.

Cosmetic differences include:

- a different armor class with the same wear-kill-extract loop;
- a different weapon family with the same kill quota;
- the next distance threshold;
- the next map with no different operational purpose;
- a larger target count;
- a different item list serving the same procurement sink.

Two operations may share components only when the **reason, decision and payoff** differ materially.

Examples:

- `Acoustic Discipline` may use a headset as a non-combat reconnaissance gate.
- `Acoustic Contact` may later use a headset in a short combat application.
- A third headset quest with another map or model list is not admitted.

## Mechanical design gate

Prefer native SPT/EFT quest mechanics with exact, testable semantics. Product copy must never promise a mechanic the runtime cannot observe.

Preferred shapes include:

- explicit location;
- VisitPlace proxy when it truthfully represents being in the area;
- explicit Equipment allowlists;
- bounded Kills counters;
- FindItem / HandoverItem for intentional procurement;
- ExitStatus(Survived) when successful extraction is part of the operation.

Fail closed when the desired player contract depends on unproven same-raid coupling or condition semantics. Simplify the quest before inventing custom profile-state machinery.

## Duration and grind

Default target is one meaningful operation or a small set of distinct operations, normally one to three ordinary raids for a competent player.

Forbidden by default:

- escalating copies of the same quest;
- four-digit target counts;
- model-by-model equipment collection;
- map-by-map duplicate kill quests;
- repeated extraction counters with no new decision;
- repeatables whose only purpose is resource generation.

## Reward doctrine

Reward must pay for risk, campaign contribution and inconvenience, not raw counter size.

Default reward:

- XP;
- RUB;
- Admiral standing.

A thematic one-time item sample may be used only when all of the following are true:

1. it reinforces the operation's identity;
2. Economy Admiral approves the item and quantity;
3. its value replaces part of the cash budget rather than stacking on top;
4. it does not create a renewable rare-item, armor, weapon, ammo, stim, keycard or container faucet;
5. the compound item is complete and valid if it has required children.

Permanent stock unlocks are rarer than quest rewards and require separate capability justification. A storefront unlock must not exist solely to start another same-shaped quest.

## Reward comparison

For every future runtime quest, compare against the nearest vanilla comparator by level, objective class and practical risk. Global percentile ceilings are a secondary guard, not the primary comparator.

Standing is campaign trust, not a completion coupon. Do not inflate standing merely because a quest was merged or another quest was removed.

## Merge/reject policy

Removing a quest does **not** automatically redistribute its XP, RUB, standing or item reward to neighboring quests.

A merged quest receives only the reward justified by its final difficulty and campaign placement.

Current product dispositions from the PR #297 archive review:

- `ballistic-head-test`: merge helmet identity into `heavy-assault-loadout`; no standalone quest planned.
- `precision-denial`: merge its advanced precision purpose into `precision-observation-window` unless a genuinely different target/problem is later proven.
- `endurance-circuit`: hold for rewrite; a three-map extraction checklist alone is not enough product identity.

## Campaign narrative spine

M3 should read as one campaign rather than a folder of capability checks.

### Act I — Establish the Network

Admiral determines whether the player can be trusted with routes, supplies, low-profile movement and restricted access.

### Act II — Keep the Routes Open

The network begins meeting active resistance. The player protects routes, uses reconnaissance under contact and denies hostile contractors a useful window.

### Act III — Deny the Threat

The player handles specialist precision, heavy assault and Rogue perimeter problems where preparation matters more than volume of kills.

### Act IV — Operate Without Support

Only operations with a distinct late-game decision belong here. Labs security is admitted. Generic multi-map survival remains on hold until it has a stronger purpose than proving repeated extraction.

## Admission checklist

Before any operation enters runtime M3, all of the following must be true:

- distinct operational problem;
- Why / What / Context / Payoff complete;
- natural EN and RU briefing, objective and success text;
- no semantic duplicate inside Admiral;
- vanilla / approved external-content overlap reviewed;
- exact SPT 4.1.5 condition semantics proven;
- same-raid promises proven when the text requires them;
- concrete level placement;
- nearest-comparator reward review;
- Economy Admiral approval for reward numbers and any item sample;
- no hidden dependency on legacy trader IDs or legacy quest templates;
- no change to M1/M2 runtime foundation until their gates are complete.
