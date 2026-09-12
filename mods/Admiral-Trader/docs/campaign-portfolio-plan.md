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

### Opening Ground Zero contract

The opening Admiral wave must send a fresh character to Ground Zero / Эпицентр, matching the natural early-game route created by the retained vanilla traders. Its objectives should be practical at level 1 and allow useful parallel progress with other opening quests. Ground Zero can be the primary map or part of a small truthful location pool; later Admiral progression must continue across the wider map rotation rather than making the whole opening act Ground-Zero-only.

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

## Persistent loadout rotation

Loadout assignments are a long-running campaign system rather than a single batch of mastery quests. The runtime contains 40 weapon quests in two continuous lanes. Preserving the 21 established Arsenal IDs and family sequences produces 22 close/support steps and 18 rifle/precision steps; the planning catalogue remains a balanced 20-by-20 coverage model. The exact SPT 4.1.5 pools, early-game gaps, exclusions, location bands and objective semantics are recorded in `docs/weapon-rotation-expansion-plan.md` and `manifests/weapon-rotation-expansion-plan.json`.

Only two weapon assignments should normally be offered at once. The 19 expansion records are woven through the retained 21 Arsenal records as two independent authored chains, so the player has a minimum choice of raid setup without receiving four parallel weapon branches. Completing either assignment exposes the next assignment in that chain; level alone never exposes the complete weapon pool.

The 43-quest stabilization pass applies that rule to the existing 21 Arsenal quests. Track A advances sidearms to shotguns, marksman/battle rifles and special weapons. Track B advances SMG/PDW to assault rifles and precision rifles. Each family still keeps its Qualification, Fieldwork and Munitions sequence, IDs, objectives and rewards. This changes five root prerequisites and reduces the Arsenal entry points from seven to two without removing content.

The first cycle introduces individual weapons or small closely related model pools with straightforward targets. A normal loadout assignment should allow a curated group of roughly 2-4 suitable locations. This preserves route choice while keeping the operation more specific than "any location." A single mandatory location is reserved for an authored story operation tied to a real place.

After broad weapon coverage is complete, weapon families return in later cycles with meaningfully different contexts, for example:

- a different location or enemy faction;
- close-range, distance, day, or night work;
- a suppressor, optic, light, magazine, or ammunition-class constraint;
- pairing with light, medium, or heavy protection;
- a one-raid survival condition only when the added risk is intentional.

The same weapon and the same objective must not simply be repeated. Later cycles should reuse learned equipment while changing the operational problem. Weapon tasks must also be scheduled against armour, rig, headset, helmet, and backpack assignments so that the two available choices do not demand mutually exclusive versions of the same slot.

The target catalog is broader than the original 49-template foundation. It includes practical starter and scavenged weapons, compact AK variants, civilian rifles, common pistols and shotguns, SMGs/PDWs, AUG and other service rifles, 9x39 weapons, battle rifles, precision rifles and late support weapons. Cosmetic variants share a model-family slot; mounted, test, event and signal weapons do not count as campaign variety.

## Story expansion

New original quests are allowed when the source mods do not contain a suitable reference. Additions should form several short authored stories rather than a flat collection of errands. Candidate story lanes include route security and access, field logistics, reconnaissance and signals, equipment evaluation, faction intelligence, and late-game command operations.

Admiral stories do not need direct prerequisites, shared objectives, or explicit intersections with native quests. They should coexist at sensible progression levels and meet the same quality standard as strong native quest lines: clear motivation, an interesting and legible objective, credible pacing, proportional rewards, and no filler grind. Locations and requirements should still leave enough flexibility that the player can combine work from several traders when the opportunity arises.

## Storefront growth

Four permanent LL1 offers are too sparse for the final trader. Admiral needs a useful finite core at every loyalty level and a second layer earned through quests.

The next storefront design must satisfy these bounds before offers are authored:

| Stage | Visible offer target | Composition |
| --- | ---: | --- |
| LL1 | 8-12 | common mission support and early field equipment |
| LL2 | 14-20 | improved logistics, signals, practical equipment alternatives |
| LL3 | 22-30 | specialist field gear and bounded ammunition access |
| LL4 plus campaign unlocks | 32-45 | complete Admiral specialty without becoming a general supermarket |

Stock remains finite and useful items should have purchase limits. Loyalty provides the dependable core; selected quests unlock distinctive equipment, ammunition, access, or prepared mission-support offers. Quest unlocks must correspond to what the player proved or recovered during that branch.

Admiral's identity is operational supply: navigation and access support, field marking and signals, reconnaissance equipment, bounded specialist ammunition, and selected prepared gear for campaign tasks. The final assortment must avoid duplicating another trader at a better price and must contain enough exclusive or differently constrained utility that the player has a reason to visit Admiral throughout progression.

Before expanding the storefront, build an exact effective-assort comparison after QuestManiac/Andrudis is disabled. The comparison set is SPT's native traders (including Ref), Admiral, Artem and Scorpion. Inventory that disappears with the retired Andrudis traders is a candidate source, but it is not imported automatically: retain only useful items absent from the remaining traders, or items whose Admiral-specific stock, price, loyalty tier or quest gate creates a distinct operational role. Preserve the finite-offer policy and reject duplicate commodity rows that merely inflate the grid.

The expanded shop must contain a bounded signature weapon rack. Each selected weapon or complete preset must provide a recognizable role and be unavailable as the same fixed purchase from the retained comparison set. Use native SPT items and the existing Admiral assort architecture; do not add an external weapon dependency. Signature weapons remain finite and are distributed across loyalty tiers and meaningful quest unlocks, so the shop is useful from the start without exposing its final inventory at level 1. A recolor, trivial attachment swap or cheaper copy of another trader's offer is not a signature weapon.

Ultimate Loot Editor is a separate optional research reference for Economy Admiral. It edits individual loose-loot spawn points and their item weights and persists map-specific JSON; Economy Admiral currently applies global map loot pressure. Any future compatibility work must establish deterministic ownership and ordering when both are installed. Ultimate Loot Editor is not a Trader dependency and its authored spawn-point data must not be absorbed into the storefront.

## Optional content candidates

WTT Armory, Icebreaker, RUAF / Black Division, and Pack 'n' Strap are recorded as later optional expansion candidates. They do not modify the stable campaign or current runtime scope until their installed content and exact IDs are inspected. WTT Armory may extend weapon rotations, rewards, and finite quest-gated stock; Icebreaker may supply optional map operations; RUAF / Black Division may supply rare optional combat targets without Admiral-owned spawn changes; Pack 'n' Strap may broaden equipment objectives, rewards, and unlocks after duplicate-storefront review.

All four integrations must degrade cleanly when absent and cannot gate the core graph. ORBIT and Extra Lives are explicitly excluded from Trader runtime integration. The detailed admission contract is in `docs/optional-content-candidates.md` and `manifests/optional-content-candidates.json`.
