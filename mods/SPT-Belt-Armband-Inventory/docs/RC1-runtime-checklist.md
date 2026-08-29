# B&A&HB #2 MOD SPT — post-stable product gate

Target: SPT 4.1.3.

**Stable Baseline 1** is exact head `d6336f290361b16c4aa54f9d7dddfe0e8f7f9bbf`, preserved on branch `belt-stable-baseline-1`. That runtime already passed the full Items / insurance / PMC lifecycle acceptance. The current development line keeps that mechanical architecture and advances the final product roster.

Do not use the user as a per-patch debugger. Scav compatibility, profile recovery, hot-path safety and the previously proven death/insurance boundaries remain CI-owned unless a concrete runtime regression appears.

## Current product roster

| Product | Host | Capacity / filter | Progression |
| --- | --- | --- | --- |
| Wrist Wallet | ArmBand | `1x1`, currency-only | Ragman LL1 — 12,500 RUB |
| Magazine Armband | ArmBand | `1x2`, MAGAZINE-only | Ragman LL1 — 25,000 RUB |
| Utility HeadBand | slot16 | `1x2`, exact currency/cigarette/wallet whitelist | Ragman LL1 — 25,000 RUB |
| Magazine Belt | slot15 | `2x2`, MAGAZINE-only | Ragman LL2 — 45,000 RUB |

Utility HeadBand wallet coverage includes both Simple Wallet (`5783c43d2459774bbe137486`) and WZ Wallet (`60b0f6c058e0b0481a09ad11`). It remains exact-item restricted rather than a broad container class.

## One combined user gate

| # | Exact action | Expected PASS | FAIL evidence |
| --- | --- | --- | --- |
| 1 | Start SPT and open **Character → Items** once, without tab-switch refresh. | Stable Baseline 1 presentation is unchanged: character panel visible immediately, Belt/HeadBand present, no duplicate/missing slot16, no UI displacement. | One screenshot + B&A&HB client lines only if wrong. |
| 2 | Open Ragman and inspect the B&A&HB products available for the profile's current LL. | Product names are `Wrist Wallet`, `Magazine Armband`, `Utility HeadBand`, `Magazine Belt`; contracts are 12.5k/LL1, 25k/LL1, 25k/LL1 and 45k/LL2 respectively. No user-visible `Runtime Candidate Magazine Belt` remains. | Screenshot or exact wrong offer/name. |
| 3 | Open the four wearable containers and check only their product-critical filters. | Wrist Wallet accepts currency; Magazine Armband and Magazine Belt accept magazines; Utility HeadBand accepts RUB/USD/EUR, the intended cigarette set, Simple Wallet and WZ Wallet, while rejecting ordinary unrelated loot/medical items. Geometry stays `1x1`, `1x2`, `1x2`, `2x2` respectively. | Name of the specific accepted/rejected item + screenshot only if UI geometry is wrong. |
| 4 | Proceed through the normal PMC pre-raid flow into one raid, open inventory once in raid, then return normally. | No Belt exception, no frozen inventory/pre-raid UI, no missing/duplicate wearable tree, no manual refresh requirement. | Client/server B&A&HB/error excerpt only on failure. |

If these four rows pass, the product candidate is accepted without repeating the already-proven death/insurance matrix.

## Regression-only retest boundaries

Repeat protection/death/insurance tests **only** if a later change touches `WearableProtectionRuntime`, `BeltDeathPolicy`, the death/insurance SPT patches, persistent template/slot identities, or runtime evidence shows a related failure. Product naming, exact whitelist additions and trader progression do not by themselves reopen that physical gate.

## Deferred final HeadBand design

The future visual redesign remains separate from this product pass: reduce the Face window roughly by half and place a compact HeadBand above/adjacent using the ArmBand + Dogtag visual principle. A possible two-independent-`1x1` HeadBand layout (one cigarettes-only, one currency/wallet-only) remains a final feasibility investigation after the product roster is stable.

## Automated-only boundaries

Scav `ReplaceInventory` compatibility is not a user test. Profile recovery is not a normal runtime test. CI owns both, together with product-contract, lifecycle/hot-path, client/server build and packaging checks.
