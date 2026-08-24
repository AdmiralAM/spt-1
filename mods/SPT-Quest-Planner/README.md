# SPT Quest Planner

Standalone quest-planning module for SPT 4.1.x. Current client version: **0.9.4**.

Quest Planner is independent from Item Intelligence, Belt/Armband Inventory, Pause, and Tactical HUD. It owns its data contracts, server extraction, domain model, client cache, tests, and presentation lifecycle.

## Current workflow

The planner now exposes a user-facing planning loop rather than a debug-only data view:

- **Recommended Raid** presents the top-ranked location with preparation context and a direct `Plan this raid` action.
- **Active Raid Plan** is explicit and independent from temporary preview selection.
- **Before Raid** shows exact proven bring-item needs, owned/missing quantities, and flags ambiguous preparation requirements for manual verification instead of pretending certainty.
- **In Raid Checklist** presents compact proven objectives with progress, remaining work, quest labels, and target context.
- **Progression** ranks actionable Active/Available quest candidates and lets the user keep a deliberate progression target.
- Raid ranking, inclusion of available quests, active raid plan, progression target, and meaningful workspace choice are persisted through BepInEx config. Temporary preview selection remains intentionally transient.
- Persisted plans are revalidated against refreshed quest state; stale active plans are cleared instead of surviving after they are no longer actionable.
- Routine refresh/recommendation diagnostics are no longer emitted as normal info-level noise.

## Purpose

The planner derives player-relative quest information from authoritative SPT data, including:

- quest topology and prerequisite chains;
- active, available, completed, and future progression state;
- quest conditions and item requirements;
- outstanding-versus-owned requirement calculations;
- readable quest/objective labels;
- raid-oriented planning data and presentation.

## Architecture

- `server/` — extracts authoritative quest/profile data and exposes planner snapshots.
- `src/` — shared contracts, normalized quest/domain models, extraction, projection, and evaluation logic.
- `client/` — BepInEx client, bounded refresh/cache lifecycle, raid-plan provider, durable planner state, and planner presentation.
- `tests/` — regression coverage for domain, extraction, projection, ranking, preparation, and presentation-state contracts.
- `docs/` — architecture and durable design notes.

The planner does not use per-frame server calls or full graph recomputation. Expensive topology/database work is cached or refreshed at bounded lifecycle events. Planner-state persistence is event-driven and config writes are flushed on the Unity main thread rather than from refresh worker tasks.

## Independence contract

Quest Planner must not depend on another SPT mod's UI classes, runtime controllers, or private registries. Shared data may be introduced only through a narrow, explicit, versioned contract when duplication is materially worse than the coupling.

Item Intelligence can expose similar quest/item facts, but it is not Quest Planner's source of truth.

## Validation and publication

Pull requests that change this module run the dedicated Quest Planner test/client/server workflow. CI build outputs are GitHub Actions artifacts and are not committed to `main`.

Quest Planner does not currently have a permanent install-only runtime branch in the suite publication workflow. Until that channel is deliberately added, use validated development builds/CI artifacts rather than treating another module's runtime branch as a distribution channel.

See [foundation architecture](docs/foundation-architecture.md) for the original data-path and domain constraints. That document is architectural history; current source and tests are authoritative where implementation has advanced beyond the foundation milestone.
