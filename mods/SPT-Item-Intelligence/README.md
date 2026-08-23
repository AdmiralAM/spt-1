# SPT Item Intelligence

Independent item-intelligence mod for SPT 4.1.x. Current version: **0.9.0** (`Phase 17 live Value pipeline`).

Implemented pipeline:

- Phase 1–4: semantic registry, FIR-aware Safe-to-Sell model, SPT snapshot route and atomic requirement index;
- Phase 5–7: requirement-state projection, pricing/value tiers and combined presentation snapshots;
- Phase 8–10: allocation-conscious hover projection, cached formatting and event-driven hover controller;
- Phase 11: explicit on-demand decision diagnostics;
- Phase 12: runtime EFT ItemView pointer integration, cached template-id extraction and minimal hover overlay sink;
- Phase 13: one-shot live SPT snapshot bootstrap, quest/hideout/profile projection and visible diagnostic fallback;
- Phase 14: compact status marker anchored to the hovered EFT item card, with full details revealed only while the marker itself is hovered;
- Phase 15: approved `ⓘ` marker contract, requirement-priority colors, Minimal/Normal/Detailed/Full tooltip modes and live F12 appearance controls.
- Phase 16: event-driven registration of every live EFT `ItemView`, persistent per-cell markers and the corrected top-left default position.
- Phase 17: schema-v2 server snapshot with flea, trader and handbook values, item dimensions and stack-aware Value/per-slot presentation.

Phase 17 requests `/spt-item-intelligence/v2/snapshot` once in the background, projects the live profile and price table into immutable requirement/presentation indexes, and refreshes registered markers after publication. There is no update-loop or network polling. Until the snapshot is ready—or when a runtime dependency is unavailable—the overlay exposes an explicit diagnostic state instead of silently displaying nothing or inventing a sell recommendation.

Phase 16 keeps a marker inside every registered live `ItemView` rectangle. `ItemView.Init` registers the view and `ItemView.Kill` removes it, so markers persist without inventory scans, polling or per-item GameObjects. Hovering an item body does nothing; the tooltip is rendered only when the cursor is inside that item's marker.

Phase 15 makes Value and concrete requirement counts the primary tooltip content: `Quest Now`, `Quest Later`, `Hideout` and `Keep ×N`. `SAFE TO SELL`/surplus is omitted from Normal and Detailed modes and appears only as low-priority Full-mode detail. Marker color follows requirement priority—not price or sell value—and every marker retains the same `ⓘ` glyph. F12 exposes tooltip mode, marker size, opacity, X/Y offsets and separate requirement-state colors.

Phase 17 supplies the previously missing live Value data. The server publishes flea, highest trader and handbook fallback unit values plus template dimensions once per snapshot. The client selects the best source, applies the live item stack count and exposes both total Value and ₽/slot without allowing price to affect marker color.

The client discovers SPT `RequestHandler` and Newtonsoft at runtime. Missing APIs, incompatible JSON, an unavailable profile or UI drawing failures remain contained and do not prevent the game or plugin from loading.

Install the complete `runtime-item-intelligence` channel into the SPT root. It contains:

- `BepInEx/plugins/SPT Item Intelligence/SPT Item Intelligence.dll`;
- `SPT_Runtime/user/mods/SPT Item Intelligence Server/SPT Item Intelligence Server.dll`.

Contracts: [Phase 1 registry](docs/phase1.md) · [Phase 2 Safe-to-Sell](docs/phase2.md) · [Phase 3 data transport](docs/phase3.md) · [Phase 4 requirement index](docs/phase4-requirement-index.md) · [Phase 12 EFT hover integration](docs/phase12-eft-hover-integration.md) · [Phase 13 live bootstrap](docs/phase13-live-requirement-bootstrap.md) · [Phase 14 marker interaction](docs/phase14-marker-interaction.md) · [Phase 15 requirement marker UX](docs/phase15-requirement-marker-ux.md) · [Phase 16 persistent markers](docs/phase16-persistent-item-markers.md) · [Phase 17 live Value](docs/phase17-live-value.md).
