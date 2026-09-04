# Admiral Trader migration contract

## Goal

Preserve finishability of legacy quests already present in an existing PMC profile without exposing the retired Andrudis trader zoo or allowing deprecated successor chains to continue.

## Preferred implementation: template-suppression completion bridge

The first migration implementation should avoid direct PMC profile mutation.

Native SPT quest behavior provides the required bridge:

1. `QuestController.AcceptQuest(...)` creates a `QuestStatus` profile record only when the player accepts a quest (or updates an existing record when restarting it).
2. `QuestHelper.GetClientQuests(...)` checks the PMC profile before evaluating normal start conditions. If a quest template has a matching profile quest record, that template is returned to the client regardless of its current start conditions.
3. Therefore a retained legacy quest template can have an impossible/closed new-start gate while an existing profile entry can still resolve to that template.
4. Deprecated successor templates are not loaded at all, so completing a retained legacy quest cannot unlock the retired chain through ordinary template discovery.

This is preferable to rewriting thousands of profile quest-state records.

## Profile classes

### New profile

- Load no legacy campaign for normal discovery.
- Load only curated Admiral Trader quests.
- Legacy completion templates may exist in the server template table only when required by the bridge, but their new-start gate must be closed.

### Existing profile: accepted legacy quest

If the profile contains a legacy quest record and its state represents an accepted/in-progress/completable quest:

- retain the exact legacy quest template required to finish it;
- retain required objectives/reward/localization data;
- remap trader presentation only if needed for the one-Admiral UI/runtime contract;
- close the template's normal new-start path;
- never load deprecated successor templates solely because this quest exists.

### Existing profile: completed/failed historical quest

- preserve the profile history;
- do not resurrect the quest;
- do not issue deprecated successors;
- the template only needs to remain loaded if native client/profile behavior requires it for a supported historical state.

### Existing profile: no legacy quest record

- legacy quest is treated as unstarted;
- it is not discoverable/startable;
- no profile write is necessary.

## Important edge case: stale profile records

`GetClientQuests(...)` returns a template whenever a matching `profile.Quests` record exists before evaluating normal start conditions. This means a stale legacy record in a non-active status cannot be hidden merely by making `AvailableForStart` impossible.

The migration layer must therefore classify actual profile statuses, not only quest-id presence. Before runtime publication, exact SPT 4.1.4 status semantics must be verified for any states that can persist in `profile.Quests` without representing an active/completable quest (for example delayed/restartable/failed states).

No direct deletion or mutation of such records is authorized until the exact 4.1.4 runtime boundary and persistence behavior are proven.

## Successor suppression

Default rule: **absence beats mutation**.

- `MIGRATION_ONLY` legacy successor templates are not loaded for normal progression.
- `DROP` templates are not loaded.
- Curated successors use new Admiral Trader IDs/graph edges and are granted only by the curated graph.
- A retained completion template must not itself contain a path that implicitly registers or materializes a deprecated successor outside the server quest table.

## Restartable quests

Restartable legacy quests require separate handling because an existing failed profile record can be accepted again by native SPT. Until the restart boundary is explicitly tested against SPT 4.1.4, restartable legacy quests are not eligible for the completion bridge by default.

The maintained inventory already extracts the `restartable` flag so this rule is mechanically enforceable.

## Data required per retained completion template

The migration manifest/runtime layer must know at minimum:

- legacy quest id;
- source bundle;
- legacy trader id;
- current profile status;
- restartable flag;
- objective/counter dependencies;
- reward records;
- localization keys;
- successor ids;
- whether any successor is curated or deprecated.

## Runtime gate criteria

Before a physical runtime package is requested, static/automated validation must prove:

1. no new profile can start a retained legacy completion template;
2. accepted legacy quests resolve to a retained template;
3. deprecated successors are absent from the loaded campaign graph;
4. no retained completion template references a missing required objective/reward item;
5. restartable legacy quests are excluded unless explicitly supported;
6. curated Admiral Trader quest IDs do not collide with legacy or vanilla IDs.

The physical runtime gate should then validate one representative active legacy completion and one new-profile non-exposure case, not a sequence of micro-tests.
