# Item Valuation MOD SPT

**Version:** 0.1.0  
**Runtime target:** SPT 4.1.3  
**Issue:** #198

A deliberately narrow server-only rework of AcidPhantasm Item Valuation focused only on inventory-cell background coloring.

## Scope

The module performs one operation during server `PostLoad`:

`item -> valuation rule -> tier -> TemplateItem.Properties.BackgroundColor`

There is no ItemView patch, client runtime loop, tooltip mutation, or name mutation.

## Economic value source

For non-ammunition items, the module uses the best realizable value:

`effective value = max(best eligible trader sell value, usable flea value)`

- Flea value comes from `TemplateTable.Prices` only when the item is valid for Ragfair.
- Trader value uses the best eligible trader buy price. Regular traders are checked first; Fence is fallback-only.
- Default weapon/equipment presets use the summed handbook value of preset children as the trader valuation basis, matching the established Item Valuation behavior.
- Handbook value is used only when neither usable flea nor trader value is available.

This deliberately avoids treating raw flea-table price as the sole definition of item worth.

Item Intelligence remains independent and is not a hard dependency. Both mods use the same underlying SPT price/trader data model, but Item Valuation performs its one startup classification without requesting or retaining the Item Intelligence snapshot.

## Category rules

| Item group | Tier input |
| --- | --- |
| Weapons | effective **total value** |
| Armor / armored rigs | effective **total value** |
| Keys | effective **total value** |
| Ordinary loot, barter items, consumables, weapon parts/mods and other normal items | effective **value per inventory slot** |
| Ammunition | **penetration power**, not monetary value |

The economic rules therefore answer “how valuable is this loot?”, while ammunition colors answer “how capable is this round against armor?”.

## Money tiers

The palette is intentionally dark and desaturated.

| Valuation | Background behavior |
| ---: | --- |
| < 10,000 | preserve original/default background; no valuation tint |
| 10,000–24,999 | muted light green `#526B3F` |
| 25,000–49,999 | muted green `#294F31` |
| 50,000–74,999 | muted navy `#253552` |
| 75,000–99,999 | muted violet `#4A3854` |
| 100,000–249,999 | muted red `#5A2C31` |
| >= 250,000 | muted gold `#5C4825` |

## Ammo penetration tiers

The penetration boundaries retain AcidPhantasm's established progression while using this mod's subdued six-color palette.

| Penetration | Background |
| ---: | --- |
| <= 15 | muted light green `#526B3F` |
| 16–26 | muted green `#294F31` |
| 27–35 | muted navy `#253552` |
| 36–44 | muted violet `#4A3854` |
| 45–54 | muted red `#5A2C31` |
| > 54 | muted gold `#5C4825` |

Thresholds and colors are configurable in `config/config.json`. The current installation already provides ColorConverterAPI for custom HEX background values; this mod itself remains server-only.

## Deliberately removed

The rework contains no damage/penetration text in names, short-name mutation, locale/description/tooltip price text, armor-class/plate coloring, flea-ban override color, live-flea refresh loop, timers, polling, repeated template scans, Harmony patches, ItemView hooks, `Update`, or `LateUpdate`.

It therefore does not compete with UI Fixes, CompatibilityHighlighter, Item Intelligence, Task Item Indicator, ArmorClassIcon, CaliberUnderName, Foldables, or FastSell for client UI hooks.

## Runtime cost

All work occurs once during server startup:

- walk the item-template dictionary once;
- ammo: classify penetration directly;
- non-ammo: resolve flea/trader/handbook effective value;
- divide by slot area only for ordinary loot;
- assign only `BackgroundColor`.

After `OnLoadAsync` returns there is no timer, subscription, retained per-frame cache, or frame-time work.

## Installation / migration

This module is intentionally incompatible with the original `com.acidphantasm.itemvaluation`. Remove the original Item Valuation server mod before installing the candidate under:

`SPT_Runtime/user/mods/Item Valuation MOD SPT/`

No file is installed under `BepInEx/plugins` by this module.

## Provenance

`acidphantasm/itemvaluation-csharp` and `acidphantasm/acidphantasm-itemvaluation` are behavioral references. This module is a new SPT 4.1.x C# implementation and does not carry forward their legacy UI/information feature code.

## Validation state

Source/static/build validation is automated by `.github/workflows/item-valuation-mod-spt-validate.yml`. Exact SPT 4.1.3 visual behavior remains a physical runtime gate.
