# Admiral Trader runtime boundary evidence

> Historical architecture record. Current release evidence and runtime scope are maintained in `README.md`, `docs/campaign-audit-172.md` and the exact-head CI workflows.

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

- SPT 4.1.3/4.1.4 logs and builds remain historical evidence only.
- The active `0.3.0-rc` source compiles against the verified SPT 4.1.5 runtime input.
- The manual combined workflow builds the exact Trader + Economy HEAD, runs deterministic suites and completes an isolated server-start smoke before publishing its install-ready artifact.
- Runtime metadata remains `~4.1.0`; exact 4.1.5 validation is reproducibility evidence rather than a patch lock.

## Decision

The one-trader native registration and fail-closed migration boundary are implemented. Repository-side validation is complete; the remaining release boundary is one coherent fresh-profile acceptance after the complete mod set is ready.
