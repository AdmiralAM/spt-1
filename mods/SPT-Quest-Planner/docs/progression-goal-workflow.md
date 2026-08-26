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
8. If only one candidate advances the actionable focus frontier, focus may select it over an otherwise comparable unrelated candidate.
9. If several candidates advance the actionable focus frontier, they are compared again using only proven focus-compatible dimensions before any global tie-breaker is allowed.
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

## Comparing multiple actionable branches

A second ambiguity appears when several maps all advance different quests on the actionable focus frontier. Merely saying that both candidates "support focus" is insufficient: falling straight back to a global ranking can let unrelated quest counts or unrelated unlocks decide a player-selected goal.

The focused comparator therefore uses conservative dominance again. Proven focused dimensions are:

1. **actionable focused quests advanced by the raid** — advancing two currently actionable branches is a real goal-specific advantage over advancing one;
2. **focused cross-quest action overlap** — a shared action is relevant when at least one participating quest is on the actionable focus frontier;
3. **focused-path immediate unlocks** — only immediate unlocks that remain on the selected target's incomplete path count as goal-specific leverage;
4. **preparation friction** — proven missing or unresolved preparation can counterbalance focused leverage;
5. **evidence coverage** — unknown semantics remain explicit uncertainty rather than silently becoming useful work.

No numeric weights are assigned. If one candidate has at least one focused advantage and no competing focused disadvantage, it may win. If one has focused unlock leverage while another has lower preparation friction, Planner **abstains** and keeps both as good options. Only when focused evidence is equivalent may the existing conservative global model act as a secondary tie-breaker.

This distinction matters because player intent constrains the optimization target; it should not erase real trade-offs, but unrelated generic evidence should not override a proven conflict inside the chosen goal.

## Why this is materially different

This is not "track quest X". The focused quest may be future/blocked and absent from every current raid.

The planner is useful when it can say, conceptually:

> You cannot work on X directly yet. Customs advances prerequisite Y, and your current SPT profile confirms Y is available now on the remaining path to X; Reserve does useful work but does not move the selected progression goal.

Or, for parallel branches:

> Customs advances two actionable prerequisites on the path to X, while Woods advances one. If both are equally prepared and understood, Customs has a proven focused advantage. If Customs needs a missing key and Woods is ready, there is no forced winner; the trade-off is explicit.

That decision requires joining:

- final modded quest topology;
- current completion/profile evaluation;
- quest-prerequisite frontier;
- authoritative Active/Available eligibility;
- current actionable raid objectives;
- cross-quest shared-action evidence;
- focused-path unlock evidence;
- preparation/readiness evidence.

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
- count an unrelated global unlock as focused-path leverage;
- let unrelated generic evidence override conflicting focused evidence;
- force a winner between two focused-frontier raids that still have real conflicting trade-offs.

The full prerequisite path and quest-prerequisite frontier are retained for explanation and diagnostics. Focus preference is granted only through the profile-confirmed actionable frontier for future/blocked goals.

## Runtime cost

No new server data is required. `PlannerQueryEngine.GetIncompletePrerequisitePlan`, `GetImmediateBlockers`, the existing cached `PlannerQuestClientState.Disposition`, raid decision signals and action-overlap groups provide the required evidence.

The focused comparison is bounded by the existing candidate/frontier sizes. No per-frame polling, global runtime scan, new route, or cross-module dependency is justified for this workflow.

## Acceptance scenarios

- future focus with one Active/Available prerequisite on Customs selects Customs over unrelated equal-value raids;
- a graph-ready but profile-blocked prerequisite does not receive focused preference;
- missing profile eligibility is reported as unknown and cannot create preference;
- a blocked inner prerequisite does not receive focused preference while its own prerequisite remains incomplete;
- parallel prerequisite branches distinguish graph-ready roots from their actionable subset;
- completing one frontier prerequisite advances the actionable frontier deterministically when the next profile snapshot makes its dependent available;
- completed prerequisites disappear from the focus path automatically;
- a raid advancing two actionable focused branches can dominate one advancing only one branch when no focused disadvantage exists;
- shared-action overlap involving an actionable focused quest is valid focused evidence;
- an immediate unlock counts as focused leverage only when that unlock is still on the selected target path;
- focused unlock leverage versus lower preparation friction remains `Several good options` rather than a forced winner;
- equivalent focused evidence may fall back to the global conservative model as a secondary tie-breaker;
- a focus with no currently actionable frontier does not falsely label unrelated work as relevant;
- a mod-added or rewired prerequisite changes the focused recommendation from the final SPT topology without an external data update.
