# SPT Item Intelligence

Independent item-intelligence mod for SPT 4.1.x. Current version: **0.5.0** (`Phase 12 integration preview`).

This product remains independent from SPT Tactical HUD.

Implemented pipeline:

- Phase 1–4: semantic registry, FIR-aware Safe-to-Sell model, SPT snapshot route and atomic requirement index;
- Phase 5–7: requirement-state projection, pricing/value tiers and combined presentation snapshots;
- Phase 8–10: allocation-conscious hover projection, cached formatting and event-driven hover controller;
- Phase 11: explicit on-demand decision diagnostics;
- Phase 12: runtime EFT ItemView pointer integration, cached template-id extraction and minimal hover overlay sink.

Phase 12 discovers EFT `ItemView` pointer methods at runtime, patches enter/exit through the Harmony instance already supplied by BepInEx, and routes template ids into the existing cached presentation pipeline. The integration has no polling loop. Missing Harmony, changed EFT types, unknown item shapes or UI drawing failures disable only the hover bridge and do not prevent the game or plugin from loading.

The server companion exposes `/spt-item-intelligence/v1/snapshot` as an on-demand, cancellation-aware SPT 4.1 route. Physical in-game validation remains the final release gate; software work continues independently until that point.

Install the complete `runtime-item-intelligence` channel into the SPT root. It contains:

- `BepInEx/plugins/SPT Item Intelligence/SPT Item Intelligence.dll`;
- `SPT_Runtime/user/mods/SPT Item Intelligence Server/SPT Item Intelligence Server.dll`.

Contracts: [Phase 1 registry](docs/phase1.md) · [Phase 2 Safe-to-Sell](docs/phase2.md) · [Phase 3 data transport](docs/phase3.md) · [Phase 4 requirement index](docs/phase4-requirement-index.md) · [Phase 12 EFT hover integration](docs/phase12-eft-hover-integration.md).
