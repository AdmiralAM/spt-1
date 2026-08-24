# Artem Revival repair log

This log records only repairs backed by direct runtime-content evidence. Speculative cleanup belongs in audit notes, not in the revival patch set.

## R1 — SPT 4.1 server lifecycle

**Problem:** the runtime package contains a legacy SPT 4.0-era `WTT-Artem.dll` built for the old metadata/lifecycle contract.

**Repair:** rebuild the server component for .NET 10 using `IModMetadata` and `IOnLoad.OnLoadAsync(CancellationToken)`, based on the upstream WTT-Artem 4.1 migration.

**Status:** implemented in `server/`.

## R2 — CommonLib dependency line

**Problem:** Artem relies on WTT CommonLib item, quest-zone, quest, and clothing services. The revival target is SPT 4.1.3.

**Repair:** build against `WTT-ServerCommonLib 3.0.4` while retaining metadata compatibility with the `~3.0.0` CommonLib line.

**Status:** implemented in the server project.

## R3 — broken quest thumbnail extension

**Problem:** quest `673f0f4d219756e158de7ab3` references `ARTT_3thumbnail.jpg`, while the runtime package contains `ARTT_3thumbnail.png`.

**Repair:** change only the quest image path extension from `.jpg` to `.png`.

**Status:** deterministic importer repair; covered by content integrity validation.

## R4 — missing Sweden Patch trader offer

**Problem:** quest `Expanding Wardrobe` rewards an `AssortmentUnlock` targeting offer `675267324707588d57c75972`, TPL `6752641b1470fc33b675d59a` (Sweden Patch), but the offer is absent from `db/assort.json`. The custom item itself exists.

**Repair:** restore the missing root offer using the same LL1 patch-offer pattern used by the adjacent campaign unlocks:

- root offer id: `675267324707588d57c75972`;
- TPL: `6752641b1470fc33b675d59a`;
- currency: RUB;
- price: 200;
- stock: 500;
- buy limit: 3;
- loyalty level: 1.

This preserves the authored quest reward instead of deleting it.

**Status:** deterministic importer repair; covered by content integrity validation.

## Deferred, not repaired

- 20 physical bundles not referenced by `bundles.json`;
- duplicate `artem_top_29/30/31.bundle` basenames across archive groups;
- economy rebalance;
- Core/campaign-required/optional asset split;
- PBS integration (explicitly disabled unless designed later).
