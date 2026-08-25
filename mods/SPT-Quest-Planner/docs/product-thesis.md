# Quest Planner product thesis

## Decision to make

Quest Planner deserves to exist only if it solves a player decision that the rest of the SPT quest stack does not already solve.

The neighboring tools are already strong at **information display**:

- vanilla Tasks: quest status, text, rewards and acceptance;
- Quest Tracker: in-raid tracked-objective overlay;
- Dynamic Maps: spatial quest/objective information;
- Task Item Indicator: nearby task-item guidance;
- TaskSearch / Task List Fixes: task browsing/search/sorting;
- Item Intelligence: item-level keep/requirement state.

Quest Planner must therefore not compete on showing more quest information. Its product is **decision compression**: combine global quest topology, current profile state, preparation state and objective overlap into a small number of explainable next-action choices.

## Competitive reality

The concept is not globally unique. External Tarkov companion tools now exist that rank maps, group compatible quests, visualize dependency leverage, or recommend a next quest. Examples observed during research include RaidIQ, QEFT-style quest-tree prioritizers, TarkovQuestie and broader companion sites with quest optimizers.

That means the defensible advantage cannot be "we invented best-next-quest".

The SPT-native advantage is different:

1. **Authoritative live profile state** — no manual checkbox maintenance is required when the server already knows the PMC state.
2. **Authoritative modded quest graph** — recommendations can use the actual final SPT runtime database, including installed custom traders/quest packs that public live-Tarkov datasets may not know.
3. **Local/offline operation** — no dependence on an external account, website, live API or current wipe dataset for the core decision.
4. **Exact SPT preparation state** — the planner can reason against the real local inventory/profile snapshot instead of a manually maintained checklist.
5. **Runtime-safe interoperability** — the result can later be exposed as a narrow local decision snapshot to other SPT mods without duplicating their UI responsibilities.

If Quest Planner cannot exploit those SPT-native advantages, an external web planner is likely a better product and this mod should be reconsidered.

## Core user question

> Given what is active or reachable now, what is the highest-value next raid or progression action, why is it better than the alternatives, and what must I prepare first?

A useful answer must change a decision the player would otherwise have to derive manually from several screens/tools.

## Unique value proposition

The unique unit of value is not a quest, marker, item or objective. It is a **cross-quest decision over the player's actual modded SPT state**.

Quest Planner should be the only local layer that deliberately reasons across:

1. **Concurrency** — multiple quests/objectives that can advance in the same raid.
2. **Action overlap** — one action that advances multiple quests, not merely several unrelated objectives on one map.
3. **Progression leverage** — completing/progressing a quest that releases downstream work or removes a prerequisite bottleneck.
4. **Preparation friction** — required items/keys/equipment already available versus missing or unresolved.
5. **Busywork suppression** — repeatables and low-leverage work must not win only because they inflate raw counts.
6. **Uncertainty** — unknown/custom condition semantics lower confidence rather than being silently treated as equivalent to known work.
7. **Opportunity cost** — explain why the chosen raid/action is better than the nearest alternative.

## Product anti-thesis

If the planner can only say:

- "Reserve has 7 kills and 2 extracts";
- "Customs has 2 kills";
- "this map has the most quests";

then the mod has no defensible reason to exist. That is counting, not planning.

Likewise, if its recommendation can be reproduced from a public quest database plus a manually selected active-quest list, the SPT-native implementation is not yet earning its maintenance cost.

## Recommendation contract

Every recommendation must be explainable without exposing an opaque numeric score.

### WHY THIS

Use concrete factors, for example:

- `2 active non-repeatable quests progress from the same PMC-kill action`;
- `finishing this quest would immediately unlock 2 downstream quests`;
- `all proven preparation requirements are already satisfied`.

### WHAT YOU PROGRESS

List the exact quests/objectives whose progress justifies the recommendation. Separate:

- simultaneous/overlapping progress;
- independent objectives merely located on the same map;
- repeatable work.

### WHAT IT UNLOCKS

Only claim topology effects that are provable from the current quest graph. Prefer immediate unlocks and explicit prerequisite bottlenecks over vague "important quest" labels.

### WHAT TO FIX FIRST

Show only proven preparation blockers. Unknown semantics should be labelled unresolved rather than guessed.

### WHY NOT THE ALTERNATIVE

Show the delta against the nearest competitor, for example:

- `Reserve has more raw objectives, but only one active non-repeatable quest and no cross-quest action overlap`;
- `Shoreline has similar progression leverage but requires two missing preparation items`.

