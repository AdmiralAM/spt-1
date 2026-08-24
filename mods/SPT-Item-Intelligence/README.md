# SPT Item Intelligence

Standalone item-intelligence module for SPT 4.1.x. Stable client/server version: **0.10.1**. Physical SPT 4.1.3 runtime acceptance is complete; active feature development is closed. The module is now maintenance-only unless a concrete runtime defect or explicitly approved future enhancement justifies reopening work.

## Purpose

Item Intelligence attaches persistent information markers to supported EFT item cells and projects player-relative item requirements and value data without per-frame inventory/network polling.

Current presentation contracts include:

- persistent per-item `ⓘ` markers;
- requirement-priority states for current quests, hideout needs, future quests, and default/no unmet requirement;
- compact Minimal / Normal / Detailed / Full tooltip modes;
- owned-versus-required counts and concrete quest/hideout targets;
- stack-aware item value;
- Flea or Best Trader value-source selection;
- semantic requirement colors;
- optional soft radial marker halo using one shared texture layer;
- F12 controls for marker appearance and tooltip presentation.

Marker state is requirement-driven, not price-driven. Value data does not change requirement classification.

## Data path

The server publishes a bounded snapshot of authoritative SPT profile/database data. The client fetches that snapshot outside the render hot path, builds immutable/cached requirement and presentation indexes, and refreshes registered item markers when new data is published.

The requirement path is:

`SPT profile/database → server snapshot → serialized payload → client bootstrap → requirement index → outstanding calculation → presentation classification`

Runtime diagnostics are used only when a boundary in that path must be proven. Unit tests alone are not treated as proof of live SPT data flow.

## UI lifecycle and performance

Supported `ItemView`/`ItemCell` lifecycle hooks register a child marker on each live item cell and remove it during cleanup. Hovering the item body does not open the Item Intelligence tooltip; the tooltip is associated with the marker itself.

Network requests, reflection discovery, requirement aggregation, and text formatting are kept out of per-frame render paths. Cached state is invalidated only when the relevant source data or UI settings change.

The rejected legacy marker glow based on Unity UI `Outline` duplication was removed. The accepted halo is a single soft radial image layer rendered behind the glyph, backed by one shared/static texture and without per-frame texture generation or multi-pass glyph duplication.

## Version and naming

The stable release uses one synchronized module version across both halves:

- client: **SPT Item Intelligence 0.10.1**;
- server: **SPT Item Intelligence Server 0.10.1**.

Runtime folder and assembly names are intentionally retained for installation compatibility.

## Installation

The install-only [`runtime-item-intelligence`](https://github.com/AdmiralAM/spt-1/tree/runtime-item-intelligence) channel contains the client and server components:

- `BepInEx/plugins/SPT Item Intelligence/SPT Item Intelligence.dll`
- `SPT_Runtime/user/mods/SPT Item Intelligence Server/SPT Item Intelligence Server.dll`

## Documentation

The `docs/phase*.md` files preserve implementation contracts and design history for earlier milestones. They are archaeology/regression references; current source, tests, runtime evidence, this README, and repository rules take precedence where later development supersedes an earlier phase description.

Key contracts: [registry](docs/phase1.md) · [data transport](docs/phase3.md) · [requirement index](docs/phase4-requirement-index.md) · [live bootstrap](docs/phase13-live-requirement-bootstrap.md) · [marker UX](docs/phase15-requirement-marker-ux.md) · [persistent markers](docs/phase16-persistent-item-markers.md) · [live value](docs/phase17-live-value.md) · [tooltip intelligence](docs/phase18-tooltip-intelligence.md) · [hot-path optimization](docs/hotpath-optimization-01.md).
