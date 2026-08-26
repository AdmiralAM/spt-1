# Item Valuation MOD SPT

**Version:** 0.1.0  
**Runtime target:** SPT 4.1.3  
**Issue:** #198

A deliberately narrow server-only rework of the useful background-color behavior from acidphantasm's Item Valuation 2.1.0.

## Scope

The module performs one operation:

`item value -> value per inventory slot -> color tier -> TemplateItem.Properties.BackgroundColor`

It runs once during server `PostLoad`. There is no client plugin and no runtime UI code.

## Price source

1. `TemplateTable.Prices` is the primary source. SPT already loads this item-price table, so the module performs a constant-time dictionary lookup instead of calculating trader/flea prices itself.
2. `TemplateTable.Handbook.Items[].Price` is indexed once and used only when the primary table has no positive price for a template.
3. Items without a positive price or valid positive `Width`/`Height` are left untouched.

The value is divided by `Width * Height` and rounded once, matching the original normal-item price-per-slot concept.

## Default tiers

The default thresholds and colors preserve the original normal-item Item Valuation 2.1.0 scale:

| Value per slot | Background color |
| ---: | --- |
| <= 5,000 | `#404040` |
| <= 10,000 | `#a3a3a3` |
| <= 15,000 | `#0c3b08` |
| <= 25,000 | `#08083b` |
| <= 35,000 | `#590b5e` |
| > 35,000 | `#5e470b` |

Only these thresholds/colors remain configurable in `config/config.json`.

## Deliberately removed

The rework contains no damage/penetration display, name/short-name/locale mutation, tooltip features, ammo/weapon/armor/key special cases, flea-ban coloring, trader-price calculation, preset calculation, live-flea integration, ColorConverterAPI integration, timers, polling, repeated template scans, Harmony patches, `ItemView` patches/scans, `Update`, or `LateUpdate`.

This also means it does not compete with UI Fixes, CompatibilityHighlighter, Item Intelligence, Task Item Indicator, ArmorClassIcon, CaliberUnderName, Foldables, or FastSell for client UI hooks. Those mods retain ownership of their overlays and ItemView behavior; this module only changes the server template background color field already consumed by EFT.

## Runtime cost

The only work is at server load:

- create a temporary handbook price dictionary;
- walk the template dictionary once;
- do at most two price dictionary lookups, one integer slot-area calculation, one tier classification, and one `BackgroundColor` assignment per eligible template.

After `OnLoadAsync` returns there is no timer, event subscription, client component, cache retained by the module, or frame-time work.

## Installation / migration

This module is intentionally incompatible with the original `com.acidphantasm.itemvaluation`. Remove the original Item Valuation 2.1.0 server mod before installing the candidate. The new module belongs under:

`SPT_Runtime/user/mods/Item Valuation MOD SPT/`

No file is installed under `BepInEx/plugins`.

## Provenance

acidphantasm Item Valuation 2.1.0 is used as a behavioral/configuration reference. This module is a new SPT 4.1.x C# implementation and does not carry forward the upstream legacy feature code.

## Validation state

Source/static/build validation is automated by `.github/workflows/item-valuation-mod-spt-validate.yml`. Exact SPT 4.1.3 UI behavior remains a physical runtime gate: successful CI is not by itself runtime acceptance.
