# Item Intelligence Admiral

Consolidated item-intelligence package for SPT 4.1.x. Current development release: **v1.2.1**.

## Stable authority

The accepted **v1.0.0** line remains available as the rollback/reference baseline. The user-authorized 2026-09-11 amendment in Issue #338 supersedes the former separate-Item-Valuation product boundary: **v1.1 delivers one consolidated Item Intelligence Admiral package**, with optional background coloring and no required external AQC, Item Valuation, Task Item Indicator or CompatibilityHighlighter installation.

Start with the requirement/FIR/hideout truth model and deterministic tests, then modular presentation. AQC is the primary visual-quality benchmark: one crisp contextual marker and one compact readable card, independently reimplemented without its GPL source/assets/text. Independent F12 layers cover markers, tooltips, active quests, future quests, hideout, value, craft/barter and background coloring. No hidden steady-state work in disabled modules. Preserve existing identifiers and rollback compatibility until consolidated physical acceptance.

The first implementation replaces maximum future-quest reserves with additive consumptive obligations, reserves FIR stock across all FIR-only quests before unrestricted consumption, and carries one immutable allocation into the tooltip. Completed hideout levels are excluded and repeated station/level entries across standard/custom tables are counted once. Current/future levels are identified explicitly. See [truth model](docs/v1.1-truth-model.md) for semantics and remaining acceptance work.

Canonical stable authority:

- roadmap / product contract: **Issue #338 — Item Intelligence Admiral — v1.1 modular UX / AQC-reference roadmap**;
- stable implementation: **PR #341 — Item Intelligence Admiral v1.1 — modular UX and contextual intelligence**;
- accepted v1.2 implementation: **PR #343 — Item Intelligence Admiral v1.2 — contextual raid decisions**;
- published install channel: **`runtime-item-intelligence`**;
- ordinary implementation validation: **`Item Intelligence Admiral Validate`** only;
- one batched physical runtime gate occurs only after the recorded roadmap is automated-green.

The branch/PR are a prepared implementation workspace for the dedicated work/Codex flow. Repository coordination and roadmap maintenance do not constitute implementation acceptance. Historical Issues/PRs remain evidence only and are not parallel authorities.

## Purpose

Item Intelligence Admiral attaches persistent information markers to supported EFT item cells and projects player-relative item requirements and value data without per-frame inventory/network polling.

Current stable v1.1.0 presentation contracts include:

- persistent per-item `ⓘ` markers with optional soft radial halo;
- requirement-priority states: `Quest Now → Hideout → Quest Later → Default`;
- compact Minimal / Normal / Detailed / Full tooltip modes;
- owned-versus-required counts, FIR-aware quest allocation, `Keep ×N`, and concrete quest/hideout targets;
- compact trader, flea, and per-slot price rows without a redundant best-sell recommendation;
- the accepted Item Valuation palette for ordinary items and its original penetration-based ammunition tiers, while keys retain BetterKeys ownership;
- price-amount bands: below 50k white, 50k+ green, 100k+ red, 250k+ gold;
- compact `Craft ×N` / `Barter ×N` relevance in regular-play and expanded modes;
- fallback to the available Flea/Trader source when the preferred source has no price;
- semantic requirement colors;
- F12 controls for marker appearance and tooltip presentation.

Marker state remains requirement-driven, not price-driven. Price colors apply to valuation text only and do not change requirement classification.

The v1.1 target is recorded in Issue #338 and centers on modular F12 control, contextual attention-driven markers, a clearer quest/hideout/value information hierarchy, runtime/performance consolidation, and a later bounded compatibility-overlap review. AllQuestsCheckmarks is a behavioral/UX reference only; GPL source/assets are not implementation inputs.

## Data path

The server publishes one bounded snapshot of authoritative SPT profile/database data. The client fetches that snapshot outside the render hot path, builds immutable/cached requirement, price, relevance, and presentation indexes, and refreshes registered item markers from those cached states.

When Tyfon Hideout In Progress is installed, Item Intelligence also reads that profile's persisted `areaProgresses`. Deposited components reduce only the matching station's current upgrade requirement. They are already committed, so they are excluded from owned/FIR inventory and cannot satisfy a quest, another station, or a future hideout level. Missing or invalid companion data falls back to the native SPT requirement model without making Item Intelligence unavailable.

`SPT profile/database → server snapshot → serialized payload → client bootstrap → cached indexes → presentation classification`

Craft/barter relevance and trader/flea valuation are precomputed while the existing snapshot is built. No additional endpoint, hover request, per-frame inventory scan, database polling, or transaction execution is used.

## UI lifecycle and performance

Supported `ItemView`/`ItemCell` lifecycle hooks register live cells and remove them during cleanup. v1.1 uses one contextual badge and one card. The badge is the card's hover target; when no badge is present, only the native caption strip at the top of the item cell acts as the fallback target. Irrelevant value-only items do not receive a marker.

Network requests, reflection discovery, requirement aggregation, valuation work, and expensive text formatting are kept out of per-frame render paths. Cached state is invalidated only when the relevant source data or UI settings change. Full-mode display stripping and rich-text price/semantic strings use bounded caches so steady-state GUI repaint reuses prepared strings instead of rebuilding them every frame.

