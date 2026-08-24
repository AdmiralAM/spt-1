# Artem Revival MOD SPT

Stable revival of **WTT-Artem** for **SPT 4.1.3**.

The module preserves Artem's trader, 23-quest campaign, unique equipment, clothing and authored progression while migrating the server runtime to the SPT 4.1/.NET 10 stack and repairing compatibility defects proven by static and runtime evidence.

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

## Runtime dependency

- SPT: `4.1.3`
- WTT-ServerCommonLib: `3.0.6` validated runtime/build baseline
- WTT-ClientCommonLib: `3.0.6` used by the validated client environment

Runtime metadata intentionally accepts the compatible CommonLib `3.0.x` line.

## Installation model

Artem is a server mod installed under:

```text
SPT_Runtime/user/mods/WTT-Artem Revival/
```

The maintained runtime channel is `runtime-artem-revival`.

The runtime channel contains the validated Artem core overlay. The large authored Unity `Bundles/` payload remains external because it is approximately 1.5 GB and is not appropriate for normal Git source history. The six supplied bundle archives together form one persistent `Bundles/` directory and do not need to be replaced by normal core updates unless the asset set changes explicitly.

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

`mods/WTT-Artem-Revival/` is independent from Item Intelligence, Quest Planner, Belt/Armband Inventory, Pause and Tactical HUD. Other modules may consume ordinary SPT quest/item state created by Artem, but Artem does not depend on their internals.

The archived full Artem package remains the authoritative archaeology/content source. The large Bundles set remains external; deterministic import/repair/validation tooling in this module records the maintained compatibility transformations.

See `docs/revival-status.md`, `docs/repair-log.md`, `docs/campaign-audit.md`, `docs/content-audit.md` and `docs/economy-audit.md` for durable technical detail.
