# Quest Planner prerequisite semantics audit

## Why this audit exists

Future-goal planning is only useful if the prerequisite topology preserves the semantics of SPT `AvailableForStart` conditions. A visually plausible quest graph is not sufficient: the planner must not turn a lossy graph into false unlock or reachability claims.

## SPT semantics confirmed

Current SPT quest documentation states that every `AvailableForStart` condition must be met before a quest becomes available. Multiple top-level start conditions therefore form a conjunction.

For a `Quest` start condition:

- `target` identifies the source quest;
- `status[]` is the set of **raw SPT quest statuses** accepted by that single condition;
- `availableAfter` delays availability after the relevant source-quest transition;
- several `Quest` start conditions are all mandatory because all top-level start conditions must be met.

Raw status identity matters. SPT distinguishes, among others, `Started = 2`, `AvailableForFinish = 3`, and `Success = 4`. Quest Planner's normalized `QuestState` intentionally collapses some raw states for ordinary disposition/UI purposes, but that normalization is **not valid evidence for prerequisite `status[]` evaluation**.

This means the planner's structural prerequisite graph can remain conjunctive, but an edge is not semantically just `source -> target`, and its accepted statuses must retain raw provenance.

## Previous loss of information

Before this audit was completed:

1. server extraction retained normalized accepted source states but originally discarded `availableAfter`;
2. the topology payload's normalized states were initially reduced client-side to source/target IDs;
3. `GetImmediateUnlocksIfCompleted()` assumed every edge meant `source Success unlocks target immediately`;
4. hypothetical reachability modeled Level + Quest conditions while silently ignoring other populated `AvailableForStart` condition types;
5. future-goal ancestor/path traversal treated `not Completed` as equivalent to `prerequisite condition remains unsatisfied`;
6. most importantly, prerequisite `status[]` was evaluated through normalized `QuestState`, which conflated raw `Started = 2` and raw `AvailableForFinish = 3` as the same internal `Started` state.

The last point is a provenance bug rather than a ranking-policy problem. A condition that explicitly accepts raw status `3` must not become satisfied merely because the source is raw status `2`.

## Raw-status provenance contract

Prerequisite truth now uses exact raw status when that provenance is present.

The server keeps both representations:

- normalized `AcceptedSourceStates` for compatibility, display and legacy/synthetic model use;
- exact `AcceptedSourceRawStatuses` for authoritative prerequisite semantics.

The player snapshot already carries each quest's raw profile status. The client index now retains that raw value alongside normalized `ProfileState` and `Disposition`.

Evaluation rule:

1. when an edge has an exact raw-status contract and the source profile exposes raw status, compare raw-to-raw;
2. normalized state is only a compatibility fallback for legacy/synthetic test edges or states where raw provenance is genuinely unavailable;
3. hypothetical completion uses raw `Success = 4` when the edge has a raw contract;
4. terminal-conflict detection uses the same exact-success rule.

This preserves useful normalization for UI while preventing it from corrupting quest-condition truth.

## Conservative contract after the audit

### State-semantic future path

A `Quest` start condition creates a structural prerequisite edge. Structural topology remains useful for dependency inspection, but **remaining future work is state-semantic** rather than a static ancestor closure.

`GetIncompletePrerequisitePlan()` and `GetIncompleteAncestors()` traverse an edge only when that edge's accepted status set does not accept the source quest's current authoritative state. When raw provenance exists, that means the exact raw SPT status.

Consequences:

- if target `T` accepts source `S` at the source's exact current raw status, `S` is not remaining prerequisite work for `T`;
- if `T` requires raw `Success = 4` while `S` is still an earlier active state, `S` remains on the plan;
- once an edge is satisfied, historical ancestors behind that edge are not dragged into the selected future-goal plan;
- mixed branches recurse only through currently unsatisfied prerequisite conditions.

This distinction is essential for progression focus: the planner should answer which prerequisite conditions still need player action, not list every structural ancestor that is not labelled `Success`.

### Terminal prerequisite-state conflict

A second state-semantic case is explicit rather than hidden inside `blocked`.

When all of the following are true:

1. source quest `S` is non-repeatable;
2. the authoritative profile says `S` is already Completed/Success;
3. target edge `S -> T` does not accept raw `Success = 4` when raw provenance is present (or normalized Success only for a legacy edge);
4. the edge is not already satisfied by the current source state;

the planner records a **terminal prerequisite-state conflict**.

This is not ordinary remaining work: normal forward progression cannot move a completed non-repeatable quest back into an earlier accepted state. A selected progression focus containing such a conflict must abstain instead of falling back to an unrelated globally attractive raid and pretending that raid advances the chosen goal.

Repeatable sources are deliberately excluded from this terminal claim because another cycle may make an earlier state reachable again.

### Immediate unlock

Completion of source quest `S` may only claim target quest `T` as an immediate modeled unlock when all of the following are proven:

1. the `S -> T` condition accepts raw `Success = 4` when raw provenance is available;
2. its `availableAfter` is zero;
3. every other quest prerequisite condition on `T` accepts the other source quest's exact current raw status when available;
4. the target level gate is satisfied when that information is present;
5. all populated `AvailableForStart` condition types on `T` are modeled by the planner;
6. `T` is not already Active, Available, or Completed.

If any condition is unknown, do not claim the immediate unlock.

### Hypothetical reachability

A locked quest must not be labelled hypothetically `Reachable` when its start-condition set contains a condition type the planner does not model. Authoritative profile states still win: a quest already reported as Active or Available remains actionable regardless of the planner's hypothetical gate coverage.

## Unsupported start-gate policy

The extractor records whether the `AvailableForStart` set is fully modeled for hypothetical reachability. At present, Level and Quest conditions are modeled for this purpose. Other condition types are preserved as warnings and make hypothetical gate coverage incomplete until a dedicated source-backed evaluator exists.

This is deliberate under-claiming. It is safer for the product to say that eligibility is unresolved than to recommend a raid on a false unlock premise.

## Remaining semantic diagnostic: configured delay

An edge whose source state is accepted but whose `availableAfter` is non-zero may require no further quest work while still delaying target availability. The planner already prevents such an edge from creating an `immediate unlock` claim.

The remaining research question is presentation/evidence precision: without a proven remaining-timer source, the planner can safely know that the dependency is delayed-by-contract but must not invent an exact countdown or assert that the delay is still pending after it may already have elapsed.

Until a trustworthy timer source is proven, configured delay should remain explanatory context and must not produce optimistic `immediate` or focus-preference evidence.

## Product consequence

This audit strengthens the product thesis rather than weakening it: the useful planner is not a generic dependency graph. It is an evidence-bounded decision layer that distinguishes:

- structural prerequisite relation;
- exact raw prerequisite-state contract;
- currently unsatisfied prerequisite condition;
- terminal prerequisite-state conflict;
- current authoritative actionability;
- modeled hypothetical reachability;
- configured delayed dependency;
- proven immediate unlock;
- unresolved eligibility.

Those states must not be collapsed into one `unlocked` boolean or one normalized quest-state enum.

## Performance constraints

The richer edge semantics are carried in the existing topology snapshot and cached client index. State-semantic traversal remains iterative and bounded by the existing query traversal limit. Terminal-conflict checks are local to a quest's prerequisite edges. Raw-status comparison is constant-time over the tiny accepted-status set for an edge. No new route, polling loop, runtime scan, Harmony patch, or external dependency is required.
