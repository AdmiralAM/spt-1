# Admiral Artyom Revival — Campaign Audit

Static campaign audit of the repaired authoritative upstream WTT-Artem core maintained by Admiral Artyom Revival for SPT 4.1.4.

## Graph and coverage

- quests: 23
- prerequisite edges: 22
- prerequisite cycles: 0
- quest types: 8 PickUp, 7 Elimination, 5 Exploration, 3 Discover
- explicit Success `AssortmentUnlock` rewards: 40

The authored quest graph is structurally recoverable and is preserved.

## Condition coverage

The campaign uses handovers, kill counters, visit-place counters, map/location constraints, beacon placement, leave-item-at-location conditions, one level gate and one trader-loyalty gate. Custom zone definitions are present for all Artem-owned zone IDs referenced by the campaign. Several other zone IDs (`pr_scout_base`, `bomj_place`, `peace_027_area`) are external/vanilla references and therefore require SPT 4.1.4 runtime validation rather than being synthesized inside the module.

## Reward/QuestAssort consistency

A deeper audit found that existence-only validation was insufficient: three explicit `AssortmentUnlock` rewards did not have corresponding `QuestAssort.success` mappings.

Repaired additively from the authored quest reward declarations:

- `Expanding Wardrobe` → Sweden Patch offer `675267324707588d57c75972`
- `Puppets` → offer `66bf757f27d0b097db0acf02`
- `Gathering Information - Part 3` → offer `66bf757f27d0b097db0acf5d`

Existing QuestAssort-only mappings are preserved. The importer does not delete them because they may represent intentional hidden unlocks.

Post-repair invariant:

- every explicit Success `AssortmentUnlock` target exists as a root trader offer;
- every explicit Success `AssortmentUnlock` target maps back to the same quest in `QuestAssort.success`;
- additional QuestAssort-only entries are allowed but remain review candidates.

Current counts after repair:

- explicit Success unlock rewards: 40
- `QuestAssort.success` mappings: 41

The one additional mapping is intentionally retained pending runtime/campaign evidence.

## Reward anomalies requiring smoke-test review

No automatic redesign is applied to authored reward structure. The following remain review targets:

- `Puppets` has two Experience rewards (`20000` and `5000`);
- `Rags to Riches` changes standing with Artem and another trader, including a negative standing change;
- `Puppets` and `The Keycard Holder` grant standing with multiple traders.

These may be intentional campaign design. They are not compatibility bugs without runtime/progression evidence.

## Locale note

Many optional quest message keys are not present in the supplied English locale file. This is not currently treated as a hard compatibility defect because SPT/EFT may tolerate absent optional message text. Required displayed quest fields must be verified in the in-game smoke test before locale cleanup is authorized.

## Runtime gate

Static campaign validation is now substantially complete. Remaining campaign evidence must come from SPT 4.1.4:

1. Introduction appears and can be accepted;
2. prerequisite chain advances in the intended order;
3. custom visit/place/beacon conditions register and complete;
4. reward items and standing changes apply;
5. all 40 explicit trader unlock rewards become available at the intended quest completion/loyalty state;
6. the retained QuestAssort-only mapping does not create an unintended unlock.
