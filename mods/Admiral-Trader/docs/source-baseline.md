# Admiral Trader source baseline

## Purpose

Record which references are authoritative for which part of the work before implementation crosses into SPT runtime code.

## Product identity

The maintained product/workstream name is **Admiral Trader**. The trader working name is **Admiral / Адмирал**. References to Andrudis/QuestManiac below describe legacy source provenance only.

## Legacy content source

Primary content/data source:

- `Thirt3nth/Andrudis-Questmaniac` — SPT 3.10-era repository containing the legacy `db/QuestBundles` tree and quest data.

Current-generation compatibility reference:

- `laurentmekka/AndrudisQuestManiac` — SPT 4.1 port. It proves that the old quest corpus can be converted/loaded on the 4.1 generation and documents repairs for removed items, invalid durability conditions, incomplete weapon rewards, and modular armour inserts.

The legacy repositories are **content/data-model sources and compatibility references**, not target runtime architecture or product identity.

## Target

- Canonical runtime target: **SPT 4.1.5**.
- Historical SPT 4.1.3/4.1.4 evidence remains useful only as provenance or compatibility history; it is not current runtime authority.
- .NET 10 server-mod generation, matching maintained server-side modules in this repository.
- One trader: working display name `Admiral` / `Адмирал`.

## Runtime/API boundaries already evidenced

The SPT 4.1 Andrudis port uses the current DI/load model (`IModMetadata`, `IOnLoad`, injected database tables, `ImageRouter`, `TraderConfig`, `RagfairConfig`) rather than the old 3.x server API.

The maintained `WTT-Artem-Revival` module in this repository targets `net10.0` and consumes `WTT-ServerCommonLib` 3.0.6. This is evidence that the WTT 3.x server-common boundary is viable in the repository's current SPT generation; it is **not** yet a decision that Admiral Trader must depend on WTT at runtime.

`acidphantasm/scorpion-csharp` is a single-custom-trader architecture reference: explicit trader identity, explicit assort/quest-assort loading, lazy locale injection, and SPT 4.1 table/config boundaries. It is a code-pattern reference, not source to copy wholesale.

Current SPT trader JSON supplied from an installed SPT runtime is a native data-shape reference for `base.json`, `assort.json`, and `questassort.json`. Those runtime files are reference input and are not committed here.

## Baseline viability conclusion

The legacy inventory/curation gates operate only on legacy JSON and remain independent of runtime migration details.

Before server implementation or profile migration crosses a boundary, re-prove the exact SPT 4.1.5 behavior required for:

1. single trader registration and image/locales;
2. quest insertion/loading;
3. quest-assort unlocks;
4. profile quest-state access required for migration;
5. load order relative to other trader/quest mods and Economy Admiral;
6. item/TPL availability used by weapon, ammunition, equipment and reward contracts.

If any boundary cannot be proven from current SPT 4.1.5 source/package/runtime references, stop at that boundary rather than inheriting a 4.1.4 assumption.
