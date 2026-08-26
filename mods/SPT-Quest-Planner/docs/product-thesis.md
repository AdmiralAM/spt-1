# Quest Planner product thesis

## Decision to make

Quest Planner deserves to exist only if it solves a player decision that the rest of the SPT quest stack and current external Tarkov planners do not already solve well.

The neighboring SPT tools are already strong at **information display and execution support**:

- vanilla Tasks: quest status, text, rewards and acceptance;
- Quest Tracker: in-raid tracked-objective overlay;
- Dynamic Maps: spatial quest/objective information;
- Task Item Indicator: nearby task-item guidance;
- TaskSearch / Task List Fixes: task browsing/search/sorting;
- Item Intelligence: item-level keep/requirement state.

Quest Planner must therefore not compete on showing more quest information. Its product is **decision compression**: combine final modded quest topology, current SPT profile evaluation, preparation state and objective compatibility into a small number of explainable next-action choices.

## Competitive reality

Generic quest planning is not unique. Current external Tarkov companion tools already cover much of the obvious product surface:

- map/task grouping and best-fit raid suggestions;
- user-selected focus over current tasks;
- automatic progress reconstruction from EFT logs;
- quest-tree visualization;
- next-quest recommendations using downstream unlock impact and special progression goals;
- objective and preparation checklists.

RaidIQ, for example, already lets a player choose current quests and builds a best-fit raid around that focus, while its planning pool explicitly excludes locked quests. TarkovQuestie already recommends a next quest using follow-up unlocks, Kappa/Lightkeeper relevance and what can be finished now.

Therefore the defensible claim cannot be any of the following:

- "we invented best next quest";
- "we automatically know quest progress";
- "we group compatible quests by map";
- "we rank unlock leverage";
- "the player can choose a focus".

Those are table stakes or existing external features.

## Strongest SPT-native wedge

The strongest differentiated workflow is:

> **I want to reach a future or currently blocked quest in this exact modded SPT profile. What work is genuinely actionable now on the prerequisite path to that goal, which raid combines it best, what will it unlock next, and what proven preparation blocker prevents me from acting?**

That workflow depends on data an external live-EFT planner normally does not possess authoritatively:

1. **Final modded quest graph** — the actual post-mod SPT topology, including custom traders, added/removed quests and rewired prerequisites.
2. **Authoritative profile evaluation** — not just graph readiness: whether SPT currently evaluates a prerequisite as Active or Available after level/profile/mod conditions.
3. **Exact local preparation state** — inventory-backed proven bring requirements and unresolved custom semantics.
4. **Current modded objective semantics** — conservative compatibility derived from what this installation actually exposes.
5. **In-SPT decision surface** — no manual reconstruction, external dataset synchronization or assumption that live-EFT quest topology matches the local game.

Local/offline operation alone is not unique enough; external tools can also operate locally. The advantage is the **authoritative combination of final modded database + server profile/inventory + in-game decision support**.

If Quest Planner cannot exploit those advantages, an external planner is likely the better product and this mod should be reduced or retired rather than expanded.

## Core user questions

The default global question is:

> Given what is active now, is there a clearly superior next raid, why, and what must I prepare first?

The stronger differentiated question is:

> I want to reach quest X, even if X is locked. Which currently actionable prerequisite work moves me toward it in my actual SPT quest graph, and why should I choose that raid over the alternatives?

A useful answer must change a decision the player would otherwise have to derive manually from Tasks, prerequisite relationships, map/objective compatibility and inventory state.

## Unique value proposition

The unique unit of value is not a quest, marker, item or objective. It is a **cross-quest decision over the player's actual modded SPT state**.

Quest Planner deliberately reasons across:

1. **Future-goal pathing** — resolve an incomplete path to a selected future/blocked quest.
2. **Actionable frontier** — distinguish graph-ready prerequisites from those SPT actually marks Active/Available now.
3. **Concurrency** — multiple quests/objectives that can advance in the same raid.
4. **Action overlap** — one action that advances multiple quests, not merely several unrelated objectives on one map.
5. **Focused progression leverage** — immediate unlocks that are actually on the selected goal path, not unrelated global unlock inflation.
6. **Preparation friction** — required items/keys/equipment already available versus missing or unresolved.
7. **Busywork suppression** — repeatables and low-leverage work must not win only because they inflate raw counts.
8. **Uncertainty** — unknown/custom condition semantics lower confidence rather than being silently treated as equivalent to known work.
9. **Opportunity cost** — explain why the chosen raid/action is better than the nearest alternative, or explicitly retain several good options.

## Product anti-thesis

If the planner can only say:

- "Reserve has 7 kills and 2 extracts";
- "Customs has 2 kills";
- "this map has the most quests";

then the mod has no defensible reason to exist. That is counting, not planning.

Likewise, if its recommendation can be reproduced from a public quest database plus a manually selected active-quest list, the SPT-native implementation is not earning its maintenance cost.

The mod should also reject pseudo-sophisticated graph scores. Path depth, descendant count and graph size are structural properties, not measured player effort or urgency. They must not become hidden ranking weights.

## Recommendation contract

Every recommendation must be explainable without exposing an opaque numeric score.

### WHY THIS

Use concrete factors, for example:

- `2 active non-repeatable quests progress from the same PMC-kill action`;
- `this raid advances prerequisite Y on the selected path to X`;
- `finishing Y would make downstream path quest Z available if its other prerequisites are already complete`;
- `all proven preparation requirements are already satisfied`.

### WHAT YOU PROGRESS

List the exact quests/objectives whose progress justifies the recommendation. Separate:

