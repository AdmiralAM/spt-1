# Admiral Trader

Official curated successor workstream for the legacy Andrudis/QuestManiac ecosystem.

## Product identity

- Mod name: **Admiral Trader**
- Trader working name: **Admiral / Адмирал**
- Trader icon/portrait and final character presentation: TBD
- Legacy Andrudis/QuestManiac names are provenance/source references only and are not the target product identity.

## Current state

Development is at the **inventory / quest-graph / campaign-manifest gate**. No runtime mod is published from this module yet.

The target is one NPC, one curated campaign, deterministic migration behavior, and reward/unlock data that remains inspectable by the future Economy MOD.

Work order:

`source inventory -> quest graph -> manifest -> migration -> trader consolidation -> curated content -> reward normalization -> tests -> runtime`

Tracked by repository Issue #115 and the active Draft PR for Admiral Trader.

## Design constraints

- Six legacy custom traders are a source-data concern, not the target runtime architecture.
- New-profile content must be explicitly selected; directory enumeration must not implicitly activate content.
- Removed legacy quests must not create successor chains on existing profiles.
- Already-active legacy quests must remain finishable through the migration layer when that layer is implemented.
- Hideout-assistant content is excluded from the curated campaign.
- Repetitive kill/headshot/FIR/handover ladders are not preserved wholesale.
- Weapon and ammo progression form one progression domain because the pinned legacy graph contains intentional cross-bundle prerequisite edges between them.
- Assort, quest-assort, reward, and unlock data should remain close to native SPT shapes so downstream economy auditing does not need an Admiral-Trader-specific opaque format.

## Baselines and findings

- [`docs/source-baseline.md`](docs/source-baseline.md) defines which external references are authoritative for which boundary.
- [`docs/inventory-findings.md`](docs/inventory-findings.md) records the full-corpus gate results.
- [`manifests/campaign-manifest.json`](manifests/campaign-manifest.json) is the maintained source of truth for campaign classification and migration policy. The legacy quest database itself remains external source material and is not copied wholesale into this repository.

## Inventory tool

`tools/build_inventory.py` walks a legacy `db/QuestBundles` tree, builds a deterministic quest inventory and predecessor/successor graph, reports graph-integrity anomalies, summarizes objectives/rewards, and applies the maintained campaign rules.

Example:

```bash
python mods/Admiral-Trader/tools/build_inventory.py \
  /path/to/Andrudis-Questmaniac/db/QuestBundles \
  --rules mods/Admiral-Trader/manifests/campaign-manifest.json \
  --output /tmp/admiral-trader-inventory.json
```

The generated inventory is analysis output and must not be committed merely as CI/runtime evidence. Durable curation decisions belong in the campaign manifest, not generated reports.

## Validation

For the current gate:

```bash
python -m unittest discover -s mods/Admiral-Trader/tests -p 'test_*.py'
```

The module CI additionally checks the pinned legacy corpus and keeps the generated inventory only as a transient Actions artifact.

Runtime validation is intentionally not requested until the manifest/migration design and server runtime boundary are ready for one focused physical gate.
