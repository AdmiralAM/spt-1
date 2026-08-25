# Admiral Trader

Official curated successor workstream for the legacy Andrudis/QuestManiac ecosystem.

## Product identity

- Mod name: **Admiral Trader**
- Trader working name: **Admiral / Адмирал**
- Trader icon/portrait and final character presentation: TBD
- Legacy Andrudis/QuestManiac names are provenance/source references only and are not the target product identity.

## Current state

Development has passed the initial **inventory / quest-graph / campaign-manifest** foundation and is now closing the **migration contract + vanilla reward benchmark** gate. No runtime mod is published from this module yet.

The target is one NPC, one curated campaign, deterministic migration behavior, and reward/unlock data that remains inspectable by Economy Admiral.

Work order:

`source inventory -> quest graph -> manifest -> migration -> trader consolidation -> curated content -> reward normalization -> tests -> runtime`

Tracked by repository Issue #115 and Draft PR #122.

## Design constraints

- Six legacy custom traders are a source-data concern, not the target runtime architecture.
- New-profile content must be explicitly selected; directory enumeration must not implicitly activate content.
- Removed legacy quests must not create successor chains on existing profiles.
- Already-accepted legacy quests should finish through the template-suppression completion bridge without direct profile mutation whenever possible.
- Direct PMC profile writes remain forbidden until the exact SPT 4.1.3 mutation/persistence boundary is proven.
- Restartable legacy quests are excluded from the completion bridge by default.
- Hideout-assistant content is excluded from the curated campaign.
- Repetitive kill/headshot/FIR/handover ladders are not preserved wholesale.
- Weapon and ammo progression form one progression domain because the pinned legacy graph contains intentional cross-bundle prerequisite edges between them.
- Assort, quest-assort, reward, and unlock data remain close to native SPT shapes so downstream economy auditing does not require an Admiral-Trader-specific opaque format.

## Baselines and findings

- [`docs/source-baseline.md`](docs/source-baseline.md) defines which external references are authoritative for which boundary.
- [`docs/inventory-findings.md`](docs/inventory-findings.md) records the full-corpus gate results.
- [`docs/runtime-boundaries.md`](docs/runtime-boundaries.md) records proven and intentionally unproven SPT runtime boundaries.
- [`docs/migration-contract.md`](docs/migration-contract.md) defines the no-profile-write legacy completion bridge and its safety limits.
- [`manifests/campaign-manifest.json`](manifests/campaign-manifest.json) is the maintained source of truth for campaign classification and migration policy.

The legacy quest database itself remains external source material and is not copied wholesale into this repository.

## Analysis tools

`tools/build_inventory.py` walks the pinned legacy `db/QuestBundles` tree, builds a deterministic predecessor/successor graph, reports graph-integrity anomalies, summarizes objectives/rewards, and applies the maintained campaign rules.

`tools/build_reward_benchmark.py` consumes native-style vanilla quest JSON and builds descriptive reward distributions by level bucket, including XP, standing, item counts and unlock counts. It intentionally does not invent a ruble valuation for arbitrary item rewards; economic valuation remains a separate layer that Economy Admiral can supply.

The CI uses the official pinned `sp-tarkov/server-csharp` vanilla `quests.json` as the reward benchmark source.

## Validation

For the current gate:

```bash
python -m unittest discover -s mods/Admiral-Trader/tests -p 'test_*.py'
```

`Admiral Trader Validate` additionally:

- checks the pinned 4,862-quest legacy corpus and graph invariants;
- builds the official vanilla reward benchmark from a pinned SPT source revision;
- keeps generated reports only as transient Actions artifacts.

Runtime validation is intentionally deferred until a mechanically validated migration/trader candidate is ready for one defined physical gate.
