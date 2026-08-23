# SPT Belt/Armband Inventory

Standalone client UI adapter for SPT 4.1.x. Phase 1 makes any container item equipped in `ArmBand` appear as a normal belt inventory row without adding items, changing profiles or depending on a specific content pack.

## Phase 1

- container armband only; empty/plain armbands are not duplicated;
- belt row above or below Pockets (F12, restart required);
- all EFT screens backed by `ContainersPanel`;
- runtime reflection for SPT 4.1 obfuscated members;
- no per-frame polling or inventory-controller replacement;
- safe conflict guard for legacy Trenchfoot-BeltSlot.

Pack 'n' Strap compatibility is item-shape based. If that pack installs `Trenchfoot-BeltSlot.dll`, remove or disable the legacy DLL before using this replacement.

Phase 1 intentionally defers ctrl-click priority and same-screen live refresh. Close and reopen inventory after equipping or removing a belt.

See [the runtime contract](docs/phase1-runtime-contract.md) for exact scope and acceptance checks.

The [archaeology note](docs/archaeology.md) records which old behavior was retained and which fragile patches were removed for SPT 4.1.
