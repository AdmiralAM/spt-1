# Admiral Trader gameplay doctrine

## Purpose

Admiral is a **full specialist trader with an authored campaign**, not a quest-unlock terminal and not a preserved QuestManiac content dump.

His long-term role is to replace the useful gameplay value of the Andrudis/QuestManiac trader ecosystem when that legacy mod is disabled, while avoiding its trader zoo, repetitive grind and uncontrolled reward economy.

The player-facing loop is:

`trade -> build relationship -> take authored contracts -> earn milestones/capabilities -> expand specialist access`

Admiral must already be worth opening before the player completes a quest. Quests deepen the relationship and unlock high-value capabilities; they do not constitute the entire shop.

Quest count is deliberately **not capped**. Thirty-one current authored quests are a proven runtime backbone, not a product target. Fifty, one hundred or more quests are acceptable when they remain distinct, authored, purposeful and progression-relevant.

## Trader identity

Admiral is an **expeditionary procurement and specialist field-logistics broker**.

He should occupy the space between ordinary commodity traders and high-end one-off rewards:

- mission-preparation and specialist field equipment;
- controlled access and clearance-related procurement;
- curated weapon/project components and presets;
- bounded ammunition capability supply;
- unusual but defensible utility stock and barters;
- milestone goods that feel earned rather than merely level-gated.

The identity test for every baseline offer is:

1. why does Admiral sell this?
2. why should the player check Admiral instead of a vanilla trader, Scorpion or Artem?
3. does the offer support preparation, access, specialization or a named Admiral project?

If those questions do not have clear answers, the item does not belong in the baseline catalogue.

## Three stock layers

### 1. Core / baseline stock

Visible from first contact. Small, finite and characteristic of Admiral.

Baseline stock must **not** be quest-gated. It exists so the trader is useful and recognizable before campaign progression. It should avoid becoming a general-purpose supermarket and should minimize direct duplication with vanilla traders, Scorpion and Artem.

### 2. Relationship stock

Additional specialist stock may appear through player level plus Admiral standing/loyalty when that progression is meaningful. Relationship stock is allowed because loyalty represents trust/status, but it must not replace explicit milestone gates for exceptional capabilities.

Sales-sum grind remains forbidden.

### 3. Capability / milestone stock

Rare ammunition, special presets, clearance items, exceptional equipment and other high-impact privileges may be quest-gated.

These unlocks must be finite, auditable and clearly communicated in the quest reward/payoff. A hidden backend `questassort` mapping is not sufficient player-facing communication by itself.

## Campaign domains

### Access / Intelligence

Operational clearance, keys, restricted areas, reconnaissance and access preparation. Requirements should prefer meaningful capability checks over generic FIR collection spam.

### Arsenal / Projects

Weapon-family competence, specialized builds, testing, acquisition and field use. The current Qualification -> Fieldwork -> Munitions tracks are one backbone, not the only acceptable weapon quest structure.

### Munitions

Capability-based ammunition progression and controlled supply. High-impact ammunition does not automatically qualify for permanent supply.

### Field Operations

Map-specific authored work with context, changing conditions and concrete operational purpose. These quests should resemble strong vanilla trader contracts rather than generated kill ladders.

### Special Operations / Milestones

Longer or harder chains with distinctive rewards, presets, access or other durable payoffs.

### Collections / Projects

Optional long-form projects are acceptable when every step contributes to a coherent project. Raw handover quantity is not sufficient purpose.

Additional domains may be added when their player-facing purpose and payoff are explicit.

## Quest quality contract

Every quest must answer four questions in player-facing EN/RU text:

- **Why** — why Admiral needs this done;
- **What** — what the player must actually accomplish;
- **Context** — why these conditions/location/items matter;
- **Payoff** — what money, items, standing, unlock or capability the player gains.

Internal TPLs, condition IDs, technical codes or opaque placeholders must never be exposed as the final objective text.

Adjacent quests may reuse a domain, but they may not differ only by item name or larger counters. A large campaign is acceptable; repetitive template churn is not.

## Reward hierarchy

Rewards are selected for the specific contract, not from a single universal ladder. Preferred components are:

1. **Milestone/capability unlock** when the quest actually proves a capability;
2. **Distinctive item/preset/sample reward** that makes the completion visible and useful;
3. **Standing / XP** as relationship/progression glue;
4. **Generic economic value** as supporting compensation, not the sole identity of most authored quests.

