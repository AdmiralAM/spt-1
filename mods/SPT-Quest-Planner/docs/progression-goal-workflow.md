# Quest Planner progression-goal workflow

## Player problem

A player often knows the destination but not the best immediate action:

> I want to reach/unlock quest X. Which raid should I run now to move toward it?

Vanilla Tasks can show quest text/status, trackers can show active objectives, maps can show locations, and item tools can show needed items. None of those answers the cross-graph question when X is not active yet and several incomplete prerequisite quests exist across different maps.

This is a high-value SPT-native workflow because the answer depends on the actual final modded prerequisite graph and the player's current evaluated quest state.

## Decision flow

1. Player keeps/selects a progression focus quest.
2. Planner resolves the **incomplete prerequisite plan** for that target from authoritative topology and current profile state.
3. Planner derives the **quest-prerequisite frontier**: incomplete path quests with no incomplete quest prerequisite.
4. Planner then intersects that frontier with authoritative profile evaluation. Only quests whose disposition is **Active** or **Available** form the **actionable focus frontier**.
5. A prerequisite-ready quest that is still profile-blocked (for example by level or another evaluated condition) remains path context but cannot resolve a recommendation conflict.
6. A frontier quest missing from the current profile evaluation is marked **eligibility unknown**, not assumed actionable.
7. Current raid candidates are checked against the actionable focus frontier, not every incomplete ancestor equally.
8. If one actionable focused candidate remains undominated, it becomes the focused recommendation.
9. If several actionable focused candidates have conflicting proven advantages, Planner keeps them as a frontier and exposes the trade-off.
10. As prerequisites and other gates change, the actionable frontier advances from the next state snapshot.
11. If no current raid advances an actionable focus frontier quest, Planner falls back to the conservative global decision model rather than inventing a connection.

## Why two frontiers are necessary

`no incomplete quest prerequisite` is not equivalent to `player can work on this quest now`.

A quest can be graph-ready while still unavailable because of level, profile state or other conditions represented by SPT's quest evaluation. A modded topology can also be incomplete or malformed. Calling the graph frontier "executable" would overclaim what topology alone proves.

Example:

- target `T` requires `A` and `B`;
- `A` requires `A1`;
- none are complete;
- topology says `A1` and `B` have no incomplete quest prerequisite;
- profile evaluation says `A1 = Active`, `B = Blocked`.

The incomplete path is `A1, A, B, T`. The quest-prerequisite frontier is `A1, B`, but the actionable focus frontier is only `A1`. `B` must not receive focused preference until the authoritative player-state evaluation says it is Active or Available.

After `A1` is completed and `A` becomes Available, the actionable frontier can advance to `A`. This is graph + profile semantics, not a depth score or arbitrary weight.

## Why this is materially different

This is not "track quest X". The focused quest may be future/blocked and absent from every current raid.

The planner is useful when it can say, conceptually:

> You cannot work on X directly yet. Customs advances prerequisite Y, and your current SPT profile confirms Y is available now on the remaining path to X; Reserve does useful work but does not move the selected progression goal.

That decision requires joining:

- final modded quest topology;
- current completion/profile evaluation;
- quest-prerequisite frontier;
- authoritative Active/Available eligibility;
- current actionable raid objectives;
- optional preparation/readiness evidence.

## Policy constraints

Progression focus is **explicit player intent**, not a hidden scoring weight. It may resolve an otherwise honest Pareto conflict because the player supplied the missing preference.

It must not:

- claim a raid advances the focus when no profile-confirmed actionable focused-path quest is present;
- treat blocked inner ancestors as equivalent to actionable frontier quests;
- equate "no quest prerequisite blocker" with full quest availability;
- assume missing profile evaluation means executable;
- fabricate prerequisite relationships from quest names or trader identity;
- bypass unknown topology;
- turn raw objective density into focused relevance;
- force a winner between two focused-frontier raids that still have real conflicting trade-offs.

The full prerequisite path and quest-prerequisite frontier are retained for explanation and diagnostics. Focus preference is granted only through the profile-confirmed actionable frontier for future/blocked goals.

## Runtime cost

No new server data is required. `PlannerQueryEngine.GetIncompletePrerequisitePlan`, `GetImmediateBlockers`, and the existing cached `PlannerQuestClientState.Disposition` provide the required topology and profile evidence.

No per-frame polling, global runtime scan, new route, or cross-module dependency is justified for this workflow.

## Acceptance scenarios

- future focus with one Active/Available prerequisite on Customs selects Customs over unrelated equal-value raids;
- a graph-ready but profile-blocked prerequisite does not receive focused preference;
- missing profile eligibility is reported as unknown and cannot create preference;
- a blocked inner prerequisite does not receive focused preference while its own prerequisite remains incomplete;
- parallel prerequisite branches distinguish graph-ready roots from their actionable subset;
- completing one frontier prerequisite advances the actionable frontier deterministically when the next profile snapshot makes its dependent available;
- completed prerequisites disappear from the focus path automatically;
- two maps that both advance the actionable focus frontier remain a trade-off frontier when one has leverage and the other has lower preparation friction;
- a focus with no currently actionable frontier does not falsely label unrelated work as relevant;
- a mod-added or rewired prerequisite changes the focused recommendation from the final SPT topology without an external data update.
