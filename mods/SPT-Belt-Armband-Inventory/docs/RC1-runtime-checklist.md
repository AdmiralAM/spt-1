# B&A&HB #2 MOD SPT RC1 — one batched runtime gate

Target: SPT 4.1.3. This checklist is packaged with RC1 for controller activation only; its presence does not activate the queued user gate.

PASS only when every row passes. On the first failure, stop and return that row number plus the smallest useful screenshot/log excerpt.

| # | Exact action | Expected PASS | Explicit FAIL | Minimal evidence |
| --- | --- | --- | --- | --- |
| 1 | Start SPT and load the existing profile to the main menu. | Server and profile load without B&A&HB exception or invalid-profile warning. | Startup/profile load fails or B&A&HB causes an exception. | `PASS 1` or first B&A&HB exception block. |
| 2 | Open Ragman LL1 and verify the four B&A&HB wearable offers. | All four authored wearable items are present and purchasable. | Any authored offer is absent/unpurchasable. | `PASS 2` or one screenshot. |
| 3 | Open character equipment. | Existing ArmBand remains usable; dedicated `BELT` is between Pockets and Backpack; dedicated `HEADBAND` is above Headwear; no numeric `15/16` captions. | Missing/misplaced slot, numeric caption, or HeadBand overlaps/replaces Headwear. | One equipment screenshot. |
| 4 | Drag the exact Magazine Belt and Emergency HeadBand across equipment targets, then equip each. | Belt accepts only its exact Belt item in slot 15; HeadBand accepts only its exact HeadBand item in slot 16; vanilla Headwear is not shown as a valid HeadBand destination. | Cross-slot acceptance, wrong-slot green compatibility, or equip failure. | `PASS 4` or screenshot of wrong highlight. |
| 5 | With the dedicated slots empty, use the normal Alt-pickup/auto-placement action on the exact Belt and HeadBand items. | Belt routes to slot 15 and HeadBand routes to slot 16 without manual dragging. | Either item routes elsewhere or no valid auto-placement occurs. | `PASS 5` or short failure description. |
| 6 | Open each wearable container and inspect its border against the cells. | ArmBand 1x2, Belt 2x2 and HeadBand 1x1 windows fit their declared cells tightly with ordinary EFT chrome and no filler/minimum-size padding. | Window is visibly larger than its cells, clipped, or wrong grid shape. | One screenshot only if any window fails. |
| 7 | Put valid contents into the wearables, close/reopen them, remove/re-equip them, and exercise normal loot/unload interaction for their supported item types. | Contents remain attached to the correct wearable and supported loot/unload actions complete without freeze/error. | Lost/duplicated contents, wrong host, unload/loot failure, freeze, or exception. | `PASS 7` or first failing action + narrow log excerpt. |
| 8 | Restart SPT with the wearables equipped/filled, then complete one raid transition and return to stash. | Slots, equipped wearables and contents persist across restart and raid transition. | Missing item/slot/content, invalid profile, or persistence drift. | `PASS 8` or first persistence failure. |
| 9 | Observe inventory/stash idle for about 30 seconds after the checks. | No recurring B&A&HB log spam, visible periodic hitch, or repeated background UI work. | Repeating B&A&HB activity/logs or periodic hitch attributable to the mod. | `PASS 9` or repeating log lines. |

Profile recovery is packaged under `SPT_Runtime/user/mods/B&A&HB #2 MOD SPT/profile-safety/`. Do not uninstall the mod as part of the normal RC gate; if disable/uninstall recovery is specifically activated by the controller, back up the profile first and follow that packaged README exactly.
