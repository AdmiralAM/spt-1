# SPT Item Intelligence

Independent item-intelligence mod for SPT 4.1.x. Current version: **0.4.0** (`Phase 4`).

This is not part of SPT Tactical HUD and does not modify HUD visuals or HUD runtime behavior.

- Phase 1: immutable item semantic registry.
- Phase 2: FIR-aware Safe-to-Sell decision model.
- Phase 3: native SPT 4.1.2 server companion and versioned requirement-data snapshot route.
- Phase 4: atomic per-template requirement index with current/future quest, hideout, FIR, keep and surplus facts.

The server companion exposes `/spt-item-intelligence/v1/snapshot` and returns one schema-versioned envelope containing the current PMC profile plus quest and hideout tables. The route is on-demand, cancellation-aware and performs no periodic scans. A missing profile is an explicit retryable state instead of malformed data.

Phase 4 still adds no tooltip, checkmark, item color, automatic sale/movement or Tactical HUD integration. It converts a successfully projected snapshot into a compact O(1) lookup index and atomically retains the last valid generation when refresh data is unavailable.

Install the complete `runtime-item-intelligence` channel into the SPT root. It contains both:

- `BepInEx/plugins/SPT Item Intelligence/SPT Item Intelligence.dll`;
- `SPT_Runtime/user/mods/SPT Item Intelligence Server/SPT Item Intelligence Server.dll`.

Contracts: [Phase 1 registry](docs/phase1.md) · [Phase 2 Safe-to-Sell](docs/phase2.md) · [Phase 3 data transport](docs/phase3.md) · [Phase 4 requirement index](docs/phase4-requirement-index.md).
