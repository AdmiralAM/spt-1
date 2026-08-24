# SPT Item Intelligence

Standalone item-intelligence module for SPT 4.1.x. Current source version: **0.10.1**. The module remains under active development and runtime validation.

## Purpose

Item Intelligence attaches persistent information markers to supported EFT item cells and projects player-relative item requirements and value data without per-frame inventory/network polling.

Current presentation contracts include:

- persistent per-item `ⓘ` markers;
- requirement-priority states for current quests, hideout needs, future quests, and default/no unmet requirement;
- compact Minimal / Normal / Detailed / Full tooltip modes;
- owned-versus-required counts and concrete quest/hideout targets;
- stack-aware item value and value-per-slot information;
- flea, trader, and handbook value-source selection;
- F12 controls for marker appearance and tooltip mode.

Marker state is requirement-driven, not price-driven. Value data must not change the requirement color classification.

## Data path

The server publishes a bounded snapshot of authoritative SPT profile/database data. The client fetches that snapshot outside the render hot path, builds immutable/cached requirement and presentation indexes, and refreshes registered item markers when new data is published.

The intended requirement path is:

`SPT profile/database → server snapshot → serialized payload → client bootstrap → requirement index → outstanding calculation → presentation classification`

Runtime diagnostics are used when a boundary in that path must be proven. Unit tests alone are not treated as proof of live SPT data flow.

## UI lifecycle and performance

Supported `ItemView`/`ItemCell` lifecycle hooks register a child marker on each live item cell and remove it during cleanup. Hovering the item body does not open the Item Intelligence tooltip; the tooltip is associated with the marker itself.

Network requests, reflection discovery, requirement aggregation, and text formatting are kept out of per-frame render paths. Cached state is invalidated only when the relevant source data or UI settings change.

## Installation

The install-only [`runtime-item-intelligence`](https://github.com/AdmiralAM/spt-1/tree/runtime-item-intelligence) channel contains the client and server components:

- `BepInEx/plugins/SPT Item Intelligence/SPT Item Intelligence.dll`
- `SPT_Runtime/user/mods/SPT Item Intelligence Server/SPT Item Intelligence Server.dll`

## Documentation

The `docs/phase*.md` files preserve implementation contracts and design history for earlier milestones. They are useful for archaeology and regression intent, but current source, tests, active runtime diagnostics, and the latest workstream requirements take precedence where later development supersedes an earlier phase description.

Key contracts: [registry](docs/phase1.md) · [data transport](docs/phase3.md) · [requirement index](docs/phase4-requirement-index.md) · [live bootstrap](docs/phase13-live-requirement-bootstrap.md) · [marker UX](docs/phase15-requirement-marker-ux.md) · [persistent markers](docs/phase16-persistent-item-markers.md) · [live value](docs/phase17-live-value.md) · [tooltip intelligence](docs/phase18-tooltip-intelligence.md) · [hot-path optimization](docs/hotpath-optimization-01.md).
