# Capability goal vertical slice

## Decision under test

Quest Planner should survive only if it can compress a modded progression/economy question that neighboring quest UI cannot answer cheaply.

The first keep/kill candidate is deliberately narrow:

> Select a concrete gameplay capability or durable access path, resolve the quest gate from explicit evidence, reuse the authoritative future-goal planner to identify actionable work, and explain what supply/access state completing that gate is expected to create.

This is not a generic economy optimizer and not a second quest tracker.

## Why Admiral Trader changes the product question

Admiral Trader's maintained doctrine treats permanent offers as capability acquisition rather than ordinary loyalty inventory. The current cross-workstream contract describes finite, quest-gated permanent offers and sample-only content. Therefore a quest completion may represent a persistent gameplay-state change: durable access to an ammo family, Labs access, or another bounded capability.

The player question becomes stronger than `which quest next?`:

> What should I do now if my actual goal is to gain capability X?

Quest Planner owns only the decision path from that goal to current raid work. Admiral Trader remains authority for its gates/capability identity. Economy Admiral remains authority for supply pressure, normalization and policy.

## Generic input contract

A capability definition contains only evidence required for planning/explanation:

- stable capability ID;
- explicit gate quest ID;
- owning source/mod identity;
- optional item template identity;
- supply kind: unknown, one-time sample, bounded renewable, unbounded renewable;
- optional finite reset limits only for bounded renewable supply;
- evidence-source identity.

The Planner must not infer this contract from trader names, LL, item names or prices.

### Fail-closed rules

- gate quest must exist in the final Planner topology;
- bounded renewable evidence requires at least one explicit positive reset limit;
- one-time sample cannot carry renewable reset capacity;
- unbounded/unknown supply cannot carry fabricated finite limits;
- missing external economy evidence may reduce the explanation but must not block ordinary quest-goal planning.

## Reuse, not a second planner

`PlannerCapabilityGoalBuilder` converts the explicit capability gate into the existing `PlannerRaidDecisionIntentBuilder` workflow. All prerequisite semantics remain owned by the quest-goal engine:

- raw prerequisite status contracts;
- final modded topology;
- profile-confirmed Active/Available frontier;
- terminal prerequisite conflicts;
- delayed availability;
- focused overlap/unlock evidence;
- conservative Best / Several / None decisions.

Capability data does not receive its own ranking weights.

## First test scenarios

### Controlled ammo capability

Given an Admiral-style bounded renewable ammo offer gated by `Munitions`, and `Fieldwork` is the currently actionable prerequisite:

- goal = ammo capability;
- quest goal = `Munitions`;
- actionable work = `Fieldwork`;
- raid comparison remains focused on advancing `Fieldwork` and its path;
- final explanation may say that the gate creates bounded renewable supply because explicit adapter evidence says so.

### Labs access

Given a finite Labs access offer gated by `Access Protocol: Clearance`:

- goal = Labs access capability;
- planner resolves the real incomplete Clearance path from the final modded graph;
- current raid recommendation must be justified by the actionable prerequisite frontier, not by raw global quest density.

### Special Weapons sample

A one-time Special Weapons reward is not presented as durable renewable capability. It may still be a goal, but its outcome must be described as one-time/sample evidence.

## UX target for the first live candidate

The first candidate should answer in one compact surface:

- `GOAL`: capability/access selected;
- `GET IT BY`: exact gate quest;
- `DO NOW`: one raid recommendation, several honest alternatives, waiting-only state, or no proven action;
- `WHY`: focused shared action / focused unlock / readiness evidence;
- `AFTER`: next focused step or gate completion;
- `RESULT`: proven capability/supply semantics when external evidence exists;
- `CAUTION`: unresolved eligibility, delay, preparation or missing external evidence.

No numeric planner score and no generic map leaderboard are required for this experiment.

## Keep / kill acceptance gate

Do not keep Quest Planner because its graph model is technically sophisticated. Keep it only if the live vertical slice changes player decisions.

### KEEP candidate

After selecting an actual capability goal, the player can determine the next useful action in roughly 10-15 seconds and the answer joins information that would otherwise require several independent surfaces: locked quest chain, current profile availability, raid overlap/preparation and the meaning of the final unlock.

### KILL candidate

Retire or drastically reduce Quest Planner if, after the vertical slice is usable:

- the answer is usually obvious from Tasks/Trader UI;
- the recommended action is not materially better than selecting the gate quest in Quest Tracker;
- most useful capability goals cannot be expressed through explicit gates/evidence;
- cross-mod evidence adds explanation but does not change a decision;
- the UI still feels like a quest spreadsheet with a different headline.

The first live decision should be binary: prove this workflow, then KEEP or KILL before expanding integrations.

## Runtime/performance constraints

The capability layer may consume explicit cached/packaged evidence, but must not introduce:

- per-frame scans;
- new polling loops;
- a global economy re-analysis;
- direct mutation of another mod's state;
- hidden cross-module hard dependencies;
- automatic inference from names/LL/prices when explicit evidence is absent.

The first implementation remains research-only while runtime gate #80 is unresolved.
