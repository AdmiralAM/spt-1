# Admiral Artyom Revival — repair log

This log records only repairs backed by direct runtime/content evidence for Admiral Artyom Revival. Speculative cleanup belongs in audit notes, not in the compatibility patch set.

## R1 — SPT 4.1 server lifecycle

**Problem:** the archived runtime package contains a legacy SPT 4.0-era `WTT-Artem.dll` built for the old metadata/lifecycle contract.

**Repair:** rebuild the server component for .NET 10 using `IModMetadata` and `IOnLoad.OnLoadAsync(CancellationToken)`.

**Status:** implemented and runtime validated on SPT 4.1.3.

## R2 — CommonLib dependency line

**Problem:** upstream Artem content relies on WTT CommonLib item, quest-zone, quest, clothing and bundle services.

**Repair:** use WTT-ServerCommonLib `3.0.6` as the maintained build/runtime baseline while retaining metadata compatibility with the `~3.0.0` CommonLib line.

**Status:** build and runtime validated with Server/Client CommonLib 3.0.6.

## R3 — broken quest thumbnail extension

**Problem:** quest `673f0f4d219756e158de7ab3` references `ARTT_3thumbnail.jpg`, while the runtime package contains `ARTT_3thumbnail.png`.

**Repair:** change only the quest image path extension from `.jpg` to `.png`.

**Status:** deterministic importer repair; content validation and runtime PASS.

## R4 — missing Sweden Patch trader offer

**Problem:** quest `Expanding Wardrobe` rewards an `AssortmentUnlock` targeting offer `675267324707588d57c75972`, TPL `6752641b1470fc33b675d59a` (Sweden Patch), but the offer is absent from `db/assort.json`.

**Repair:** restore the missing LL1 root offer using the neighboring authored patch-offer pattern: 200 RUB, stock 500, buy limit 3.

**Status:** deterministic importer repair; unlock/assort validation PASS.

## R5 — explicit quest unlock / QuestAssort divergence

**Problem:** three explicit Success `AssortmentUnlock` rewards were not represented by matching `QuestAssort.success` mappings.

**Repair:** additively synchronize explicit authored rewards to QuestAssort without removing QuestAssort-only entries.

Affected authored rewards include `Expanding Wardrobe`, `Puppets`, and `Gathering Information - Part 3`.

**Status:** 40/40 explicit Success unlock rewards map to the correct quest after repair.

## R6 — SPT 4.1.3 armor preset compatibility

**Problem:** runtime logs showed six upstream Artem `Item deserialization error` failures:

- two DevTac Ronin variants used `Helmet_eyes` / `Helmet_jaw` preset slot names while their templates declared lowercase `helmet_eyes` / `helmet_jaw`;
- OPENLAND HEXAGON referenced `Soft_armor_left` / `soft_armor_right` preset children without defining those slots on the custom template.

**Repair:** normalize the Ronin preset slot casing and restore the two OPENLAND side soft-armor slot definitions required by the authored preset.

**Status:** regression test validates root-child preset slot references; the accepted r5 runtime test no longer reproduces the six deserialization failures.

## R7 — Russian localization compatibility

**Problem:** the legacy quest locale filename `artemenglish.json` is not a valid CommonLib locale code and upstream Artem lacked Russian coverage.

**Repair:** normalize quest locales to `en.json` / `ru.json`; add Russian source for all 204 quest keys, all 131 custom item entries, all 64 clothing entries and trader identity text.

**Status:** localization regression PASS and user runtime verification PASS.

## Deferred, not repaired

- physical bundles outside `bundles.json` and duplicate archive basenames remain audit-only candidates;
- economy rebalance;
- Core/campaign-required/optional asset split;
- PBS integration (explicitly disabled unless designed later).
