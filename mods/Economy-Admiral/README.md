# Economy Admiral

**Economy Admiral** is a server-side economy audit/enforcement mod for SPT 4.1.x.

## Current status

The module is in its first MVP: **read-only final-database economy audit**.

Implemented now:

- final DB scan at the SPT 4.1 `PostLoad` lifecycle boundary;
- trader acquisition scan from final trader assort roots;
- quest reward acquisition scan and handbook-value audit;
- deterministic per-item acquisition aggregation and initial rarity classification;
- trader malformed-offer and source-saturation findings;
- vanilla raw and progression-normalized quest reward benchmarks;
- typed XP / standing / trader-unlock / assortment-unlock / production-unlock distributions;
- per-dimension vanilla-relative utility ratios without ruble conversion;
- explicit prerequisite graph with cycle detection, cached snapshot reuse and depth benchmarks;
- structured final-DB quest constraint audit for timed, one-session, FIR, plant, distance and daytime constraints;
- unified per-quest analysis view combining all measured dimensions without a composite score;
- observational cross-dimension flags for suspicious reward/structure combinations and restartable outliers;
- functional `Easy / Normal / Hard / Custom` audit policies;
- `Off / Audit / Enforce` contract with `Enforce` fail-closed/read-only;
- deterministic JSON reports and manual item overrides;
- future `RepeatedRaidLootDecay` policy represented but disabled by default.

No economy mutation is implemented yet.

## SPT 4.1 architecture boundary

Economy Admiral consumes final injected `TemplateTable` and `TradersTable` instances at `OnLoadOrder.PostLoad + 1000`, after normal content registration.

## Reports

Five deterministic reports are emitted under the mod directory:

- `reports/economy-admiral-audit.json` — acquisition, handbook-value and trader/quest findings;
- `reports/economy-admiral-reward-utility.json` — XP, standing and unlock distributions/ratios;
- `reports/economy-admiral-progression-graph.json` — prerequisite graph, cycles and depth benchmarks;
- `reports/economy-admiral-quest-constraints.json` — structured objective constraints;
- `reports/economy-admiral-quest-analysis.json` — unified observational view and cross-dimension flags.

All report paths are constrained to stay inside the mod directory.

## Unified quest analysis

The unified report puts the main measurable dimensions beside each other for the same quest:

- success-reward known handbook value;
- XP and trader standing;
- trader / assortment / production unlock counts;
- direct prerequisites and maximum prerequisite depth;
- prerequisite-cycle membership;
- objective count;
- timed / one-session / FIR / plant / distance / daytime constraint counts;
- vanilla-relative ratios for handbook value, XP, standing, prerequisite depth and structured constraints.

Sparse dimensions use positive-sample vanilla medians so zero-dominated distributions do not erase the baseline.

Schema 2 adds observational flags including:

- `HIGH_ITEM_VALUE_LOW_STRUCTURE`;
- `HIGH_XP_LOW_DEPTH`;
- `HIGH_STANDING_LOW_DEPTH`;
- `RESTARTABLE_HIGH_ITEM_VALUE`;
- `RESTARTABLE_HIGH_XP`;
- `PREREQUISITE_CYCLE`.

These are diagnostic classifications only. `CompositeScoreApplied = false`, `RewardAllowanceAffected = false`, and `OutlierFlagsAffectEnforcement = false` are explicit contracts.

## Reward budget model

The main audit preserves raw handbook value and computes a second progression-normalized signal from structured level and objective-count inputs. Contributions are capped.

Prerequisite depth and structured constraints are deliberately excluded from the current reward allowance. Their reports explicitly retain `DepthAffectsRewardAllowance = false` and `ConstraintsAffectRewardAllowance = false`.

## Typed reward utility benchmark

SPT reward types `Experience`, `TraderStanding`, `TraderUnlock`, `AssortmentUnlock`, and `ProductionScheme` are measured directly. Vanilla and restartable distributions are separated and sparse unlock medians use positive samples only.

No XP/standing/unlock dimension is converted into rubles and no hidden composite utility score exists.

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
    "duplicateTraderSourcesWarnCount": 6
  },
  "manualOverrides": {}
}
```

| Preset | raw normal | raw restartable | normalized normal | normalized restartable | trader saturation |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy | 5.0x | 2.5x | 4.0x | 2.0x | 8 traders |
| Normal | 3.0x | 1.5x | 2.5x | 1.25x | 6 traders |
| Hard | 2.0x | 1.25x | 1.75x | 1.10x | 4 traders |
| Custom | config | config | config | config | config |

## Current limitations

The MVP still lacks an approved cross-dimension composite policy, mutation-plan schema, repeatable replacement-rate enforcement, PBS adapter/trader normalization, and flea/world-loot/craft/insurance modeling.

## Planned next slices

MVP remainder:

- move observational flag thresholds into an explicit policy/config surface instead of hard-coded prototype constants;
- add deterministic flag counts/summary to the unified report;
- define composite-policy candidates while preserving all raw dimensions;
- define enforcement-plan and mutation-report schemas without activating mutation;
- deterministic enforcement tests before `Enforce` can become active.

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