Quest UI should make the important payoff legible. If a quest permanently unlocks a trader offer, its briefing/completion text must state that explicitly even when the native UI cannot render `questassort` as an item reward.

Special Weapons remains sample-only by default. High-impact or exceptional ammunition requires an explicit design decision before becoming renewable supply.

## Anti-goals

Admiral must not become:

- an empty trader whose entire catalogue is hidden behind quests;
- a seventh unrestricted supermarket;
- a copy of vanilla trader assortments;
- a copy of Scorpion or Artem;
- a generic loyalty ladder where level alone grants best-in-slot capability;
- a resurrection of six legacy trader identities;
- a generated daily/repeatable task machine;
- an FIR/handover busywork machine;
- an opaque economy subsystem that downstream auditing cannot inspect.

## Quest admission test

A proposed quest enters the campaign when it passes the following checks:

1. It has an identifiable authored purpose in Admiral's world and relationship.
2. Its objective is materially distinct from adjacent quests; changing only item names or kill counts is insufficient.
3. Its completion has a meaningful payoff: narrative milestone, item/preset, standing, access, project advancement or capability.
4. It does not merely duplicate an existing vanilla quest without new context or progression value.
5. It can be represented using maintainable SPT quest/reward data and audited deterministically.
6. It does not require unsafe broad profile mutation to function.

A quest does **not** fail merely because the campaign is already large. Curation optimizes quality density, not minimum quest count.

## Andrudis curation rule

Legacy Andrudis/QuestManiac content is source material.

Preserve or re-author strong concepts, unusual objectives, unique weapon/preset ideas, milestone rewards and coherent narrative chains. Remove or rewrite literal duplicates, vanilla duplicates without added value, empty 10/20/30 ladders, excessive FIR/handover spam, purposeless hideout chores and reward faucets.

The target integration test assumes QuestManiac can be disabled and Admiral progressively replaces its useful authored value. Admiral does not depend on keeping the six legacy traders enabled.

## Economy Admiral boundary

Admiral Trader owns:

- authored quest/progression semantics;
- baseline / relationship / milestone stock classification;
- explicit quest and standing gates;
- finite stock and buy ceilings;
- capability and sample/permanent semantics;
- ownership/provenance declarations.

Economy Admiral owns:

- global source-pressure analysis;
- price/reward benchmarking and normalization proposals;
- provenance/health checks;
- future enforcement policy where explicitly approved.

Admiral therefore publishes transparent native-shaped data plus an ExplicitAdapter contract. It must not implement a second global economy policy engine.

## Balance invariants

- baseline stock exists and is not quest-gated;
- milestone/capability offers may be quest-gated and remain finite/buy-limited;
- sales sum does not gate Admiral progression;
- standing/loyalty cannot bypass an explicit milestone quest gate;
- exceptional/high-impact ammunition may remain sample-only;
- Access content must not collapse exploration into unlimited access supply;
- reward value should remain defensible against vanilla progression and Economy Admiral evidence;
- removed legacy content stays removed unless it independently passes the current admission test.

## Evaluation metrics

Evaluate Admiral by behavior rather than by quest count:

- **baseline usefulness:** fresh-profile Admiral has a reason to visit;
- **identity overlap:** direct baseline duplication with vanilla/Scorpion/Artem is minimized and justified where unavoidable;
- **quest quality density:** authored purpose and payoff remain high even as campaign size grows;
- **privilege clarity:** the player understands what changed after a quest;
- **reward visibility:** important items/standing/unlocks are visible or explicitly explained;
- **economy displacement:** Admiral complements rather than dominates other acquisition routes;
- **legacy leakage:** disabled QuestManiac content does not reactivate implicitly;
- **runtime safety:** SPT 4.1.3 startup, trader/quest registration and native schema contracts remain fail-closed and regression-tested.

## Current campaign interpretation

The existing 31 quests and seven quest-gated offers are **Runtime Prototype A**: they proved the registration, quest graph and capability-unlock foundations. They are not the final gameplay scope.

Gameplay Alpha begins by adding a unique non-empty baseline catalogue, making objectives/rewards legible, then expanding/re-authoring the campaign from strong Andrudis material without an arbitrary quest-count ceiling.
