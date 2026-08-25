# SPT Economy

SPT Economy is a server-side economy audit/enforcement workstream for SPT 4.1.x.

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
- vanilla quest reward median/P90 benchmark;
- restartable-quest reward outlier checks;
- trader-source saturation findings;
- functional `Easy / Normal / Hard / Custom` audit policies;
- `Off / Audit / Enforce` mode contract;
- deterministic JSON report;
- manual item overrides;
- future `RepeatedRaidLootDecay` policy represented but disabled by default.

`Enforce` is intentionally fail-closed in this slice: selecting it produces an explicit warning and still performs only the read-only audit. No economy mutation is implemented yet.

## SPT 4.1 architecture boundary

SPT 4.1 removed the old `DatabaseService.GetTables()` model. Database tables are injected directly. Economy consumes the final `TemplateTable` and `TradersTable` instances and runs after normal load work at `OnLoadOrder.PostLoad + 1000`.

This is deliberate: the audit observes trader, quest, item, handbook, and mod-added data after other content mods have completed normal registration rather than scanning an early vanilla-only snapshot.

## Configuration

Default configuration lives in `config/config.json`.

```json
{
  "mode": "Audit",
  "preset": "Normal",
  "reportRelativePath": "reports/economy-audit.json",
  "repeatedRaidLootDecay": false,
  "rarity": {
    "commonMinSources": 8,
    "uncommonMinSources": 4,
    "rareMinSources": 2
  },
  "customAuditPolicy": {
    "questRewardVsVanillaMedianWarnMultiple": 3.0,
    "restartableRewardVsVanillaMedianWarnMultiple": 1.5,
    "duplicateTraderSourcesWarnCount": 6
  },
  "manualOverrides": {}
}
```

Preset audit policies currently resolve as follows:

| Preset | normal quest warning | restartable warning/error | trader-source saturation |
| --- | ---: | ---: | ---: |
| Easy | 5.0x vanilla median | 2.5x vanilla median | 8 traders |
| Normal | 3.0x vanilla median | 1.5x vanilla median | 6 traders |
| Hard | 2.0x vanilla median | 1.25x vanilla median | 4 traders |
| Custom | `customAuditPolicy` | `customAuditPolicy` | `customAuditPolicy` |

Manual overrides are keyed by item template ID. Example:

```json
"manualOverrides": {
  "0123456789abcdef01234567": {
    "rarity": "Rare",
    "ignore": false,
    "note": "curated progression exception"
  }
}
```

## Report

Default output:

`reports/economy-audit.json`

The schema-2 report records:

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
- reward records without handbook prices;
- vanilla non-restartable median and P90 reward-value benchmark;
- vanilla restartable median where samples exist;
- reward-value outlier findings;
- trader-source saturation findings;
- exact policy thresholds used for the report.

The report path is constrained to remain inside the mod directory.

## Benchmark limitations

The current benchmark deliberately uses **handbook value of item rewards**. It is deterministic and useful for detecting gross reward inflation, but it is not yet a complete reward budget. It does not yet fully price:

- trader standing;
- skill/experience rewards;
- unlock value;
- FIR/progression utility;
- quest difficulty, duration, risk, or prerequisite depth;
- repeatable replacement rate;
- flea scarcity or world-loot rarity.

Those dimensions belong in later MVP/Stage 2 scoring before Economy is allowed to enforce changes.

## Rarity model in this slice

The first classifier is intentionally simple and deterministic: it uses the number of distinct trader and quest-reward acquisition sources for an item. This is an audit signal, not the final economic rarity model.

The architecture keeps acquisition scanning, benchmarking, findings, presets, and manual overrides separate so later sources and weighting can extend the model without replacing the final-DB scanner.

## Planned next slices

MVP remainder:

- richer acquisition/value weighting;
- quest difficulty/time/risk/progression scoring;
- broader reward-budget model beyond handbook-priced items;
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

`Issue -> feature branch -> PR -> Economy-specific CI -> runtime gate when required -> merge -> cleanup`

Do not use the repository-wide publisher for ordinary Economy development validation.
