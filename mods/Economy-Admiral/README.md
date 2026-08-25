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
- separate vanilla/restartable utility distributions without arbitrary ruble conversion;
- trader-source saturation findings;
- functional `Easy / Normal / Hard / Custom` audit policies;
- `Off / Audit / Enforce` mode contract;
- deterministic JSON reports;
- manual item overrides;
- future `RepeatedRaidLootDecay` policy represented but disabled by default.

`Enforce` is intentionally fail-closed in this slice: selecting it produces an explicit warning and still performs only the read-only audit. No economy mutation is implemented yet.

## SPT 4.1 architecture boundary

SPT 4.1 removed the old `DatabaseService.GetTables()` model. Database tables are injected directly. Economy Admiral consumes the final `TemplateTable` and `TradersTable` instances and runs after normal load work at `OnLoadOrder.PostLoad + 1000`.

This is deliberate: the audit observes trader, quest, item, handbook, and mod-added data after other content mods have completed normal registration rather than scanning an early vanilla-only snapshot.

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

Preset audit thresholds currently resolve as follows:

| Preset | raw normal quest | raw restartable | normalized normal | normalized restartable | trader saturation |
| --- | ---: | ---: | ---: | ---: | ---: |
| Easy | 5.0x | 2.5x | 4.0x | 2.0x | 8 traders |
| Normal | 3.0x | 1.5x | 2.5x | 1.25x | 6 traders |
| Hard | 2.0x | 1.25x | 1.75x | 1.10x | 4 traders |
| Custom | config | config | config | config | config |

Manual overrides are keyed by item template ID.

## Reward budget model

The main audit preserves the raw handbook-value benchmark and adds a second, progression-normalized signal. It intentionally uses only structured final-DB data rather than subjective text interpretation.

For each quest it records:

- required player level inferred from `AvailableForStart` level conditions;
- count of `AvailableForFinish` and `Success` conditions;
- a capped progression score;
- raw known handbook reward value;
- progression-normalized handbook reward value.

The current score is:

`1 + capped(level gate contribution) + capped(objective-condition contribution)`

and the normalized value is:

`known handbook reward value / progression score`

Default weights are conservative and capped. This prevents a quest with an extreme level gate or a very large number of conditions from receiving an unlimited reward allowance. Restartable quests are benchmarked separately where vanilla samples exist and use stricter warning thresholds.

This is still an audit proxy, not a claim that condition count equals true gameplay difficulty.

## Typed reward utility benchmark

SPT 4.1 exposes typed quest rewards including `Experience`, `TraderStanding`, `TraderUnlock`, `AssortmentUnlock`, and `ProductionScheme`. Economy Admiral audits those directly in a second deterministic report:

`reports/economy-admiral-reward-utility.json`

For each quest the utility report records success-reward XP, trader standing, and distinct unlock targets. It also calculates vanilla and vanilla-restartable median/P90 distributions where meaningful.

**No unified utility score is applied yet.** XP, standing, and unlocks are deliberately not converted into rubles or handbook value in this slice. The purpose of this pass is to establish real vanilla distributions before selecting weights.

## Main report

Default output:

`reports/economy-admiral-audit.json`

The schema-3 report records:

- total template items and handbook-priced items;
- quest and trader counts;
- total trader assort records;
- items with known trader/quest acquisition;
- trader-source and quest-reward source edges;
- deterministic per-item trader and quest source lists;
- initial rarity classification and manual overrides;
- per-trader root-offer audit summaries;
- root offers missing barter schemes or loyalty mappings;
- per-quest reward item counts and known handbook value;
- required level, objective-condition count and progression score;
- progression-normalized handbook reward value;
- reward records without handbook prices;
- vanilla raw median/P90 benchmark;
- vanilla normalized median/P90 benchmark;
- vanilla restartable raw/normalized medians where samples exist;
- raw reward-value outlier findings;
- progression-normalized reward-budget outlier findings;
- trader-source saturation findings;
- exact policy thresholds and normalization model used for the report.

Both report paths are constrained to remain inside the mod directory.

## Current limitations

The current budget still does not fully price:

- XP relative utility versus item value;
- trader standing relative utility;
- assort/trader/production unlock utility;
- FIR/progression utility;
- actual duration or combat risk;
- prerequisite-chain depth;
- repeatable replacement rate;
- flea scarcity or world-loot rarity.

Those dimensions belong in later MVP/Stage 2 scoring before Economy Admiral is allowed to enforce changes.

## Rarity model

The first classifier is intentionally simple and deterministic: it uses the number of distinct trader and quest-reward acquisition sources for an item. This is an audit signal, not the final economic rarity model.

The architecture keeps acquisition scanning, benchmarking, findings, presets, and manual overrides separate so later sources and weighting can extend the model without replacing the final-DB scanner.

## Planned next slices

MVP remainder:

- derive XP/standing/unlock weighting from the typed vanilla distributions;
- prerequisite-depth and structured risk/time signals where reliable;
- broader acquisition/value weighting;
- explicit enforcement rules and mutation report;
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

`RepeatedRaidLootDecay` remains **OFF by default** and is not implemented in the current slice.

## Development lifecycle

This module follows repository policy:

`Issue -> feature branch -> PR -> Economy Admiral-specific CI -> runtime gate when required -> merge -> cleanup`

Do not use the repository-wide publisher for ordinary Economy Admiral development validation.
