# Economy Admiral

**Economy Admiral** is a server-side economy audit/enforcement mod for SPT 4.1.x.

## Current status

The first MVP is a **read-only final-database economy analysis and fail-closed enforcement-planning pipeline**.

Implemented now:

- final DB scan at `OnLoadOrder.PostLoad + 1000`;
- centralized runtime config gate: `Off` returns before any analysis pass runs;
- trader acquisition scan and deterministic per-item acquisition aggregation;
- handbook-value quest reward audit and vanilla raw/normalized benchmarks;
- typed XP / standing / trader-unlock / assortment-unlock / production-unlock distributions;
- per-dimension vanilla-relative ratios without ruble conversion;
- explicit prerequisite graph with cycle detection, cached snapshot reuse and depth benchmarks;
- structured quest constraint audit for timed, one-session, FIR, plant, distance and daytime constraints;
- unified per-quest analysis view;
- configurable observational cross-dimension flags;
- explicit composite-policy candidate evaluation with no selected candidate;
- deterministic non-mutating target envelopes derived from vanilla medians and resolved policy thresholds;
- fail-closed enforcement-plan report derived from the in-memory unified analysis snapshot;
- runtime evidence manifest with before/after SHA-256 fingerprinting of the analyzed economy DB surfaces;
- functional `Easy / Normal / Hard / Custom` policies;
- `Off / Audit / Enforce` contract with **zero active mutations**;
- deterministic JSON reports and manual item overrides;
- future `RepeatedRaidLootDecay` policy represented but disabled by default.

`Enforce` does not mutate the final DB in this MVP. It produces review/planning artifacts while retaining `ApplyMutations = false` and `MutationCount = 0`.

## SPT 4.1 architecture boundary

Economy Admiral consumes final injected `TemplateTable` and `TradersTable` instances after normal content registration. `EconomyRuntimeConfigService` loads the runtime config once for the top-level module gate; `Mode.Off` exits before the first audit service executes.

## Reports

Economy Admiral emits eight analysis/planning reports plus one runtime-evidence manifest when analysis is enabled:

- `reports/economy-admiral-audit.json` — acquisition, handbook-value and trader/quest findings;
- `reports/economy-admiral-reward-utility.json` — XP, standing and unlock distributions/ratios;
- `reports/economy-admiral-progression-graph.json` — prerequisite graph, cycles and depth benchmarks;
- `reports/economy-admiral-quest-constraints.json` — structured objective constraints;
- `reports/economy-admiral-quest-analysis.json` — unified observational view and cross-dimension flags;
- `reports/economy-admiral-composite-candidates.json` — explicit candidate composite metrics, with no candidate selected;
- `reports/economy-admiral-target-proposals.json` — non-mutating review ceilings for flagged dimensions;
- `reports/economy-admiral-enforcement-plan.json` — fail-closed review candidates and proposed review actions;
- `reports/economy-admiral-runtime-evidence.json` — runtime gate manifest proving report presence and comparing before/after economy fingerprints.

All report paths are constrained to stay inside the mod directory.

## Runtime evidence gate

`RuntimeEvidenceService` captures a deterministic SHA-256 fingerprint **before** the first analysis service and again **after** the enforcement-plan service. The fingerprint covers the current mutation-relevant surfaces:

- item template identities;
- handbook item identities/prices;
- quest reward structures;
- trader assort items;
- trader barter schemes;
- trader loyalty mappings.

The runtime manifest records both hashes, canonical-line counts and structural DB counts. It also verifies that all eight expected analysis/planning reports exist and are non-empty.

The runtime gate is considered passed only when:

- `DatabaseUnchangedAcrossPipeline = true`;
- `AllExpectedReportsPresent = true`;
- `ApplyMutations = false`;
- `DeclaredMutationCount = 0`;
- `RuntimeGatePassed = true`.

This is stronger than a compile-only claim: on the target SPT/mod stack it provides direct before/after evidence that the current Economy Admiral pipeline did not modify the economy surfaces it audits.

## Unified quest analysis

For each quest the unified report places the main measured dimensions together:

- success-reward known handbook value;
- XP and trader standing;
- trader / assortment / production unlock counts;
- direct prerequisites and maximum prerequisite depth;
- prerequisite-cycle membership;
- objective count;
- timed / one-session / FIR / plant / distance / daytime constraint counts;
- vanilla-relative ratios for handbook value, XP, standing, prerequisite depth and structured constraints.

Sparse dimensions use positive-sample vanilla medians so zero-dominated distributions do not erase the baseline.

Current observational flags:

- `HIGH_ITEM_VALUE_LOW_STRUCTURE`;
- `HIGH_XP_LOW_DEPTH`;
- `HIGH_STANDING_LOW_DEPTH`;
- `RESTARTABLE_HIGH_ITEM_VALUE`;
- `RESTARTABLE_HIGH_XP`;
- `PREREQUISITE_CYCLE`.

The report records resolved policy thresholds and deterministic `FlagCounts`. `CompositeScoreApplied = false`, `RewardAllowanceAffected = false`, and `OutlierFlagsAffectEnforcement = false` remain explicit contracts.

## Composite policy candidates

