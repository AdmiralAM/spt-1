# Quest Planner decision UX contract

## Purpose

The F9 surface must answer a decision, not present another quest spreadsheet. The default view is therefore driven by `PlannerRaidDecisionPresentation`, not by ordinal rank cards.

## State 1 — Best next raid

Use only when the decision set contains one undominated candidate, or when an explicit player progression focus resolves an otherwise ambiguous frontier.

Required content:

- location;
- `WHY THIS`: the strongest proven cross-quest overlap, progression leverage, readiness evidence, or explicit player focus;
- `WHAT YOU PROGRESS`: concrete non-repeatable quest identities;
- `WHAT IT UNLOCKS`: concrete immediate downstream quest identities when proven;
- `PREPARATION`: ready, missing proven item types, or unresolved requirements;
- `CAUTIONS`: repeatables, unknown objective semantics and incomplete evidence.

Do not show a numeric planner score.

## State 2 — Several good options

Use when multiple candidates remain on the Pareto frontier because each has a defensible advantage.

The UI must not assign `#1`, `#2`, `#3` to frontier candidates. Present a compact trade-off comparison instead, e.g.:

- Customs — stronger cross-quest PMC-kill overlap and downstream leverage;
- Woods — fully prepared, lower friction;
- Reserve — more independent work but no proven shared-action advantage.

The player's preference is the unresolved variable. This is a useful planner result, not a failure.

If the player has selected a progression focus, that explicit intent may resolve the frontier. The UI must then state that the recommendation is focus-driven rather than pretending the candidate is globally dominant.

## State 3 — No meaningful recommendation

Use when there are no usable candidates or no meaningful proven difference.

Preferred copy:

> No clearly superior next raid. Choose based on preference.

Do not fall back to raw quest/objective density merely to populate a recommendation.

## Progression focus

Progression focus is an explicit player preference, not another hidden score dimension.

If a focused quest is supported by only one of two otherwise competing candidates, the planner may prefer that candidate and say why. If both or neither candidate supports the focus, normal conservative comparison applies.

This makes the existing focus feature useful: it converts a genuine multi-objective trade-off into an intentional recommendation without inventing arbitrary global weights.

## Information hierarchy

The intended 10–20 second read order is:

1. decision state and location(s);
2. why the choice matters;
3. exact quests advanced together;
4. immediate unlock leverage;
5. preparation blocker(s);
6. evidence caveats;
7. optional detailed objective list.

Raw counts belong in optional detail only.

## Removal / demotion from current 0.9.4 UI

The following current concepts should not remain primary decision surfaces after migration:

- global ordinal ranking when candidates are not actually comparable;
- `High quest density` as a recommendation reason;
- kill/extract counts as the headline justification;
- generic `Ready` without showing what readiness enables;
- generic `Nothing proven ... missing` as the main preparation message.

They may survive as supporting detail when useful.

## Runtime constraints

The presentation layer is derived from already cached planner state. It must not add:

- per-frame polling;
- global runtime scans;
- new Harmony patches;
- an additional server route solely for presentation;
- external web/API dependencies.

## Migration gate

This contract remains research-only until runtime gate #80 permits feature integration. The accepted implementation should replace the decision semantics first and only then rebuild the F9 visual hierarchy around them.
