# Economy Admiral

**Economy Admiral** is an SPT 4.1.3 server-side economy difficulty layer. It makes money, loot and progression retain value longer by applying one coherent preset across quest rewards, traders, flea and world loot.

Version: **0.1.0**.

## What it changes

When enabled in `Enforce` mode, Economy Admiral can apply four economic clusters:

### Quests
- reduces excessive quest item reward stacks;
- applies bounded XP reward pressure;
- applies bounded trader-standing reward pressure;
- uses stricter limits for restartable/repeatable reward outliers;
- preserves pristine/provenance protections and transactional rollback.

### Traders
- raises player purchase prices for supported currency offers;
- reduces effective payout when the player sells items to traders;
- does not replace trader progression, loyalty, quest locks or authored stock logic.

### Flea
- increases economic pressure on flea purchase pricing;
- tightens below-handbook/anti-arbitrage behavior;
- increases listing-fee pressure;
- remains configuration-based and does not implement a second flea simulator.

### Loot
- reduces native SPT loose-loot multipliers;
- reduces native SPT static/container-loot multipliers;
- preserves relative map differences instead of flattening every map to one fixed value.

## Basic configuration

For normal use only three settings matter:

```json
{
  "mode": "Enforce",
  "preset": "Normal",
  "enablePlayableEconomyBundle": true
}
```

`Off` disables Economy Admiral. `Audit` analyzes without changing the final SPT database. `Enforce` applies the selected economy policy.

With `enablePlayableEconomyBundle=true`, all enabled economic clusters use the selected preset automatically. The granular legacy feature flags can remain `false`.

Committed repository defaults remain safe:

```json
{
  "mode": "Audit",
  "preset": "Normal",
  "enablePlayableEconomyBundle": true
}
```

## Presets

Presets control **strength**, not which systems exist.

| Surface | Easy | Normal | Hard |
| --- | ---: | ---: | ---: |
| Trader purchase price | +5% | +15% | +30% |
| Trader sell payout | x0.95 | x0.85 | x0.70 |
| Flea listing-fee pressure | x1.10 | x1.25 | x1.50 |
| Loose loot | x0.95 | x0.85 | x0.70 |
| Static/container loot | x0.95 | x0.85 | x0.70 |
| Normal quest reward cap basis | 2.25x | 1.50x | 1.10x |
| Restartable quest reward cap basis | 1.75x | 1.15x | 1.00x |

`Normal` is the intended balanced starting point.

## Advanced cluster controls

Advanced configuration separates **where Economy Admiral acts** from **how strongly it acts**.

```json
{
  "enableQuestEconomyCluster": true,
  "enableTraderEconomyCluster": true,
  "enableFleaEconomyCluster": true,
  "enableLootEconomyCluster": true
}
```

A cluster set to `false` is a hard gate over that entire economic area.

Examples:

- `Normal + enableQuestEconomyCluster=false` keeps Normal trader/flea/loot pressure while quest economy remains untouched.
- `Hard + enableFleaEconomyCluster=false` keeps Hard quest/trader/loot pressure while flea remains vanilla/current-mod behavior.
- cluster OFF overrides any granular `true` feature flag inside that cluster.

Cluster mapping:

| Cluster | Surfaces |
| --- | --- |
| Quests | item stacks, XP, trader standing, restartable reward pressure, manual quest reward targets |
| Traders | purchase-price pressure, sell-payout pressure |
| Flea | purchase/base-price pressure, handbook/anti-arbitrage pressure, listing-fee pressure |
| Loot | loose loot, static/container loot |

If `enablePlayableEconomyBundle=false`, existing granular switches can still be used inside enabled clusters for selective/custom deployments.

## Custom preset

`Custom` uses the explicit numeric settings in `config.json`, including trader purchase/sell multipliers, flea price/listing-fee multipliers, loot scales and custom quest reward policy values.

The server remains the only source of economic calculations. A future client GUI must edit this same settings contract and must not implement a second economy engine.

## Quest enforcement safety

The production mutation path supports `Experience`, `TraderStanding` and bounded `ItemRewardStackCount` for one structurally unambiguous existing Success reward stack.

Provenance rules:

- `PristineUnchanged`: never mutate;
- `ModAdded`: only supported flagged/manual dimensions may mutate;
- `PristineModified`: only dimensions proven changed may mutate;
- unknown provenance: block;
- manual exact targets do not bypass provenance or dimension safety.

Item-stack normalization never replaces `_tpl`, creates/deletes reward records, removes the last reward item to satisfy a budget or rewrites structural quest fields. `Reward.Value` and `Upd.StackObjectsCount` are updated and rolled back together.

Every active reward mutation shares a transaction contract: deterministic plan, journal originals before first write, apply, verify exact targets, rollback entire batch on any error, then verify rollback.

## Admiral Trader compatibility

Admiral Trader is an **optional integration**, not a dependency.

If Admiral Trader is absent, Economy Admiral runs standalone. If the maintained Admiral Trader contract is installed, Economy Admiral validates its explicit identity/schema/offer classes and treats compatibility fail-closed on drift. It does not implement or replace Admiral Trader's own economy engine.

Maintained Gameplay Alpha v4 identity includes product `Admiral Trader`, modGuid `com.admiralam.spt.admiraltrader` and trader ID `d5c27bb3169f8dfbc13f6b69`.

## Runtime reports and validators

Runtime reports remain available for development/diagnostics, including audit, quest analysis/provenance, enforcement plan, source pressure, health, optional Admiral Trader adapter evidence and combined runtime evidence.

Packaged developer validators:

- `Validate-Runtime.ps1` — Audit/read-only contract;
- `Validate-Enforce.ps1` — transactional Enforce contract;
- `Validate-Beta.ps1` — combined physical release-candidate gate.

These validators are development/release tools, not part of normal player interaction.

## Installation and publication

The maintained install-only channel is `runtime-economy-admiral`. Its package root is directly copyable into the SPT root and contains:

`SPT_Runtime/user/mods/Economy Admiral/`

Compile boundary: `SPTarkov.Server.Core 4.1.2` / .NET 10. Physical target: **SPT 4.1.3**.

Runtime load order keeps immutable pristine capture early and applies final-DB analysis/economy changes at `PostLoad + 1000`. There is no permanent raid/frame polling.
