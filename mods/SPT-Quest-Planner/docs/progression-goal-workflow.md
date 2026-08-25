# Quest Planner progression-goal workflow

## Player problem

A player often knows the destination but not the best immediate action:

> I want to reach/unlock quest X. Which raid should I run now to move toward it?

Vanilla Tasks can show quest text/status, trackers can show active objectives, maps can show locations, and item tools can show needed items. None of those answers the cross-graph question when X is not active yet and several incomplete prerequisite quests exist across different maps.

This is a high-value SPT-native workflow because the answer depends on the actual final modded prerequisite graph and the player's current completion state.

## Decision flow

1. Player keeps/selects a progression focus quest.
2. Planner resolves the **incomplete prerequisite plan** for that target from the authoritative topology and current profile state.
3. Current raid candidates are checked for quests that belong to that incomplete path.
4. Candidates that do not advance the chosen path are deprioritized when at least one candidate does.
5. If one focused-path candidate remains undominated, it becomes the focused recommendation.
6. If several focused-path candidates have conflicting proven advantages, Planner keeps them as a frontier and exposes the trade-off.
7. If no current raid advances the path, Planner falls back to the conservative global decision model rather than inventing a connection.

## Why this is materially different

This is not "track quest X". The focused quest may be future/blocked and absent from every current raid.

The planner is useful when it can say, conceptually:

> You cannot work on X directly yet. Customs advances prerequisite Y, which is on the remaining path to X; Reserve does useful work but does not move the selected progression goal.

That decision requires joining:

- final modded quest topology;
- current completion state;
- current actionable raid objectives;
- optional preparation/readiness evidence.

## Policy constraints

Progression focus is **explicit player intent**, not a hidden scoring weight. It may resolve an otherwise honest Pareto conflict because the player supplied the missing preference.

It must not:

- claim a raid advances the focus when no focused quest/path quest is present;
- fabricate prerequisite relationships from quest names or trader identity;
- bypass unknown topology;
- turn raw objective density into focused relevance;
- force a winner between two focused-path raids that still have real conflicting trade-offs.

## Runtime cost

No new server data is required. `PlannerQueryEngine.GetIncompletePrerequisitePlan` already provides a bounded graph traversal over cached topology/profile state. The decision layer consumes that result and current cached raid candidates.

No per-frame polling, global runtime scan, new route, or cross-module dependency is justified for this workflow.

## Acceptance scenarios

- future focus with one incomplete prerequisite on Customs selects Customs over unrelated equal-value raids;
- completed prerequisites disappear from the focus path automatically;
- two maps that both advance the focus remain a trade-off frontier when one has leverage and the other has lower preparation friction;
- a focus with no currently actionable path does not falsely label unrelated work as relevant;
- a mod-added or rewired prerequisite changes the focused recommendation from the final SPT topology without an external data update.
