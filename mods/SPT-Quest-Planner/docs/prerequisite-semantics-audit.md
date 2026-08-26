# Quest Planner prerequisite semantics audit

## Why this audit exists

Future-goal planning is only useful if the prerequisite topology preserves the semantics of SPT `AvailableForStart` conditions. A visually plausible quest graph is not sufficient: the planner must not turn a lossy graph into false unlock or reachability claims.

## SPT semantics confirmed

Current SPT quest documentation states that every `AvailableForStart` condition must be met before a quest becomes available. Multiple top-level start conditions therefore form a conjunction.

For a `Quest` start condition:

- `target` identifies the source quest;
- `status[]` is the set of source-quest statuses accepted by that single condition;
- `availableAfter` delays availability after the relevant source-quest transition;
- several `Quest` start conditions are all mandatory because all top-level start conditions must be met.

This means the planner's structural prerequisite graph can remain conjunctive, but an edge is not semantically just `source -> target`.

## Previous loss of information

Before this audit:

1. server extraction retained accepted source states but discarded `availableAfter`;
2. the topology payload already contained accepted states, but the client index reduced each edge to source/target quest IDs;
3. `GetImmediateUnlocksIfCompleted()` assumed every edge meant `source Success unlocks target immediately`;
4. hypothetical reachability modeled Level + Quest conditions while silently ignoring other populated `AvailableForStart` condition types;
5. future-goal ancestor/path traversal still treated `not Completed` as equivalent to `prerequisite condition remains unsatisfied`.

That was sufficient for graph visualization but too weak for decision support.

## Conservative contract after the audit

### Structural path

A `Quest` start condition still creates a structural prerequisite edge. Structural topology remains useful for dependency inspection, but **remaining future work is state-semantic** rather than a static ancestor closure.

`GetIncompletePrerequisitePlan()` and `GetIncompleteAncestors()` traverse an edge only when that edge's accepted source-state set does not accept the source quest's effective current profile state.

Consequences:

- if target `T` accepts source `S` in `Started` and `S` is Active/Started, `S` is not remaining prerequisite work for `T`;
- if `T` requires `S = Success` while `S` is Active/Started, `S` remains on the plan;
- once an edge is satisfied, historical ancestors behind that edge are not dragged into the selected future-goal plan merely because those quests are not themselves represented as current work;
- mixed branches recurse only through currently unsatisfied prerequisite conditions.

This distinction is essential for progression focus: the planner should answer which prerequisite conditions still need player action, not list every structural ancestor that is not labelled `Success`.

### Immediate unlock

Completion of source quest `S` may only claim target quest `T` as an immediate modeled unlock when all of the following are proven:

1. the `S -> T` condition accepts `Success`;
2. its `availableAfter` is zero;
3. every other quest prerequisite condition on `T` accepts the other source quest's current profile state;
4. the target level gate is satisfied when that information is present;
5. all populated `AvailableForStart` condition types on `T` are modeled by the planner;
6. `T` is not already Active, Available, or Completed.

If any condition is unknown, do not claim the immediate unlock.

### Hypothetical reachability

A locked quest must not be labelled hypothetically `Reachable` when its start-condition set contains a condition type the planner does not model. Authoritative profile states still win: a quest already reported as Active or Available remains actionable regardless of the planner's hypothetical gate coverage.

## Unsupported start-gate policy

The extractor records whether the `AvailableForStart` set is fully modeled for hypothetical reachability. At present, Level and Quest conditions are modeled for this purpose. Other condition types are preserved as warnings and make hypothetical gate coverage incomplete until a dedicated source-backed evaluator exists.

This is deliberate under-claiming. It is safer for the product to say that eligibility is unresolved than to recommend a raid on a false unlock premise.

## Remaining semantic diagnostics

Two cases should remain explicit research questions rather than being guessed into normal actionable work:

1. **Configured delay** — an edge whose source state is accepted but whose `availableAfter` is non-zero may require no further quest work while still delaying target availability. Without a proven remaining-timer source, the planner can safely know that the edge is delayed-by-contract but must not invent an exact countdown.
2. **Terminal state mismatch** — a completed non-repeatable source whose target condition accepts only an earlier state such as `Started` is not ordinary remaining work. It may represent an unsatisfiable/malformed modded condition and should eventually be surfaced as a diagnostic instead of being presented as something the player can simply progress again.

Until these cases have dedicated evidence models, they must not produce optimistic `immediate`, `reachable`, or focus-preference claims.

## Product consequence

This audit strengthens the product thesis rather than weakening it: the useful planner is not a generic dependency graph. It is an evidence-bounded decision layer that distinguishes:

- structural prerequisite relation;
- currently unsatisfied prerequisite condition;
- current authoritative actionability;
- modeled hypothetical reachability;
- configured delayed dependency;
- proven immediate unlock;
- unresolved or contradictory eligibility.

Those states must not be collapsed into one `unlocked` boolean.

## Performance constraints

The richer edge semantics are carried in the existing topology snapshot and cached client index. State-semantic traversal remains iterative and bounded by the existing query traversal limit. No new route, polling loop, runtime scan, Harmony patch, or external dependency is required.
