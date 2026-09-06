# Admiral Artyom Revival — SPT 4.1.4 Status

## Result

**Module version: 3.0.0. Stable runtime baseline: r5-RU-compat — PASS on SPT 4.1.4.**

The archived upstream WTT-Artem package has been successfully revived as **Admiral Artyom Revival** for the SPT 4.1/.NET 10 server stack while preserving the authored Artem trader, 23-quest campaign, unique equipment and clothing set.

## Validated environment

- SPT `4.1.4`
- WTT-ServerCommonLib `3.0.6`
- WTT-ClientCommonLib `3.0.6`
- server runtime: .NET 10

## Runtime evidence

The accepted user runtime test confirmed:

1. Admiral Artyom Revival mod discovery and dependency resolution;
2. custom item creation;
3. custom quest-zone creation;
4. Artem trader registration;
5. custom quest loading;
6. custom clothing loading;
7. trader assort application;
8. client bundle discovery with all 239 required bundles already present;
9. Russian quest/item/clothing/trader localization;
10. trader and quest UI usability;
11. no recurrence of the previously observed upstream Artem armor deserialization errors;
12. no recurrence of the previously observed upstream Artem clothing/ArmBandView warning set in the accepted test.

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

## Bundles and runtime publication model

The six supplied bundle archives are one logical `Bundles/` directory. All 239 paths referenced by `bundles.json` are present.

The accepted r5 runtime uses three preserved layers:

- the validated SPT 4.1.4 Admiral Artyom Revival server build;
- repaired authored upstream Artem core data reconstructed from the archived source set by deterministic importer/repair/localization tooling in this module;
- the external approximately 1.5 GB Unity `Bundles/` set retained in the installed Admiral Artyom Revival folder.

The permanent `runtime-artem-revival` branch name is retained as a publication compatibility identifier. It pins the validated server identity plus a runtime manifest containing the accepted candidate identity and hashes. It deliberately does not duplicate the authored core data or Bundles and is therefore not a standalone full-install Git archive.

## Deferred work

The following are deliberately **not** part of the compatibility revival and remain future policy/maintenance work only if needed:

- economy rebalance;
- reward tuning;
- optional cosmetic/bundle pruning;
- removal of orphan/stale candidates;
- PBS pool integration.

No further compatibility changes are required without new runtime evidence.
