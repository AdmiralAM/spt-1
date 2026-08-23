# SPT Item Intelligence

Independent item-intelligence mod for SPT 4.1.x. Current version: **0.10.1** (`Phase 18 corrective patch`).

Implemented pipeline:

- Phase 1–4: semantic registry, FIR-aware Safe-to-Sell model, SPT snapshot route and atomic requirement index;
- Phase 5–7: requirement-state projection, pricing/value tiers and combined presentation snapshots;
- Phase 8–10: allocation-conscious hover projection, cached formatting and event-driven hover controller;
- Phase 11: explicit on-demand decision diagnostics;
- Phase 12: runtime EFT ItemView pointer integration, cached template-id extraction and minimal hover overlay sink;
- Phase 13: one-shot live SPT snapshot bootstrap, quest/hideout/profile projection and visible diagnostic fallback;
- Phase 14: compact status marker anchored to the hovered EFT item card, with full details revealed only while the marker itself is hovered;
- Phase 15: approved `ⓘ` marker contract, requirement-priority colors, Minimal/Normal/Detailed/Full tooltip modes and live F12 appearance controls.
- Phase 16: event-driven registration of every live EFT `ItemView`/`ItemCell` and persistent per-cell markers; the current Discussion default is upper-left.
- Phase 17: schema-v2 server snapshot with flea, trader and handbook values, item dimensions and stack-aware Value/per-slot presentation.
- Phase 18: real winning trader/source labels plus named quest and hideout requirement breakdowns in deeper tooltip modes.

Phase 17 requests `/spt-item-intelligence/v2/snapshot` once in the background, projects the live profile and price table into immutable requirement/presentation indexes, and refreshes registered markers after publication. There is no update-loop or network polling. Until the snapshot is ready—or when a runtime dependency is unavailable—the overlay exposes an explicit diagnostic state instead of silently displaying nothing or inventing a sell recommendation.

Phase 16 keeps a child UI marker inside every registered live `ItemView`/`ItemCell`. Supported lifecycle initialization registers the view and cleanup removes it, so markers persist without inventory scans or polling. Hovering an item body does nothing; the tooltip is rendered only when the cursor is inside that item's marker.

Phase 15 makes Value and concrete requirement progress the primary tooltip content: `Quest Now: owned/required`, `Hideout: owned/required`, `Quest Later: owned/required` and `Keep ×N`; fulfilled rows retain a `✓`. `ID`, `SAFE TO SELL` and surplus never appear in user-facing modes. Marker color follows unmet requirement priority (`Quest Now → Hideout → Quest Later → Default`), never price, sell value or generic Keep. Every marker retains the same `ⓘ` glyph. F12 exposes tooltip mode, marker size, opacity, X/Y offsets and native color selectors for the four marker states.

The 0.10.1 correction attaches the marker to each supported item cell, removes the global per-frame marker scan, and opens the tooltip only while the pointer is over the marker itself. It also handles `ItemCell`, parameterized lifecycle methods, nested item contexts, completed quest conditions, constructing/custom hideout areas, and the SPT 4.1.2 Bulbex requirement (`619cbfeb6b8a1b37a54eebfa`, area type 4, stage 2).

Phase 17 supplies the previously missing live Value data. The server publishes flea, highest trader and handbook fallback unit values plus template dimensions once per snapshot. The client selects the best source, applies the live item stack count and exposes both total Value and ₽/slot without allowing price to affect marker color.

Phase 18 replaces the generic `Trader` label with the actual highest-price trader name, displays the winning source and unit price in Detailed/Full modes, and preserves concrete requirement targets such as `Now: Signal - Part 1 ×2 · FIR` and `Hideout: Workbench L1 ×3`. Completed quest conditions are excluded. Detailed mode stays bounded to three target lines plus a remainder count; Full mode exposes the complete target list. All formatting remains snapshot-backed and cached outside the render hot path.

The client discovers SPT `RequestHandler` and Newtonsoft at runtime. Missing APIs, incompatible JSON, an unavailable profile or UI drawing failures remain contained and do not prevent the game or plugin from loading.

Install the complete `runtime-item-intelligence` channel into the SPT root. It contains:

- `BepInEx/plugins/SPT Item Intelligence/SPT Item Intelligence.dll`;
- `SPT_Runtime/user/mods/SPT Item Intelligence Server/SPT Item Intelligence Server.dll`.

Contracts: [Phase 1 registry](docs/phase1.md) · [Phase 2 Safe-to-Sell](docs/phase2.md) · [Phase 3 data transport](docs/phase3.md) · [Phase 4 requirement index](docs/phase4-requirement-index.md) · [Phase 12 EFT hover integration](docs/phase12-eft-hover-integration.md) · [Phase 13 live bootstrap](docs/phase13-live-requirement-bootstrap.md) · [Phase 14 marker interaction](docs/phase14-marker-interaction.md) · [Phase 15 requirement marker UX](docs/phase15-requirement-marker-ux.md) · [Phase 16 persistent markers](docs/phase16-persistent-item-markers.md) · [Phase 17 live Value](docs/phase17-live-value.md) · [Phase 18 tooltip intelligence](docs/phase18-tooltip-intelligence.md).
