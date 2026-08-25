# Legacy inventory findings

## Pinned corpus

The first full-corpus gate uses `Thirt3nth/Andrudis-Questmaniac` commit:

`4e15990ac18f469ef7818475ce8553bbbb7cebb4`

This is intentionally pinned in CI so future upstream edits cannot silently change curation input.

## Full-corpus result

The deterministic inventory pass found:

- **4,862 quests** across **30 core bundles**;
- **1,143 quests** with a found-in-raid requirement;
- **50 restartable quests**;
- **0 parse errors**;
- **0 unclassified quests** under the current seed rules.

Seed curation distribution:

| Decision | Quests | Meaning at this stage |
| --- | ---: | --- |
| `MIGRATION_ONLY` | 3,817 | Do not expose to new profiles; preserve only as needed for existing-profile completion/migration. |
| `REWRITE` | 788 | Progression concept may survive, but the legacy chain/reward/trader structure does not. |
| `MERGE` | 170 | Collapse repetitive ladders into a much smaller milestone chain. |
| `DROP` | 60 | Explicitly excluded content, currently the Hideout Assistant family. |
| `KEEP` | 27 | Small authored candidate family pending vanilla-overlap/reward audit. |

The result confirms that the target is a curated rebuild rather than a reduced six-trader legacy installation.

## Highest-volume legacy families

The dominant content by raw count is:

- Weapon Mastery — 1,702;
- Errand Boy — 920;
- Ammo Proficiency — 321;
- Deep Pockets Legend — 274;
- Iron Head Legend — 229;
- Juggernaut Legend — 200.

The first two families alone account for more than half of the corpus and are both outside the target new-profile experience.

## FIR pressure

The largest FIR-heavy families are:

- Errand Boy — 920/920;
- Tarkov Mule — 60/60;
- Deep Pockets — 45/90;
- Iron Head — 33/66;
- Juggernaut — 29/58;
- Weapon Proficiency — 24/117;
- Meds Proficiency — 21/42;
- Ultrasound — 9/18.

This validates the requirement to sharply reduce FIR spam rather than carrying legacy handover mechanics forward unchanged.

## Restartable content

All 50 restartable quests found in the pinned corpus are in the Survivalist family. Any retained successor design must use a strict repeatability reward budget; the legacy rewards are not accepted as the target economy contract.

## Cross-bundle dependency

The corpus contains intentional cross-bundle dependencies from Weapon Proficiency into Ammo Proficiency. This is a structural reason to design weapon and ammo progression together in the curated manifest rather than treating them as independent content packs.

The inventory tool now reports missing prerequisites, cross-bundle edges, duplicate quest IDs, and graph cycles so migration/campaign manifests can fail closed on broken graph assumptions.

## Native SPT trader reference

The supplied current SPT runtime trader data confirms the native persisted JSON shape used by vanilla traders:

- `base.json` uses the native trader identity/config fields;
- `assort.json` has `items`, `barter_scheme`, and `loyal_level_items`;
- `questassort.json` uses `fail`, `started`, and `success` maps;
- optional trader-specific files include `dialogue.json`, `services.json`, and clothing data.

The curated trader should preserve these native data concepts rather than hide economy/unlock state behind a custom opaque format.

## Next analysis gate

Before trader/runtime implementation:

1. freeze graph-integrity findings from the pinned corpus;
2. replace bundle-level seed decisions with a maintained campaign/migration manifest;
3. identify the exact weapon/ammo chains to rebuild;
4. audit `KEEP`/`REWRITE` candidates against vanilla quest overlap;
5. establish reward benchmark fields needed by the Economy MOD;
6. prove the SPT 4.1.3 profile quest-state boundary required to let active legacy quests finish while blocking deprecated successors.
