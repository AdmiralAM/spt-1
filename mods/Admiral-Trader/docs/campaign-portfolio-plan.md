# Campaign portfolio plan

This document sizes Admiral Trader against the complete intended SPT campaign instead of judging its quest count in isolation.

## Measured installed portfolio

The SPT 4.1.5 database contains 558 quest records. The installed sources add 122 Scorpion quests, 23 Artem Revival quests, and 43 Admiral quests. The 15 Ref quests are already part of the 558 SPT records. Ref Friendly Quests modifies six of those Ref records and adds no quest IDs.

| Source | Quest records | Portfolio treatment |
| --- | ---: | --- |
| SPT 4.1.5, including Ref | 558 | Keep |
| Scorpion 1.1.1 | 122 | Keep as an independent campaign |
| Artem Revival 3.0.0 | 23 | Keep as an independent campaign |
| Admiral Trader current runtime | 43 | Keep and continue curating |
| Ref Friendly Quests 2.1.0 | 0 added / 6 modified | Keep as a compatibility patch |
| **Intended total without QuestManiac runtime** | **746** | Target portfolio before future Admiral additions |

The physical 2026-09-07 server run also loaded Andrudis QuestManiac. Economy Admiral measured 5,362 final quest records from a pristine 558-record baseline: 4,804 mod-added records. With the current 12 additional Admiral Operations, the comparable installed total becomes approximately 5,374 records. QuestManiac must therefore be removed from the final runtime after selected content has been absorbed; leaving it enabled defeats campaign curation and can duplicate inherited quest IDs.

## Player-facing load target

Total record count does not determine journal quality. The controlling measure is the number and composition of quests offered at the same time.

Admiral should normally expose 6-10 offered or active quests at once:

- 2-4 location operations that can be paired with native, Scorpion, Artem, or Ref work;
- 2-3 equipment or weapon rotation assignments;
- 1-2 acquisition, handover, reconnaissance, or access tasks;
- no more than one high-risk one-raid operation in the same progression band.

Completing a quest should replace it with one or two authored successors. Level gates alone must not release a large set of unrelated root quests. The observed level-35 state of one completed quest plus 17 newly offered quests exceeds this target and is a graph-staging defect to correct during campaign expansion.

## Map coverage target

Every ordinary progression band should give the player a useful reason to visit several locations, while avoiding a demand to visit every location at once.

| Progression band | Primary locations | Expected Admiral activity |
| --- | --- | --- |
| Levels 1-9 | Ground Zero, Customs, Factory, Woods | access basics, common gear, early weapons, survival |
| Levels 10-19 | Customs, Woods, Shoreline, Interchange, Factory | route work, light protection, rigs, bags, headsets |
| Levels 20-29 | Reserve, Lighthouse, Streets, Shoreline, Interchange | faction contacts, medium protection, specialist weapons |
| Levels 30-39 | Reserve, Lighthouse, Streets, The Lab | heavy equipment, precision work, Raiders and Rogues |
| Level 40+ | The Lab and rotating endgame locations | short capstone branches and mastery tasks |

Factory night and Ground Zero high-level variants may be used where SPT exposes stable location contracts. Labs tasks must remain late and must not become the only endgame activity.

## Loadout variation target

Weapon quests remain a staged rotation across weapon families and specific models. New campaign work should also rotate the rest of the raid loadout:

- body armour: light, medium, heavy, armoured rigs;
- tactical rigs: low-capacity scav gear, standard rigs, specialist rigs;
- headwear: soft headwear, common helmets, heavy helmets;
- hearing: entry, mid-tier, and specialist headsets;
- backpacks: compact, standard, and heavy-haul classes;
- weapons: model pools appropriate to the progression band, with no complete-family pool repeated at every stage;
- supporting constraints: suppressors, optics, lights, magazines, ammunition class, distance, time of day, faction, and survival only where they serve the operation.

Equipment constraints should normally use a small truthful pool rather than one exact rare item. A constrained loadout quest should share a useful destination with other available work so the player can plan one coherent raid instead of making a disposable single-purpose run.

## Expansion rule

The 43-quest runtime is the current validated foundation, not the desired final volume. Future additions should be accepted only when they fill a measured map, level, objective, story, or equipment-rotation gap. The initial planning range is 60-80 Admiral quests, subject to a simulation of concurrent availability across the complete 746-record portfolio. The final count is secondary to keeping the Admiral layer near 6-10 concurrent tasks and avoiding duplicate objectives already supplied by SPT, Scorpion, Artem, or Ref.

Runtime graph and rewards remain unchanged by this planning document.
