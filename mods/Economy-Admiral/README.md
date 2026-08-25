# Economy Admiral

**Economy Admiral** is a server-side economy audit/enforcement mod for SPT 4.1.x.

## Current status

The module is in its first MVP: **read-only final-database economy audit**.

Implemented now:

- final DB scan at the SPT 4.1 `PostLoad` lifecycle boundary;
- trader acquisition scan from final trader assort roots;
- quest reward acquisition scan;
- deterministic per-item acquisition aggregation;
- initial acquisition-density rarity classification;
- trader structural audit for malformed root offers;
- quest reward value audit using final handbook prices;
- vanilla quest reward raw median/P90 benchmark;
- progression-normalized quest reward median/P90 benchmark;
- restartable-quest raw and normalized reward outlier checks;
- typed SPT 4.1 reward-utility inventory for XP, trader standing, trader unlocks, assortment unlocks, and production-scheme unlocks;
- separate vanilla/restartable utility distributions and per-dimension vanilla-relative ratios;
- explicit quest prerequisite graph with cycle detection, depth benchmarks and cached snapshot reuse;
- structured final-DB quest constraint audit for timed, one-session, FIR, plant, distance and daytime constraints;
- unified per-quest analysis view combining all measured dimensions without a composite score;
- trader-source saturation findings;
- functional `Easy / Normal / Hard / Custom` audit policies;
- `Off / Audit / Enforce` mode contract;
- deterministic JSON reports;
- manual item overrides;
- future `RepeatedRaidLootDecay` policy represented but disabled by default.

`Enforce` is intentionally fail-closed in this slice: selecting it produces an explicit warning and still performs only read-only analysis. No economy mutation is implemented yet.

## SPT 4.1 architecture boundary

SPT 4.1 uses directly injected database tables. Economy Admiral consumes final `TemplateTable` and `TradersTable` instances and runs at `OnLoadOrder.PostLoad + 1000`, after normal content registration.

## Reports

Economy Admiral currently emits five deterministic reports under the mod directory:

- `reports/economy-admiral-audit.json` — acquisition, handbook-value and trader/quest findings;
- `reports/economy-admiral-reward-utility.json` — XP, standing and unlock distributions/ratios;
- `reports/economy-admiral-progression-graph.json` — explicit prerequisite graph, cycles and depth benchmarks;
- `reports/economy-admiral-quest-constraints.json` — structured objective constraints;
- `reports/economy-admiral-quest-analysis.json` — unified observational view per quest.

All report paths are constrained to stay inside the mod directory.

## Unified quest analysis

The unified report places the main measurable dimensions beside each other for the same quest:

- success-reward known handbook value;
- XP and trader standing;
- trader / assortment / production unlock counts;
- direct prerequisites and maximum prerequisite depth;
- prerequisite-cycle membership;
- objective count;
- timed / one-session / FIR / plant / distance / daytime constraint counts;
- vanilla-relative ratios for handbook value, XP, standing, prerequisite depth and structured constraints.

`CompositeScoreApplied = false` and `RewardAllowanceAffected = false` are explicit schema contracts. The report is intended to expose relationships and outliers before Economy Admiral chooses any cross-dimension weights.

## Reward budget model

The main audit preserves raw handbook value and also computes a progression-normalized value from structured level and objective-count signals. The score is capped so extreme level gates or condition counts cannot create unlimited reward allowance.

Prerequisite depth and structured constraints are **not** currently included in this allowance. Their independent reports explicitly set `DepthAffectsRewardAllowance = false` and `ConstraintsAffectRewardAllowance = false`.

## Typed reward utility benchmark

Typed SPT rewards including `Experience`, `TraderStanding`, `TraderUnlock`, `AssortmentUnlock`, and `ProductionScheme` are measured directly. Vanilla and restartable distributions are separated. Sparse unlock medians use positive samples only.

No reward utility is converted into rubles and no hidden cross-dimension utility score exists.

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

Preset audit thresholds:

| Preset | raw normal quest | raw restartable | normalized normal | normalized restartable | trader saturation |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy | 5.0x | 2.5x | 4.0x | 2.0x | 8 traders |
| Normal | 3.0x | 1.5x | 2.5x | 1.25x | 6 traders |
| Hard | 2.0x | 1.25x | 1.75x | 1.10x | 4 traders |
| Custom | config | config | config | config | config |

## Current limitations

The MVP still does not provide:

- an approved cross-dimension composite utility policy;
- prerequisite-depth or constraint reward multipliers;
- repeatable replacement-rate enforcement;
- PBS adapter / trader normalization;
- flea, world-loot, craft or insurance modeling;
- active economy mutation.

Those remain later work before `Enforce` may mutate final DB state.

## Planned next slices

MVP remainder:

- use the unified report to define explicit outlier classes across independent dimensions;
- design an explicit configurable composite policy, retaining each raw dimension in the report;
- define an enforcement plan and mutation-report schema without activating mutation;
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

Do not use the repository-wide publisher for ordinary Economy Admiral development validation.
