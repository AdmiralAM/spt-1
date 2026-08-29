# Economy Admiral

**Economy Admiral** is an SPT 4.1.3 economy difficulty layer. It makes money, loot and progression retain value longer by applying one coherent profile across quest rewards, traders, flea and world loot.

Version: **0.1.0** during final product acceptance.

## Player quick start

Install the package into the SPT root and start SPT normally. The shipped profile is immediately playable:

- Economy Admiral: **enabled** (`Enforce`)
- Preset: **Normal**
- Quest Economy: **ON**
- Trader Economy: **ON**
- Flea Market Economy: **ON**
- Loot Economy: **ON**

Open the BepInEx/F12 Configuration Manager to change the preset or disable an economic cluster. F12 reads and writes the same server-owned `user/mods/Economy Admiral/config/config.json`; it is not a second economy engine. Saved changes apply after the next SPT server restart.

`Normal` is the recommended balanced starting point. `Easy` is lighter pressure, `Hard` is stronger pressure, and `Custom` uses the explicit Advanced numeric values.

## What it changes

### Quest Economy
- reduces excessive quest item reward stacks;
- applies bounded XP reward pressure;
- applies bounded trader-standing reward pressure;
- uses stricter limits for restartable/repeatable reward outliers;
- preserves pristine/provenance protections and transactional rollback.

### Trader Economy
- raises player purchase prices for supported currency offers;
- reduces effective payout when the player sells items to traders;
- preserves barter structure, trader progression, loyalty, quest locks and authored stock logic.

### Flea Market Economy
- increases economic pressure on flea purchase/base pricing;
- protects handbook/trader-value floors against obvious cheap-buy/resell arbitrage;
- increases listing-fee pressure;
- remains a pressure layer rather than replacing SPT flea generation.

### Loot Economy
- scales native SPT loose-loot multipliers downward;
- scales native SPT static/container-loot multipliers downward;
- preserves relative map/mod differences instead of flattening every map to one fixed value.

## Presets

Presets control **strength**. Advanced cluster switches control **where** Economy Admiral acts.

| Surface | Easy | Normal | Hard |
| --- | ---: | ---: | ---: |
| Trader purchase price | +5% | **+15%** | +30% |
| Trader sell payout | x0.95 | **x0.85** | x0.70 |
| Flea listing-fee pressure | x1.10 | **x1.25** | x1.50 |
| Loose loot | x0.95 | **x0.85** | x0.70 |
| Static/container loot | x0.95 | **x0.85** | x0.70 |
| Normal quest reward cap basis | 2.25x | **1.50x** | 1.10x |
| Restartable quest reward cap basis | 1.75x | **1.15x** | 1.00x |

The goal is not poverty for its own sake. The combined profile should keep raid loot, cash, barter items and progression decisions relevant for longer without turning SPT into grind-only play.

## F12 controls

### Basic
- **Mode** — Off / Audit / Enforce. Normal players use Enforce; Audit is diagnostic/read-only.
- **Preset** — Easy / Normal / Hard / Custom.
- **Playable Economy Bundle** — master high-level preset path.

### Advanced - Clusters
- **Quest Economy** — item stacks, XP, trader standing and repeatable reward pressure.
- **Trader Economy** — purchase-price and sell-payout pressure.
- **Flea Market Economy** — base-price/floor/anti-arbitrage and listing-fee pressure.
- **Loot Economy** — loose and static/container loot pressure.

A cluster set to OFF is a hard gate for that entire economic area. For example, `Normal + Quest Economy OFF` keeps Normal trader/flea/loot pressure while leaving quests untouched.

### Advanced - Custom
Custom exposes bounded numeric controls for trader purchase/sell multipliers, flea base/listing-fee multipliers, loose/static loot scales and quest item/XP/standing reward caps. These values are used by the `Custom` preset; Easy/Normal/Hard retain their maintained profile values.

The server remains the only source of economic calculations. The F12 plugin is only a settings client.

## Direct config equivalent

F12 is the intended user interface. The equivalent recommended server configuration is:

```json
{
  "mode": "Enforce",
  "preset": "Normal",
  "enablePlayableEconomyBundle": true,
  "enableQuestEconomyCluster": true,
  "enableTraderEconomyCluster": true,
  "enableFleaEconomyCluster": true,
  "enableLootEconomyCluster": true
}
```

Legacy granular feature switches remain available for specialized/manual deployments when the Playable Economy Bundle is disabled, but ordinary users should not need them.

## Quest enforcement safety

The production mutation path supports `Experience`, `TraderStanding` and bounded `ItemRewardStackCount` for structurally unambiguous existing Success rewards.

- `PristineUnchanged`: never mutate;
- `ModAdded`: only supported flagged/manual dimensions may mutate;
- `PristineModified`: only dimensions proven changed may mutate;
- unknown provenance: block;
- manual exact targets do not bypass provenance or dimension safety.

Item-stack normalization never replaces `_tpl`, creates/deletes reward records, removes the last reward item to satisfy a budget or rewrites structural quest fields. `Reward.Value` and `Upd.StackObjectsCount` are updated and rolled back together.

## Admiral Trader compatibility

Admiral Trader is an **optional integration**, not a dependency.

If Admiral Trader is absent, Economy Admiral runs standalone. If the maintained Admiral Trader contract is installed, Economy Admiral validates its explicit identity/schema/offer classes and treats compatibility fail-closed on drift. Economy Admiral does not duplicate Admiral Trader's own progression/store logic.

## Development diagnostics

Runtime reports and `Validate-Runtime.ps1`, `Validate-Enforce.ps1`, `Validate-Beta.ps1` remain packaged for development/release diagnosis. They are **not** part of normal player interaction and are not required to use Economy Admiral.

## Installation

The complete package owns only its own files:

- `SPT_Runtime/user/mods/Economy Admiral/` — server module, config and diagnostics;
- `BepInEx/plugins/Economy Admiral/Economy Admiral v0.1.0.dll` — F12 settings client.

It does not bundle or replace the BepInEx runtime itself.

Compile boundary: `SPTarkov.Server.Core 4.1.2` / .NET 10. Physical target: **SPT 4.1.3**. Runtime economy changes are applied during server database load; there is no permanent raid/frame economy polling.
