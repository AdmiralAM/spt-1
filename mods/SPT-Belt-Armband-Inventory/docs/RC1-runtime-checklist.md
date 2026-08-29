# B&A&HB #2 MOD SPT — v0.2 runtime gate

Target: **SPT 4.1.3**.

Stable **v0.1.0** is already accepted and published. This checklist covers only the new v0.2 scope in Issue #285 / PR #286 plus the bundled reload extension tracked by Issue #287. Do not reopen the full v0.1.0 death/insurance matrix unless a concrete regression appears.

## Candidate contract

| Product | Host | Capacity / filter | Progression |
| --- | --- | --- | --- |
| Wrist Wallet | ArmBand | `1x1`, currency-only | Ragman LL1 — 12,500 RUB |
| Magazine Armband | ArmBand | `1x2`, MAGAZINE-only | Ragman LL1 — 25,000 RUB |
| Utility HeadBand | slot16 | two native `1x1` cells: currency/wallet + cigarettes | Ragman LL1 — 25,000 RUB |
| Magazine Belt | slot15 | `2x2`, MAGAZINE-only | Ragman LL2 — 45,000 RUB |

Utility HeadBand exact filters:
- `main`: RUB, USD, EUR, Simple Wallet, WZ Wallet;
- `cigarettes`: Apollo Soyuz, Malboro, Wilston, Strike.

Reload contract:
- ordinary EFT reachable magazine locations remain first/default;
- Magazine Armband and Magazine Belt may supply a compatible magazine only as additional reachable fallback sources;
- Wrist Wallet and Utility HeadBand are not reload sources;
- no broad inventory scan, polling or UI/scene scan is part of the reload path.

The v0.2 compact presentation must remain inside the original FaceCover footprint and must not resize/translate Gear Panel or move unrelated native slots.

## One combined physical gate

1. **First opening — Character → Items.** Open Items once after launch without tab-switch refresh. PASS: HeadBand is visible immediately; FaceCover is compact; HeadBand is directly above it inside the same old FaceCover area; no unrelated equipment slot moved; no duplicate/missing slot16.

2. **HeadBand internal cells.** Open Utility HeadBand. PASS: exactly two usable `1x1` cells are visible; currency/wallet items fit only the money cell; the intended cigarettes fit only the cigarette cell; unrelated medical/barter/container items are rejected.

3. **Existing-profile migration.** If the profile already contained a v0.1.0 Utility HeadBand, inspect it after first launch. PASS: one currency/wallet item remains in `main`, one cigarette item is in the cigarette cell, no valid item is silently deleted. Any same-category overflow should be preserved in the PMC sorting table.

4. **Product roster and localization.** Check Ragman when the relevant LL is available. PASS: Wrist Wallet 12.5k/LL1, Magazine Armband 25k/LL1, Utility HeadBand 25k/LL1, Magazine Belt 45k/LL2; no `Runtime Candidate` user-visible name remains; with Russian EFT locale the four B&A&HB product names/descriptions use the bundled Russian localization rather than falling back to technical English names.

5. **Reload — vanilla source remains first.** Equip a weapon with a compatible magazine, keep at least one compatible spare in an ordinary vanilla reachable location (pockets/rig as normally supported) and another compatible spare in Magazine Armband or Magazine Belt. Trigger normal reload. PASS: the ordinary vanilla reachable magazine is selected before the wearable fallback; B&A&HB does not become the preferred/default source.

6. **Reload — wearable fallback.** Leave no compatible spare magazine in ordinary reachable reload locations, but keep a compatible spare in Magazine Armband or Magazine Belt. Trigger normal reload. PASS: reload can use the compatible magazine from the B&A&HB magazine wearable. Control: a magazine nested under Wrist Wallet/Utility HeadBand must not gain reload reachability from B&A&HB.

7. **One normal PMC lifecycle.** Proceed through pre-raid, enter one PMC raid, open inventory once, perform at least one ordinary reload, return normally and open Items once again. PASS: no freeze/exception, no manual refresh, no missing/duplicate wearable, compact Face/HeadBand layout and wearable contents remain intact after the lifecycle.

Return evidence is only: `1 PASS / 2 PASS / ... / 7 PASS` plus one screenshot for a visual FAIL or focused B&A&HB/error log excerpt for a runtime FAIL.

## CI-owned boundaries

No user microtests are required for Scav replacement, profile cleanup tooling, hot-path safety, persistent-ID collision checks, reload implementation internals or the previously accepted death/insurance matrix. CI owns those boundaries unless runtime evidence specifically points back to them.
