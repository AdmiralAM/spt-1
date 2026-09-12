# Optional content candidates

These integrations are candidates for later Admiral milestones. They do not change the stable campaign, the current 43 quest records, rewards, graph, assortment, or runtime behavior.

The 2026-09-12 inspection of the active `C:\Games\SPT\SPT_Runtime\user\mods` installation found none of the candidate content mods. `WTT-ServerCommonLib` and `WTT-Artem Revival` are installed, but neither is WTT Armory. Runtime implementation therefore remains blocked on an exact installed-content inventory.

## Shared compatibility contract

- Admiral must load and remain fully playable when every candidate mod is absent.
- Detection must use exact runtime database identities: item template IDs, location IDs, quest IDs, and role IDs. Display names and folder-name guesses are not authority.
- No candidate quest may become a prerequisite of the core Admiral graph.
- Optional item pools must be added only when every referenced template exists. Missing templates remove the optional alternative; they must not suppress or break the native objective.
- Optional rewards and offers remain finite and use the same risk, duration, rarity, progression-stage, and duplicate-storefront checks as native content.
- Admiral does not copy another mod's DLL, custom item definitions, maps, spawn controller, or runtime engine.
- Removing an item mod after receiving its items retains the ordinary SPT mod-item removal risk. Admiral must not claim to migrate or recreate those foreign templates.

## WTT Armory

Candidate roles:

- additional models in later weapon-rotation stages;
- optional weapon-specific objectives with small concurrent choice;
- finite quest rewards or Admiral offers unlocked by the corresponding qualification.

Admission requires an installed-version inventory containing exact weapon, magazine, ammunition, and required-part template IDs. A complete preset must be validated as purchasable and usable against the exact SPT runtime. WTT Armory models may extend a pool, but no core quest may require one and no empty fallback objective may be published when the mod is absent.

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
