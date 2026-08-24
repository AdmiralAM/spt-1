# Artem Revival runtime source set

The revival is reconstructed from one authoritative Artem runtime installation split for transport into seven independent ZIP archives.

## Core/base

`artem main 1.zip` is the authoritative runtime/core package. It contains the trader database, assort, custom items, clothing, quest zones, quests, locales, quest images, trader image, `bundles.json`, and the legacy SPT 4.0-era `WTT-Artem.dll`.

The legacy DLL is evidence only and must never be copied into a 4.1.3 package. `tools/import_runtime_core.py` imports the remaining runtime content into `server/Resources` and applies only proven deterministic repairs.

## Bundles folder

The following six archives together represent the original Artem `Bundles` directory:

- `artem bundles hand.zip`
- `Pants.zip`
- `Tops.zip`
- `bundles 1.zip`
- `bundles 2.zip`
- `bundles 3.zip`

These archives are intentionally not committed to this repository because they are large binary Unity assets. Their contents are audited against `bundles.json`; the revival package must preserve required bundle paths exactly unless an explicit asset migration is performed.

## Proven baseline

Current audit evidence:

- 239 entries are declared by `bundles.json`;
- all 239 declared bundle basenames are present across the six bundle archives;
- 20 physical bundle files are not referenced by the manifest and require later classification;
- duplicate basenames exist for `artem_top_29.bundle`, `artem_top_30.bundle`, and `artem_top_31.bundle` and require provenance review before cleanup;
- custom Artem gear is not automatically injected into PBS pools.

This document defines provenance only. It does not authorize removal or rebalance of original content.