The marker uses original embedded raster artwork with selectable frame and symbol variants, a configurable inner fill, requirement-source color, and stock-coverage color. Fill color and 0–100% fill opacity are independent F12 controls. It does not copy an AQC sprite or depend on a font glyph. The optional halo also uses one shared/static texture. The information card follows the game's Russian or English UI language; other game languages use English.

In v1.2, optional Amands Sense integration carries the same cached unmet-requirement decision into loose world loot. Active quest and hideout needs take visual priority, completed tracked items keep their green check, and native valuable/favorite marks remain ahead of fallback categories. Item Intelligence adds independently configurable key, grenade, currency, food and water categories using sprites already loaded by Sense; food uses the lightning icon and water the droplet. Currency-only containers use the separate money icon and denomination label. Container value comes from flea totals and adjusts the brightness of the selected category tint, preserving its hue; the four F12 brightness breakpoints rise from 50k to 500k+. Rescanning a Sense container refreshes its contents and reapplies the marker. The adapter adds no required dependency, performs no world polling, and is independently switchable in F12.

In v1.2.1, successful pickups observed through that event-driven integration are folded into the same authoritative owned count during the raid, including full stack sizes and FIR state. Normal presentation stays concise with one combined total; Full adds the FIR/non-FIR split. Item-instance IDs make repeated Sense callbacks idempotent, and dropping an observed pickup removes it from the raid ledger.

Normal shows the selected F12 value source without per-slot value, plus compact requirement and craft/barter relevance. Detailed adds one nearest concrete target. Full shows both trader and flea values, per-slot value, every concrete target, craft and barter counts. The rounded card auto-fits short content up to its configurable maximum width.

Background ownership is cooperative. Item Intelligence restores the accepted Item Valuation palette through the same authoritative template `BackgroundColor` path that EFT renders natively. Ordinary items use value tiers and ammunition uses penetration tiers; values below the first threshold retain their original background. Keys remain under BetterKeys ownership and CompatibilityHighlighter/EFT keeps ownership of temporary compatibility outlines.

The optional `Modules / Ammo Penetration Class` badge adds a compact Roman I–VI to ammunition cells. It uses EFT's own six armor-penetration ratings: the highest class graded High or Very High is shown, and ammunition without such a class has no badge. The badge works for modded ammo templates and does not alter their background colors.

## Version and naming

The official product name is **Item Intelligence Admiral**. The current development release is **v1.2.1**.

- client: **Item Intelligence Admiral v1.2.1**;
- server: **Item Intelligence Admiral Server v1.2.1**.

The existing source directory, namespace, GUID, endpoint, and `runtime-item-intelligence` branch are retained as technical compatibility identifiers. They are not the product name and should not be renamed casually because doing so would create unnecessary migration risk.

## Deferred items

`On You` is intentionally not part of v1. A one-shot profile implementation was removed because it could become stale after inventory/equipment changes; it should only return with a proven event-driven inventory lifecycle.

Wishlist, prerequisite-distance, encyclopedia-style item statistics, and direct selling actions remain optional future ideas, not active backlog or release blockers unless explicitly added to the canonical roadmap by user instruction.

## Installation

The install-only `runtime-item-intelligence` channel contains the accepted stable client and server components. The v1 package contract uses:

- `BepInEx/plugins/Admiral SPT/SPT Item Intelligence/Item Intelligence Admiral.dll`
- `SPT_Runtime/user/mods/Item Intelligence Admiral Server/Item Intelligence Admiral Server.dll`

The stable runtime channel is published from the accepted exact source commit and contains only the consolidated client and server package.

The exhaustive [Stable Beta product map](docs/stable-beta-product-map.md) lists every compiled source unit, server registration, route, Harmony/runtime patch, scheduled path, F12 entry and dependency boundary. Its regression guard fails if a new compiled module, patch owner, dependency or configuration entry is added without updating that map.

External AllQuestsCheckmarks and legacy Item Valuation are unnecessary with v1.1. `Background Coloring (Valuation)` remains independently switchable in F12 and restores each native cell color when disabled. Restore the v1.0 package and legacy Item Valuation configuration only when rolling back.

## Documentation

The `docs/phase*.md` files preserve implementation contracts and design history for earlier milestones under the former development naming. They are archaeology/regression references; current source, tests, runtime evidence, this README, Issue #338, and repository rules take precedence where later development supersedes an earlier phase description.

Key contracts: [registry](docs/phase1.md) · [data transport](docs/phase3.md) · [requirement index](docs/phase4-requirement-index.md) · [live bootstrap](docs/phase13-live-requirement-bootstrap.md) · [marker UX](docs/phase15-requirement-marker-ux.md) · [persistent markers](docs/phase16-persistent-item-markers.md) · [live value](docs/phase17-live-value.md) · [tooltip intelligence](docs/phase18-tooltip-intelligence.md) · [hot-path optimization](docs/hotpath-optimization-01.md).