Economy Admiral evaluates multiple dimensionless candidates without selecting one as policy:

- `RewardPeak` — maximum available handbook/XP/standing vanilla-relative ratio;
- `RewardMean` — mean of the available positive reward-dimension ratios;
- `StructureAdjustedPeak` — `RewardPeak` divided by measured structural support, floored so structure never inflates the score.

Vanilla and restartable median/P90 distributions are emitted for comparison. The composite report explicitly keeps:

- `SelectedCandidate = null`;
- `AffectsRewardAllowance = false`;
- `AffectsEnforcement = false`.

This lets the candidates be inspected against real final-DB data before any one formula is promoted to policy.

## Target envelopes

For flagged quests, `economy-admiral-target-proposals.json` derives deterministic **review ceilings**, not mutation instructions.

Each envelope records:

- current measured value;
- applicable vanilla median;
- resolved policy multiple;
- `CandidateCeiling = vanilla median × policy multiple`;
- a dimension-specific interpretation.

Supported envelope dimensions currently include item reward handbook-value budget, XP, and absolute trader standing. Item budget ceilings do not select replacement item templates, and trader-standing envelopes do not change sign/direction.

The target-proposal contract remains:

- `ProposalsAreMutations = false`;
- `ApplyMutations = false`;
- `SelectedCompositePolicy = null`;
- `AutomaticMutationAllowed = false`;
- `ProposedMutation = null`.

## Enforcement plan

The enforcement planner consumes the **in-memory** unified analysis snapshot; JSON files are output artifacts, not an internal service-to-service API.

Flagged quests become review candidates with actions such as:

- `ReviewItemRewardBudget`;
- `ReviewXpRewardBudget`;
- `ReviewStandingRewardBudget`;
- `ReviewPrerequisiteGraph`.

The plan deliberately contains no invented mutations. Every candidate has `AutomaticMutationAllowed = false` and `ProposedMutation = null`.

## Configuration

Default configuration lives in `config/config.json`.

```json
{
  "mode": "Audit",
  "preset": "Normal",
  "reportRelativePath": "reports/economy-admiral-audit.json",
  "repeatedRaidLootDecay": false,
  "rarity": {
    "commonMinSources": 8,
    "uncommonMinSources": 4,
    "rareMinSources": 2
  },
  "customAuditPolicy": {
    "questRewardVsVanillaMedianWarnMultiple": 3.0,
    "restartableRewardVsVanillaMedianWarnMultiple": 1.5,
    "normalizedRewardVsVanillaMedianWarnMultiple": 2.5,
    "restartableNormalizedRewardVsVanillaMedianWarnMultiple": 1.25,
    "levelGateWeight": 0.05,
    "objectiveConditionWeight": 0.35,
    "maxLevelGateContribution": 3.0,
    "maxObjectiveContribution": 5.0,
    "duplicateTraderSourcesWarnCount": 6,
    "highItemValueLowStructureWarnMultiple": 3.0,
    "highXpLowDepthWarnMultiple": 3.0,
    "highStandingLowDepthWarnMultiple": 3.0,
    "restartableHighItemValueWarnMultiple": 2.0,
    "restartableHighXpWarnMultiple": 2.0,
    "lowDepthMaxRelativeMultiple": 1.0,
    "lowStructureMaxRelativeMultiple": 1.0
  },
  "manualOverrides": {}
}
```

Easy raises cross-dimension warning thresholds; Hard lowers them and treats moderately shallow/low-structure quests more aggressively. Custom uses the explicit configuration.

## Reward budget model

The main audit preserves raw handbook value and computes a second progression-normalized signal from structured level and objective-count inputs. Contributions are capped.

Prerequisite depth and structured constraints remain excluded from the active reward allowance. Their reports explicitly retain `DepthAffectsRewardAllowance = false` and `ConstraintsAffectRewardAllowance = false`.

Typed XP/standing/unlock dimensions are not converted into rubles and are not merged into a hidden utility score.

## Current limitations

The MVP still lacks:

- an approved composite-policy formula;
- concrete item-template replacement logic;
- active mutation execution;
- mutation transaction/rollback reporting;
- physical runtime evidence from the target SPT/mod stack;
- repeatable replacement-rate enforcement;
- PBS adapter / trader normalization;
- flea, world-loot, craft and insurance modeling.

## Planned next slices

MVP remainder:

- run the exact compiled candidate against the target SPT/mod stack;
- inspect `economy-admiral-runtime-evidence.json` and the eight generated analysis/planning reports;
- use real distributions to decide whether any composite candidate deserves promotion;
- define mutation transaction / rollback / before-after report contracts;
- add deterministic enforcement tests before any mutation path can be enabled.

Stage 2:

- PBS adapter;
- trader normalization;
- Scorpion / Artem / Andrudis integration;
- repeatable reward enforcement;
- replacement-rate model.

Stage 3:

- world loot;
- flea;
- craft;
- insurance;
- optional Vagabond-like progression policies.

`RepeatedRaidLootDecay` remains **OFF by default** and unimplemented.

## Development lifecycle

`Issue -> feature branch -> PR -> Economy Admiral-specific CI -> runtime gate when required -> merge -> cleanup`
