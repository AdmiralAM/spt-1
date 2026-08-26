# Capability goal selection contract

## Purpose

The KEEP/KILL candidate must not solve goal discovery by creating another full quest/item browser. Goal selection is deliberately narrower than decision planning.

The selector consumes only explicit capability definitions already supplied by trusted/versioned evidence. It does not infer goals from trader names, loyalty levels, item names, prices, handbook values, or every quest in the final database.

## Minimal catalog

`PlannerCapabilityGoalCatalog` groups explicit goals into two deterministic collections:

- `OpenGoals` — capability goals that are not already achieved;
- `UnlockedGoals` — capability gates already confirmed completed by authoritative profile state.

Open goals carry only current planning state, not a score or rank:

- `Actionable` — at least one profile-confirmed prerequisite frontier quest can be acted on now;
- `Waiting` — the selected path is currently held by known/unresolved SPT delayed availability and requires no fabricated raid action;
- `EvidenceIncomplete` — topology is ready but authoritative eligibility evidence is missing;
- `ProgressionConflict` — prerequisite-state semantics prove a terminal contradiction;
- `NoActionProven` — Planner cannot prove a useful current action.

`AlreadyUnlocked` goals are separated so the default goal picker does not become cluttered with completed capabilities while still preserving historical/status visibility.

## Deliberate non-features

The catalog has no:

- recommended goal;
- goal score;
- economic value ranking;
- path-depth ranking;
- rarity ranking;
- global quest search;
- item browser;
- automatic inference of player intent.

The player chooses the desired outcome. Planner answers how to reach it.

## Bounds and performance

Goal definitions are explicitly bounded (default 32, hard maximum 128). Catalog construction uses existing cached topology/profile/delay indexes only. It performs no raid opportunity scan and no decision ranking for every catalog entry.

The heavier raid decision path is invoked only for the selected goal through `PlannerCapabilityDecisionProvider`.

This separation is important: the goal picker stays cheap even if the final modded quest topology is large.

## Freshness provenance

`PlannerCapabilityDecisionSnapshot` now carries:

- `SourceRevision` — the `PlannerClientCache.Revision` used to derive the decision;
- `GeneratedAtUnixSeconds` — authoritative cached state generation time.

The provider already invalidates derived decisions when cache revision changes. The explicit provenance lets the future F9 reject or visibly mark a snapshot that no longer corresponds to the currently displayed cache revision instead of silently presenting stale advice.

No wall-clock timer is used to promote quest state locally; SPT remains authoritative.

## KEEP/KILL implication

Goal discovery is not intended to be a standalone reason to keep Quest Planner. It exists only to make the decision-changing vertical slice usable without recreating Tasks, TaskSearch, Item Intelligence, or trader browsing.

If the first live candidate still needs a large browsing/search UI to make capability goals understandable, that is evidence against the product thesis rather than justification for expanding this catalog.
