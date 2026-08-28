# B&A&HB #2 MOD SPT RC1 — one batched runtime gate

Target: SPT 4.1.3.

This is the single combined physical gate after automated source/build/package work. The user chooses when to run it. PASS only when every row below passes; on the first failure, stop and return that row number plus the smallest useful screenshot/log excerpt.

Current Emergency HeadBand contract for this candidate is intentionally **1x1 and medical-only** (`MED_KIT`, `STIMULATOR`, `MEDICAL`). Do not treat cigarettes/currency as expected contents yet; that is later product polish.

| # | Exact action | Expected PASS | Explicit FAIL | Minimal evidence |
| --- | --- | --- | --- | --- |
| 1 | Start SPT and load the existing profile to the main menu. | Server/profile load without B&A&HB exception, invalid-profile warning, `Ambiguous match found`, or insurance-target warning. | Any B&A&HB startup/profile exception or `HandleInsuredItemLostEventPatch` target failure. | `PASS 1` or first B&A&HB exception block. |
| 2 | Open **Items** once from the main menu and inspect character equipment without leaving/re-entering the tab. | Layout is correct on first entry. Dedicated `BELT/ПОЯС` is between Pockets and Backpack with no numeric `15`; compact `HEADBAND/ПОВЯЗКА НА ГОЛОВУ` is directly above Headwear and does not stretch the equipment panel. | Numeric `15/16`, HeadBand missing until tab re-entry, full helmet-sized HeadBand block, overlap, or panel distortion. | One equipment screenshot. |
| 3 | Drag the exact Magazine Belt and Emergency HeadBand across equipment targets, then equip each. | Belt accepts only its exact Belt item in slot15. Emergency HeadBand is accepted only by dedicated slot16; vanilla ArmBand and Headwear do not show as valid destinations. | Cross-slot acceptance, wrong green highlight, or equip failure. | `PASS 3` or screenshot of wrong highlight. |
| 4 | With dedicated slots empty, use normal Alt-pickup/auto-placement on exact Belt and HeadBand. | Belt routes to slot15 and HeadBand to slot16 without manual dragging. | Either item routes elsewhere or does not auto-place. | `PASS 4` or short failure description. |
| 5 | Open ArmBand, Belt and HeadBand containers and inspect borders against their cells. | ArmBand `1x2`, Belt `2x2`, HeadBand `1x1`; each window fits its grid tightly with ordinary EFT chrome and no filler/minimum-size padding. | Window visibly larger than cells, clipped, or wrong shape. | One screenshot only for any failing window. |
| 6 | Put valid contents into each wearable, close/reopen, then unequip/re-equip. | Contents stay attached to the correct wearable with no loss/duplication. | Lost/duplicated contents or wrong host. | `PASS 6` or first failing action. |
| 7 | Restart SPT with wearables equipped/filled and reopen Items. | Dedicated slots, equipped wearables, labels/layout and contents persist after restart. | Missing slot/item/content, profile invalidation, or layout regression. | `PASS 7` or first failure screenshot/log. |
| 8 | Exercise one normal supported loot/unload/reload interaction involving wearable contents. | Action completes without freeze/error and uses the intended reachable container behavior. | Freeze, exception, wrong host, or inaccessible valid content. | `PASS 8` or first failing action + narrow log excerpt. |
| 9 | Observe inventory/stash idle for ~30 seconds. | No recurring B&A&HB log spam, visible periodic hitch, or repeated background UI work. | Repeating B&A&HB activity/logs or periodic hitch attributable to the mod. | `PASS 9` or repeating log lines. |

A full raid/death/insurance traversal is not required for this remediation gate unless a startup/lifecycle error points there; those paths already have automated policies and the user previously deferred the raid-specific pass. Profile recovery is packaged under `SPT_Runtime/user/mods/B&A&HB #2 MOD SPT/profile-safety/`. Do not uninstall as part of the normal gate; if recovery is needed, back up the profile and follow the packaged README exactly.