### CONFIDENCE

Confidence is based on evidence completeness, not model certainty theater:

- **High**: known objective kinds, known progress, known preparation, known topology;
- **Medium**: useful recommendation but some objective/preparation semantics are unresolved;
- **Low / abstain**: alternatives are effectively tied or too much important state is unknown.

## Abstention is a feature

The planner must be allowed to say:

> No clearly superior next raid. Choose based on preference.

Forced ranking creates fake value. A recommendation should require a meaningful decision delta.

## Ranking model direction

Do not replace the current raw-count ranker with another monolithic weighted score. Build an explainable **decision profile** for each candidate and compare profiles in stable layers.

Candidate factors, in priority order to validate through scenarios/tests:

1. executable/readiness state;
2. active non-repeatable quest concurrency;
3. cross-quest action overlap;
4. immediate downstream unlock leverage;
5. missing/unresolved preparation friction;
6. repeatable share / busywork penalty;
7. known remaining work and objective density as tie-breakers, not primary value.

Weights and ordering are hypotheses until realistic scenarios prove them.

## Action-overlap definition

Raw objective count is insufficient. Objectives should be grouped into an action signature using only semantics the planner can prove.

Initial conservative signature:

- objective kind;
- normalized target set where present;
- effective location.

Two objectives create cross-quest overlap only when they share an action signature and belong to different quests. This catches cases such as two PMC-kill objectives that can advance from the same kill while avoiding the claim that unrelated `kill`, `visit` and `extract` work is synergistic merely because all occur on Customs.

Unknown/custom condition kinds do not create synergy without a proven normalizer.

## Busywork policy

The project-wide quest cleanup direction treats repetitive/redundant work as something to suppress rather than optimize around. Quest Planner should reflect that policy.

Initial safe rule:

- repeatables contribute useful context but do not outrank equivalent non-repeatable progression;
- do not infer that a high kill count is intrinsically busywork;
- stronger busywork classification requires explicit quest metadata/policy rather than name heuristics.

## Required implementation slices

### Slice A — decision signals (safe research slice)

Pure deterministic model, not wired into runtime ranking:

- derive non-repeatable/repeatable counts;
- derive cross-quest action-overlap groups;
- derive immediate-unlock leverage from existing topology/query APIs;
- expose preparation friction and evidence completeness;
- tests for realistic decision conflicts.

Purpose: prove that the data already available in 0.9.4 can support a unique recommendation model.

### Slice B — explainable comparator

After runtime gate #80 is accepted:

- compare decision profiles;
- include explicit abstention threshold/tie behavior;
- generate structured reasons and alternative deltas;
- keep old ranker available behind tests until migration is proven.

### Slice C — UX

Present one decision, not a spreadsheet:

- best next raid/action;
- why;
- overlapping progress;
- unlock leverage;
- preparation blockers;
- nearest alternative and tradeoff;
- confidence/abstention.

### Slice D — optional integrations

Only after the core decision layer proves value. Dynamic Maps, Quest Tracker or Item Intelligence integrations should consume a narrow planner decision snapshot; Quest Planner must not absorb their UI responsibilities.

## Scenario acceptance tests

The product model is not accepted until it handles at least these scenarios:

1. **Raw density loses to synergy** — map A has many unrelated objectives; map B has fewer objectives but one action advances two active non-repeatable quests. B should be explainably preferred when other factors are comparable.
2. **Unlock bottleneck wins** — a modest quest that immediately opens several blocked quests outranks a dead-end side task when both are executable.
3. **Preparation reverses choice** — higher-leverage raid with missing required preparation can lose to a ready alternative.
4. **Repeatable inflation loses** — several repeatables do not beat one meaningful non-repeatable progression path solely by count.
5. **Unknown semantics reduce confidence** — custom/unknown objectives do not create fabricated synergy.
6. **Near tie abstains** — candidates with no meaningful proven delta produce no forced recommendation.
7. **Modded-data advantage** — a custom SPT quest or altered prerequisite chain changes the recommendation without requiring an external dataset update.

## Product success criterion

Quest Planner is successful when a player can open it for 10–20 seconds and receive a defensible answer to a planning question that would otherwise require mentally joining the Tasks screen, map, inventory requirements and quest prerequisite graph.

The stronger criterion is that the answer remains correct for the player's **actual installed SPT quest ecosystem**, not merely vanilla live Tarkov.

If it cannot do that, it should be reduced or retired rather than expanded.