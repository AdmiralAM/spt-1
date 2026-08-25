# Admiral Trader runtime boundary evidence

## Target runtime

- SPT 4.1.x; current project runtime evidence is SPT 4.1.3.
- Server-side implementation target is .NET 10, matching maintained repository server mods.

## Single trader registration boundary — proven

The maintained `mods/WTT-Artem-Revival` module already compiles and runs against the current repository SPT generation using:

- `ModHelper` for module path resolution;
- `ImageRouter` for trader avatar route registration;
- `TraderConfig.UpdateTime` for refresh configuration;
- `RagfairConfig.Traders` for flea visibility;
- `TradersTable.TryAdd(...)` with native `Trader`, `TraderBase` and `TraderAssort` records;
- locale transformers through `LocaleTable.Global`;
- explicit assort replacement after registration.

This is the preferred Admiral Trader baseline. We do not need legacy six-directory trader enumeration.

## Quest loading boundary — proven for current generation

The maintained Artem path uses `WTT-ServerCommonLib` custom quest loading after trader registration. The SPT 4.1 Andrudis port and Scorpion C# reference also prove current-generation quest insertion/loading patterns. Admiral Trader should retain WTT only if the curated quest feature set actually requires it; native table insertion remains a viable alternative for ordinary quest records.

## PMC profile quest-state boundary — proven read boundary

The maintained `SPT-Quest-Planner` server module injects `SPTarkov.Server.Core.Helpers.Profile.ProfileHelper` and calls `GetPmcProfile(sessionId)` on SPT `~4.1.0`.

Its profile projection reads the PMC profile `Quests` collection and establishes the fields required for migration classification:

- quest id: `qid` / `Qid` / `questId`;
- quest state: `status`;
- timing metadata: `startTime`, `statusTimer`;
- task progress: `TaskConditionCounters` including `id`, `type`, `value`, `sourceId`.

This proves that Admiral Trader can classify existing-profile legacy quests as active/completed/failed/unstarted from the current PMC profile boundary without inventing a new persistence model.

## Migration write boundary — not yet proven

Read access is proven. Directly mutating profile quest records or intercepting successor issuance is a separate boundary and must be proven before implementation.

Required proof before profile writes:

1. exact native type for the PMC quest-state collection on the current SPT 4.1.3 package;
2. safe mutation point/load order for suppressing unstarted deprecated legacy quests;
3. safe mechanism for preserving active legacy quests while preventing deprecated successors;
4. whether blocking successors is best implemented by filtering loaded quest templates, rewriting prerequisites, or profile-state mutation;
5. persistence/save behavior after any mutation.

Until these are proven, migration remains manifest/design only.

## Runtime evidence

Project SPT logs from the current environment report `Server: 4.1.3` and show maintained custom trader/quest modules loading successfully, including Artem Revival, Quest Planner and Item Intelligence. This corroborates that the repository references above are not merely stale compile-time examples.

## Decision

The next implementation may safely use the one-trader registration/read-profile boundaries above. It must not yet write PMC quest state. The next technical gate is to prove successor suppression/profile mutation semantics, then implement the smallest deterministic migration layer.
