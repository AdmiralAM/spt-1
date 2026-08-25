# Economy Admiral

**Economy Admiral** is a server-side economy analysis and enforcement-policy mod for SPT 4.1.x. The current physical target is **SPT 4.1.3**.

## Current status

The current MVP is a **read-only, provenance-aware economy audit and fail-closed enforcement-planning pipeline**. No final DB mutation is enabled.

Implemented:

- pristine startup snapshot at `OnLoadOrder.Watermark + 1`;
- final modded DB analysis at `OnLoadOrder.PostLoad + 1000`;
- centralized `Off` gate;
- typed trader/quest acquisition and typed quest-item reward accounting;
- rarity classification and manual item overrides;
- trader malformed-offer/source-saturation audit;
- pristine vanilla reward, XP, standing, unlock, progression and constraint benchmarks;
- prerequisite graph/cycle/depth analysis;
- timed / one-session / FIR / plant / distance / daytime constraints;
- unified per-quest analysis and configurable observational flags;
- `Easy / Normal / Hard / Custom` policies;
- `Off / Audit / Enforce`, with `Enforce` still fail-closed;
- candidate composite metrics with no selected policy;
- non-mutating target envelopes;
- quest provenance delta (`PristineUnchanged / PristineModified / ModAdded / removed`);
- exact provenance partition validation in runtime evidence;
- provenance-aware, dimension-scoped mutation-eligibility classification;
- before/after SHA-256 final-DB fingerprint;
- exact GitHub Actions build identity and runtime validator;
- future `RepeatedRaidLootDecay` represented and **OFF by default**.

## Pristine provenance

Trader ID is not sufficient to identify vanilla content: mods can add quests to vanilla traders or modify existing vanilla quests. Economy Admiral therefore captures an immutable baseline before normal mod callbacks and compares it to the final PostLoad DB.

`VanillaBaselineService` is an explicit singleton at priority `1`. It captures pristine quest IDs and the raw dimensions required for benchmarking: reward handbook value, XP, standing, unlocks, required level/objectives, prerequisite structure, cycles and structured constraints.

Final “vanilla” membership is quest-ID provenance, not trader-ID inference.

## SPT boundary

Public compile boundary: `SPTarkov.Server.Core 4.1.2` / .NET 10. Physical runtime target: **SPT 4.1.3**. `BUILD_INFO.json` records the exact head/workflow plus both boundaries.

Load order:

1. priority `1` — pristine baseline;
2. normal SPT/mod callbacks;
3. `PostLoad + 1000` — final DB analysis;
4. reports + zero-mutation runtime evidence.

Critical intermediate state is value-threaded; the old transient-DI snapshot dependency is not used.

## Pipeline

With `mode != Off`:

1. obtain pristine baseline and capture final-DB fingerprint-before;
2. run primary acquisition/trader/quest audit;
3. correct quest membership by pristine IDs;
4. apply typed reward-item accounting;
5. replace primary reward benchmark with pristine values;
6. generate and pristine-correct utility/progression/constraint reports;
7. build unified quest analysis and apply pristine-relative ratios;
8. calculate exact quest provenance delta;
9. evaluate non-selected composite candidates;
10. derive non-mutating target envelopes;
11. build provenance-aware, dimension-scoped fail-closed enforcement review plan;
12. capture fingerprint-after and write runtime evidence using the exact provenance delta.

## Reports

**9 working reports + 1 runtime manifest:**

1. `economy-admiral-audit.json`
2. `economy-admiral-reward-utility.json`
3. `economy-admiral-progression-graph.json`
4. `economy-admiral-quest-constraints.json`
5. `economy-admiral-quest-analysis.json`
6. `economy-admiral-provenance-delta.json`
7. `economy-admiral-composite-candidates.json`
8. `economy-admiral-target-proposals.json`
9. `economy-admiral-enforcement-plan.json`
10. `economy-admiral-runtime-evidence.json` — manifest over the 9 working reports.

All paths are constrained to the mod directory.

## Runtime gate

Runtime evidence schema **v3** requires:

- exact packaged build identity;
- pristine capture priority `1`;
- positive pristine quest count;
- exact non-negative provenance counts;
- `modified + unchanged + removed = pristine`;
- `added + modified + unchanged = final`;
- all **9/9** working reports;
- `PristineStartupSnapshot` benchmark provenance in primary/utility/progression/constraint reports;
- provenance-delta counts exactly equal to runtime-manifest counts;
- identical before/after final-DB fingerprints;
- zero declared mutations and `RuntimeGatePassed = true`.

