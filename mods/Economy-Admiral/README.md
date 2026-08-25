# Economy Admiral

**Economy Admiral** is a server-side economy analysis and enforcement-policy mod for SPT 4.1.x. The current physical target is **SPT 4.1.3**.

## Current status

The current MVP is a **read-only, provenance-aware economy audit and fail-closed enforcement-planning pipeline**. No final DB mutation is enabled.

Already implemented:

- pristine startup snapshot at `OnLoadOrder.Watermark + 1`;
- final modded DB analysis at `OnLoadOrder.PostLoad + 1000`;
- centralized `Off` gate;
- typed trader and quest acquisition accounting;
- typed quest item reward accounting through `Reward.Items`, `Item.Template` and `Upd.StackObjectsCount`;
- rarity classification and manual item overrides;
- trader malformed-offer and source-saturation audit;
- pristine vanilla reward-value benchmark;
- XP, trader standing and unlock utility dimensions;
- prerequisite graph, cycle detection and depth metrics;
- timed / one-session / FIR / plant / distance / daytime constraint metrics;
- unified per-quest analysis;
- configurable `Easy / Normal / Hard / Custom` observational policy;
- `Off / Audit / Enforce` modes, with `Enforce` still fail-closed;
- candidate composite metrics without a selected policy;
- non-mutating target envelopes;
- quest provenance delta (`PristineUnchanged / PristineModified / ModAdded / removed`);
- provenance-aware enforcement review plan;
- before/after SHA-256 final-DB fingerprint;
- exact GitHub Actions build identity in runtime evidence;
- packaged runtime validator with synthetic PASS/FAIL smoke tests;
- `RepeatedRaidLootDecay` represented as a future optional policy and **OFF by default**.

## Why pristine provenance exists

A final-DB quest cannot be called “vanilla” merely because its trader ID belongs to Prapor, Therapist, Jaeger, etc. Mods can add quests to vanilla traders and can modify existing vanilla quests. That polluted the first benchmark implementation.

Economy Admiral now captures an immutable baseline **before normal mod callbacks** and compares that snapshot to the final PostLoad database.

`VanillaBaselineService` is an explicit DI singleton at priority `1`. It records pristine quest IDs and the raw dimensions needed for later benchmarking:

- success/all item handbook value;
- XP and trader standing;
- trader / assortment / production unlock counts;
- required level and objective count;
- prerequisite edges/depth/cycles;
- structured constraint dimensions;
- pristine quest/trader/handbook counts.

The final analysis therefore uses quest-ID provenance rather than trader-ID inference.

## SPT architecture boundary

The public compile boundary remains `SPTarkov.Server.Core 4.1.2` on .NET 10. Physical acceptance is against SPT **4.1.3**. `BUILD_INFO.json` records both boundaries and the exact PR head/workflow run.

Load order:

1. `Watermark + 1` — capture pristine baseline;
2. normal SPT/mod startup callbacks mutate/register content;
3. `PostLoad + 1000` — analyze the final DB;
4. write reports and verify no Economy Admiral DB mutation occurred.

Critical intermediate state is passed explicitly as values. The old transient-DI snapshot dependency that caused the first runtime crash is not used in the analysis chain.

## Analysis pipeline

With `mode != Off`:

1. read runtime config and obtain the already-captured pristine baseline;
2. capture final-DB fingerprint-before;
3. run primary final-DB acquisition/trader/quest audit;
4. reclassify primary quest membership by pristine quest IDs;
5. apply typed item-reward accounting;
6. replace primary vanilla benchmark with pristine values;
7. run and pristine-correct reward utility report;
8. run and pristine-correct progression graph report;
9. run and pristine-correct structured constraint report;
10. build unified quest analysis;
11. apply typed item values and pristine relative baselines;
12. calculate quest provenance delta;
13. evaluate non-selected composite candidates;
14. derive non-mutating target envelopes;
15. build provenance-aware fail-closed enforcement review plan;
16. capture fingerprint-after and write runtime evidence.

## Reports

Economy Admiral currently emits **9 working reports plus 1 runtime manifest**:

1. `reports/economy-admiral-audit.json` — acquisition, trader audit, quest item reward values and pristine reward benchmark.
2. `reports/economy-admiral-reward-utility.json` — XP, standing and unlock distributions/ratios using the pristine baseline.
3. `reports/economy-admiral-progression-graph.json` — prerequisite graph, cycles and pristine depth benchmark.
4. `reports/economy-admiral-quest-constraints.json` — structured constraints and pristine constraint benchmark.
5. `reports/economy-admiral-quest-analysis.json` — unified dimensions, pristine-relative ratios and observational flags.
6. `reports/economy-admiral-provenance-delta.json` — `PristineUnchanged`, `PristineModified`, `ModAdded`, removed pristine IDs, changed dimensions and trader grouping.
7. `reports/economy-admiral-composite-candidates.json` — candidate dimensionless composite metrics; no candidate selected.
8. `reports/economy-admiral-target-proposals.json` — review ceilings only; no mutation instructions.
9. `reports/economy-admiral-enforcement-plan.json` — provenance-aware review candidates; zero mutations.
10. `reports/economy-admiral-runtime-evidence.json` — schema-v3 manifest over the working reports, pristine provenance and DB fingerprints.

