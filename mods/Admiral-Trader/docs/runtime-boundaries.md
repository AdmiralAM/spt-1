# Admiral Trader runtime boundary evidence

## Target runtime

- SPT 4.1.x; current project runtime evidence is SPT 4.1.3.
- Server-side implementation target is .NET 10, matching maintained repository server mods.
- Official NuGet packages currently top out at `SPTarkov.Server.Core` / `SPTarkov.Common` / `SPTarkov.DI` 4.1.2, while the installed runtime is 4.1.3. Therefore NuGet compile proof is valid for the 4.1.2 public API surface, but exact 4.1.3 profile-write behavior must be proven from the runtime assemblies/references before direct profile mutation is implemented.

## Single trader registration boundary — proven

The maintained `mods/Admiral-Artyom-Revival` module already compiles and runs against the current repository SPT generation using:

- `ModHelper` for module path resolution;
- `ImageRouter` for trader avatar route registration;
- `TraderConfig.UpdateTime` for refresh configuration;
- `RagfairConfig.Traders` for flea visibility;
- `TradersTable.TryAdd(...)` with native `Trader`, `TraderBase` and `TraderAssort` records;
- locale transformers through `LocaleTable.Global`;
- explicit assort replacement after registration.

This is the preferred Admiral Trader baseline. We do not need legacy six-directory trader enumeration.

## Quest loading boundary — proven for current generation

Admiral Artyom Revival uses `WTT-ServerCommonLib` custom quest loading after trader registration. The SPT 4.1 Andrudis port and Scorpion C# reference also prove current-generation quest insertion/loading patterns. Admiral Trader should retain WTT only if the curated quest feature set actually requires it; native table insertion remains a viable alternative for ordinary quest records.

## Native acceptance/profile semantics — proven on current upstream source

Pinned inspection of current `sp-tarkov/server-csharp` source shows:

- `QuestController.AcceptQuest(...)` checks `pmcData.Quests` for the accepted quest id;
- when no profile record exists, it calls `QuestHelper.GetQuestReadyForProfile(...)` and adds the returned `QuestStatus` to `pmcData.Quests`;
- `GetQuestReadyForProfile(...)` creates the profile record in `Started` state, except a quest with an `AvailableAfter` delay can enter `AvailableAfter` state;
- therefore normal unstarted quests are not represented in `profile.Quests` merely because they are visible/available to start.

This sharply reduces the migration problem: for the normal case, absence of a profile record is a reliable unstarted signal.

## Existing-profile completion bridge — proven design boundary

Current `QuestHelper.GetClientQuests(...)` iterates server quest templates and checks the PMC profile before normal start-condition filtering. When a matching `profile.Quests` record exists, the quest template is returned to the client regardless of its start conditions.

This enables a no-profile-write migration design:

1. retain only legacy templates required to finish already accepted profile quests;
2. close their normal `AvailableForStart` path so a new/no-record profile cannot start them;
3. omit deprecated successor templates from the loaded quest database;
4. keep new Admiral Trader progression on new curated ids/edges.

The detailed contract is in `docs/migration-contract.md`.

## Important status edge cases

`GetClientQuests(...)` returns a matching profile quest regardless of status. Therefore a stale existing profile record cannot be hidden solely by changing `AvailableForStart`.

Known native profile-state creation also includes `AvailableAfter` for delayed accepted quests, and restartable failed quests can reuse an existing profile record when accepted again. These cases must be explicitly classified before migration runtime publication.

Default safety policy:

- active/completable accepted legacy quest: eligible for completion bridge;
- no profile record: suppress legacy template from normal start;
- restartable legacy quest: bridge-disabled until explicitly supported;
- stale/non-active profile record: preserve, do not mutate, and do not claim full suppression until exact 4.1.3 behavior is validated.

## Direct migration write boundary — intentionally deferred

The preferred migration path above avoids direct profile writes for the primary existing-profile case. Directly deleting or rewriting `pmcData.Quests` remains unauthorized until exact SPT 4.1.3 runtime assembly/persistence behavior is proven.

Required proof before any direct profile write:

1. exact native type/API surface in the installed 4.1.3 runtime;
2. safe mutation point/load order;
3. save/persistence behavior;
4. behavior for delayed and restartable states;
5. recovery behavior if the mod is removed after migration.

## Runtime evidence

Project SPT logs from the current environment report `Server: 4.1.3` and show maintained custom trader/quest modules loading successfully. This corroborates that the repository references above are not merely stale compile-time examples.

## Decision

The next implementation may safely proceed with one-trader registration, profile classification, and a template-suppression completion bridge without direct PMC profile writes. Before a runtime package is published, the bridge must be mechanically validated against retained-template/start-gate/successor rules, then checked once on the exact 4.1.3 runtime at a defined gate.
