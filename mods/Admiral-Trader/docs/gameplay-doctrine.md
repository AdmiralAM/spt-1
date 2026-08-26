# Admiral Trader gameplay doctrine

## Purpose

Admiral is not another general-purpose shop and not a preserved QuestManiac content dump. He is a **capability broker**: a compact campaign that asks the player to demonstrate access and weapon competence, then grants narrow, finite logistical privileges.

The loop is:

`prove capability -> receive a bounded privilege -> use that privilege elsewhere`

A quest should therefore exist only when it changes what the player can reliably do after completion. Raw quest count is not a success metric.

## Three product pillars

### 1. Access Protocol — mobility and permission

Access Protocol represents operational clearance. Its rewards should improve reliable access to places or activities without replacing exploration. Requirements should prefer capability checks over FIR collection spam.

Design test: after the quest, can the player attempt something they could not reliably schedule before?

### 2. Arsenal Protocol — specialization, not linear power creep

Each weapon family is an independent three-step micro-campaign:

1. **Qualification** — demonstrate baseline handling/ownership capability.
2. **Fieldwork** — use the family under meaningful combat conditions.
3. **Munitions** — prove sustained commitment and earn a narrow ammunition privilege or sample.

Families should remain parallel wherever possible. Completing one family must not silently unlock another. The result is player-selected specialization rather than a mandatory 21-quest ladder.

### 3. Logistics — reliability with scarcity

Admiral converts accomplishments into predictable but bounded supply. Permanent offers must remain finite per reset and purchase-limited. The trader should reduce frustrating acquisition variance, not erase scarcity or make other traders, loot, crafting, or the flea irrelevant.

## Reward hierarchy

Prefer rewards in this order:

1. **Capability unlock** — a narrow finite offer tied directly to the demonstrated skill/access domain.
2. **Sample** — one or a few units that let the player experience a capability without creating permanent supply.
3. **Standing / XP** — progression glue, benchmarked against vanilla quest bands.
4. **Generic economic value** — only when needed to make the quest reward coherent; never the primary identity.

Special Weapons is intentionally sample-only. High-impact or exceptional ammunition does not automatically qualify for a permanent Admiral offer.

## Anti-goals

Admiral must not become:

- a seventh unrestricted supermarket;
- a duplicate of vanilla trader loyalty progression;
- a source of best-in-slot ammunition merely for reaching a level threshold;
- a resurrection of six legacy traders or thousands of legacy quests;
- a daily/repeatable task generator;
- an FIR/handover busywork machine;
- an opaque economy subsystem that downstream auditing cannot inspect.

## Quest admission test

A proposed quest enters the curated campaign only if all of these are true:

1. It expresses one identifiable capability: access, weapon-family competence, or a future explicitly approved Admiral domain.
2. Its objective is materially different from adjacent quests; changing only item names or kill counts is insufficient.
3. Its completion grants or advances a meaningful bounded privilege, sample, or campaign decision.
4. It does not duplicate a vanilla quest whose existing progression already serves the same purpose.
5. It can be represented using native-style SPT quest/reward/assort data and audited deterministically.
6. It does not require broad permanent profile mutation to function.

If any condition fails, default action is exclusion rather than preservation.

## Expansion rules

Future content should deepen the capability-broker role instead of increasing catalogue size. Preferred expansion directions are:

- **clearance chains** where one earned permission enables a small set of meaningful operations;
- **family specialization** with alternative weapon families that remain independent;
- **bounded requisition** where a difficult proof unlocks a scarce replenishable resource;
- **sample missions** for exceptional equipment that should be experienced but not sold permanently;
- **cross-system contracts** only when they connect existing SPT systems without bypassing their progression.

Do not add a new domain until its player-facing privilege can be stated in one sentence.

## Balance invariants

These are design invariants, not tuning suggestions:

- unlocks are earned by quests, never merely exposed by trader level;
- permanent offers are finite and buy-limited;
- one family cannot unlock another family's ammunition;
- exceptional/high-impact ammunition may remain sample-only;
- Access Protocol must not collapse exploration into unlimited key-card supply;
- reward value should stay within defensible vanilla progression bands unless a deliberate scarcity privilege justifies the difference;
- removed legacy content stays removed unless it independently passes the current admission test.

## Evaluation metrics

Before publication, evaluate the campaign by behavior rather than quest count:

- **choice density:** how often the player can choose which capability to pursue next;
- **privilege clarity:** whether each quest's lasting benefit can be explained succinctly;
- **redundancy:** adjacent quests that ask effectively the same thing should approach zero;
- **economy displacement:** Admiral supply should complement, not dominate, other acquisition routes;
- **cross-family leakage:** must be zero;
- **legacy leakage:** removed traders/quests must not reactivate implicitly.

These metrics are suitable for static validation except economy displacement, which also benefits from later runtime/playthrough evidence.

## Current campaign interpretation

The current 31-quest shape is therefore not "31 quests worth of content" as an objective. It is ten access proofs plus seven optional three-step specialization tracks. The seven finite permanent offers and the Special Weapons sample are consequences of those proofs, not the trader's primary catalogue.

This doctrine is the acceptance filter for subsequent concept polish. New features that cannot strengthen this loop should live in another mod rather than expanding Admiral Trader by default.
