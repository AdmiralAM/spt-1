# SPT Belt/Armband Inventory

Inventory extension for SPT 4.1.x that gives compatible accessory containers
ArmBand/Belt/HeadBand-style inventory behavior. The validated runtime currently
uses `ArmBand` as its host; Belt and HeadBand are the next category foundations.
Current module version: **0.1.0**.

The implementation is generic: it does not require a specific Pack 'n' Strap item ID or content class. Plain armbands remain ordinary armbands; container-capable items receive the additional inventory behavior.

## Current architecture

The module now contains both client and server components:

- `src/` — client-side inventory presentation, slot integration, quick-move/priority behavior, and compatibility patches;
- `server/` — SPT 4.1.3 server integration, including belt persistence/death-policy behavior;
- `tests/` — regression coverage for the current client/server contracts;
- `tools/` — deterministic hot-path checks;
- `docs/` — compatibility archaeology and runtime contracts.
- `docs/accessory-taxonomy.md` — shared category, capacity and UI geometry contract.
- `docs/product-concept.md` — product purpose, balance rules and activation gates for ArmBand, Belt and HeadBand.

The client remains event/interaction driven; it does not use per-frame inventory polling. Runtime reflection is used where EFT/SPT client members are obfuscated and is resolved outside hot paths.

## Compatibility

The original Trenchfoot BeltSlot and Pack 'n' Strap implementations are archaeology/reference material, not runtime dependencies. If a legacy `Trenchfoot-BeltSlot.dll` is installed, remove or disable it before using this module to avoid two implementations patching the same inventory behavior.

The server project targets the SPT 4.1.3 `SPTushonka.*` packages. See [archaeology and SPT 4.1 mapping](docs/archaeology.md) for the retained behavior and rejected legacy patch patterns.

## Development status

Belt/Armband Inventory is under active development. Historical Phase 1 documentation records the original presentation contract and should be read as design history where later source/tests have expanded the behavior beyond that baseline.

Use the current source, regression tests, and active development branch as the authority for ongoing work. The `runtime-belt-armband` channel is the install-only publication channel for validated builds.
