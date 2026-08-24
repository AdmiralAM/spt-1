# Artem Revival — SPT 4.1.3 Status

## Result

**Stable runtime baseline: r5-RU-compat — PASS on SPT 4.1.3.**

The archived WTT-Artem package has been successfully revived for the SPT 4.1/.NET 10 server stack while preserving the authored trader, 23-quest campaign, unique equipment and clothing set.

## Validated environment

- SPT `4.1.3`
- WTT-ServerCommonLib `3.0.6`
- WTT-ClientCommonLib `3.0.6`
- server runtime: .NET 10

## Runtime evidence

The accepted user runtime test confirmed:

1. Artem mod discovery and dependency resolution;
2. custom item creation;
3. custom quest-zone creation;
4. trader registration;
5. custom quest loading;
6. custom clothing loading;
7. trader assort application;
8. client bundle discovery with all 239 required bundles already present;
9. Russian quest/item/clothing/trader localization;
10. trader and quest UI usability;
11. no recurrence of the previously observed Artem armor deserialization errors;
12. no recurrence of the previously observed Artem clothing/ArmBandView warning set in the accepted test.

The server-side startup telemetry reaches `startup complete` after loading 703 assort item records.

## Preserved content baseline

- quests: 23
- prerequisite edges: 22
- dependency cycles: 0
- custom item entries: 131
- clothing entries: 64
- trader root offers after compatibility repair: 281
- assort item records: 703
- explicit Success `AssortmentUnlock` rewards: 40
- missing quest images after repair: 0
- dangling explicit unlock mappings after repair: 0
- bundle manifest resolution: 239/239

## Compatibility repairs accepted

- SPT 4.0/.NET 9 metadata/lifecycle replaced by SPT 4.1 `IModMetadata` + asynchronous `IOnLoad` on .NET 10;
- broken quest thumbnail extension repaired;
- missing Sweden Patch trader offer restored;
- explicit quest unlock rewards synchronized with QuestAssort success mappings;
- quest localization normalized to CommonLib-compatible `en.json` / `ru.json`;
- Russian localization added for all 204 quest locale keys, 131 custom item entries and 64 clothing entries;
- DevTac Ronin armor preset slot casing repaired for current item deserialization;
- OPENLAND HEXAGON side soft-armor slot definitions restored to match its authored preset.

## Bundles and package model

The six supplied bundle archives are one logical `Bundles/` directory. All 239 paths referenced by `bundles.json` are present.

The large Unity bundle payload remains external to normal Git source history. It is retained once in the installed Artem folder. The maintained `runtime-artem-revival` channel represents the validated core overlay and documents the external Bundles requirement.

## Deferred work

The following are deliberately **not** part of the compatibility revival and remain future policy/maintenance work only if needed:

- economy rebalance;
- reward tuning;
- optional cosmetic/bundle pruning;
- removal of orphan/stale candidates;
- PBS pool integration.

No further compatibility changes are required without new runtime evidence.
