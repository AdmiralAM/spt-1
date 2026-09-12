# Optional content candidates

These integrations remain optional. M7 admits only verified WTT weapon templates as alternatives inside the 19 new Arsenal assignments; it adds no dependency, reward, offer, graph edge, or mandatory objective. Icebreaker and the other candidates remain reserved.

The 2026-09-12 inspection of the active `C:\Games\SPT` installation confirmed WTT Armory, WTT Content Backport and WTT CommonLib. The authoritative server metadata is Armory **2.0.5** (`com.wtt.armory`, SPT `~4.1.0`), Content Backport **2.0.1** (`com.wtt.contentbackport`, SPT `~4.1.1`) and CommonLib **3.0.6** (`com.wtt.commonlib`, SPT `~4.1.3`). The installed Armory server and client assemblies both report 2.0.5; the folder supplied as “3.0.0” therefore does not contain an Armory 3.0.0 runtime.

Static inventory validation found 833 unique Armory templates including 77 weapon roots, and 642 unique Backport templates including 14 weapon-shaped roots. There are no duplicate IDs between the two mods and no collisions with the SPT 4.1.5 item database. Representative exact weapon IDs are `6868d249cdee524f8c0ba45f` (Beretta 92FS), `68fd4feab87d77a5aaf6bf64` (CheyTac M200), `6920a431c8f2ed5000c540a0` (XM8), `6871284e9a353bb50606f3ed` (AS VAL MOD.4), `69f9ebbcaae020b0db02f65d` (QBZ-191), and `68aee763130c00663d08aea8` (TKPD). Exact admission remains per-template rather than folder-name based.

Complete combined server starts prove all three GUIDs pass SPT validation and Economy Admiral completes its final database analysis and Enforce transaction with WTT present. The admitted inventory is recorded in `manifests/optional-weapon-runtime.json`: 50 weapons map safely to native mechanical pools; four clone-based false classifications remain deferred.

## Shared compatibility contract

- Admiral must load and remain fully playable when every candidate mod is absent.
- Detection must use exact runtime database identities: item template IDs, location IDs, quest IDs, and role IDs. Display names and folder-name guesses are not authority.
- No candidate quest may become a prerequisite of the core Admiral graph.
- Optional item pools must be added only when every referenced template exists. Missing templates remove the optional alternative; they must not suppress or break the native objective.
- Optional rewards and offers remain finite and use the same risk, duration, rarity, progression-stage, and duplicate-storefront checks as native content.
- Admiral does not copy another mod's DLL, custom item definitions, maps, spawn controller, or runtime engine.
- Removing an item mod after receiving its items retains the ordinary SPT mod-item removal risk. Admiral must not claim to migrate or recreate those foreign templates.

## WTT Armory

Runtime role:

- additional models in later weapon-rotation stages;
- optional weapon-specific objectives with small concurrent choice;
- finite quest rewards or Admiral offers unlocked by the corresponding qualification.

Admission uses exact weapon template IDs and their verified native clone role. These templates extend matching kill-condition pools only; Admiral neither sells nor rewards an incomplete WTT preset in this milestone. No core quest requires one and no empty fallback objective is published when the mod is absent.

The native insertion points are authored in `manifests/weapon-rotation-expansion-plan.json`. A WTT model that behaves like an existing class joins the closest later pool after exact-ID validation. A categorically different model may receive an optional side assignment, but every affected quest retains a native weapon route and the two-active-assignment pacing limit.

## WTT Content Backport

Content Backport is a second optional source for weapon rotation and future rewards, operations and finite unlocks. Three verified rifle roots currently join matching native pools. No Backport ID enters a prerequisite.

Armory and Backport authored handbook and flea values remain the price authority for their items. Economy Admiral may apply its configured global trader-fiat, trader-sell, Flea and map-loot pressure after registration, but it must not rewrite foreign templates, authored barter composition, spawn tables, or individual custom-item values. Unknown acquisition channels remain explicitly unknown instead of being guessed.

## Icebreaker

Candidate role: optional operations on the additional map after its installation, location identity, access rules, extraction contract, and existing quest progression have been inspected.

Icebreaker quests must remain side operations. They cannot gate Admiral loyalty, the core campaign, native-map quests, or essential storefront access. Admiral does not provide or bypass access to the map unless that behavior belongs to an explicitly verified item or quest contract.

## RUAF / Black Division

Candidate role: rare combat targets for late optional operations.

Admiral does not alter their spawn rates, inject their controllers, or require a random encounter for core progression. Exact runtime role IDs and observed spawn availability are required before authoring. Any admitted objective must either remain optional or include a truthful, mechanically equivalent fallback that does not mislabel ordinary targets as the special faction.

## Pack 'n' Strap

Candidate roles:

- equipment requirements that broaden raid-loadout variation;
- proportional item rewards;
- finite unlocks where the item fills a distinct role in Admiral's shop.

Admission requires exact container template IDs, slot behavior, dimensions, capacity, restrictions, value, and the effective installed trader assortment. Items already sold appropriately elsewhere are not duplicated merely to fill Admiral's grid.

## Explicit exclusions

ORBIT and Extra Lives are not Trader integration candidates. Admiral must not detect them, reference their runtime IDs, reward their content, create quests for their mechanics, or declare either as a dependency.
