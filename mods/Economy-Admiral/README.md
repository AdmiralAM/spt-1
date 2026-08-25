# Economy Admiral

**Economy Admiral** is a server-side economy audit/enforcement mod for SPT 4.1.x.

## Current status

The module is in its first MVP: **read-only final-database economy analysis plus fail-closed enforcement planning**.

Implemented now:

- final DB scan at the SPT 4.1 `PostLoad` lifecycle boundary;
- trader acquisition scan and deterministic per-item acquisition aggregation;
- handbook-value quest reward audit and vanilla raw/normalized benchmarks;
- typed XP / standing / trader-unlock / assortment-unlock / production-unlock distributions;
- per-dimension vanilla-relative ratios without ruble conversion;
- explicit prerequisite graph with cycle detection, cached snapshot reuse and depth benchmarks;
- structured quest constraint audit for timed, one-session, FIR, plant, distance and daytime constraints;
- unified per-quest analysis view;
- configurable observational cross-dimension flags;
- fail-closed enforcement-plan report derived from the in-memory unified analysis snapshot;
- functional `Easy / Normal / Hard / Custom` policies;
- `Off / Audit / Enforce` contract with **zero active mutations**;
- deterministic JSON reports and manual item overrides;
- future `RepeatedRaidLootDecay` policy represented but disabled by default.

`Enforce` does not mutate the final DB in this MVP. It produces an explicit warning and a review plan with `ApplyMutations = false` and `MutationCount = 0`.

## SPT 4.1 architecture boundary

Economy Admiral consumes final injected `TemplateTable` and `TradersTable` instances at `OnLoadOrder.PostLoad + 1000`, after normal content registration.

## Reports

Economy Admiral emits six deterministic reports under the mod directory:

- `reports/economy-admiral-audit.json` — acquisition, handbook-value and trader/quest findings;
- `reports/economy-admiral-reward-utility.json` — XP, standing and unlock distributions/ratios;
- `reports/economy-admiral-progression-graph.json` — prerequisite graph, cycles and depth benchmarks;
- `reports/economy-admiral-quest-constraints.json` — structured objective constraints;
- `reports/economy-admiral-quest-analysis.json` — unified observational view and cross-dimension flags;
- `reports/economy-admiral-enforcement-plan.json` — fail-closed review candidates and proposed review actions.

All report paths are constrained to stay inside the mod directory.

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

The unified report records resolved policy thresholds and deterministic `FlagCounts`. `CompositeScoreApplied = false`, `RewardAllowanceAffected = false`, and `OutlierFlagsAffectEnforcement = false` remain explicit schema contracts.

## Enforcement plan

The enforcement planner consumes the **in-memory** unified analysis snapshot; JSON files are output artifacts, not an internal service-to-service API.

Flagged quests become review candidates with actions such as:

- `ReviewItemRewardBudget`;
- `ReviewXpRewardBudget`;
- `ReviewStandingRewardBudget`;
- `ReviewPrerequisiteGraph`.

The plan deliberately contains no invented target reward values. Every candidate has `AutomaticMutationAllowed = false` and `ProposedMutation = null`.

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

Easy raises cross-dimension warning thresholds; Hard lowers them and treats moderately shallow/low-structure quests more aggressively. Custom uses the explicit configuration above.

## Reward budget model

The main audit preserves raw handbook value and computes a second progression-normalized signal from structured level and objective-count inputs. Contributions are capped.

Prerequisite depth and structured constraints remain excluded from the active reward allowance. Their reports explicitly retain `DepthAffectsRewardAllowance = false` and `ConstraintsAffectRewardAllowance = false`.

Typed XP/standing/unlock dimensions are also not converted into rubles and are not merged into a hidden utility score.

## Current limitations

The MVP still lacks:

- an approved cross-dimension composite utility formula;
- numeric proposed mutation targets;
- mutation execution and rollback/reporting;
- repeatable replacement-rate enforcement;
- PBS adapter / trader normalization;
- flea, world-loot, craft and insurance modeling.

## Planned next slices

MVP remainder:

- evaluate explicit composite-policy candidates while preserving every raw input dimension;
- define target-generation rules for proposed mutations without applying them;
- add mutation-report/rollback contract;
- deterministic enforcement tests before any mutation path can be enabled.

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
