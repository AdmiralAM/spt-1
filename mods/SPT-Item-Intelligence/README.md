# Item Intelligence Admiral

Standalone item-intelligence module for SPT 4.1.x. Stable release: **v1.0.0**. Physical SPT 4.1.3 runtime acceptance is complete.

## Current development authority

The accepted **v1.0.0** line remains the stable rollback/reference baseline while **v1.1** is reopened for a deliberately bounded UX/modularity pass.

Canonical v1.1 authority:

- roadmap / product contract: **Issue #338 — Item Intelligence Admiral — v1.1 modular UX / AQC-reference roadmap**;
- implementation branch: **`feature/item-intelligence-v1.1`**;
- single live implementation PR: **PR #341 — Item Intelligence Admiral v1.1 — modular UX and contextual intelligence**;
- ordinary implementation validation: **`Item Intelligence Admiral Validate`** only;
- one batched physical runtime gate occurs only after the recorded roadmap is automated-green.

The branch/PR are a prepared implementation workspace for the dedicated work/Codex flow. Repository coordination and roadmap maintenance do not constitute implementation acceptance. Historical Issues/PRs remain evidence only and are not parallel authorities.

## Purpose

Item Intelligence Admiral attaches persistent information markers to supported EFT item cells and projects player-relative item requirements and value data without per-frame inventory/network polling.

Current stable v1.0.0 presentation contracts include:

- persistent per-item `ⓘ` markers with optional soft radial halo;
- requirement-priority states: `Quest Now → Hideout → Quest Later → Default`;
- compact Minimal / Normal / Detailed / Full tooltip modes;
- owned-versus-required counts, FIR-aware quest allocation, `Keep ×N`, and concrete quest/hideout targets;
- Full-mode sell decision rows: `Best sell`, `Best trader`, `Flea`, and `Per slot`;
- best sell destination derived from already-precomputed trader/flea pricing state rather than recalculated during hover;
- price-amount bands: below 50k white, 50k+ green, 100k+ red, 250k+ gold;
- compact Full-only `Craft ×N` / `Barter ×N` relevance;
- fallback to the available Flea/Trader source when the preferred source has no price;
- semantic requirement colors;
- F12 controls for marker appearance and tooltip presentation.

Marker state remains requirement-driven, not price-driven. Price colors apply to valuation text only and do not change requirement classification.

The v1.1 target is recorded in Issue #338 and centers on modular F12 control, contextual attention-driven markers, a clearer quest/hideout/value information hierarchy, runtime/performance consolidation, and a later bounded compatibility-overlap review. AllQuestsCheckmarks is a behavioral/UX reference only; GPL source/assets are not implementation inputs.

## Data path

The server publishes one bounded snapshot of authoritative SPT profile/database data. The client fetches that snapshot outside the render hot path, builds immutable/cached requirement, price, relevance, and presentation indexes, and refreshes registered item markers from those cached states.

`SPT profile/database → server snapshot → serialized payload → client bootstrap → cached indexes → presentation classification`

Craft/barter relevance and trader/flea valuation are precomputed while the existing snapshot is built. No additional endpoint, hover request, per-frame inventory scan, database polling, or transaction execution is used.

## UI lifecycle and performance

Supported `ItemView`/`ItemCell` lifecycle hooks register a child marker on each live item cell and remove it during cleanup. Hovering the item body does not open the Item Intelligence Admiral tooltip; the stable v1.0.0 tooltip belongs to the marker itself.

Network requests, reflection discovery, requirement aggregation, valuation work, and expensive text formatting are kept out of per-frame render paths. Cached state is invalidated only when the relevant source data or UI settings change. Full-mode display stripping and rich-text price/semantic strings use bounded caches so steady-state GUI repaint reuses prepared strings instead of rebuilding them every frame.

The accepted v1.0.0 marker glow is a single soft radial image layer behind the glyph, backed by one shared/static texture. The rejected legacy Unity UI `Outline` duplication approach is not used.

## Version and naming

The official product name is **Item Intelligence Admiral**. The current stable release is **v1.0.0**; **v1.1** is active development and is not stable/published until its recorded runtime gate passes.

- stable client: **Item Intelligence Admiral v1.0.0**;
- stable server: **Item Intelligence Admiral Server v1.0.0**.

The existing source directory, namespace, GUID, endpoint, and `runtime-item-intelligence` branch are retained as technical compatibility identifiers. They are not the product name and should not be renamed casually because doing so would create unnecessary migration risk.

## Deferred items

`On You` is intentionally not part of v1. A one-shot profile implementation was removed because it could become stale after inventory/equipment changes; it should only return with a proven event-driven inventory lifecycle.

Wishlist, prerequisite-distance, encyclopedia-style item statistics, and direct selling actions remain optional future ideas, not active backlog or release blockers unless explicitly added to the canonical roadmap by user instruction.

## Installation

The install-only `runtime-item-intelligence` channel contains the accepted stable client and server components. The v1 package contract uses:

- `BepInEx/plugins/Item Intelligence Admiral/Item Intelligence Admiral.dll`
- `SPT_Runtime/user/mods/Item Intelligence Admiral Server/Item Intelligence Admiral Server.dll`

Development PR artifacts are test candidates only and do not replace the stable runtime channel before deliberate acceptance/publication.

## Documentation

The `docs/phase*.md` files preserve implementation contracts and design history for earlier milestones under the former development naming. They are archaeology/regression references; current source, tests, runtime evidence, this README, Issue #338, and repository rules take precedence where later development supersedes an earlier phase description.

Key contracts: [registry](docs/phase1.md) · [data transport](docs/phase3.md) · [requirement index](docs/phase4-requirement-index.md) · [live bootstrap](docs/phase13-live-requirement-bootstrap.md) · [marker UX](docs/phase15-requirement-marker-ux.md) · [persistent markers](docs/phase16-persistent-item-markers.md) · [live value](docs/phase17-live-value.md) · [tooltip intelligence](docs/phase18-tooltip-intelligence.md) · [hot-path optimization](docs/hotpath-optimization-01.md).