## Provenance delta

Final quests are classified as:

- `PristineUnchanged`;
- `PristineModified`;
- `ModAdded`.

Removed pristine IDs are listed separately. Tracked changes include restartability, item reward value, XP, standing, unlocks, objectives, prerequisite structure/cycles and structured constraints.

`EnforcementAffected = false` remains explicit.

## Unified analysis and policy flags

The unified view exposes item reward value, XP/standing, unlocks, prerequisite structure, constraints and pristine-relative ratios.

Current flags:

- `HIGH_ITEM_VALUE_LOW_STRUCTURE`;
- `HIGH_XP_LOW_DEPTH`;
- `HIGH_STANDING_LOW_DEPTH`;
- `RESTARTABLE_HIGH_ITEM_VALUE`;
- `RESTARTABLE_HIGH_XP`;
- `PREREQUISITE_CYCLE`.

Flags remain observational.

## Composite / target / enforcement contracts

Composite candidates: `RewardPeak`, `RewardMean`, `StructureAdjustedPeak`.

- `SelectedCandidate = null`;
- `AffectsRewardAllowance = false`;
- `AffectsEnforcement = false`.

Target envelopes support item reward budget, XP and absolute standing, but:

- `ProposalsAreMutations = false`;
- `ApplyMutations = false`;
- `SelectedCompositePolicy = null`;
- every `AutomaticMutationAllowed = false`;
- every `ProposedMutation = null`.

Enforcement plan schema **v4** is provenance-aware and uses mutation-eligibility policy **v2**. Every flagged quest is classified before any future mutation implementation:

- `ProtectedPristine` — `PristineUnchanged`, never automatically eligible by default;
- `PolicyEligibleModAdded` — mod-added quest with at least one flagged reward dimension;
- `ReviewOnlyModAdded` — mod-added quest with no currently mapped reward mutation dimension;
- `PolicyEligibleModifiedPristine` — pristine quest where a flagged reward dimension is also proven changed by the mod stack;
- `ProtectedUnchangedRewardDimensions` — pristine quest was modified, but the flagged reward dimensions themselves were not changed;
- `BlockedUnknownProvenance` — provenance cannot be proven.

`PotentialMutationDimensions` is limited to `ItemRewardBudget`, `Experience`, and `TraderStanding`. For `PristineModified`, each potential dimension must map to a proven changed source dimension (`SuccessItemHandbookValue`, `Experience`, or `TraderStanding`). Structural changes alone never make reward fields eligible.

This is still **classification only**: `AutomaticMutationAllowed = false`, `ApplyMutations = false`, `MutationCount = 0`, and `ProposedMutation = null` remain mandatory.

## Configuration

Default config is `config/config.json`; default mode/preset are `Audit / Normal`. Easy raises warning thresholds, Hard lowers them, Custom uses explicit values. `RepeatedRaidLootDecay` remains `false`.

## Current physical gate

Earlier runtime testing already proved the value-threaded pipeline, typed quest-item accounting and zero-mutation fingerprint on the target SPT 4.1.3 stack.

The next runtime test is specifically for **pristine provenance and dimension-scoped eligibility**. It must prove early baseline capture, corrected benchmark distributions, exact provenance partition, safe eligibility classifications, 9/9 reports and unchanged final DB.

No composite policy or mutation path is promoted before that evidence is reviewed.

## Next stages

After pristine-provenance acceptance:

- analyze `ModAdded` vs `PristineModified` outliers by trader/source;
- select/reject composite policy candidates from real distributions;
- define explicit first mutation policy only for provenance/dimension-eligible reward fields;
- design mutation transaction + rollback + before/after evidence;
- implement the first deterministic enforcement rule behind explicit config gates.

Stage 2: PBS adapter, trader normalization, Scorpion / Artem / Andrudis integration, repeatable reward enforcement and replacement-rate model.

Stage 3: world loot, flea, craft, insurance and optional Vagabond-like progression policies.

## Development lifecycle

`Issue -> feature branch -> draft PR -> Economy Admiral CI -> physical runtime gate when required -> review -> merge -> cleanup`
