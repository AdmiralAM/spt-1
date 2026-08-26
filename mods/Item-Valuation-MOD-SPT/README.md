# Item Valuation MOD SPT

**Version:** 0.1.0  
**Runtime target:** SPT 4.1.3  
**Issue:** #198

A deliberately narrow server-only rework of the useful background-color behavior from acidphantasm's Item Valuation, using both the current C# implementation and the earlier server mod as behavioral references.

## Scope

The module performs one operation:

`item monetary value -> category valuation rule -> color tier -> TemplateItem.Properties.BackgroundColor`

It runs once during server `PostLoad`. There is no client plugin and no runtime UI code.

## Price source

1. `TemplateTable.Prices` is the primary source. SPT already loads this item-price table, so the module performs a constant-time dictionary lookup instead of calculating trader/flea prices itself.
2. `TemplateTable.Handbook.Items[].Price` is indexed once and used only when the primary table has no positive price for a template.
3. Items without a positive price or valid positive `Width`/`Height` are left untouched.

Item Intelligence is not a hard dependency. Its richer price snapshot remains independent; coupling Item Valuation to that request-time service would add dependency and work without improving this one-time startup classification.

## Valuation semantics

The background always represents monetary value; the old penetration/armor-class semantics are removed.

| Item group | Value used for tiering |
| --- | --- |
| Weapons | total item template value |
| Armor and armored rigs | total item template value |
| Keys | total item template value |
| Ordinary loot, barter items, consumables, weapon parts/mods and other normal items | value per inventory slot (`price / Width / Height`) |
| Ammo | monetary value only; because ammunition templates are normally 1x1, this effectively remains the monetary template value rather than penetration |

This avoids penalizing physically large weapons/armor while retaining the useful value-density signal for normal loot.

## Default tiers

The palette is intentionally dark and desaturated so inventory cells remain readable and do not become bright UI blocks.

| Valuation | Background behavior |
| ---: | --- |
| < 10,000 | preserve original/default background; no valuation tint |
| 10,000–24,999 | muted light green `#526B3F` |
| 25,000–49,999 | muted green `#294F31` |
| 50,000–74,999 | muted navy `#253552` |
| 75,000–99,999 | muted violet `#4A3854` |
| 100,000–249,999 | muted red `#5A2C31` |
| >= 250,000 | muted gold `#5C4825` |

Thresholds and colors are configurable in `config/config.json`. The current installation already provides ColorConverterAPI for custom HEX background values; this mod itself remains server-only and does not ship or patch a client color converter.

## Deliberately removed

The rework contains no damage/penetration display, name/short-name/locale mutation, tooltip price text, penetration-based ammo coloring, armor-class/plate coloring, flea-ban coloring, trader-price calculation, preset calculation, live-flea integration, timers, polling, repeated template scans, Harmony patches, `ItemView` patches/scans, `Update`, or `LateUpdate`.

This means it does not compete with UI Fixes, CompatibilityHighlighter, Item Intelligence, Task Item Indicator, ArmorClassIcon, CaliberUnderName, Foldables, or FastSell for client UI hooks. Those mods retain ownership of their overlays and ItemView behavior; this module only changes the server template background color field already consumed by EFT.

## Runtime cost

The only work is at server load:

- create a temporary handbook price dictionary;
- walk the template dictionary once;
- do at most two price lookups and one lightweight category test per eligible template;
- divide by slot area only for the ordinary/per-slot group;
- classify one tier and assign `BackgroundColor` only when valuation is at least 10,000.

After `OnLoadAsync` returns there is no timer, event subscription, client component, retained valuation cache, or frame-time work.

## Installation / migration

This module is intentionally incompatible with the original `com.acidphantasm.itemvaluation`. Remove the original Item Valuation server mod before installing the candidate. The new module belongs under:

`SPT_Runtime/user/mods/Item Valuation MOD SPT/`

No file is installed under `BepInEx/plugins` by this module.

## Provenance

`acidphantasm/itemvaluation-csharp` and `acidphantasm/acidphantasm-itemvaluation` are behavioral references. This module is a new SPT 4.1.x C# implementation and does not carry forward their legacy feature code.

## Validation state

Source/static/build validation is automated by `.github/workflows/item-valuation-mod-spt-validate.yml`. Exact SPT 4.1.3 visual behavior remains a physical runtime gate: successful CI is not by itself runtime acceptance.
