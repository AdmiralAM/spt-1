# Weapon rotation expansion plan

The current 21 Arsenal quests are a validated foundation, not the final weapon campaign. Their 49-template selection proves the lifecycle and unlock structure, but it omits too much practical early and middle-game equipment and moves several familiar weapons too late.

The next authored runtime milestone expands Arsenal to 40 assignments in two sequential lanes of 20. Only the next assignment in each lane may be offered, so the player normally has two weapon choices rather than a journal full of parallel kill tasks. The 21 persistent quest IDs remain in use; 19 new IDs will be authored when this plan is materialized.

## Coverage

The early bands deliberately include weapons a fresh profile can own, buy, find or encounter during ordinary trader work: service pistols; compact AKs; starter service and civilian rifles; SKS variants; common pump and self-loading shotguns; early Russian SMGs; MP5/MP5K; PP-19; and UMP.

AUG, MP7, pistols and short AKs are examples of the correction, not its whole scope. The plan also covers classic AK variants, NATO rifles, bullpups, 9x39 weapons, battle rifles, DMRs, bolt actions, magnum rifles, machine guns, unusual shotguns and controlled grenade-launcher work. Cosmetic colour variants share a family slot. Mounted guns, test objects, event flare launchers and signal pistols are explicitly excluded instead of being counted as campaign variety.

## Pacing and repetition

Lane A moves through sidearms, compact weapons, shotguns, SMGs/PDWs and support weapons. Lane B moves through starter rifles, civilian and service rifles, carbines, battle rifles and precision weapons. Both begin on Ground Zero and nearby early maps, then widen naturally across the campaign.

Weapons are not permanently tied to one location. A normal assignment permits two to four locations selected for its progression band. A single-map weapon task is allowed only when a real story operation depends on that location.

Later reuse changes the problem through faction, distance, day/night, suppressor, optic, survival or deliberate one-raid mastery. The same model pool with the same generic kill objective cannot be repeated. Kill counts and rewards will be normalized during materialization against level, access cost, ammunition rarity, target type and survival risk.

## Runtime boundary

`manifests/weapon-rotation-expansion-plan.json` contains the exact SPT 4.1.5 template pools, both 20-step lanes, level bands, location pools, objective semantics and explicit exclusions. It is bound to the verified SPT 4.1.5 item-database hash.

This step does not alter the current 43 runtime quests, rewards, prerequisites or assortment. Runtime materialization is one coherent later change: allocate the retained 21 quest IDs into the new sequence, author 19 new records, update descriptions and rewards, simulate concurrent availability, build the DLL and artifact, and run one full validation cycle.

## WTT Armory

WTT Armory remains optional. After installation, exact weapon, magazine, ammunition and required-part templates may extend the closest mechanical pool. A categorically different weapon may receive an optional side assignment. Every core assignment retains a native SPT route, and removing WTT Armory cannot leave an empty objective or break the graph.
