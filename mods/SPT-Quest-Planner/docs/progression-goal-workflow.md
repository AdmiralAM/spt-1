# Quest Planner progression-goal workflow

## Player problem

A player often knows the destination but not the best immediate action:

> I want to reach/unlock quest X. Which raid should I run now to move toward it?

Vanilla Tasks can show quest text/status, trackers can show active objectives, maps can show locations, and item tools can show needed items. None of those answers the cross-graph question when X is not active yet and several incomplete prerequisite quests exist across different maps.

This is a high-value SPT-native workflow because the answer depends on the actual final modded prerequisite graph and the player's current completion state.

## Decision flow

1. Player keeps/selects a progression focus quest.
2. Planner resolves the **incomplete prerequisite plan** for that target from the authoritative topology and current profile state.
3. Planner derives the **executable focus frontier**: incomplete path quests whose immediate prerequisites are all complete now.
4. Current raid candidates are checked first against that executable frontier, not against every incomplete ancestor equally.
5. A blocked inner path node remains useful explanation context but cannot by itself resolve a Pareto conflict as if it were actionable now.
6. Candidates that do not advance the executable focus frontier are deprioritized when at least one candidate does.
7. If one frontier candidate remains undominated, it becomes the focused recommendation.
8. If several frontier candidates have conflicting proven advantages, Planner keeps them as a frontier and exposes the trade-off.
9. As prerequisites complete, the executable frontier advances deterministically through the graph.
10. If no current raid advances the executable frontier, Planner falls back to the conservative global decision model rather than inventing a connection.

## Why the executable frontier matters

An incomplete prerequisite plan can contain several graph layers at once. Treating every incomplete ancestor as equally actionable is wrong.

Example:

- target `T` requires `A` and `B`;
- `A` requires `A1`;
- none are complete.

The incomplete path is `A1, A, B, T`, but the executable frontier is only `A1, B`. `A` and `T` are still blocked. A raid associated with `A` must not receive focused preference merely because `A` appears somewhere on the path.

After `A1` is completed, the frontier advances to `A, B` automatically. This is graph semantics, not a depth score or arbitrary weight.

## Why this is materially different

This is not "track quest X". The focused quest may be future/blocked and absent from every current raid.

The planner is useful when it can say, conceptually:

> You cannot work on X directly yet. Customs advances prerequisite Y, which is executable now on the remaining path to X; Reserve does useful work but does not move the selected progression goal.

That decision requires joining:

- final modded quest topology;
- current completion state;
- currently executable prerequisite frontier;
- current actionable raid objectives;
- optional preparation/readiness evidence.

## Policy constraints

Progression focus is **explicit player intent**, not a hidden scoring weight. It may resolve an otherwise honest Pareto conflict because the player supplied the missing preference.

It must not:

- claim a raid advances the focus when no executable focused-path quest is present;
- treat blocked inner ancestors as equivalent to actionable frontier quests;
- fabricate prerequisite relationships from quest names or trader identity;
- bypass unknown topology;
- turn raw objective density into focused relevance;
- force a winner between two focused-frontier raids that still have real conflicting trade-offs.

The full prerequisite path is retained for explanation and future planning. Focus preference is granted only through the executable frontier when one can be derived.

## Runtime cost

No new server data is required. `PlannerQueryEngine.GetIncompletePrerequisitePlan` and `GetImmediateBlockers` already provide bounded queries over cached topology/profile state. The research decision layer derives the frontier from those existing APIs and current cached raid candidates.

No per-frame polling, global runtime scan, new route, or cross-module dependency is justified for this workflow.

## Acceptance scenarios

- future focus with one executable prerequisite on Customs selects Customs over unrelated equal-value raids;
- a blocked inner prerequisite does not receive focused preference while its own prerequisite remains incomplete;
- parallel prerequisite branches expose all currently executable branch roots as the focus frontier;
- completing one frontier prerequisite advances the frontier deterministically;
- completed prerequisites disappear from the focus path automatically;
- two maps that both advance the executable focus frontier remain a trade-off frontier when one has leverage and the other has lower preparation friction;
- a focus with no currently actionable frontier does not falsely label unrelated work as relevant;
- a mod-added or rewired prerequisite changes the focused recommendation from the final SPT topology without an external data update.
