# SPT Item Intelligence

Independent item-intelligence mod for SPT 4.1.x. Current version: **0.2.0** (`Phase 2`).

This is not part of SPT Tactical HUD and does not modify HUD visuals or HUD runtime behavior.

Phase 1 provides the immutable item semantic registry. Phase 2 adds the first consumer of that registry: a deterministic **Safe-to-Sell** decision model with:

- active quest and FIR-aware deficits;
- selected-target and next-level hideout requirements;
- bounded near-future quest horizon;
- wishlist plus optional barter/craft scopes;
- explicit owned-total / owned-FIR allocation;
- protected count, missing count and actually saleable surplus;
- stable reason priority and compact `KEEP / SAFE TO SELL / NO REQUIREMENT` summaries.

Phase 2 is intentionally data-only: it adds no inventory scan, tooltip, checkmark, item color or Tactical HUD integration.

Install from the separate `runtime-item-intelligence` channel. The plugin lives at `BepInEx/plugins/SPT Item Intelligence/` and has its own GUID, DLL and version lifecycle.

Contracts: [Phase 1 registry](docs/phase1.md) · [Phase 2 Safe-to-Sell](docs/phase2.md).
