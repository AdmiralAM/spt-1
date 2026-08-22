# SPT Item Intelligence — Phase 4 Requirement Index

## Goal

Convert the Phase 3 on-demand SPT 4.1.2 snapshot into a compact client-side per-template requirement index that can later drive Safe-to-Sell, hover labels and planner UX without scanning profile/quest/hideout data on every item hover.

This phase remains data-only: no inventory decoration, no automatic selling, no gameplay/balance changes, and no Tactical HUD dependency.

## Core model

Build one immutable/batch-replaced dictionary keyed by normalized template id. Each entry should expose only precomputed facts needed by consumers:

- `QuestNeededNow` — remaining count for active/currently hand-in-able quest requirements;
- `QuestNeededLater` — remaining count for known future quest requirements, kept separate from current progression;
- `HideoutNeeded` — remaining count for applicable hideout upgrades/crafts tracked by the planner layer;
- `KeepCount` — conservative target count derived from enabled requirement sources;
- `OwnedCount` — count from the snapshot used to build this generation;
- `SurplusCount` — `max(0, OwnedCount - KeepCount)`;
- reason/source flags so UI can explain *why* an item should be kept.

Do not store Unity item references in this index. The key is the template id; runtime item instances query by template id only.

## Update strategy

1. Request `/spt-item-intelligence/v1/snapshot` only at bounded lifecycle points (stash/menu entry, explicit refresh, or known profile-state changes).
2. Parse/project the snapshot once into temporary dictionaries.
3. Replace the published index atomically after a successful build.
4. Never mutate the published dictionary while hover/UI consumers are reading it.
5. If refresh fails or `profileReady == false`, retain the last valid generation and expose freshness/readiness separately; never silently publish a partial zero-count index.

No `Update()` polling, per-frame profile traversal, repeated quest-table scans, or per-hover reflection is permitted.

## Requirement semantics

### Current quest need

Count only outstanding item hand-in requirements for quests that are currently active/available under the profile state. Subtract already satisfied/turned-in progress where the SPT profile exposes it. Preserve Found-in-Raid constraints as a separate reason flag rather than collapsing them into a generic count.

### Future quest need

Future requirements are advisory and must remain distinguishable from current requirements. They must not make an item look immediately mandatory if the user later chooses to disable future-planning retention.

### Hideout need

Use SPT 4.1.2 hideout tables plus current profile area levels/progress. Do not duplicate Hideout In Progress UI. Item Intelligence should only produce the requirement fact/count for its own Safe-to-Sell and planner consumers.

### Keep / surplus

Default conservative rule:

`KeepCount = max(QuestNeededNow, QuestNeededLater if enabled, HideoutNeeded if enabled)` only when the same physical copies can satisfy alternative progression paths; use additive counts when requirements are independently consumptive and can overlap in time.

Because additive-vs-max semantics depend on source meaning, implement source contributions explicitly rather than summing every requirement blindly.

`SurplusCount` is advisory. Safe-to-Sell must never report safe solely because market value is high; requirement state wins over valuation.

## Performance acceptance criteria

- O(1) lookup by normalized template id for hover/Safe-to-Sell consumers.
- Snapshot projection occurs outside render/hot hover paths.
- No LINQ/materialization inside repeated item lookup methods.
- Reuse temporary collections where practical; publish a compact result set only for templates with a requirement or owned count relevant to consumers.
- Normalize template ids once during index construction; avoid repeated `ToLowerInvariant`/whitespace normalization on each query when the caller already supplies a normalized id.
- A failed refresh must not trigger retry loops faster than a bounded user/lifecycle event cadence.

## Validation cases

Before UI integration, add deterministic tests for:

1. active quest: 5 required, 2 satisfied, 1 owned => need 3, surplus 0;
2. completed quest requirement => contributes 0;
3. future quest only => flagged future, not current;
4. hideout need only => correct keep count without quest flag;
5. overlapping sources => explicit max/additive rule is stable;
6. owned > keep => exact surplus;
7. `profileReady == false` => last valid index retained;
8. snapshot generation replacement => no mixed old/new counts;
9. FIR-required quest item => FIR reason retained separately;
10. unknown template => zero-requirement lookup without allocation-heavy fallback.

## Phase boundary

Phase 4 is complete when a tested `RequirementIndex` can be rebuilt from the Phase 3 snapshot and queried by template id with stable requirement counts/reasons. Inventory tooltip/checkmark rendering is a later phase and should consume this index rather than re-reading raw SPT data.
