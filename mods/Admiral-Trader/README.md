# Admiral Trader

Official curated successor workstream for the legacy Andrudis/QuestManiac ecosystem.

## Product identity

- Mod name: **Admiral Trader**
- Trader working name: **Admiral / Адмирал**
- Trader icon/portrait and final character presentation: TBD
- Legacy Andrudis/QuestManiac names are provenance/source references only and are not the target product identity.

## Current state

The inventory / quest-graph / campaign-manifest / migration / reward-benchmark foundation is established. The exact SPT 4.1.3 test-candidate gate is isolated in Draft PR #151 under Issue #146; post-candidate concept and static-policy work continues separately so the exact physical candidate remains immutable.

The current authored runtime set contains **31 quests**:

- 10 **Access Protocol** quests replacing the legacy key-collection ladder with compact non-FIR capability checks;
- 21 **Arsenal Protocol** quests across seven independent weapon families, with distinct Qualification → Fieldwork → Munitions proofs;
- Qualification is non-FIR/non-consumptive possession of one compatible family weapon;
- Fieldwork is family weapon use in combat;
- six normal Munitions tracks add the selected capability-caliber combat constraint before the controlled ammunition unlock;
- six controlled ammunition capability rewards/unlocks;
- **Special Weapons** uses the same three-stage structure but ends in one explicit green RSP-30 sample reward and no permanent assort unlock.

Admiral's product role is **capability broker**, not general-purpose shop: prove a capability, receive a bounded privilege, then use that privilege in the wider SPT progression. `docs/gameplay-doctrine.md` defines that design contract and `manifests/gameplay-policy.json` encodes its currently enforceable campaign/logistics invariants for automated regression tests.

Trader loyalty is deliberately non-authoritative: standing represents overall relationship/status only. All capability offers remain LL1 plus explicit quest gates, there is no sales-sum grind, loyalty tiers do not change purchase prices, and repair/insurance remain disabled. `docs/loyalty-role.md` records the rationale and static boundary.

The server runtime validates the mixed 10 Access + 7 Arsenal readiness + 14 Arsenal combat registry, trader identity, quest IDs, objective shapes and all referenced runtime item TPLs before publication. Missing authored locale entries fail over to deterministic QuestName-based runtime text so incomplete localization cannot expose raw locale keys; complete authored EN/RU text remains a polish target before final publication.

Source registration remains **fail-closed** through `runtime-manifest.json`. The exact-runtime builder is the only supported path that creates an enabled test candidate, and it must compile against the user's real SPT 4.1.3 assemblies. Candidate staging records source HEAD, clean-tree state, SPT Server.Core version/SHA-256 and built Admiral DLL SHA-256 in `candidate-provenance.json` so live evidence can be tied to the exact CI-tested source head.

All Admiral Trader source/module workflows are expected to be green before physical handoff. Merge remains blocked until one defined SPT 4.1.3 physical runtime gate provides accepted build/start/UI evidence.

The target remains one NPC, one curated campaign, deterministic migration behavior, and reward/unlock data that remains inspectable by Economy Admiral.

Work order:

`source inventory -> quest graph -> manifest -> migration -> trader consolidation -> curated content -> reward normalization -> tests -> runtime`

Tracked by repository Issue #115; the active runtime gate is Issue #146 / Draft PR #151. Post-candidate doctrine/policy work is isolated from that exact-head gate.

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
- Permanent Admiral offers must remain finite, quest-gated, and family-specific; Special Weapons remains sample-only unless a later design decision explicitly changes the doctrine and its machine policy together.
- Loyalty standing must not become a second capability gate or bypass explicit quest proof.

## Baselines and findings

- [`docs/gameplay-doctrine.md`](docs/gameplay-doctrine.md) defines Admiral's player-facing purpose, quest admission test, anti-goals, balance invariants, and preferred expansion directions.
- [`docs/loyalty-role.md`](docs/loyalty-role.md) defines standing/loyalty as relationship status rather than capability authority.
- [`manifests/gameplay-policy.json`](manifests/gameplay-policy.json) is the machine-readable subset of those contracts used to prevent campaign/logistics/loyalty drift.
- [`docs/source-baseline.md`](docs/source-baseline.md) defines which external references are authoritative for which boundary.
- [`docs/inventory-findings.md`](docs/inventory-findings.md) records the full-corpus gate results.
- [`docs/runtime-boundaries.md`](docs/runtime-boundaries.md) records proven and intentionally unproven SPT runtime boundaries.
- [`docs/migration-contract.md`](docs/migration-contract.md) defines the no-profile-write legacy completion bridge and its safety limits.
- [`docs/spt413-test-candidate.md`](docs/spt413-test-candidate.md) is the exact SPT 4.1.3 physical-runtime handoff/evidence contract.
- [`manifests/campaign-manifest.json`](manifests/campaign-manifest.json) is the maintained source of truth for campaign classification and migration policy.

The legacy quest database itself remains external source material and is not copied wholesale into this repository.

## Analysis tools

`tools/build_inventory.py` walks the pinned legacy `db/QuestBundles` tree, builds a deterministic predecessor/successor graph, reports graph-integrity anomalies, summarizes objectives/rewards, and applies the maintained campaign rules.

`tools/build_reward_benchmark.py` consumes native-style vanilla quest JSON and builds descriptive reward distributions by level bucket, including XP, standing, item counts and unlock counts. It intentionally does not invent a ruble valuation for arbitrary item rewards; economic valuation remains a separate layer that Economy Admiral can supply.

`tools/build_weapon_ammo_runtime_templates.py` compiles the maintained Arsenal Protocol plan, authored specification, capability selections and frozen runtime weapon-family pools into deterministic native SPT quest templates.

`tools/build_spt413_test_candidate.ps1` validates a clean exact-head checkout, compiles against real SPT 4.1.3 runtime assemblies, stages an enabled test-only package and emits candidate provenance hashes for the physical gate.

The CI uses the official pinned `sp-tarkov/server-csharp` vanilla `quests.json` as the reward benchmark source and independently validates committed runtime materialization against compiler output.

## Validation

```bash
python -m unittest discover -s mods/Admiral-Trader/tests -p 'test_*.py'
```

Module-specific CI additionally:

- checks the pinned 4,862-quest legacy corpus and graph invariants;
- builds the official vanilla reward benchmark from a pinned SPT source revision;
- validates Access Protocol and Arsenal Protocol compiler output;
- validates the 7 readiness + 14 combat Arsenal objective mix and six caliber-constrained Munitions proofs;
- validates frozen weapon-family pools and controlled ammo capability selections;
- enforces the machine-readable gameplay policy against authored specs, loyalty/base data and packaged assort/questassort data;
- builds the .NET 10 server runtime against the published SPTushonka 4.1.3 package line;
- validates the packaged 31-quest mixed runtime layout;
- validates the exact-runtime candidate/provenance source contract;
- keeps generated reports only as transient Actions artifacts.

Final runtime validation requires the exact-head SPT 4.1.3 physical gate defined in `docs/spt413-test-candidate.md`.
