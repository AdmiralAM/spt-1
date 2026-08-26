# Capability decision snapshot

## Purpose

The first live KEEP/KILL candidate must consume one immutable decision contract rather than recomputing product semantics inside the F9 view.

`PlannerCapabilityDecisionSnapshot` is the research-side boundary for that contract.

It composes only already-proven evidence:

- selected capability ID;
- exact gate quest ID;
- capability progression state;
- KEEP/KILL decision-value classification;
- optional primary raid;
- optional non-ranked alternatives;
- actionable prerequisite quest IDs;
- waiting prerequisite quest IDs;
- eligibility-unknown quest IDs;
- proven capability result summary;
- caution text;
- compact decision-value evidence.

## Deliberate exclusions

The snapshot does not contain:

- numeric planner score;
- global map rank or `#1/#2/#3` positions;
- raw objective density as a primary field;
- a second economy model;
- runtime polling state;
- direct external-mod objects.

This prevents the future UI from accidentally falling back to the old leaderboard product merely because legacy ranker/view-model classes still exist during migration.

## State semantics

The snapshot preserves the capability presentation state exactly:

- `RaidDecision`;
- `WaitingForAvailability`;
- `EvidenceIncomplete`;
- `ProgressionConflict`;
- `CapabilityAlreadyUnlocked`;
- `NoActionProven`.

`DecisionValue` is separate from correctness state. A technically correct snapshot can still be `NavigationOnly` and therefore fail the KEEP criterion.

## Raid semantics

`PrimaryLocationId` is populated only when the conservative raid presentation has a proven primary candidate.

`AlternativeLocationIds` are a set of non-ranked alternatives. Their ordering is deterministic for transport/testing only and must not be rendered as ordinal preference unless a future decision policy explicitly proves such an ordering.

A waiting-only, already-unlocked, evidence-incomplete, conflict, or no-action snapshot may have no raid at all.

## KEEP/KILL protection

The snapshot carries `CountsTowardKeepCandidate` from `PlannerCapabilityDecisionValueClassifier` so the live experiment can distinguish:

- useful decision compression;
- plain prerequisite navigation;
- correctness/fail-closed behavior.

The UI must not promote `NavigationOnly`, `GoalAlreadyResolved`, or `UnsupportedDecisionPrevented` into KEEP evidence merely because the presentation looks polished.

## Runtime boundary

This remains research-only until runtime gate #80 permits feature integration.

When that gate opens, the preferred migration is:

1. build this snapshot from already-cached Planner state;
2. render F9 from this snapshot;
3. keep old ranker/view-model paths isolated during comparison;
4. do not add per-frame scans or new polling to populate presentation fields;
5. validate at least one live `DecisionChanged`, `TradeoffClarified`, or `UnnecessaryRaidAvoided` case before deciding KEEP.
