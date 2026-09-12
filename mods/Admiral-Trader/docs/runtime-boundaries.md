# Admiral Trader runtime boundary evidence

## Target runtime

- Canonical target is **SPT 4.1.5**.
- Server-side implementation target is .NET 10, matching maintained repository server mods.
- Historical SPT 4.1.3/4.1.4 runtime evidence remains useful for provenance and regression context only; it does not authorize carrying exact-runtime assumptions forward unchanged.
- `SPTushonka.Server.Core`, `SPTushonka.Common`, and `SPTushonka.DI` package references must resolve at the canonical 4.1.5 baseline on the active branch. Exact installed-runtime validation remains required before direct profile mutation is implemented.

## Single trader registration boundary — current-generation evidence

The maintained `mods/Admiral-Artyom-Revival` module demonstrates the current repository registration pattern using:

- `ModHelper` for module path resolution;
- `ImageRouter` for trader avatar route registration;
- `TraderConfig.UpdateTime` for refresh configuration;
- `RagfairConfig.Traders` for flea visibility;
- `TradersTable.TryAdd(...)` with native `Trader`, `TraderBase` and `TraderAssort` records;
- locale transformers through `LocaleTable.Global`;
- explicit assort replacement after registration.

This is the preferred Admiral Trader baseline. We do not need legacy six-directory trader enumeration. Before runtime publication, the exact SPT 4.1.5 API signatures used by the active implementation must still compile and pass the exact-runtime gate.

## Quest loading boundary — proven for the 4.1 generation, exact target still gated

Admiral Artyom Revival uses `WTT-ServerCommonLib` custom quest loading after trader registration. The SPT 4.1 Andrudis port and Scorpion C# reference also prove current-generation quest insertion/loading patterns. Admiral Trader should retain WTT only if the curated quest feature set actually requires it; native table insertion remains a viable alternative for ordinary quest records.

These references establish architecture shape, not exact SPT 4.1.5 behavior. Exact target validation remains mandatory where the implementation depends on a concrete signature or lifecycle detail.

## Native acceptance/profile semantics — source-backed, runtime-gated

Pinned inspection of SPT server source established the expected native pattern:

- `QuestController.AcceptQuest(...)` checks `pmcData.Quests` for the accepted quest id;
- when no profile record exists, it calls `QuestHelper.GetQuestReadyForProfile(...)` and adds the returned `QuestStatus` to `pmcData.Quests`;
- `GetQuestReadyForProfile(...)` creates the profile record in `Started` state, except a quest with an `AvailableAfter` delay can enter `AvailableAfter` state;
- therefore normal unstarted quests are not represented in `profile.Quests` merely because they are visible/available to start.

This is strong design evidence for the completion bridge, but exact SPT 4.1.5 runtime behavior remains the publication authority.

## Existing-profile completion bridge — design boundary

The source-backed `QuestHelper.GetClientQuests(...)` flow checks the PMC profile before normal start-condition filtering. When a matching `profile.Quests` record exists, the quest template is returned to the client regardless of its normal start conditions.

This supports a no-profile-write migration design:

1. retain only legacy templates required to finish already accepted profile quests;
2. close their normal `AvailableForStart` path so a new/no-record profile cannot start them;
3. omit deprecated successor templates from the loaded quest database;
4. keep new Admiral Trader progression on new curated ids/edges.

The detailed contract is in `docs/migration-contract.md`.

## Important status edge cases

A matching profile quest can bypass normal start-condition filtering. Therefore a stale existing profile record cannot be hidden solely by changing `AvailableForStart`.

Known profile-state creation also includes `AvailableAfter` for delayed accepted quests, and restartable failed quests can reuse an existing profile record when accepted again. These cases must be explicitly classified before migration runtime publication.

Default safety policy:

- active/completable accepted legacy quest: eligible for completion bridge;
- no profile record: suppress legacy template from normal start;
- restartable legacy quest: bridge-disabled until explicitly supported;
- stale/non-active profile record: preserve, do not mutate, and do not claim full suppression until exact SPT 4.1.5 behavior is validated.

## Direct migration write boundary — intentionally deferred

The preferred migration path above avoids direct profile writes for the primary existing-profile case. Directly deleting or rewriting `pmcData.Quests` remains unauthorized until exact SPT 4.1.5 runtime assembly/persistence behavior is proven.

Required proof before any direct profile write:

1. exact native type/API surface in the installed 4.1.5 runtime;
2. safe mutation point/load order;
3. save/persistence behavior;
4. behavior for delayed and restartable states;
5. recovery behavior if the mod is removed after migration.

## Runtime evidence status

- SPT 4.1.3/4.1.4 logs and exact-runtime builds are historical evidence only.
- The repository now has a verified SPT 4.1.5 runtime archive identity and combined server-start workflow, but the combined RC workflow intentionally builds the historical frozen Trader 0.1.0 worktree. It therefore does **not** by itself prove the current active Trader branch's 4.1.5 package/API contract.
- The active Trader branch must compile its own `AdmiralTrader.Server.csproj` against the 4.1.5 published API and pass its deterministic workflows before any current-branch runtime claim is made.

## Decision

Proceed with one-trader registration, profile classification, and a template-suppression completion bridge only within the fail-closed design/validation boundary. Before any runtime package is published, every implementation dependency must be revalidated against the exact SPT 4.1.5 target, then covered by one coherent batched physical gate rather than micro-tests.
