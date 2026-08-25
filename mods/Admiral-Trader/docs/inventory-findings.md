# Admiral Trader inventory findings

This document records findings from the pinned legacy Andrudis/QuestManiac corpus used as source material for Admiral Trader. Product naming is intentionally separate from legacy source provenance.

## Full-corpus gate

Pinned source: `Thirt3nth/Andrudis-Questmaniac@4e15990ac18f469ef7818475ce8553bbbb7cebb4`, `db/QuestBundles`.

Current deterministic inventory result:

- 4,862 quests across 30 bundles;
- 0 parse errors;
- 0 unclassified quests under the maintained manifest;
- 0 duplicate quest IDs;
- 0 missing prerequisite references;
- 0 cycles;
- 23 cross-bundle prerequisite edges, all intentional Weapon Proficiency -> Ammo Proficiency dependencies.

## First-pass curation distribution

- `MIGRATION_ONLY`: 3,817
- `DROP`: 60
- `REWRITE`: 788
- `MERGE`: 170
- `KEEP`: 27

The distribution confirms that Admiral Trader is a curated rebuild, not a reduced legacy bundle pack.

## Implications

Weapon Proficiency and Ammo Proficiency must be designed as one progression domain. Large repetitive ladders remain migration knowledge only. Hideout Assistant content is dropped. Authored hunts/capability families remain source material for curated milestones. Rewards/unlocks must stay close to native SPT shapes for Economy Admiral compatibility.

## Evidence handling

The generated full inventory is transient Actions output. It is not committed as durable evidence. Durable decisions belong in `manifests/campaign-manifest.json`, while CI rebuilds and validates the pinned source corpus deterministically.
