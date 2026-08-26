# Capability workspace composition contract

## Purpose

This is the final research-side composition boundary before a live KEEP/KILL F9 candidate. It proves that the future screen can be driven by a bounded capability catalog plus one explicitly selected capability decision, without reviving the legacy map leaderboard.

## Inputs

The workspace consumes only:

- a bounded `PlannerCapabilityGoalCatalog`;
- an explicit selected capability ID, or no selection;
- an optional `PlannerCapabilityDecisionSnapshot` for that exact selection.

The workspace does not compute quest/raid policy. Catalog classification and decision computation remain in their existing domain/provider layers.

## Output

`PlannerCapabilityWorkspaceSnapshot` exposes:

- open capability goals;
- already-unlocked capability goals;
- the explicit selected capability ID;
- the selected immutable decision snapshot, when computed.

Each catalog row exposes only capability identity, exact gate quest, coarse current state, supply kind and selection state.

## Explicit non-features

The workspace contract intentionally contains no:

- `RankingMode`;
- numeric score;
- `#1/#2/#3` rank;
- global map list;
- objective-density field;
- legacy `PlannerRaidPlanCard`;
- automatic recommended goal;
- automatic first-item selection;
- quest/item search catalog.

A decision snapshot may contain a primary raid only when the focused policy has actually produced one. Its alternatives remain non-ranked trade-off candidates.

## Fail-closed selection rules

- selected capability must exist in the bounded catalog;
- a supplied decision must match the selected capability ID;
- a supplied decision gate must match the selected catalog definition;
- no selection means no goal is silently promoted because of catalog ordering.

These constraints are specifically intended to prevent the old rank-first UX semantics from leaking back into the capability experiment through presentation convenience.

## KEEP/KILL implication

Research is considered internally complete when this contract and the underlying provider/catalog/decision models are green in CI. Further feature work before the runtime reliability gate is lifted would add architecture without increasing the strength of the KEEP/KILL experiment.

The next meaningful product step after the runtime gate is a narrow live F9 candidate using this composition:

`bounded goals -> explicit selection -> immutable decision -> compact GOAL / DO NOW / WHY / AFTER / RESULT / CAUTION view`.

If that live surface does not materially change player decisions, Quest Planner should be retired or drastically reduced rather than expanded.
