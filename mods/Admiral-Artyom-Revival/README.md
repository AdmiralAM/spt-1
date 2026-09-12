# Admiral Artyom Revival

Stable revival of the upstream **WTT-Artem** mod for **SPT 4.1.3**. Current module version: **3.0.0**.

**Admiral Artyom Revival** is the official maintained product identity in this repository. The original WTT-Artem/Artem names remain only where they identify upstream provenance, the in-game Artem trader/content, or technical compatibility identifiers that should not be renamed casually.

The module preserves the upstream Artem trader, 23-quest campaign, unique equipment, clothing and authored progression while migrating the server runtime to the SPT 4.1/.NET 10 stack and repairing compatibility defects proven by static and runtime evidence.

## Stable status

Runtime validation on SPT 4.1.3 is **PASS**.

Confirmed in game:

- Artem is discovered and completes its startup pipeline;
- custom items, quest zones, trader, quests, clothing and trader assort load successfully;
- trader UI, assortment, quest UI and clothing/customization are usable;
- Russian localization is active for quests, custom items, clothing and trader text;
- the previous armor deserialization failures are resolved;
- the previous Artem clothing/ArmBandView visual warnings are no longer reproduced in the accepted runtime test;
- all 239 required bundle paths are present in the supplied external Bundles set.

The accepted runtime baseline is **r5-RU-compat**.

## Version and compatibility identity

- product: **Admiral Artyom Revival**;
- module version: **3.0.0**;
- SPT runtime: **4.1.3**;
- WTT-ServerCommonLib: **3.0.6** validated runtime/build baseline;
- WTT-ClientCommonLib: **3.0.6** used by the validated client environment.

Runtime metadata intentionally accepts the compatible CommonLib `3.0.x` line. Legacy GUID, namespace, upstream URL and in-game Artem identifiers are compatibility/provenance identifiers, not the maintained product name.

## Installation model

Admiral Artyom Revival is a server mod installed under:

```text
SPT_Runtime/user/mods/Admiral Artyom Revival/
```

The maintained stable runtime branch remains `runtime-artem-revival` as an established publication compatibility identifier.

That branch pins the validated r5 server DLL plus a runtime manifest identifying the accepted candidate and hashes. It is deliberately **not** a standalone full installation archive: the repaired authored core data remains tied to the archived upstream source set and the deterministic importer/repair/localization tooling in this module, while the approximately 1.5 GB Unity `Bundles/` payload remains external. The six supplied bundle archives together form one persistent `Bundles/` directory and do not need to be replaced by normal server-DLL updates unless the asset set changes explicitly.

For an installed validated r5 setup, retain the repaired core data and `Bundles/` directory and use the DLL pinned by `runtime-artem-revival`. Do not restore the legacy SPT 4.0 DLL.

## Preserved scope

- authored Artem trader/campaign identity;
- 23-quest dependency graph;
- 131 custom item entries;
- 64 clothing entries;
- quest rewards and unlocks unless a concrete defect required repair;
- unique Artem assets and gear;
- no automatic injection of Artem gear into PBS pools.

Economy rebalance and optional asset pruning remain separate future policy work. They were not mixed into the compatibility revival.

## Proven repairs

- migrated legacy SPT 4.0/.NET 9 lifecycle to SPT 4.1 `IModMetadata` + `OnLoadAsync` on .NET 10;
- repaired the broken `ARTT_3thumbnail.jpg` quest reference;
- restored the missing Sweden Patch trader offer;
- synchronized explicit quest `AssortmentUnlock` rewards with `QuestAssort.success` mappings;
- normalized quest locale naming to `en.json` / `ru.json`;
- added Russian localization for all 204 quest locale keys, 131 custom items and 64 clothing entries;
- repaired DevTac Ronin preset slot casing for SPT 4.1.3 armor deserialization;
- restored the missing OPENLAND HEXAGON side soft-armor slots required by its preset.

## Repository contract

`mods/Admiral-Artyom-Revival/` is independent from Item Intelligence, Quest Planner, Belt/Armband Inventory, Pause and Tactical HUD. Other modules may consume ordinary SPT quest/item state created by Artem, but Admiral Artyom Revival does not depend on their internals.

The archived full upstream WTT-Artem package remains the authoritative archaeology/content source. The large Bundles set remains external; deterministic import/repair/validation tooling in this module records the maintained compatibility transformations. `runtime-artem-revival` is publication/runtime state only and is never used as a development branch.

See `docs/revival-status.md`, `docs/repair-log.md`, `docs/campaign-audit.md`, `docs/content-audit.md` and `docs/economy-audit.md` for durable technical detail.