- actionable focused-path quests;
- simultaneous/overlapping progress;
- independent objectives merely located on the same map;
- repeatable work.

### WHAT IT UNLOCKS

Only claim topology effects that are provable from the current quest graph and profile state. For a selected future goal, distinguish focused-path unlocks from unrelated global unlocks.

### WHAT TO FIX FIRST

Show only proven preparation blockers. Unknown semantics should be labelled unresolved rather than guessed.

### WHY NOT THE ALTERNATIVE

Show the delta against the nearest competitor, for example:

- `Reserve has more raw objectives, but none advance the selected future goal`;
- `Woods advances another mandatory branch of the same goal and is fully prepared, while Customs has stronger focused overlap but needs a missing key`.

### CONFIDENCE

Confidence is based on evidence completeness, not model certainty theater:

- **High**: known objective kinds, known progress, known preparation, known topology and authoritative profile eligibility;
- **Medium**: useful recommendation but some objective/preparation semantics are unresolved;
- **Low / abstain**: alternatives are effectively tied, eligibility is unknown, or too much important state is unresolved.

## Abstention is a feature

The planner must be allowed to say:

> No clearly superior next raid. Choose based on preference.

Or, under a focused future goal:

> Several mandatory branches are actionable and none has a proven advantage.

Forced ranking creates fake value. A recommendation should require a meaningful decision delta.

The research decision policy therefore uses **conservative dominance rather than a weighted score**:

- raw objective count and raw remaining-work totals are not decisive dimensions;
- a candidate can be preferred when it has one or more proven advantages and no competing disadvantage across the applicable decision dimensions;
- explicit player focus constrains relevance but does not erase real trade-offs;
- if each candidate has at least one proven advantage, the policy abstains and exposes the trade-off;
- if no meaningful proven dimension differs, the policy abstains as a true tie.

## Parallel mandatory branches

For ordinary prerequisite conjunctions, all incomplete prerequisite branches are required. Topology alone does not prove that one branch is strategically more important because it is longer, deeper or has more descendants.

Therefore two equally actionable mandatory branches with equivalent focused evidence remain **Several good options**. Symmetry may be broken only by evidence that changes the player's immediate decision, such as:

- one raid advancing multiple actionable focused quests;
- focused shared-action overlap;
- a real focused-path immediate unlock;
- preparation friction;
- evidence completeness;
- or, only after focused evidence is equivalent, a conservative global side-benefit.

This preserves honesty and prevents a graph-theory label from becoming another arbitrary score.

## Busywork policy

The project-wide quest cleanup direction treats repetitive/redundant work as something to suppress rather than optimize around. Quest Planner should reflect that policy.

Initial safe rule:

- repeatables contribute useful context but do not outrank equivalent non-repeatable progression;
- do not infer that a high kill count is intrinsically busywork;
- stronger busywork classification requires explicit quest metadata/policy rather than name heuristics.

## UX direction

The F9 surface should present a decision, not a spreadsheet:

- **Best next raid** when one candidate is conservatively dominant;
- **Several good options** when multiple candidates remain on the Pareto frontier;
- **No meaningful recommendation** when the evidence does not justify a choice.

For a selected future goal, the explanation hierarchy should be:

1. selected goal;
2. actionable prerequisite(s) now;
3. recommended raid or unresolved alternatives;
4. exact shared progress;
5. focused-path unlocks;
6. preparation blockers;
7. uncertainty/cautions;
8. optional detailed objective list.

No numeric planner score is required.

## Runtime and ownership constraints

The planner should derive decisions from already cached planner state and bounded topology queries. It must not add per-frame polling, global scans, unnecessary Harmony patches or external web/API dependencies.

Quest Planner owns **decision semantics**. Dynamic Maps owns map visualization, Quest Tracker owns in-raid objective tracking, Task Item Indicator owns task-item signaling, and Item Intelligence owns general item requirement intelligence. Future integrations should consume a narrow planner decision snapshot rather than duplicating those UIs.

## Scenario acceptance tests

The product model is not accepted until it handles at least these scenarios:

1. **Raw density loses to synergy** — many unrelated objectives do not automatically beat fewer but truly overlapping progression objectives.
2. **Unlock bottleneck wins when proven** — an executable quest that immediately opens relevant work can beat a dead-end equivalent.
3. **Preparation creates a real trade-off** — higher leverage with missing preparation can remain tied against a ready alternative.
4. **Repeatable inflation loses** — repeatables do not win solely by count.
5. **Unknown semantics reduce confidence** — custom/unknown objectives do not fabricate synergy.
6. **Near tie abstains** — no meaningful delta means no fake #1.
7. **Future locked goal resolves** — Planner finds the incomplete prerequisite path even when the selected goal itself is not currently active.
8. **Graph-ready is not automatically actionable** — profile-blocked or eligibility-unknown prerequisites cannot create focus preference.
9. **Parallel mandatory branches remain honest** — asymmetric depth alone does not rank them.
10. **Focused unlock breaks symmetry only when real** — an immediate unlock must be on the selected path and actually become available under current prerequisite state.
11. **Modded-data advantage** — a custom quest or rewired prerequisite changes the recommendation directly from final SPT topology without an external dataset update.

## Product success criterion

Quest Planner is successful when a player can open it for 10–20 seconds and receive a defensible answer to a planning question that would otherwise require mentally joining Tasks, quest prerequisites, current SPT eligibility, map compatibility and inventory requirements.

The strongest criterion is:

> A player can select a future/blocked quest from their actual modded SPT ecosystem and get an explainable, current, profile-correct next-action decision without reconstructing that modded progression externally.

If the mod cannot reliably do that, it should be reduced or retired rather than expanded.
