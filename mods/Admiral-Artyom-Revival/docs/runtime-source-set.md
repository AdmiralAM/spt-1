# Admiral Artyom Revival — runtime source set

Admiral Artyom Revival is reconstructed from one authoritative upstream WTT-Artem runtime installation split for transport into seven independent ZIP archives.

## Core/base

`artem main 1.zip` is the authoritative upstream runtime/core package. It contains the trader database, assort, custom items, clothing, quest zones, quests, locales, quest images, trader image, `bundles.json`, and the legacy SPT 4.0-era `WTT-Artem.dll`.

The legacy DLL is evidence only and must never be copied into a 4.1.4 package. `tools/import_runtime_core.py` imports the remaining runtime content into `server/Resources` and applies only proven deterministic repairs.

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

Current exact-path audit evidence:

- 239 entries are declared by `bundles.json`;
- all 239 declared paths are physically present across the six bundle archives;
- 262 physical `.bundle` files exist across the six archives;
- 23 physical files are outside the manifest;
- 20 of those 23 have basenames not referenced anywhere in the manifest;
- 3 are path-collision copies in `Hands/` named `artem_top_29.bundle`, `artem_top_30.bundle`, and `artem_top_31.bundle`, while the manifest selects the distinct `Tops/` versions;
- custom Artem gear is not automatically injected into PBS pools.

No out-of-manifest file is deleted until runtime/package validation proves it stale.

This document defines provenance only. It does not authorize removal or rebalance of original content.
