# Item Intelligence Admiral

Standalone item-intelligence module for SPT 4.1.x. Stable release: **v1.0.0**. Physical SPT 4.1.3 runtime acceptance is complete.

## Current development authority

The accepted **v1.0.0** line remains the stable rollback/reference baseline. The user-authorized 2026-09-11 amendment in Issue #338 supersedes the former separate-Item-Valuation product boundary: **v1.1 delivers one consolidated Item Intelligence Admiral package**, with optional background coloring and no required external AQC, Item Valuation, Task Item Indicator or CompatibilityHighlighter installation.

Start with the requirement/FIR/hideout truth model and deterministic tests, then modular presentation. AQC is the primary visual-quality benchmark: one crisp contextual marker and one compact readable card, independently reimplemented without its GPL source/assets/text. Independent F12 layers cover markers, tooltips, active quests, future quests, hideout, value, craft/barter and background coloring. No hidden steady-state work in disabled modules. Preserve existing identifiers and rollback compatibility until consolidated physical acceptance.

The first implementation replaces maximum future-quest reserves with additive consumptive obligations, reserves FIR stock across all FIR-only quests before unrestricted consumption, and carries one immutable allocation into the tooltip. Completed hideout levels are excluded and repeated station/level entries across standard/custom tables are counted once. Current/future levels are identified explicitly. See [truth model](docs/v1.1-truth-model.md) for semantics and remaining acceptance work.

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
- compact trader, flea, and per-slot price rows without a redundant best-sell recommendation;
- the accepted Item Valuation palette for neutral ordinary items and its original penetration-based ammunition tiers, while keys and already-authored backgrounds retain their dedicated game/mod owner;
- price-amount bands: below 50k white, 50k+ green, 100k+ red, 250k+ gold;
- compact `Craft ×N` / `Barter ×N` relevance in regular-play and expanded modes;
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

Supported `ItemView`/`ItemCell` lifecycle hooks register live cells and remove them during cleanup. v1.1 uses one contextual badge and one card. The badge is the card's hover target; when no badge is present, only the native caption strip at the top of the item cell acts as the fallback target. Irrelevant value-only items do not receive a marker.

Network requests, reflection discovery, requirement aggregation, valuation work, and expensive text formatting are kept out of per-frame render paths. Cached state is invalidated only when the relevant source data or UI settings change. Full-mode display stripping and rich-text price/semantic strings use bounded caches so steady-state GUI repaint reuses prepared strings instead of rebuilding them every frame.

The v1.1 marker is an original procedural three-layer badge: configurable inner fill, a requirement-source ring, and a stock-coverage check. For example, an entirely missing hideout item has a blue hideout ring and red check. Fill color and 0–100% fill opacity are independent F12 controls. It does not copy an AQC sprite or depend on a font glyph. The optional halo also uses one shared/static texture. The information card follows the game's Russian or English UI language; other game languages use English.

Normal shows the selected F12 value source without per-slot value, plus compact requirement and craft/barter relevance. Detailed adds one nearest concrete target. Full shows both trader and flea values, per-slot value, every concrete target, craft and barter counts. The rounded card auto-fits short content up to its configurable maximum width.

Background ownership is cooperative. Item Intelligence restores the accepted Item Valuation palette for neutral ordinary items and ammunition penetration tiers. It yields keys to BetterKeys-style location coloring, existing authored/custom colors to their source, and temporary native highlights to CompatibilityHighlighter/EFT. Disabling the F12 module restores the native color captured for that cell.

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

For the v1.1 candidate, remove/disable external AllQuestsCheckmarks to verify replacement UX and remove/disable legacy Item Valuation so only the consolidated package owns background coloring. `Background Coloring (Valuation)` remains independently switchable in F12 and restores each native cell color when disabled. Restore the v1.0 package and legacy Item Valuation configuration to roll back.

## Documentation

The `docs/phase*.md` files preserve implementation contracts and design history for earlier milestones under the former development naming. They are archaeology/regression references; current source, tests, runtime evidence, this README, Issue #338, and repository rules take precedence where later development supersedes an earlier phase description.

Key contracts: [registry](docs/phase1.md) · [data transport](docs/phase3.md) · [requirement index](docs/phase4-requirement-index.md) · [live bootstrap](docs/phase13-live-requirement-bootstrap.md) · [marker UX](docs/phase15-requirement-marker-ux.md) · [persistent markers](docs/phase16-persistent-item-markers.md) · [live value](docs/phase17-live-value.md) · [tooltip intelligence](docs/phase18-tooltip-intelligence.md) · [hot-path optimization](docs/hotpath-optimization-01.md).
