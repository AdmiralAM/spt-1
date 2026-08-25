# SPT Andrudis Curated

Curated successor workstream for the legacy Andrudis/QuestManiac ecosystem.

## Current state

Development is at the **inventory / quest-graph gate**. No runtime mod is published from this module yet.

The target is one NPC, one curated campaign, deterministic migration behavior, and reward/unlock data that remains inspectable by the future Economy MOD.

Work order:

`source inventory -> quest graph -> manifest -> migration -> trader consolidation -> curated content -> reward normalization -> tests -> runtime`

Tracked by repository Issue #115.

## Design constraints

- Six legacy custom traders are a source-data concern, not the target runtime architecture.
- New-profile content must be explicitly selected; directory enumeration must not implicitly activate content.
- Removed legacy quests must not create successor chains on existing profiles.
- Already-active legacy quests must remain finishable through the migration layer when that layer is implemented.
- Hideout-assistant content is excluded from the curated campaign.
- Repetitive kill/headshot/FIR/handover ladders are not preserved wholesale.
- Weapon/ammo progression is retained only where it can become short capability-based chains with controlled unlocks.
- Assort, quest-assort, reward, and unlock data should remain close to native SPT shapes so downstream economy auditing does not need an Andrudis-specific opaque format.

## Baselines

See [`docs/source-baseline.md`](docs/source-baseline.md). The legacy quest database is external source material and is not copied wholesale into this repository.

## Inventory tool

`tools/build_inventory.py` walks a legacy `db/QuestBundles` tree, builds a deterministic quest inventory and predecessor/successor graph, summarizes objectives/rewards, and optionally applies seed curation rules.

Example:

```bash
python mods/SPT-Andrudis-Curated/tools/build_inventory.py \
  /path/to/Andrudis-Questmaniac/db/QuestBundles \
  --rules mods/SPT-Andrudis-Curated/manifests/seed-rules.json \
  --output /tmp/andrudis-inventory.json
```

The generated inventory is analysis output and must not be committed merely as CI/runtime evidence. Durable curation decisions belong in maintained manifests, not generated reports.

## Validation

For the current gate:

```bash
python -m unittest discover -s mods/SPT-Andrudis-Curated/tests -p 'test_*.py'
```

Runtime validation is intentionally not requested until the graph/manifest and server runtime boundary are ready for one focused physical gate.
