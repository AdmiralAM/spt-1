# Item Valuation MOD SPT

**Version:** 1.0.0  
**Runtime target:** SPT 4.1.3  
**Issue:** #198 (completed)  
**Implementation PR:** #199 (merged)

A deliberately narrow server-only rework of AcidPhantasm Item Valuation focused only on inventory-cell background coloring.

## Scope

The module performs one operation during server `PostLoad`:

`item -> category valuation rule -> tier -> TemplateItem.Properties.BackgroundColor`

There is no ItemView patch, client runtime loop, tooltip mutation, or name mutation.

## Category rules

| Item group | Tier input |
| --- | --- |
| Weapons | best realizable **total economic value** |
| Armor / armored rigs | best realizable **total economic value** |
| Keys | restored original AcidPhantasm **key-specific price logic** |
| Ordinary loot, barter items, consumables, weapon parts/mods and other normal items | best realizable **value per inventory slot** |
| Ammunition | **penetration power**, not monetary value |

## Economic value source for normal non-key items

`effective value = max(best eligible trader sell value, usable flea value)`

- Flea value comes from `TemplateTable.Prices` only when the item is valid for Ragfair.
- Trader value uses the best eligible trader buy price. Regular traders are checked first; Fence is fallback-only.
- Default weapon/equipment presets use the summed handbook value of preset children as the trader valuation basis.
- Handbook value is fallback when neither usable flea nor trader value exists.

Item Intelligence remains independent and is not a hard dependency.

## Keys — restored original logic

Keys deliberately bypass the general `max(trader, flea)` valuation path. Matching AcidPhantasm's original behavior:

1. use the full `TemplateTable.Prices` value when present;
2. otherwise use handbook price;
3. classify by the original dedicated key thresholds;
4. if the key is not a valid flea item, apply the original flea-banned key override color.

| Key price | Background |
| ---: | --- |
| < 10,000 | `#404040` |
| 10,000–19,999 | `#a3a3a3` |
| 20,000–29,999 | `#0c3b08` |
| 30,000–49,999 | `#08083b` |
| 50,000–74,999 | `#590b5e` |
| >= 75,000 | `#5e470b` |
| flea-banned key | `#660415` override |

Only the background-color behavior is restored; key descriptions/tooltips remain untouched.

## Money tiers for other economic items

The general palette remains intentionally dark and desaturated.

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

| Penetration | Background behavior |
| ---: | --- |
| < 10 | preserve original/default background |
| 10–20 | muted light green `#526B3F` |
| >20–40 | muted navy `#253552` |
| >40–50 | muted violet `#4A3854` |
| >50–70 | muted red `#5A2C31` |
| >70 | muted gold `#5C4825` |

There is no green ammo tier.

Thresholds and colors are configurable in `config/config.json`. The current installation provides ColorConverterAPI for custom HEX background values; this mod itself remains server-only.

## Deliberately removed

The rework contains no damage/penetration text in names, short-name mutation, locale/description/tooltip price text, armor-class/plate coloring, generic flea-ban override color, live-flea refresh loop, timers, polling, repeated template scans, Harmony patches, ItemView hooks, `Update`, or `LateUpdate`.

The only restored flea-ban override is the original key-specific one described above.

## Runtime cost

All work occurs once during server startup:

- walk the item-template dictionary once;
- ammo: classify penetration directly;
- keys: price-table/handbook lookup plus key-specific classification;
- other non-ammo: resolve flea/trader/handbook effective value;
- divide by slot area only for ordinary loot;
- assign only `BackgroundColor`.

After `OnLoadAsync` returns there is no timer, subscription, retained per-frame cache, or frame-time work.

## Installation / migration

This module is intentionally incompatible with the original `com.acidphantasm.itemvaluation`. Remove the original Item Valuation server mod before installing release 1.0.0 under:

`SPT_Runtime/user/mods/Item Valuation MOD SPT/`

No file is installed under `BepInEx/plugins` by this module.

## Provenance

`acidphantasm/itemvaluation-csharp` and `acidphantasm/acidphantasm-itemvaluation` are behavioral references. This module is a new SPT 4.1.x C# implementation and does not carry forward their legacy UI/information feature code.

## Validation state

Release 1.0.0 is accepted for SPT 4.1.3. Source/static/build validation is automated by `.github/workflows/item-valuation-mod-spt-validate.yml`, and runtime validation was completed against the current mod stack before development closure.
