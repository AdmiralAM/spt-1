# SPT Economy

SPT Economy is a server-side economy audit/enforcement workstream for SPT 4.1.x.

## Current status

This module is in the first MVP slice: **read-only final-database audit foundation**.

Implemented now:

- final DB scan at the SPT 4.1 `PostLoad` lifecycle boundary;
- trader acquisition scan from final trader assort roots;
- quest reward acquisition scan;
- deterministic per-item acquisition aggregation;
- initial acquisition-density rarity classification;
- `Easy / Normal / Hard / Custom` preset contract;
- `Off / Audit / Enforce` mode contract;
- deterministic JSON report;
- manual item overrides;
- future `RepeatedRaidLootDecay` policy represented but disabled by default.

`Enforce` is intentionally fail-closed in this slice: selecting it produces an explicit warning and still performs only the read-only audit. No economy mutation is implemented yet.

## SPT 4.1 architecture boundary

SPT 4.1 removed the old `DatabaseService.GetTables()` model. Database tables are injected directly. Economy therefore consumes the final `TemplateTable` and `TradersTable` instances and runs after normal load work at `OnLoadOrder.PostLoad + 1000`.

This is deliberate: the audit is intended to observe trader, quest, and item data after other content mods have completed normal registration rather than scanning an early vanilla-only snapshot.

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
  "manualOverrides": {}
}
```

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

The report currently records:

- total template items;
- quest count;
- trader count;
- total trader assort records;
- items with known trader/quest acquisition;
- trader-source and quest-reward source edges;
- deterministic per-item trader and quest source lists;
- initial rarity classification;
- applied manual override metadata.

The report path is constrained to remain inside the mod directory.

## Rarity model in this slice

The first classifier is intentionally simple and deterministic: it uses the number of distinct trader and quest-reward acquisition sources for an item. This is an audit foundation, not the final economic rarity model.

Later MVP work will add value, progression, repeatability, quest difficulty/time/risk, vanilla reward benchmarks, and source-specific weights before enforcement is enabled.

## Planned next slices

MVP remainder:

- richer acquisition/value model;
- trader audit findings;
- quest reward audit findings;
- vanilla reward benchmark;
- meaningful preset parameterization;
- enforcement rules with explicit mutation reports.

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