All report paths are constrained to the mod directory.

## Runtime evidence gate

Runtime evidence schema **v3** requires:

- exact packaged `BuildIdentity`;
- pristine capture priority `1`;
- positive `PristineQuestCount`;
- consistent `FinalQuestCount` and `ModAddedQuestCount`;
- all **9/9** working reports present and non-empty;
- `PristineStartupSnapshot` benchmark source in the primary/utility/progression/constraint reports;
- provenance delta counts consistent with the runtime manifest;
- identical before/after final-DB SHA-256 fingerprints;
- `DatabaseUnchangedAcrossPipeline = true`;
- `ApplyMutations = false`;
- `DeclaredMutationCount = 0`;
- `RuntimeGatePassed = true`.

The fingerprint covers item identities, handbook prices, quest rewards and trader assort/barter/loyalty structures.

## Quest provenance delta

Every final quest is classified as:

- `PristineUnchanged` — existed in the early snapshot and the tracked economic/progression dimensions did not change;
- `PristineModified` — existed in pristine SPT but one or more tracked dimensions changed by final PostLoad;
- `ModAdded` — absent from pristine SPT and present in the final DB.

Pristine quest IDs missing from the final DB are reported separately as removed.

Tracked change dimensions include restartability, success item handbook value, XP, trader standing, unlock counts, objective count, prerequisite count/depth/cycle membership and structured constraint count.

The delta report is observational: `EnforcementAffected = false`.

## Unified quest analysis

Each quest exposes success-reward handbook value, XP/standing, unlocks, prerequisite structure, objective count, structured constraints and pristine-relative ratios.

Current observational flags:

- `HIGH_ITEM_VALUE_LOW_STRUCTURE`;
- `HIGH_XP_LOW_DEPTH`;
- `HIGH_STANDING_LOW_DEPTH`;
- `RESTARTABLE_HIGH_ITEM_VALUE`;
- `RESTARTABLE_HIGH_XP`;
- `PREREQUISITE_CYCLE`.

These flags do not directly change reward allowance or DB records.

## Composite candidates and target envelopes

Current composite candidates are `RewardPeak`, `RewardMean` and `StructureAdjustedPeak`. Contract remains:

- `SelectedCandidate = null`;
- `AffectsRewardAllowance = false`;
- `AffectsEnforcement = false`.

Target envelopes support item handbook-value budget, XP and absolute trader standing. They remain review ceilings only:

- `ProposalsAreMutations = false`;
- `ApplyMutations = false`;
- `SelectedCompositePolicy = null`;
- `AutomaticMutationAllowed = false`;
- `ProposedMutation = null`.

## Enforcement plan

The enforcement plan is schema v2 and provenance-aware. Every review candidate records `ProvenanceClass`, `PristineUntouched`, `ChangedDimensions`, reason flags and proposed review actions. Candidate counts are also grouped by provenance class.

Hard contract:

- `ApplyMutations = false`;
- `MutationCount = 0`;
- every `AutomaticMutationAllowed = false`;
- every `ProposedMutation = null`.

## Configuration

Default config is `config/config.json`. Default mode/preset remain `Audit / Normal`. Easy raises warning thresholds, Hard lowers them, Custom uses explicit configured values. `RepeatedRaidLootDecay` remains `false`.

## Current gate

Earlier physical runtime acceptance proved the value-threaded pipeline starts on the target SPT 4.1.3 stack, typed quest-item accounting works, the final-DB fingerprint remains unchanged and zero mutations are applied.

The **next physical gate is specifically the new pristine-provenance architecture**. It must verify:

- early baseline capture at priority `1`;
- pristine/final/mod-added counts;
- corrected pristine benchmarks;
- the provenance delta report;
- provenance-aware enforcement candidates;
- 9/9 working reports + runtime manifest;
- unchanged before/after final-DB fingerprint.

Until this gate is reviewed, no composite candidate is selected and no mutation transaction is implemented.

## Planned next stages

After pristine-provenance runtime acceptance:

- inspect mod-added vs pristine-modified outliers by trader/source;
- choose or reject composite policy candidates from real distributions;
- define explicit protection/default policy for untouched pristine quests;
- design mutation transaction + rollback + before/after report contracts;
- implement the first deterministic enforcement rule behind explicit policy/config gates.

Stage 2: PBS adapter, trader normalization, Scorpion / Artem / Andrudis integration, repeatable reward enforcement and replacement-rate model.

Stage 3: world loot, flea, craft, insurance and optional Vagabond-like progression policies.

## Development lifecycle

`Issue -> feature branch -> draft PR -> Economy Admiral CI -> physical runtime gate when required -> review -> merge -> cleanup`
